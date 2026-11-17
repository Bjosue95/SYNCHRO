-- SYNCHRO: validar PIN sin liberar casillero.
-- Solo para el prototipo en SynchroDB_Pruebas.

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57600, 'Base de datos incorrecta.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class = 0 AND name = N'SynchroFixtureDeposito'
)
    THROW 57601, 'Base de pruebas no autorizada.', 1;

IF OBJECT_ID(N'dbo.CobrosRetiroPersonal', N'U') IS NULL
    THROW 57602, 'Falta la estructura de cobros.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.PrevalidarRetiroPersonalSynchro
    @UsoId INT,
    @PinHashCandidato VARBINARY(32),
    @PinSaltLeido VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 57600, 'Base incorrecta.', 1;

    IF @UsoId IS NULL OR @UsoId <= 0
       OR @PinHashCandidato IS NULL
       OR DATALENGTH(@PinHashCandidato) <> 32
       OR @PinSaltLeido IS NULL
       OR DATALENGTH(@PinSaltLeido) <> 32
        THROW 57603, 'Credencial invalida.', 1;

    DECLARE @Codigo NVARCHAR(10);
    DECLARE @EstadoCasillero NVARCHAR(20);
    DECLARE @EstadoUso NVARCHAR(20);
    DECLARE @TipoServicio NVARCHAR(30);
    DECLARE @OrigenReserva NVARCHAR(20);
    DECLARE @Ingreso DATETIME2(7);
    DECLARE @Retiro DATETIME2(7);
    DECLARE @Salt VARBINARY(32);
    DECLARE @Hash VARBINARY(32);
    DECLARE @FechaUso DATETIME2(7);
    DECLARE @Vence DATETIME2(7);
    DECLARE @Intentos INT;
    DECLARE @Ahora DATETIME2(7) = SYSUTCDATETIME();

    SELECT @Codigo = CodigoCasillero
    FROM dbo.UsosCasillero
    WHERE Id = @UsoId;

    IF @Codigo IS NULL
    BEGIN
        SELECT CAST(0 AS BIT) AS PinValido;
        RETURN;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @EstadoCasillero = Estado
        FROM dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE Codigo = @Codigo;

        SELECT
            @EstadoUso = Estado,
            @TipoServicio = TipoServicio,
            @OrigenReserva = OrigenReserva,
            @Ingreso = FechaIngreso,
            @Retiro = FechaRetiro
        FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @UsoId
          AND CodigoCasillero = @Codigo;

        IF @EstadoCasillero IS NULL
           OR @EstadoCasillero <> N'Ocupado'
           OR @EstadoUso IS NULL
           OR @EstadoUso <> N'Ocupado'
           OR @TipoServicio IS NULL
           OR @TipoServicio <> N'ResguardoPersonal'
           OR @OrigenReserva IS NULL
           OR @OrigenReserva <> N'Terminal'
           OR @Ingreso IS NULL
           OR @Retiro IS NOT NULL
           OR NOT EXISTS (
                SELECT 1
                FROM dbo.AutorizacionesWhatsAppManuales
                WHERE UsoId = @UsoId
                  AND Estado = N'Confirmado'
           )
        BEGIN
            COMMIT TRANSACTION;
            SELECT CAST(0 AS BIT) AS PinValido;
            RETURN;
        END;

        SELECT
            @Salt = PinSalt,
            @Hash = PinHash,
            @FechaUso = FechaUso,
            @Vence = FechaVencimiento,
            @Intentos = IntentosFallidos
        FROM dbo.CodigosRetiroPersonal WITH (UPDLOCK, HOLDLOCK)
        WHERE UsoId = @UsoId;

        IF @Salt IS NULL
           OR @Hash IS NULL
           OR @Intentos IS NULL
           OR @Intentos >= 5
           OR @FechaUso IS NOT NULL
           OR (@Vence IS NOT NULL AND @Vence <= @Ahora)
        BEGIN
            COMMIT TRANSACTION;
            SELECT CAST(0 AS BIT) AS PinValido;
            RETURN;
        END;

        IF @Salt <> @PinSaltLeido
           OR @Hash <> @PinHashCandidato
        BEGIN
            UPDATE dbo.CodigosRetiroPersonal
            SET IntentosFallidos = IntentosFallidos + 1
            WHERE UsoId = @UsoId
              AND IntentosFallidos < 5;

            COMMIT TRANSACTION;
            SELECT CAST(0 AS BIT) AS PinValido;
            RETURN;
        END;

        -- PIN correcto: calcular tiempo, sin finalizar.
        DECLARE @Segundos BIGINT =
            DATEDIFF_BIG(SECOND, @Ingreso, @Ahora);

        DECLARE @Minutos INT = CONVERT(INT,
            CASE
                WHEN @Segundos <= 0 THEN 1
                WHEN @Segundos > 128849016000
                    THEN 2147483600
                ELSE (@Segundos + 59) / 60
            END
        );

        COMMIT TRANSACTION;

        SELECT
            CAST(1 AS BIT) AS PinValido,
            @Codigo AS CodigoCasillero,
            @Minutos AS MinutosUtilizados,
            N'Ocupado' AS EstadoCasillero;

        -- No consumir PIN.
        -- No actualizar estado del casillero.
        -- No generar un pago ni marcarlo como confirmado.
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;