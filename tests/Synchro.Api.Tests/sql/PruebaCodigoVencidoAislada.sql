-- SYNCHRO
-- Prueba funcional: rechazo de codigos vencidos.
-- Exclusiva de SynchroDB_Pruebas.
-- Los datos ficticios se revierten al finalizar.
SET NOCOUNT ON;

SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56820, 'Base de datos no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 56821, 'Falta la marca del prototipo.', 1;

DECLARE @PersonaId AS INT;

DECLARE @VerificacionId AS INT;

DECLARE @Telefono AS NVARCHAR (20) = N'+999123456786';

DECLARE @HashCorrecto AS VARBINARY (32);

SET @HashCorrecto = HASHBYTES('SHA2_256', CONVERT (VARBINARY (MAX), N'SYNCHRO_PRUEBA_VENCIMIENTO'));

BEGIN TRY
    BEGIN TRANSACTION;
    -- Crear persona ficticia.
    INSERT  INTO dbo.Personas (
        Nombre,
        TelefonoWhatsapp,
        TieneCuenta
    )
    VALUES                    (N'Prueba de vencimiento SYNCHRO', @Telefono, 0);
    SET @PersonaId = CONVERT (INT, SCOPE_IDENTITY());
    -- Crear verificacion pendiente.
    EXECUTE dbo.CrearVerificacionTelefonoPendienteSynchro @PersonaId = @PersonaId, @Telefono = @Telefono, @CodigoHash = @HashCorrecto, @Finalidad = N'InicioSesionDestinatario';
    SELECT   TOP (1) @VerificacionId = Id
    FROM     dbo.VerificacionesTelefono
    WHERE    PersonaId = @PersonaId
    ORDER BY Id DESC;
    IF @VerificacionId IS NULL
        THROW 56822, 'No se creo la verificacion.', 1;
    PRINT 'PRUEBA 1 APROBADA: Verificacion creada.';
    -- Simular que el codigo ya vencio.
    UPDATE dbo.VerificacionesTelefono
    SET    FechaExpiracion = DATEADD(MINUTE, -1, SYSUTCDATETIME())
    WHERE  Id = @VerificacionId
           AND PersonaId = @PersonaId
           AND Estado = N'Pendiente';
    IF @@ROWCOUNT <> 1
        THROW 56823, 'No se pudo preparar el vencimiento.', 1;
    PRINT 'PRUEBA 2 APROBADA: Vencimiento simulado.';
    -- Intentar validar usando el codigo correcto.
    EXECUTE dbo.ValidarCodigoTelefonoConFinalidadSynchro @VerificacionId = @VerificacionId, @PersonaId = @PersonaId, @CodigoHash = @HashCorrecto, @FinalidadEsperada = N'InicioSesionDestinatario';
    -- La verificacion debe quedar vencida.
    IF NOT EXISTS (SELECT 1
                   FROM   dbo.VerificacionesTelefono
                   WHERE  Id = @VerificacionId
                          AND PersonaId = @PersonaId
                          AND Estado = N'Vencido'
                          AND CodigoHash IS NULL
                          AND FechaVerificacion IS NULL
                          AND FechaSesionEmitida IS NULL)
        THROW 56824, 'Fallo el rechazo del codigo vencido.', 1;
    PRINT 'PRUEBA 3 APROBADA: Codigo vencido rechazado.';
    PRINT 'El hash fue eliminado correctamente.';
    -- Comprobar que el codigo no pueda reactivarse.
    EXECUTE dbo.ValidarCodigoTelefonoConFinalidadSynchro @VerificacionId = @VerificacionId, @PersonaId = @PersonaId, @CodigoHash = @HashCorrecto, @FinalidadEsperada = N'InicioSesionDestinatario';
    IF NOT EXISTS (SELECT 1
                   FROM   dbo.VerificacionesTelefono
                   WHERE  Id = @VerificacionId
                          AND Estado = N'Vencido'
                          AND CodigoHash IS NULL
                          AND FechaVerificacion IS NULL
                          AND FechaSesionEmitida IS NULL)
        THROW 56825, 'Se reactivo una verificacion vencida.', 1;
    PRINT 'PRUEBA 4 APROBADA: Codigo vencido no reactivado.';
    -- Revertir los datos ficticios.
    ROLLBACK;
    IF EXISTS (SELECT 1
               FROM   dbo.Personas
               WHERE  Id = @PersonaId)
        THROW 56826, 'No se revirtieron los datos ficticios.', 1;
    PRINT 'PRUEBA 5 APROBADA: Datos ficticios revertidos.';
    PRINT 'TODAS LAS PRUEBAS DE VENCIMIENTO APROBADAS.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK;
    THROW;
END CATCH