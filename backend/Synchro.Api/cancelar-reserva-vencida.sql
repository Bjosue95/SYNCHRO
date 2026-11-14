-- SYNCHRO - Cancelacion de reservas vencidas
-- Exclusivo para SynchroDB_Pruebas

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 58120, 'Base incorrecta.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class = 0
      AND name = N'SynchroFixtureDeposito'
      AND CONVERT(nvarchar(10), value) = N'1'
)
    THROW 58121, 'Base no autorizada.', 1;

IF COL_LENGTH(N'dbo.UsosCasillero', N'FechaVencimiento') IS NULL
    OR COL_LENGTH(N'dbo.UsosCasillero', N'TokenDepositoHash') IS NULL
    THROW 58122, 'Faltan columnas de reservas.', 1;

GO

CREATE OR ALTER PROCEDURE dbo.CancelarReservaCasilleroSynchro
    @UsoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 58120, 'Base incorrecta.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE class = 0
          AND name = N'SynchroFixtureDeposito'
          AND CONVERT(nvarchar(10), value) = N'1'
    )
        THROW 58121, 'Base no autorizada.', 1;

    IF @UsoId IS NULL OR @UsoId <= 0
        THROW 50031, 'Reserva invalida.', 1;

    DECLARE @Codigo NVARCHAR(10);
    DECLARE @EstadoCasillero NVARCHAR(20);
    DECLARE @EstadoUso NVARCHAR(20);
    DECLARE @Origen NVARCHAR(20);
    DECLARE @TipoServicio NVARCHAR(20);
    DECLARE @Vencimiento DATETIME2(7);
    DECLARE @Ingreso DATETIME2(7);
    DECLARE @Retiro DATETIME2(7);
    DECLARE @Ahora DATETIME2(7);

    SELECT @Codigo = CodigoCasillero
    FROM dbo.UsosCasillero
    WHERE Id = @UsoId;

    IF @Codigo IS NULL
        THROW 50031, 'Reserva no encontrada.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Mismo orden de bloqueos que deposito y retiro:
        -- primero casillero, despues reserva.
        SELECT @EstadoCasillero = Estado
        FROM dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE Codigo = @Codigo;

        SELECT
            @EstadoUso = Estado,
            @Origen = OrigenReserva,
            @TipoServicio = TipoServicio,
            @Vencimiento = FechaVencimiento,
            @Ingreso = FechaIngreso,
            @Retiro = FechaRetiro
        FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @UsoId AND CodigoCasillero = @Codigo;

        IF @EstadoCasillero <> N'Reservado'
            OR @EstadoUso <> N'Reservado'
            OR @Origen <> N'Terminal'
            OR @TipoServicio <> N'ResguardoPersonal'
            OR @Ingreso IS NOT NULL
            OR @Retiro IS NOT NULL
            THROW 50033, 'Reserva no cancelable.', 1;

        SET @Ahora = SYSUTCDATETIME();

        IF @Vencimiento IS NULL OR @Vencimiento > @Ahora
            THROW 50034, 'Reserva aun vigente.', 1;

        IF EXISTS (
            SELECT 1 FROM dbo.UsosCasillero
            WHERE CodigoCasillero = @Codigo
              AND Id <> @UsoId
              AND Estado IN (N'Reservado', N'Ocupado')
              AND FechaRetiro IS NULL
        )
            THROW 50033, 'Otra operacion activa.', 1;

        UPDATE dbo.UsosCasillero
        SET Estado = N'Cancelado',
            TokenDepositoHash = NULL
        WHERE Id = @UsoId
          AND Estado = N'Reservado'
          AND FechaIngreso IS NULL
          AND FechaVencimiento <= @Ahora;

        IF @@ROWCOUNT <> 1
            THROW 50033, 'No se pudo cancelar la reserva.', 1;

        UPDATE dbo.Casilleros
        SET Estado = N'Disponible'
        WHERE Codigo = @Codigo
          AND Estado = N'Reservado';

        IF @@ROWCOUNT <> 1
            THROW 50033, 'No se pudo liberar el casillero.', 1;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
