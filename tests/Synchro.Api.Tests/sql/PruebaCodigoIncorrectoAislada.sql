
-- SYNCHRO
-- Prueba de codigo incorrecto y bloqueo por cinco intentos.
-- Exclusiva de SynchroDB_Pruebas.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56800, 'Base de datos no autorizada.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.extended_properties
    WHERE class = 0
      AND name = N'SynchroFixtureDeposito'
)
    THROW 56801, 'Falta la marca del prototipo.', 1;

DECLARE @PersonaId INT;
DECLARE @VerificacionId INT;
DECLARE @Telefono NVARCHAR(20) = N'+999123456787';
DECLARE @HashCorrecto VARBINARY(32);
DECLARE @HashIncorrecto VARBINARY(32);
DECLARE @Intento INT = 1;
DECLARE @Estado NVARCHAR(20);
DECLARE @IntentosRegistrados INT;
DECLARE @HashGuardado VARBINARY(32);

SET @HashCorrecto = HASHBYTES(
    'SHA2_256',
    CONVERT(VARBINARY(MAX), N'SYNCHRO_CODIGO_CORRECTO')
);

SET @HashIncorrecto = HASHBYTES(
    'SHA2_256',
    CONVERT(VARBINARY(MAX), N'SYNCHRO_CODIGO_INCORRECTO')
);

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO dbo.Personas (
        Nombre,
        TelefonoWhatsapp,
        TieneCuenta
    )
    VALUES (
        N'Prueba de bloqueo SYNCHRO',
        @Telefono,
        0
    );

    SET @PersonaId = CONVERT(INT, SCOPE_IDENTITY());

    EXEC dbo.CrearVerificacionTelefonoPendienteSynchro
        @PersonaId = @PersonaId,
        @Telefono = @Telefono,
        @CodigoHash = @HashCorrecto,
        @Finalidad = N'InicioSesionDestinatario';

    SELECT TOP (1)
        @VerificacionId = Id
    FROM dbo.VerificacionesTelefono
    WHERE PersonaId = @PersonaId
    ORDER BY Id DESC;

    IF @VerificacionId IS NULL
        THROW 56802, 'No se creo la verificacion.', 1;

    PRINT 'PRUEBA 1 APROBADA: Verificacion creada.';

    -- Realizar cinco intentos con un hash incorrecto.
    WHILE @Intento <= 5
    BEGIN
        EXEC dbo.ValidarCodigoTelefonoConFinalidadSynchro
            @VerificacionId = @VerificacionId,
            @PersonaId = @PersonaId,
            @CodigoHash = @HashIncorrecto,
            @FinalidadEsperada = N'InicioSesionDestinatario';

        SELECT
            @Estado = Estado,
            @IntentosRegistrados = IntentosFallidos,
            @HashGuardado = CodigoHash
        FROM dbo.VerificacionesTelefono
        WHERE Id = @VerificacionId;

        IF @IntentosRegistrados <> @Intento
            THROW 56803, 'Conteo incorrecto de intentos.', 1;

        IF @Intento < 5
        BEGIN
            IF @Estado <> N'Pendiente'
               OR @HashGuardado IS NULL
                THROW 56804, 'Bloqueo anticipado o hash perdido.', 1;
        END
        ELSE
        BEGIN
            IF @Estado <> N'Bloqueado'
               OR @HashGuardado IS NOT NULL
                THROW 56805, 'No se bloqueo el quinto intento.', 1;
        END;

        PRINT 'Intento incorrecto comprobado.';
        SET @Intento = @Intento + 1;
    END;

    PRINT 'PRUEBA 2 APROBADA: Bloqueo en el quinto intento.';

    -- Un codigo correcto no debe desbloquear la verificacion.
    EXEC dbo.ValidarCodigoTelefonoConFinalidadSynchro
        @VerificacionId = @VerificacionId,
        @PersonaId = @PersonaId,
        @CodigoHash = @HashCorrecto,
        @FinalidadEsperada = N'InicioSesionDestinatario';

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.VerificacionesTelefono
        WHERE Id = @VerificacionId
          AND Estado = N'Bloqueado'
          AND IntentosFallidos = 5
          AND CodigoHash IS NULL
          AND FechaVerificacion IS NULL
    )
        THROW 56806, 'Se permitio validar un codigo bloqueado.', 1;

    PRINT 'PRUEBA 3 APROBADA: Codigo correcto rechazado tras bloqueo.';

    ROLLBACK TRANSACTION;

    IF EXISTS (
        SELECT 1
        FROM dbo.Personas
        WHERE Id = @PersonaId
    )
        THROW 56807, 'Los datos ficticios no se revirtieron.', 1;

    PRINT 'PRUEBA 4 APROBADA: Datos ficticios revertidos.';
    PRINT 'TODAS LAS PRUEBAS DE BLOQUEO APROBADAS.';

END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
