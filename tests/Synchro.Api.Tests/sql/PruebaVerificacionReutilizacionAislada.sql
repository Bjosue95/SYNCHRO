-- SYNCHRO
-- Prueba: impedir reutilizar una verificacion de inicio de sesion.
-- Solo para la base aislada SynchroDB_Pruebas.
SET NOCOUNT ON;

SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56780, 'Base de datos no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 56781, 'Falta la marca del prototipo.', 1;

DECLARE @PersonaId AS INT;

DECLARE @VerificacionId AS INT;

DECLARE @Telefono AS NVARCHAR (20) = N'+999123456788';

DECLARE @Hash AS VARBINARY (32);

DECLARE @ErrorSegundoUso AS INT;

SET @Hash = HASHBYTES('SHA2_256', CONVERT (VARBINARY (MAX), N'SYNCHRO_PRUEBA_REUTILIZACION'));

BEGIN TRY
    BEGIN TRANSACTION;
    -- Crear persona ficticia.
    INSERT  INTO dbo.Personas (
        Nombre,
        TelefonoWhatsapp,
        TieneCuenta
    )
    VALUES                    (N'Prueba de reutilizacion SYNCHRO', @Telefono, 0);
    SET @PersonaId = CONVERT (INT, SCOPE_IDENTITY());
    -- Crear verificacion.
    EXECUTE dbo.CrearVerificacionTelefonoPendienteSynchro @PersonaId = @PersonaId, @Telefono = @Telefono, @CodigoHash = @Hash, @Finalidad = N'InicioSesionDestinatario';
    SELECT   TOP (1) @VerificacionId = Id
    FROM     dbo.VerificacionesTelefono
    WHERE    PersonaId = @PersonaId
    ORDER BY Id DESC;
    IF @VerificacionId IS NULL
        THROW 56782, 'No se creo la verificacion.', 1;
    -- Validar verificacion.
    EXECUTE dbo.ValidarCodigoTelefonoConFinalidadSynchro @VerificacionId = @VerificacionId, @PersonaId = @PersonaId, @CodigoHash = @Hash, @FinalidadEsperada = N'InicioSesionDestinatario';
    -- Primer consumo: debe funcionar.
    EXECUTE dbo.ConsumirVerificacionInicioSesionSynchro @VerificacionId = @VerificacionId, @PersonaId = @PersonaId;
    PRINT 'PRUEBA 1 APROBADA: Primer consumo permitido.';
    -- Segundo consumo: debe ser rechazado.
    BEGIN TRY
        EXECUTE dbo.ConsumirVerificacionInicioSesionSynchro @VerificacionId = @VerificacionId, @PersonaId = @PersonaId;
        THROW 56783, 'FALLO DE SEGURIDAD: Se permitio reutilizar la verificacion.', 1;
    END TRY
    BEGIN CATCH
        SET @ErrorSegundoUso = ERROR_NUMBER();
        IF @ErrorSegundoUso <> 50222
            THROW;
        PRINT 'PRUEBA 2 APROBADA: Segundo consumo rechazado.';
        PRINT 'Codigo de error esperado: 50222.';
    END CATCH
    -- El procedimiento puede revertir la transaccion completa.
    IF XACT_STATE() <> 0
        ROLLBACK;
    -- Comprobar que no queden datos ficticios.
    IF EXISTS (SELECT 1
               FROM   dbo.Personas
               WHERE  Id = @PersonaId)
        THROW 56784, 'La persona ficticia no fue revertida.', 1;
    PRINT 'PRUEBA 3 APROBADA: Datos ficticios revertidos.';
    PRINT 'TODAS LAS PRUEBAS DE REUTILIZACION APROBADAS.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK;
    THROW;
END CATCH