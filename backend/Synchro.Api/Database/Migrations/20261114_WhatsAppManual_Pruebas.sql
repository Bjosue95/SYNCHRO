/* SYNCHRO: autorizaciones manuales de resguardo personal. Solo BD aislada. */
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57000, 'Este script solo puede ejecutarse en SynchroDB_Pruebas.', 1;
IF OBJECT_ID(N'dbo.UsosCasillero', N'U') IS NULL OR OBJECT_ID(N'dbo.Casilleros', N'U') IS NULL
    OR OBJECT_ID(N'dbo.CodigosRetiroPersonal', N'U') IS NULL
    THROW 57001, 'Faltan las tablas del resguardo personal.', 1;
IF OBJECT_ID(N'dbo.AutorizacionesWhatsAppManuales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AutorizacionesWhatsAppManuales (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AutorizacionesWhatsAppManuales PRIMARY KEY,
        CodigoAutorizacion VARCHAR(16) NOT NULL,
        CodigoCasillero NVARCHAR(10) NOT NULL,
        TokenConsultaHash VARBINARY(32) NOT NULL,
        Estado NVARCHAR(20) NOT NULL,
        UsoId INT NULL,
        TelefonoCliente VARCHAR(15) NULL,
        OperadorId INT NULL,
        CreadoUtc DATETIME2(3) NOT NULL CONSTRAINT DF_WAM_Creado DEFAULT SYSUTCDATETIME(),
        VenceUtc DATETIME2(3) NOT NULL,
        FechaEnvioDeclaradaUtc DATETIME2(3) NULL,
        FechaConfirmacionDepositoUtc DATETIME2(3) NULL,
        CONSTRAINT UQ_WAM_CodigoAutorizacion UNIQUE(CodigoAutorizacion),
        CONSTRAINT FK_WAM_Casillero FOREIGN KEY(CodigoCasillero) REFERENCES dbo.Casilleros(Codigo),
        CONSTRAINT FK_WAM_Uso FOREIGN KEY(UsoId) REFERENCES dbo.UsosCasillero(Id),
        CONSTRAINT CK_WAM_Estado CHECK (Estado IN
          (N'Pendiente', N'Reservado', N'PinEmitido', N'Enviado', N'Confirmado'))
    );
    CREATE UNIQUE INDEX UX_WAM_Uso ON dbo.AutorizacionesWhatsAppManuales(UsoId) WHERE UsoId IS NOT NULL;
    CREATE INDEX IX_WAM_Pendientes ON dbo.AutorizacionesWhatsAppManuales(Estado,CreadoUtc);
END;
GO
CREATE OR ALTER PROCEDURE dbo.ConfirmarDepositoPersonalManualSynchro
    @IntentoId UNIQUEIDENTIFIER,
    @TokenConsultaHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 57000, 'Base de datos no autorizada.', 1;
    IF @TokenConsultaHash IS NULL OR DATALENGTH(@TokenConsultaHash) <> 32
        THROW 57002, 'Token invalido.', 1;

    DECLARE @UsoId INT, @CodigoCasillero NVARCHAR(10), @EstadoManual NVARCHAR(20),
            @VenceIntento DATETIME2(3), @EstadoUso NVARCHAR(20),
            @EstadoCasillero NVARCHAR(20), @VenceReserva DATETIME2(7),
            @Ahora DATETIME2(3) = SYSUTCDATETIME();
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @UsoId=UsoId, @CodigoCasillero=CodigoCasillero, @EstadoManual=Estado, @VenceIntento=VenceUtc
        FROM dbo.AutorizacionesWhatsAppManuales WITH (UPDLOCK, HOLDLOCK)
        WHERE Id=@IntentoId AND TokenConsultaHash=@TokenConsultaHash;
        IF @EstadoManual IS NULL OR @EstadoManual <> N'Enviado' OR @VenceIntento <= @Ahora
            THROW 57003, 'Operacion manual no habilitada.', 1;

        SELECT @EstadoCasillero=Estado FROM dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE Codigo=@CodigoCasillero;
        SELECT @EstadoUso=Estado,@VenceReserva=FechaVencimiento
        FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE Id=@UsoId AND CodigoCasillero=@CodigoCasillero
          AND OrigenReserva=N'Terminal' AND TipoServicio=N'ResguardoPersonal'
          AND FechaIngreso IS NULL AND FechaRetiro IS NULL;

        IF @EstadoUso IS NULL OR @EstadoUso <> N'Reservado' OR @EstadoCasillero IS NULL OR @EstadoCasillero <> N'Reservado'
            OR @VenceReserva IS NULL OR @VenceReserva <= @Ahora
            THROW 57004, 'Reserva ya no disponible.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.CodigosRetiroPersonal WITH (UPDLOCK, HOLDLOCK)
                       WHERE UsoId=@UsoId AND FechaUso IS NULL)
            THROW 57005, 'No existe PIN valido vinculado al casillero.', 1;
        IF EXISTS (SELECT 1 FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
                   WHERE CodigoCasillero=@CodigoCasillero AND Id<>@UsoId
                     AND Estado IN (N'Reservado',N'Ocupado') AND FechaRetiro IS NULL)
            THROW 57006, 'Otra operacion activa.', 1;

        UPDATE dbo.UsosCasillero SET Estado=N'Ocupado', FechaIngreso=@Ahora
        WHERE Id=@UsoId AND Estado=N'Reservado' AND FechaIngreso IS NULL AND FechaRetiro IS NULL;
        IF @@ROWCOUNT<>1 THROW 57007, 'No se pudo registrar el deposito.', 1;
        UPDATE dbo.Casilleros SET Estado=N'Ocupado'
        WHERE Codigo=@CodigoCasillero AND Estado=N'Reservado';
        IF @@ROWCOUNT<>1 THROW 57008, 'No se pudo ocupar el casillero.', 1;
        UPDATE dbo.AutorizacionesWhatsAppManuales
        SET Estado=N'Confirmado', FechaConfirmacionDepositoUtc=@Ahora
        WHERE Id=@IntentoId AND Estado=N'Enviado';
        IF @@ROWCOUNT<>1 THROW 57009, 'No se pudo cerrar la autorizacion.', 1;
        COMMIT TRANSACTION;
        SELECT @UsoId AS IdUso, @CodigoCasillero AS CodigoCasillero, N'Ocupado' AS EstadoCasillero;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
