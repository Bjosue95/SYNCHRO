-- SYNCHRO: finalizar retiro solamente despues de un pago simulado.
-- Exclusivo del entorno SQL aislado.

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57700, 'Base incorrecta.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class = 0
      AND name = N'SynchroFixtureDeposito'
      AND CONVERT(nvarchar(10), value) = N'1'
)
    THROW 57701, 'Base de pruebas no autorizada.', 1;

IF OBJECT_ID(N'dbo.CobrosRetiroPersonal', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CodigosRetiroPersonal', N'U') IS NULL
   OR OBJECT_ID(N'dbo.AutorizacionesWhatsAppManuales', N'U') IS NULL
    THROW 57702, 'Faltan tablas necesarias.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.FinalizarRetiroPersonalPagadoSynchro
    @UsoId INT,
    @CobroId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 57700, 'Base incorrecta.', 1;

    IF @UsoId IS NULL OR @UsoId <= 0
       OR @CobroId IS NULL
       OR @CobroId = '00000000-0000-0000-0000-000000000000'
        THROW 57703, 'Operacion invalida.', 1;

    DECLARE
        @Codigo NVARCHAR(10),
        @EstadoCasillero NVARCHAR(20),
        @EstadoUso NVARCHAR(20),
        @Tipo NVARCHAR(30),
        @Origen NVARCHAR(20),
        @Ingreso DATETIME2(7),
        @Retiro DATETIME2(7),
        @FechaUsoPin DATETIME2(7),
        @VencePin DATETIME2(7),
        @IntentosPin INT,
        @EstadoPago NVARCHAR(24),
        @Importe DECIMAL(12,2),
        @MetodoPago NVARCHAR(24),
        @ConfirmadoUtc DATETIME2(3),
        @Ahora DATETIME2(7) = SYSUTCDATETIME();

    SELECT @Codigo = CodigoCasillero
    FROM dbo.UsosCasillero
    WHERE Id = @UsoId;

    IF @Codigo IS NULL
        THROW 57704, 'Uso no encontrado.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Mantener el orden de bloqueo del retiro existente.
        SELECT @EstadoCasillero = Estado
        FROM dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE Codigo = @Codigo;

        SELECT
            @EstadoUso = Estado,
            @Tipo = TipoServicio,
            @Origen = OrigenReserva,
            @Ingreso = FechaIngreso,
            @Retiro = FechaRetiro
        FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @UsoId
          AND CodigoCasillero = @Codigo;

        SELECT
            @FechaUsoPin = FechaUso,
            @VencePin = FechaVencimiento,
            @IntentosPin = IntentosFallidos
        FROM dbo.CodigosRetiroPersonal WITH (UPDLOCK, HOLDLOCK)
        WHERE UsoId = @UsoId;

        SELECT
            @EstadoPago = Estado,
            @Importe = ImporteHnl,
            @MetodoPago = MetodoPago,
            @ConfirmadoUtc = ConfirmadoUtc
        FROM dbo.CobrosRetiroPersonal WITH (UPDLOCK, HOLDLOCK)
        WHERE UsoId = @UsoId
          AND CobroId = @CobroId;

        -- El casillero debe continuar ocupado.
        IF @EstadoCasillero IS NULL
           OR @EstadoCasillero <> N'Ocupado'
           OR @EstadoUso IS NULL
           OR @EstadoUso <> N'Ocupado'
           OR @Tipo IS NULL
           OR @Tipo <> N'ResguardoPersonal'
           OR @Origen IS NULL
           OR @Origen <> N'Terminal'
           OR @Ingreso IS NULL
           OR @Retiro IS NOT NULL
            THROW 57705, 'Retiro no disponible.', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.AutorizacionesWhatsAppManuales
            WHERE UsoId = @UsoId
              AND Estado = N'Confirmado'
        )
            THROW 57706, 'Autorizacion no disponible.', 1;

        -- No finalizar con PIN consumido, bloqueado o vencido.
        IF @IntentosPin IS NULL
           OR @IntentosPin >= 5
           OR @FechaUsoPin IS NOT NULL
           OR (@VencePin IS NOT NULL AND @VencePin <= @Ahora)
            THROW 57707, 'PIN no disponible.', 1;

        -- Exigir un cobro simulado confirmado y valido.
        IF @EstadoPago IS NULL
           OR @EstadoPago <> N'ConfirmadoSimulado'
           OR @ConfirmadoUtc IS NULL
           OR @ConfirmadoUtc > @Ahora
           OR @Importe IS NULL
           OR @Importe <= 0
           OR @MetodoPago IS NULL
           OR @MetodoPago NOT IN (
                N'EfectivoSimulado',
                N'TarjetaSimulada'
           )
            THROW 57708, 'Pago no confirmado.', 1;

        IF EXISTS (
            SELECT 1 FROM dbo.UsosCasillero
            WHERE CodigoCasillero = @Codigo
              AND Id <> @UsoId
              AND Estado IN (N'Reservado', N'Ocupado')
              AND FechaRetiro IS NULL
        )
            THROW 57709, 'Existe otra operacion activa.', 1;

        UPDATE dbo.UsosCasillero
        SET Estado = N'Finalizado',
            FechaRetiro = @Ahora
        WHERE Id = @UsoId
          AND Estado = N'Ocupado'
          AND FechaRetiro IS NULL;

        IF @@ROWCOUNT <> 1
            THROW 57710, 'No se pudo finalizar el uso.', 1;

        UPDATE dbo.Casilleros
        SET Estado = N'Disponible'
        WHERE Codigo = @Codigo
          AND Estado = N'Ocupado';

        IF @@ROWCOUNT <> 1
            THROW 57711, 'No se pudo liberar el casillero.', 1;

        UPDATE dbo.CodigosRetiroPersonal
        SET FechaUso = @Ahora
        WHERE UsoId = @UsoId
          AND FechaUso IS NULL;

        IF @@ROWCOUNT <> 1
            THROW 57712, 'No se pudo consumir el PIN.', 1;

        UPDATE dbo.CobrosRetiroPersonal
        SET Estado = N'Aplicado',
            FinalizadoUtc = @Ahora
        WHERE UsoId = @UsoId
          AND CobroId = @CobroId
          AND Estado = N'ConfirmadoSimulado';

        IF @@ROWCOUNT <> 1
            THROW 57713, 'No se pudo aplicar el cobro.', 1;

        -- SYNCHRO-LIMPIEZA-ROSTRO-RETIRO-PAGADO-V1
        -- Eliminar la evidencia facial simulada en la misma
        -- transaccion que consume el PIN y libera el casillero.
        DELETE FROM dbo.RostrosRetiroPersonalManual
        WHERE UsoId = @UsoId;

        IF @@ROWCOUNT <> 1
            THROW 57714, 'No se pudo consumir la evidencia facial.', 1;
        COMMIT TRANSACTION;

        SELECT
            CAST(1 AS BIT) AS RetiroConfirmado,
            @Codigo AS CodigoCasillero,
            N'Disponible' AS EstadoCasillero,
            CAST(1 AS BIT) AS PagoSimulado,
            @Importe AS ImporteHnl,
            @Ahora AS FechaRetiro;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;