-- SYNCHRO
-- Registro de deposito de pertenencias personales
-- Exclusivo para SynchroDB_Pruebas
IF DB_NAME() <> N'SynchroDB_Pruebas'
    BEGIN
        THROW 51090, 'Base de datos incorrecta.', 1;
    END


GO
CREATE OR ALTER PROCEDURE dbo.RegistrarDepositoPersonalSynchro
@UsoId INT, @TokenDeposito VARBINARY (32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- 1. Validar los datos recibidos.
    IF @UsoId IS NULL
       OR @UsoId <= 0
        BEGIN
            THROW 50200, 'Identificador invalido.', 1;
        END
    IF @TokenDeposito IS NULL
       OR DATALENGTH(@TokenDeposito) <> 32
        BEGIN
            THROW 50201, 'Token de deposito invalido.', 1;
        END
    -- 2. Declarar las variables.
    DECLARE @CodigoCasillero AS NVARCHAR (10);
    DECLARE @EstadoCasillero AS NVARCHAR (20);
    DECLARE @EstadoReserva AS NVARCHAR (20);
    DECLARE @TipoServicio AS NVARCHAR (20);
    DECLARE @OrigenReserva AS NVARCHAR (20);
    DECLARE @FechaVencimiento AS DATETIME2 (7);
    DECLARE @FechaIngreso AS DATETIME2 (7);
    DECLARE @FechaRetiro AS DATETIME2 (7);
    DECLARE @HashGuardado AS VARBINARY (32);
    DECLARE @Ahora AS DATETIME2 (7);
    -- 3. Identificar el casillero de la reserva.
    SELECT @CodigoCasillero = CodigoCasillero
    FROM   dbo.UsosCasillero
    WHERE  Id = @UsoId;
    IF @CodigoCasillero IS NULL
        BEGIN
            THROW 50202, 'Reserva no encontrada.', 1;
        END
    BEGIN TRY
        BEGIN TRANSACTION;
        -- 4. Bloquear y verificar el casillero.
        SELECT @EstadoCasillero = Estado
        FROM   dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE  Codigo = @CodigoCasillero;
        IF @EstadoCasillero IS NULL
            BEGIN
                THROW 50203, 'Casillero no encontrado.', 1;
            END
        IF @EstadoCasillero <> N'Reservado'
            BEGIN
                THROW 50204, 'El casillero no esta reservado.', 1;
            END
        -- 5. Bloquear y consultar la reserva.
        SELECT @EstadoReserva = Estado,
               @TipoServicio = TipoServicio,
               @OrigenReserva = OrigenReserva,
               @FechaVencimiento = FechaVencimiento,
               @FechaIngreso = FechaIngreso,
               @FechaRetiro = FechaRetiro,
               @HashGuardado = TokenDepositoHash
        FROM   dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE  Id = @UsoId
               AND CodigoCasillero = @CodigoCasillero;
        IF @EstadoReserva IS NULL
           OR @EstadoReserva <> N'Reservado'
            BEGIN
                THROW 50205, 'La reserva no esta activa.', 1;
            END
        -- 6. Permitir solamente resguardo personal
        -- realizado desde la terminal.
        IF @TipoServicio <> N'ResguardoPersonal'
           OR @OrigenReserva <> N'Terminal'
            BEGIN
                THROW 50206, 'Servicio no permitido.', 1;
            END
        -- 7. Impedir depositos duplicados.
        IF @FechaIngreso IS NOT NULL
           OR @FechaRetiro IS NOT NULL
            BEGIN
                THROW 50207, 'Operacion ya procesada.', 1;
            END
        -- 8. Verificar el vencimiento.
        SET @Ahora = SYSUTCDATETIME();
        IF @FechaVencimiento IS NULL
           OR @FechaVencimiento <= @Ahora
            BEGIN
                THROW 50208, 'La reserva ha vencido.', 1;
            END
        -- 9. Validar el token secreto.
        IF @HashGuardado IS NULL
           OR @HashGuardado <> HASHBYTES('SHA2_256', @TokenDeposito)
            BEGIN
                THROW 50209, 'Token de deposito incorrecto.', 1;
            END
        -- 10. Comprobar otras operaciones activas.
        IF EXISTS (SELECT 1
                   FROM   dbo.UsosCasillero
                   WHERE  CodigoCasillero = @CodigoCasillero
                          AND Id <> @UsoId
                          AND Estado IN (N'Reservado', N'Ocupado')
                          AND FechaRetiro IS NULL)
            BEGIN
                THROW 50210, 'Existe otra operacion activa.', 1;
            END
        -- 11. Registrar el deposito.
        UPDATE dbo.UsosCasillero
        SET    Estado       = N'Ocupado',
               FechaIngreso = @Ahora
        WHERE  Id = @UsoId
               AND CodigoCasillero = @CodigoCasillero
               AND Estado = N'Reservado'
               AND FechaIngreso IS NULL
               AND FechaRetiro IS NULL
               AND FechaVencimiento > @Ahora;
        IF @@ROWCOUNT <> 1
            BEGIN
                THROW 50211, 'No se pudo registrar el deposito.', 1;
            END
        -- 12. Cambiar el estado del casillero.
        UPDATE dbo.Casilleros
        SET    Estado = N'Ocupado'
        WHERE  Codigo = @CodigoCasillero
               AND Estado = N'Reservado';
        IF @@ROWCOUNT <> 1
            BEGIN
                THROW 50212, 'No se pudo ocupar el casillero.', 1;
            END
        -- 13. Confirmar los cambios.
        COMMIT TRANSACTION;
        -- 14. Devolver el resultado.
        SELECT u.Id AS IdUso,
               u.CodigoCasillero,
               u.TipoServicio,
               u.Estado AS EstadoOperacion,
               c.Estado AS EstadoCasillero,
               u.FechaIngreso
        FROM   dbo.UsosCasillero AS u
               INNER JOIN
               dbo.Casilleros AS c
               ON c.Codigo = u.CodigoCasillero
        WHERE  u.Id = @UsoId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            BEGIN
                ROLLBACK;
            END;
        THROW;
    END CATCH
END