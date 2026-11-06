
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Nunca ejecutar sobre la base original.
IF DB_NAME() <> N'SynchroDB_Detalle_Pruebas'
    THROW 51200, 'Base de pruebas incorrecta.', 1;

IF OBJECT_ID(N'dbo.EmitirPinRetiroSynchro', N'P') IS NULL
    THROW 51201, 'Falta el procedimiento de emision.', 1;

DECLARE @UsoId INT;
DECLARE @DestinatarioId INT;
DECLARE @RemitenteId INT;
DECLARE @CodigoCasillero NVARCHAR(10);
DECLARE @Hash VARBINARY(32);
DECLARE @Salt VARBINARY(32);

BEGIN TRY
    BEGIN TRANSACTION;

    -- Crear un casillero completamente ficticio.
    SET @CodigoCasillero =
        N'IT' + LEFT(CONVERT(NVARCHAR(36), NEWID()), 8);

    INSERT INTO dbo.Casilleros
        (Codigo, Tamano, Tipo, Estado)
    VALUES
        (@CodigoCasillero, N'Pequeño', N'Normal', N'Ocupado');

    -- Crear remitente ficticio.
    INSERT INTO dbo.Personas
        (Nombre, TelefonoWhatsapp, TieneCuenta)
    VALUES
        (N'Remitente de prueba PIN', NULL, 0);

    SET @RemitenteId = SCOPE_IDENTITY();

    -- Crear destinatario ficticio.
    INSERT INTO dbo.Personas
        (Nombre, TelefonoWhatsapp, TieneCuenta)
    VALUES
        (N'Destinatario de prueba PIN', NULL, 0);

    SET @DestinatarioId = SCOPE_IDENTITY();

    -- Simular una encomienda depositada.
    INSERT INTO dbo.UsosCasillero
    (
        CodigoCasillero,
        TipoServicio,
        FechaReserva,
        FechaIngreso,
        FechaRetiro,
        Estado
    )
    VALUES
    (
        @CodigoCasillero,
        N'Encomienda',
        DATEADD(MINUTE, -5, SYSUTCDATETIME()),
        SYSUTCDATETIME(),
        NULL,
        N'Ocupado'
    );

    SET @UsoId = SCOPE_IDENTITY();

    INSERT INTO dbo.ParticipantesUso
        (UsoId, PersonaId, Rol)
    VALUES
        (@UsoId, @RemitenteId, N'Remitente'),
        (@UsoId, @DestinatarioId, N'Destinatario');

    -- Valores binarios sinteticos, NO un PIN real.
    SET @Hash = CRYPT_GEN_RANDOM(32);
    SET @Salt = CRYPT_GEN_RANDOM(32);

    EXEC dbo.EmitirPinRetiroSynchro
        @UsoId = @UsoId,
        @DestinatarioId = @DestinatarioId,
        @PinHash = @Hash,
        @PinSalt = @Salt,
        @VigenciaMinutos = 15;

    -- Comprobar que el procedimiento registro el PIN.
    IF NOT EXISTS (
        SELECT 1
        FROM dbo.CodigosRetiro
        WHERE UsoId = @UsoId
          AND PinHash = @Hash
          AND PinSalt = @Salt
          AND IntentosFallidos = 0
          AND FechaUso IS NULL
          AND FechaVencimiento > SYSUTCDATETIME()
    )
        THROW 51202, 'La emision no fue correcta.', 1;
    -- PRUEBA: PIN correcto, pero vencido.
    -- Cambiar las fechas solo en los datos ficticios.
    UPDATE dbo.CodigosRetiro
    SET
        FechaCreacion =
            DATEADD(MINUTE, -30, SYSUTCDATETIME()),
        FechaVencimiento =
            DATEADD(MINUTE, -1, SYSUTCDATETIME())
    WHERE UsoId = @UsoId;

    IF @@ROWCOUNT <> 1
        THROW 51450, 'No se preparo el PIN vencido.', 1;

    DECLARE @ResultadoPin TABLE (
        Resultado NVARCHAR(20)
    );

    INSERT INTO @ResultadoPin (Resultado)
    EXEC dbo.ValidarPinRetiroSynchro
        @UsoId = @UsoId,
        @DestinatarioId = @DestinatarioId,
        @PinHashCandidato = @Hash,
        @PinSaltLeido = @Salt;

    IF (SELECT COUNT(*) FROM @ResultadoPin) <> 1
       OR NOT EXISTS (
           SELECT 1
           FROM @ResultadoPin
           WHERE Resultado = N'Vencido'
       )
        THROW 51451, 'Se acepto un PIN vencido.', 1;

    IF EXISTS (
        SELECT 1
        FROM dbo.VerificacionesRetiro
        WHERE UsoId = @UsoId
          AND Metodo = N'PIN'
          AND Resultado = N'Aprobado'
    )
        THROW 51452, 'PIN vencido aprobado.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.CodigosRetiro
        WHERE UsoId = @UsoId
          AND IntentosFallidos = 0
          AND FechaUso IS NULL
    )
        THROW 51453, 'Se modifico indebidamente el PIN.', 1;

    PRINT 'OK: PIN VENCIDO RECHAZADO';
    PRINT 'OK: SIN APROBACION INDEBIDA';
    PRINT 'OK: PIN VENCIDO NO CONSUMIDO';
-- Deshacer TODOS los datos ficticios.
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    -- Confirmar que no quedo el PIN de prueba.
    IF EXISTS (
        SELECT 1
        FROM dbo.CodigosRetiro
        WHERE UsoId = @UsoId
    )
        THROW 51203, 'El PIN ficticio no fue eliminado.', 1;

    PRINT 'OK: EMISION FICTICIA CORRECTA';
    PRINT 'OK: TRANSACCION REVERTIDA';
    PRINT 'OK: SIN DATOS DE PRUEBA PERSISTENTES';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
