-- SYNCHRO
-- Prueba funcional de verificacion telefonica.
-- Utiliza datos ficticios y revierte los cambios.
SET NOCOUNT ON;

SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56700, 'Base no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 56701, 'Falta la marca del prototipo.', 1;

DECLARE @PersonaId AS INT;

DECLARE @VerificacionId AS INT;

DECLARE @Telefono AS NVARCHAR (20) = N'+999123456789';

DECLARE @Hash AS VARBINARY (32);

-- Hash sintético: no representa un código real.
SET @Hash = HASHBYTES('SHA2_256', CONVERT (VARBINARY (MAX), N'SYNCHRO_PRUEBA_LOCAL'));

BEGIN TRY
    BEGIN TRANSACTION;
    -- Crear persona ficticia.
    INSERT  INTO dbo.Personas (
        Nombre,
        TelefonoWhatsapp,
        TieneCuenta
    )
    VALUES                    (N'Persona de prueba SYNCHRO', @Telefono, 0);
    SET @PersonaId = CONVERT (INT, SCOPE_IDENTITY());
    -- Crear una verificacion pendiente.
    EXECUTE dbo.CrearVerificacionTelefonoPendienteSynchro @PersonaId = @PersonaId, @Telefono = @Telefono, @CodigoHash = @Hash, @Finalidad = N'InicioSesionDestinatario';
    SELECT   TOP (1) @VerificacionId = Id
    FROM     dbo.VerificacionesTelefono
    WHERE    PersonaId = @PersonaId
    ORDER BY Id DESC;
    IF @VerificacionId IS NULL
        THROW 56702, 'No se creo la verificacion.', 1;
    PRINT 'PRUEBA 1: Verificacion creada correctamente.';
    -- Validar el hash y su finalidad.
    EXECUTE dbo.ValidarCodigoTelefonoConFinalidadSynchro @VerificacionId = @VerificacionId, @PersonaId = @PersonaId, @CodigoHash = @Hash, @FinalidadEsperada = N'InicioSesionDestinatario';
    IF NOT EXISTS (SELECT 1
                   FROM   dbo.VerificacionesTelefono
                   WHERE  Id = @VerificacionId
                          AND Estado = N'Verificado'
                          AND CodigoHash IS NULL)
        THROW 56703, 'Fallo la validacion.', 1;
    PRINT 'PRUEBA 2: Codigo validado correctamente.';
    -- Consumir la verificacion.
    EXECUTE dbo.ConsumirVerificacionInicioSesionSynchro @VerificacionId = @VerificacionId, @PersonaId = @PersonaId;
    IF NOT EXISTS (SELECT 1
                   FROM   dbo.VerificacionesTelefono
                   WHERE  Id = @VerificacionId
                          AND FechaSesionEmitida IS NOT NULL)
        THROW 56704, 'No se consumio la verificacion.', 1;
    PRINT 'PRUEBA 3: Verificacion consumida correctamente.';
    -- Deshacer todos los datos ficticios.
    ROLLBACK;
    PRINT 'PRUEBAS FINALIZADAS.';
    PRINT 'Datos ficticios revertidos.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK;
    THROW;
END CATCH