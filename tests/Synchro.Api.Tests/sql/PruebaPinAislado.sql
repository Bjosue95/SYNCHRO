SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- SYNCHRO: prueba funcional aislada del PIN.
-- No ejecutar sobre la base original.

IF DB_NAME() <> N'SynchroDB'
   OR NOT EXISTS (
       SELECT 1
       FROM sys.database_files
       WHERE type_desc = 'ROWS'
         AND physical_name LIKE
             N'%\SYNCHRO_Aislado_Datos\SynchroDB_Aislado.mdf'
   )
    THROW 51930, 'No es la base aislada.', 1;

DECLARE @UsoId INT;
DECLARE @RemitenteId INT;
DECLARE @DestinatarioId INT;

DECLARE @Codigo NVARCHAR(10) =
    N'IT' + LEFT(CONVERT(NVARCHAR(36), NEWID()), 8);

DECLARE @Hash VARBINARY(32) = CRYPT_GEN_RANDOM(32);
DECLARE @Salt VARBINARY(32) = CRYPT_GEN_RANDOM(32);

DECLARE @Primera TABLE (Resultado NVARCHAR(20));
DECLARE @Segunda TABLE (Resultado NVARCHAR(20));

BEGIN TRY
    BEGIN TRANSACTION;

    -- Crear casillero ficticio.
    INSERT INTO dbo.Casilleros
        (Codigo, Tamano, Tipo, Estado)
    VALUES
        (@Codigo, N'Mediano', N'Normal', N'Ocupado');

    -- Crear remitente ficticio.
    INSERT INTO dbo.Personas
        (Nombre, TelefonoWhatsapp, TieneCuenta)
    VALUES
        (N'Remitente prueba PIN', NULL, 0);

    SET @RemitenteId = SCOPE_IDENTITY();

    -- Crear destinatario ficticio.
    INSERT INTO dbo.Personas
        (Nombre, TelefonoWhatsapp, TieneCuenta)
    VALUES
        (N'Destinatario prueba PIN', NULL, 0);

    SET @DestinatarioId = SCOPE_IDENTITY();

    -- Simular deposito de encomienda.
    INSERT INTO dbo.UsosCasillero
        (CodigoCasillero, TipoServicio,
         FechaReserva, FechaIngreso,
         FechaRetiro, Estado)
    VALUES
        (@Codigo, N'Encomienda',
         DATEADD(MINUTE, -5, SYSUTCDATETIME()),
         SYSUTCDATETIME(),
         NULL, N'Ocupado');

    SET @UsoId = SCOPE_IDENTITY();

    INSERT INTO dbo.ParticipantesUso
        (UsoId, PersonaId, Rol)
    VALUES
        (@UsoId, @RemitenteId, N'Remitente'),
        (@UsoId, @DestinatarioId, N'Destinatario');

    -- Emitir PIN con hash sintetico de prueba.
    EXEC dbo.EmitirPinRetiroSynchro
        @UsoId = @UsoId,
        @DestinatarioId = @DestinatarioId,
        @PinHash = @Hash,
        @PinSalt = @Salt,
        @VigenciaMinutos = 15;

    -- Primera validacion.
    INSERT INTO @Primera
    EXEC dbo.ValidarPinRetiroSynchro
        @UsoId, @DestinatarioId, @Hash, @Salt;

    IF NOT EXISTS (
        SELECT 1 FROM @Primera
        WHERE Resultado = N'Aprobado'
    )
        THROW 51931, 'Fallo la validacion.', 1;

    -- Verificar evidencia.
    IF NOT EXISTS (
        SELECT 1
        FROM dbo.VerificacionesRetiro
        WHERE UsoId = @UsoId
          AND PersonaId = @DestinatarioId
          AND Metodo = N'PIN'
          AND Resultado = N'Aprobado'
    )
        THROW 51932, 'Falta evidencia PIN.', 1;

    -- Intentar reutilizar el PIN.
    INSERT INTO @Segunda
    EXEC dbo.ValidarPinRetiroSynchro
        @UsoId, @DestinatarioId, @Hash, @Salt;

    IF NOT EXISTS (
        SELECT 1 FROM @Segunda
        WHERE Resultado = N'Utilizado'
    )
        THROW 51933, 'Se reutilizo el PIN.', 1;

    -- Deshacer los datos ficticios.
    ROLLBACK TRANSACTION;

    PRINT 'OK: PIN EMITIDO Y VALIDADO';
    PRINT 'OK: REUTILIZACION RECHAZADA';
    PRINT 'OK: TRANSACCION REVERTIDA';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;