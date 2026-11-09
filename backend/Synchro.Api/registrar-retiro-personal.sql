-- SYNCHRO / RETIRO PERSONAL / SOLO SynchroDB_Pruebas
-- Instalador: scripts/INSTALAR-Y-COMPILAR.ps1 (valida la base ANTES de ejecutar cada lote).
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 50400, 'Base de datos incorrecta.', 1;
GO
CREATE OR ALTER PROCEDURE dbo.RegistrarRetiroPersonalSynchro
    @UsoId INT,
    @PinHashCandidato VARBINARY(32),
    @PinSaltLeido VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 50400, 'Base de datos incorrecta.', 1;
    IF @UsoId IS NULL OR @UsoId <= 0
        THROW 50401, 'Identificador invalido.', 1;
    IF @PinHashCandidato IS NULL OR DATALENGTH(@PinHashCandidato) <> 32 OR
       @PinSaltLeido IS NULL OR DATALENGTH(@PinSaltLeido) <> 32
        THROW 50402, 'Credencial invalida.', 1;

    DECLARE @Codigo NVARCHAR(10);
    DECLARE @EstadoCasillero NVARCHAR(20);
    DECLARE @EstadoUso NVARCHAR(20);
    DECLARE @TipoServicio NVARCHAR(30);
    DECLARE @OrigenReserva NVARCHAR(20);
    DECLARE @FechaIngreso DATETIME2(7);
    DECLARE @FechaRetiro DATETIME2(7);
    DECLARE @SaltGuardado VARBINARY(32);
    DECLARE @HashGuardado VARBINARY(32);
    DECLARE @FechaUso DATETIME2(7);
    DECLARE @Vencimiento DATETIME2(7);
    DECLARE @Intentos INT;
    DECLARE @Ahora DATETIME2(7);

    SELECT @Codigo = CodigoCasillero FROM dbo.UsosCasillero WHERE Id = @UsoId;
    IF @Codigo IS NULL
        THROW 50403, 'Retiro no disponible.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Orden de bloqueo consistente con el deposito y las cancelaciones:
        -- primero Casilleros, luego UsosCasillero, finalmente CodigosRetiroPersonal.
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

        IF @EstadoCasillero <> N'Ocupado' OR @EstadoUso <> N'Ocupado'
           OR @TipoServicio <> N'ResguardoPersonal'
           OR @OrigenReserva <> N'Terminal'
           OR @FechaIngreso IS NULL OR @FechaRetiro IS NOT NULL
        BEGIN
            COMMIT TRANSACTION;
            SELECT CAST(0 AS BIT) AS RetiroConfirmado;
            RETURN;
        END;

        SELECT @HashGuardado = PinHash,
               @SaltGuardado = PinSalt,
               @Intentos = IntentosFallidos,
               @FechaUso = FechaUso,
               @Vencimiento = FechaVencimiento
        FROM dbo.CodigosRetiroPersonal WITH (UPDLOCK, HOLDLOCK)
        WHERE UsoId = @UsoId;

        SET @Ahora = SYSUTCDATETIME();

        -- En esta etapa del prototipo FechaVencimiento puede ser NULL.
        -- No se debe caducar una credencial sin un proceso de recuperacion.
        IF @HashGuardado IS NULL OR @SaltGuardado IS NULL OR
           @Intentos IS NULL OR @Intentos >= 5 OR
           @FechaUso IS NOT NULL OR
           (@Vencimiento IS NOT NULL AND @Vencimiento <= @Ahora)
        BEGIN
            COMMIT TRANSACTION;
            SELECT CAST(0 AS BIT) AS RetiroConfirmado;
            RETURN;
        END;

        IF @SaltGuardado <> @PinSaltLeido OR @HashGuardado <> @PinHashCandidato
        BEGIN
            -- NO lanzar THROW tras sumar un intento: provocaria ROLLBACK del contador.
            UPDATE dbo.CodigosRetiroPersonal
            SET IntentosFallidos = IntentosFallidos + 1
            WHERE UsoId = @UsoId AND IntentosFallidos < 5;

            COMMIT TRANSACTION;
            SELECT CAST(0 AS BIT) AS RetiroConfirmado;
            RETURN;
        END;

        IF EXISTS (
            SELECT 1 FROM dbo.UsosCasillero
            WHERE CodigoCasillero = @Codigo
              AND Id <> @UsoId
              AND Estado IN (N'Reservado', N'Ocupado')
              AND FechaRetiro IS NULL
        )
            THROW 50404, 'Otra operacion activa en el casillero.', 1;

        UPDATE dbo.UsosCasillero
        SET Estado = N'Finalizado', FechaRetiro = @Ahora
        WHERE Id = @UsoId AND CodigoCasillero = @Codigo
          AND Estado = N'Ocupado' AND FechaRetiro IS NULL;
        IF @@ROWCOUNT <> 1
            THROW 50405, 'No se pudo registrar el retiro.', 1;

        UPDATE dbo.Casilleros
        SET Estado = N'Disponible'
        WHERE Codigo = @Codigo AND Estado = N'Ocupado';
        IF @@ROWCOUNT <> 1
            THROW 50406, 'No se pudo liberar el casillero.', 1;

        UPDATE dbo.CodigosRetiroPersonal
        SET FechaUso = @Ahora
        WHERE UsoId = @UsoId AND FechaUso IS NULL;
        IF @@ROWCOUNT <> 1
            THROW 50407, 'No se pudo invalidar el PIN.', 1;

        COMMIT TRANSACTION;

        SELECT CAST(1 AS BIT) AS RetiroConfirmado,
               @UsoId AS IdUso,
               @Codigo AS CodigoCasillero,
               N'Disponible' AS EstadoCasillero,
               @Ahora AS FechaRetiro;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
