-- SYNCHRO: Recuperacion ADMINISTRATIVA del PIN personal.
-- SOLO Development + SynchroDB_Pruebas.
-- No mueve ni libera los casilleros; conserva la reserva y el deposito.
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 50500, 'Base de datos incorrecta.', 1;

IF OBJECT_ID(N'dbo.AuditoriaRecuperacionPinPersonal', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditoriaRecuperacionPinPersonal
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UsoId INT NOT NULL,
        AdministradorId INT NOT NULL,
        Motivo NVARCHAR(200) NOT NULL,
        EraReservaSinPin BIT NOT NULL,
        FechaUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_AuditoriaRecuperacionPin_FechaUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AuditoriaPinPersonal_Uso
            FOREIGN KEY (UsoId) REFERENCES dbo.UsosCasillero(Id),
        CONSTRAINT FK_AuditoriaPinPersonal_Admin
            FOREIGN KEY (AdministradorId) REFERENCES dbo.Administradores(Id)
    );
END;
GO
CREATE OR ALTER PROCEDURE dbo.RecuperarPinPersonalSynchro
    @UsoId INT,
    @AdministradorId INT,
    @PinHash VARBINARY(32),
    @PinSalt VARBINARY(32),
    @Motivo NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 50500, 'Base de datos incorrecta.', 1;
    IF @UsoId IS NULL OR @UsoId <= 0 OR
       @AdministradorId IS NULL OR @AdministradorId <= 0
        THROW 50501, 'Identificadores invalidos.', 1;
    IF @PinHash IS NULL OR DATALENGTH(@PinHash) <> 32 OR
       @PinSalt IS NULL OR DATALENGTH(@PinSalt) <> 32
        THROW 50502, 'Hash o salt invalidos.', 1;
    IF @Motivo IS NULL OR LEN(LTRIM(RTRIM(@Motivo))) < 12 OR LEN(@Motivo) > 200
        THROW 50503, 'Motivo invalido.', 1;

    DECLARE @Codigo NVARCHAR(10);
    DECLARE @EstadoCasillero NVARCHAR(20);
    DECLARE @EstadoUso NVARCHAR(20);
    DECLARE @TipoServicio NVARCHAR(30);
    DECLARE @OrigenReserva NVARCHAR(20);
    DECLARE @FechaIngreso DATETIME2(7);
    DECLARE @FechaRetiro DATETIME2(7);
    DECLARE @ExistePin BIT = 0;
    DECLARE @Vencimiento DATETIME2(7);
    DECLARE @AuditoriaId INT;

    SELECT @Codigo = CodigoCasillero
    FROM dbo.UsosCasillero
    WHERE Id = @UsoId;

    IF @Codigo IS NULL
        THROW 50504, 'Reserva no encontrada.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- El administrador activo debe existir. El backend ya requiere autenticacion.
        IF NOT EXISTS (
            SELECT 1 FROM dbo.Administradores WITH (HOLDLOCK)
            WHERE Id = @AdministradorId AND Activo = 1
        ) THROW 50505, 'Administrador no autorizado.', 1;

        -- Bloqueos en el mismo orden que deposito/retiro: casillero > uso > PIN.
        SELECT @EstadoCasillero = Estado
        FROM dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE Codigo = @Codigo;

        SELECT @EstadoUso = Estado,
               @TipoServicio = TipoServicio,
               @OrigenReserva = OrigenReserva,
               @FechaIngreso = FechaIngreso,
               @FechaRetiro = FechaRetiro
        FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @UsoId AND CodigoCasillero = @Codigo;

        IF @EstadoCasillero IS NULL OR @EstadoCasillero <> N'Ocupado' OR
           @EstadoUso IS NULL OR @EstadoUso <> N'Ocupado' OR
           @TipoServicio <> N'ResguardoPersonal' OR
           @OrigenReserva <> N'Terminal' OR
           @FechaIngreso IS NULL OR @FechaRetiro IS NOT NULL
            THROW 50506, 'Reserva no elegible para recuperar PIN.', 1;

        IF EXISTS (
            SELECT 1 FROM dbo.UsosCasillero
            WHERE CodigoCasillero = @Codigo AND Id <> @UsoId
              AND Estado IN (N'Reservado', N'Ocupado') AND FechaRetiro IS NULL
        ) THROW 50507, 'Casillero con operaciones conflictivas.', 1;

        IF EXISTS (
            SELECT 1 FROM dbo.CodigosRetiroPersonal WITH (UPDLOCK, HOLDLOCK)
            WHERE UsoId = @UsoId
        ) SET @ExistePin = 1;

        -- La nueva credencial expira en 24 horas. Puede reemitirse con autorizacion.
        SET @Vencimiento = DATEADD(HOUR, 24, SYSUTCDATETIME());

        IF @ExistePin = 1
        BEGIN
            UPDATE dbo.CodigosRetiroPersonal
               SET PinHash = @PinHash,
                   PinSalt = @PinSalt,
                   FechaCreacion = SYSUTCDATETIME(),
                   FechaVencimiento = @Vencimiento,
                   IntentosFallidos = 0,
                   FechaUso = NULL
            WHERE UsoId = @UsoId;
        END
        ELSE
        BEGIN
            INSERT dbo.CodigosRetiroPersonal (
                UsoId, PinHash, PinSalt, FechaCreacion,
                FechaVencimiento, IntentosFallidos, FechaUso
            ) VALUES (
                @UsoId, @PinHash, @PinSalt, SYSUTCDATETIME(),
                @Vencimiento, 0, NULL
            );
        END;
        IF @@ROWCOUNT <> 1
            THROW 50508, 'No se guardo la nueva credencial.', 1;

        INSERT dbo.AuditoriaRecuperacionPinPersonal (
            UsoId, AdministradorId, Motivo, EraReservaSinPin
        ) VALUES (@UsoId, @AdministradorId, @Motivo,
                  CASE WHEN @ExistePin = 0 THEN 1 ELSE 0 END);
        SET @AuditoriaId = CONVERT(INT, SCOPE_IDENTITY());

        COMMIT TRANSACTION;

        SELECT @UsoId AS IdUso, @Codigo AS CodigoCasillero,
               @Vencimiento AS FechaVencimiento,
               @AuditoriaId AS AuditoriaId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
