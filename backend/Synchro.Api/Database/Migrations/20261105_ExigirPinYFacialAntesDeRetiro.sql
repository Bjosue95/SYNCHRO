-- SYNCHRO: control preventivo del retiro en base de DESARROLLO SynchroDB.
-- Version: 2026-11-05 / paso 452.
-- La logica de este procedimiento fue verificada previamente SOLO EN CLON durante V9.
-- No valida por si misma rostros ni codigos: exige filas de evidencia generadas
-- por futuros componentes confiables. No instalar en produccion.
-- El lanzador 02 crea y verifica un BACKUP COPY_ONLY antes de aplicar.
-- Se ejecuta con sqlcmd -b (detiene la ejecucion si SQL informa error).
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'SynchroDB'
    THROW 51061, 'La migracion solo se permite en la base local SynchroDB.', 1;
IF OBJECT_ID(N'dbo.RegistrarRetiroEncomiendaSynchro', N'P') IS NULL
    THROW 51062, 'No se encontro el procedimiento de retiro.', 1;
IF OBJECT_ID(N'dbo.VerificacionesRetiro', N'U') IS NULL
    THROW 51063, 'No se encontro la tabla VerificacionesRetiro.', 1;
IF OBJECT_ID(N'dbo.ParticipantesUso', N'U') IS NULL
    THROW 51064, 'No se encontro la tabla ParticipantesUso.', 1;
-- Se mantiene una transaccion SQL entre los bloques GO de sqlcmd.
-- Si el proceso se detiene, la conexion termina y SQL revierte la transaccion.
BEGIN TRANSACTION;
GO
CREATE OR ALTER PROCEDURE dbo.RegistrarRetiroEncomiendaSynchro
    @UsoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @UsoId IS NULL OR @UsoId <= 0
        THROW 50050, 'Identificador invalido.', 1;

    DECLARE @CodigoCasillero NVARCHAR(10);
    DECLARE @EstadoCasillero NVARCHAR(20);
    DECLARE @EstadoUso NVARCHAR(20);
    DECLARE @TipoServicio NVARCHAR(20);
    DECLARE @FechaIngreso DATETIME2;
    DECLARE @FechaRetiro DATETIME2;

    SELECT @CodigoCasillero = CodigoCasillero
    FROM dbo.UsosCasillero
    WHERE Id = @UsoId;

    IF @CodigoCasillero IS NULL
        THROW 50051, 'Operacion no encontrada.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @EstadoCasillero = Estado
        FROM dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE Codigo = @CodigoCasillero;

        IF @EstadoCasillero IS NULL
            THROW 50052, 'Casillero no encontrado.', 1;

        IF @EstadoCasillero <> N'Ocupado'
            THROW 50053, 'El casillero no esta ocupado.', 1;

        SELECT
            @EstadoUso = Estado,
            @TipoServicio = TipoServicio,
            @FechaIngreso = FechaIngreso,
            @FechaRetiro = FechaRetiro
        FROM dbo.UsosCasillero WITH (UPDLOCK, HOLDLOCK)
        WHERE Id = @UsoId
          AND CodigoCasillero = @CodigoCasillero;

        IF @EstadoUso IS NULL OR @EstadoUso <> N'Ocupado'
            THROW 50054, 'La operacion no esta ocupada.', 1;

        IF @TipoServicio <> N'Encomienda'
            THROW 50055, 'La operacion no es una encomienda.', 1;

        IF @FechaIngreso IS NULL OR @FechaRetiro IS NOT NULL
            THROW 50056, 'El retiro no es valido.', 1;

        IF EXISTS (
            SELECT 1
            FROM dbo.UsosCasillero
            WHERE CodigoCasillero = @CodigoCasillero
              AND Id <> @UsoId
              AND Estado IN (N'Reservado', N'Ocupado')
              AND FechaRetiro IS NULL
        )
            THROW 50057, 'Existe otra operacion activa.', 1;

        -- SYNCHRO_GUARD_PIN_FACIAL_V9
        -- Esta prueba exige las dos verificaciones del destinatario,
        -- asociadas a esta operacion y registradas despues del deposito.
        -- Son evidencias sinteticas: la generacion/verificacion real de
        -- PIN y biometria debera implementarse en un servicio confiable.
        DECLARE @DestinatarioId INT;
        DECLARE @AhoraVerificacion DATETIME2 = SYSUTCDATETIME();

        SELECT @DestinatarioId = PersonaId
        FROM dbo.ParticipantesUso WITH (HOLDLOCK)
        WHERE UsoId = @UsoId AND Rol = N'Destinatario';

        IF @DestinatarioId IS NULL
           OR NOT EXISTS (
                SELECT 1
                FROM dbo.VerificacionesRetiro WITH (HOLDLOCK)
                WHERE UsoId = @UsoId
                  AND PersonaId = @DestinatarioId
                  AND Metodo = N'PIN'
                  AND Resultado = N'Aprobado'
                  AND FechaVerificacion >= @FechaIngreso
                  AND FechaVerificacion >= DATEADD(MINUTE, -10, @AhoraVerificacion)
                  AND FechaVerificacion <= @AhoraVerificacion
           )
           OR NOT EXISTS (
                SELECT 1
                FROM dbo.VerificacionesRetiro WITH (HOLDLOCK)
                WHERE UsoId = @UsoId
                  AND PersonaId = @DestinatarioId
                  AND Metodo = N'Facial'
                  AND Resultado = N'Aprobado'
                  AND FechaVerificacion >= @FechaIngreso
                  AND FechaVerificacion >= DATEADD(MINUTE, -10, @AhoraVerificacion)
                  AND FechaVerificacion <= @AhoraVerificacion
           )
            -- 50058 esta gestionado como HTTP 409 por el backend ACTUAL.
            -- Para produccion crear codigo distinto y mapearlo en la API.
            THROW 50058, 'PIN y facial del destinatario vigentes son obligatorios.', 1;

        UPDATE dbo.UsosCasillero
        SET Estado = N'Finalizado',
            FechaRetiro = SYSUTCDATETIME()
        WHERE Id = @UsoId
          AND Estado = N'Ocupado'
          AND FechaIngreso IS NOT NULL
          AND FechaRetiro IS NULL;

        IF @@ROWCOUNT <> 1
            THROW 50058, 'No se pudo finalizar la operacion.', 1;

        UPDATE dbo.Casilleros
        SET Estado = N'Disponible'
        WHERE Codigo = @CodigoCasillero
          AND Estado = N'Ocupado';

        IF @@ROWCOUNT <> 1
            THROW 50059, 'No se pudo liberar el casillero.', 1;

        COMMIT TRANSACTION;

        SELECT
            u.Id AS IdUso,
            u.CodigoCasillero,
            u.TipoServicio,
            u.Estado AS EstadoOperacion,
            c.Tipo AS TipoCasillero,
            c.Estado AS EstadoCasillero,
            u.FechaIngreso,
            u.FechaRetiro
        FROM dbo.UsosCasillero u
        INNER JOIN dbo.Casilleros c
            ON c.Codigo = u.CodigoCasillero
        WHERE u.Id = @UsoId;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
GO
IF DB_NAME() <> N'SynchroDB'
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51065, 'La base activa cambio durante la migracion.', 1;
END;
IF OBJECT_DEFINITION(OBJECT_ID(N'dbo.RegistrarRetiroEncomiendaSynchro'))
       NOT LIKE N'%SYNCHRO_GUARD_PIN_FACIAL_V9%'
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51066, 'No se encontro el control PIN y Facial luego del cambio.', 1;
END;
IF @@TRANCOUNT <> 1
    THROW 51067, 'Transaccion inesperada durante migracion.', 1;
COMMIT TRANSACTION;
PRINT 'MIGRACION SYNCHRO INSTALADA EN BASE LOCAL DE DESARROLLO';
GO
