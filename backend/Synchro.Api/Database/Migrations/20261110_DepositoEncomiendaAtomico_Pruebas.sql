-- Ejecutar con un runner que aborte ante el primer error (sqlcmd -b).
-- Ningun dato biometrico ni PIN en claro se almacena aqui.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 55600, 'Base no autorizada.', 1;
IF OBJECT_ID(N'dbo.EvidenciasFacialesRemitente', N'U') IS NULL
   OR OBJECT_ID(N'dbo.ConsentimientosPreReservaWhatsApp', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Casilleros', N'U') IS NULL
   OR OBJECT_ID(N'dbo.UsosCasillero', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Personas', N'U') IS NULL
   OR OBJECT_ID(N'dbo.ParticipantesUso', N'U') IS NULL
    THROW 55601, 'Faltan prerrequisitos del deposito.', 1;

-- Este vinculo solo lo podra crear el servidor despues de autenticar
-- al remitente y comprobar la identidad/telefono de ambos participantes.
-- No existe endpoint publico ni se deduce de IDs enviados por el navegador.
IF OBJECT_ID(N'dbo.ParticipantesPreReservaVerificados', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ParticipantesPreReservaVerificados
    (
        ReservaId INT NOT NULL PRIMARY KEY REFERENCES dbo.PreReservasEncomiendasWeb(Id),
        RemitenteId INT NOT NULL REFERENCES dbo.Personas(Id),
        DestinatarioId INT NOT NULL REFERENCES dbo.Personas(Id),
        FechaVerificacionUtc DATETIME2(3) NOT NULL,
        FechaExpiracionUtc DATETIME2(3) NOT NULL,
        CONSTRAINT CK_ParticipantesPreReserva_Distintos CHECK (RemitenteId <> DestinatarioId),
        CONSTRAINT CK_ParticipantesPreReserva_Fechas CHECK (FechaExpiracionUtc > FechaVerificacionUtc)
    );
END;
IF OBJECT_ID(N'dbo.DepositosPreReservaEncomienda', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DepositosPreReservaEncomienda
    (
        ReservaId INT NOT NULL PRIMARY KEY REFERENCES dbo.PreReservasEncomiendasWeb(Id),
        UsoId INT NOT NULL UNIQUE REFERENCES dbo.UsosCasillero(Id),
        EvidenciaId UNIQUEIDENTIFIER NOT NULL UNIQUE REFERENCES dbo.EvidenciasFacialesRemitente(EvidenciaId),
        TerminalId INT NOT NULL REFERENCES dbo.Terminales(Id),
        FechaDepositoUtc DATETIME2(3) NOT NULL
    );
END;
GO
CREATE OR ALTER PROCEDURE dbo.DepositarEncomiendaReservaSynchro
    @ReservaId INT,
    @TerminalId INT,
    @EvidenciaId UNIQUEIDENTIFIER,
    @CodigoCasillero NVARCHAR(10),
    @TokenGestionHash VARBINARY(32)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 55600, 'Base no autorizada.', 1;
    IF @ReservaId IS NULL OR @ReservaId <= 0 OR @TerminalId IS NULL OR @TerminalId <= 0
       OR @EvidenciaId IS NULL OR @EvidenciaId = '00000000-0000-0000-0000-000000000000'
       OR NULLIF(LTRIM(RTRIM(@CodigoCasillero)), N'') IS NULL
        THROW 55602, 'Datos invalidos.', 1;
    -- La aplicacion confirma deposito, PIN y outbox juntos. No permitir un
    -- deposito autonomo que pueda confirmarse sin crear sus credenciales.
    IF @@TRANCOUNT <> 1
        THROW 55603, 'Se requiere una transaccion de la aplicacion.', 1;

    DECLARE @Ahora DATETIME2(3), @Tamano NVARCHAR(12), @Remitente INT, @Destinatario INT,
            @Telefono NVARCHAR(20), @UsoId INT;
    BEGIN TRY
        -- Orden estable: terminal, casillero, reserva, participantes, consentimiento, evidencia.
        IF NOT EXISTS (SELECT 1 FROM dbo.Terminales WITH (UPDLOCK, HOLDLOCK)
                       WHERE Id = @TerminalId AND Activa = 1)
            THROW 55604, 'Terminal no autorizada.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
                       WHERE Codigo = @CodigoCasillero AND Estado = N'Disponible' AND Tipo = N'Normal')
            THROW 55605, 'Casillero no disponible.', 1;

        SET @Ahora = SYSUTCDATETIME();
        SELECT @Tamano = TamanoSolicitado,
               @Telefono = CASE WHEN LEFT(TelefonoDestinatario,1) = N'+'
                                THEN TelefonoDestinatario ELSE N'+' + TelefonoDestinatario END
        FROM dbo.PreReservasEncomiendasWeb WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @ReservaId AND Estado = N'PendienteConsentimiento' AND FechaExpiracion > @Ahora
          AND (@TokenGestionHash IS NULL OR (DATALENGTH(@TokenGestionHash)=32 AND TokenGestionHash=@TokenGestionHash));
        IF @Tamano IS NULL OR EXISTS (
            SELECT 1 FROM dbo.DepositosPreReservaEncomienda WITH (UPDLOCK, HOLDLOCK) WHERE ReservaId = @ReservaId)
            THROW 55606, 'Reserva no disponible.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Casilleros
                       WHERE Codigo = @CodigoCasillero AND
                             (Tamano = @Tamano OR (Tamano = N'Pequeño' AND @Tamano = N'Pequeno')))
            THROW 55607, 'Tamano incompatible.', 1;

        SELECT @Remitente = RemitenteId, @Destinatario = DestinatarioId
        FROM dbo.ParticipantesPreReservaVerificados WITH (UPDLOCK, HOLDLOCK)
        WHERE ReservaId = @ReservaId AND FechaVerificacionUtc <= @Ahora AND FechaExpiracionUtc > @Ahora;
        IF @Remitente IS NULL OR @Destinatario IS NULL
            THROW 55608, 'Participantes no verificados.', 1;
        IF NOT EXISTS (
            SELECT 1 FROM dbo.ConsentimientosPreReservaWhatsApp WITH (UPDLOCK, HOLDLOCK)
            WHERE ReservaId = @ReservaId AND Estado = N'Confirmado'
              AND TelefonoWhatsApp = @Telefono AND NULLIF(MensajeIdConfirmacion,N'') IS NOT NULL
              AND FechaConfirmacionUtc >= FechaCreacionUtc
              AND FechaConfirmacionUtc <= FechaExpiracionUtc AND FechaConfirmacionUtc <= @Ahora)
            THROW 55609, 'Consentimiento no valido.', 1;
        IF EXISTS (SELECT 1 FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
                   WHERE CodigoCasillero = @CodigoCasillero AND Estado IN (N'Reservado',N'Ocupado')
                     AND FechaRetiro IS NULL)
            THROW 55605, 'Casillero con operacion activa.', 1;

        -- Bloquear la evidencia antes de tomar la hora definitiva: una espera de
        -- bloqueo no debe prolongar la vigencia de una reserva o de la biometria.
        IF NOT EXISTS (SELECT 1 FROM dbo.EvidenciasFacialesRemitente WITH (UPDLOCK, HOLDLOCK)
                       WHERE EvidenciaId = @EvidenciaId)
            THROW 55610, 'Evidencia no valida o consumida.', 1;
        SET @Ahora = SYSUTCDATETIME();
        IF NOT EXISTS (SELECT 1 FROM dbo.PreReservasEncomiendasWeb
                       WHERE Id = @ReservaId AND FechaExpiracion > @Ahora)
           OR NOT EXISTS (SELECT 1 FROM dbo.ParticipantesPreReservaVerificados
                          WHERE ReservaId = @ReservaId AND FechaExpiracionUtc > @Ahora)
            THROW 55606, 'Reserva o vinculo vencidos durante la operacion.', 1;

        UPDATE dbo.EvidenciasFacialesRemitente WITH (UPDLOCK)
        SET Consumida = 1, FechaConsumoUtc = @Ahora
        WHERE EvidenciaId = @EvidenciaId AND ReservaId = @ReservaId
          AND RemitenteId = @Remitente AND TerminalId = @TerminalId
          AND Consumida = 0 AND FechaConsumoUtc IS NULL
          AND FechaEmisionUtc <= @Ahora AND FechaExpiracionUtc > @Ahora
          AND DATEDIFF_BIG(MILLISECOND,FechaEmisionUtc,FechaExpiracionUtc) BETWEEN 1 AND 120000
          AND RostroCoincide = 1 AND PruebaDeVidaSuperada = 1;
        IF @@ROWCOUNT <> 1
            THROW 55610, 'Evidencia no valida o consumida.', 1;

        INSERT INTO dbo.UsosCasillero
            (CodigoCasillero,TipoServicio,FechaReserva,FechaIngreso,Estado,OrigenReserva)
        VALUES (@CodigoCasillero,N'Encomienda',@Ahora,@Ahora,N'Ocupado',N'Web');
        SET @UsoId = CONVERT(INT,SCOPE_IDENTITY());
        INSERT INTO dbo.ParticipantesUso (UsoId,PersonaId,Rol)
        VALUES (@UsoId,@Remitente,N'Remitente'),(@UsoId,@Destinatario,N'Destinatario');
        UPDATE dbo.Casilleros SET Estado = N'Ocupado'
        WHERE Codigo = @CodigoCasillero AND Estado = N'Disponible';
        IF @@ROWCOUNT <> 1
            THROW 55605, 'Casillero no disponible.', 1;
        INSERT INTO dbo.DepositosPreReservaEncomienda (ReservaId,UsoId,EvidenciaId,TerminalId,FechaDepositoUtc)
        VALUES (@ReservaId,@UsoId,@EvidenciaId,@TerminalId,@Ahora);
        SELECT @UsoId AS UsoId, @CodigoCasillero AS CodigoCasillero;
    END TRY
    BEGIN CATCH
        -- La aplicacion revierte tambien PIN y outbox. XACT_ABORT impide
        -- confirmar una transaccion abortada por una violacion SQL.
        THROW;
    END CATCH;
END;
