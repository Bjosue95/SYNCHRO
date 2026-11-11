-- SYNCHRO
-- Procedimientos de verificacion telefonica.
-- Exclusivos de SynchroDB_Pruebas.
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56650, 'Base no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 56651, 'Base de pruebas sin marca.', 1;

IF OBJECT_ID(N'dbo.VerificacionesTelefono', N'U') IS NULL
    THROW 56652, 'Falta la tabla VerificacionesTelefono.', 1;

IF COL_LENGTH(N'dbo.Personas', N'TelefonoWhatsapp') IS NULL
    THROW 56653, 'Falta la columna TelefonoWhatsapp.', 1;


GO
CREATE OR ALTER PROCEDURE dbo.CrearVerificacionTelefonoPendienteSynchro
@PersonaId INT, @Telefono NVARCHAR (20), @CodigoHash VARBINARY (32), @Finalidad NVARCHAR (40)=N'PruebaAdministrativa'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Telefono = NULLIF (LTRIM(RTRIM(@Telefono)), N'');
    IF @PersonaId IS NULL
       OR @PersonaId <= 0
        THROW 50100, 'Persona invalida.', 1;
    IF @Telefono IS NULL
       OR LEN(@Telefono) < 9
       OR LEN(@Telefono) > 16
       OR LEFT(@Telefono, 1) <> N'+'
       OR SUBSTRING(@Telefono, 2, 20) LIKE N'%[^0-9]%'
       OR SUBSTRING(@Telefono, 2, 1) = N'0'
        THROW 50101, 'Telefono internacional invalido.', 1;
    IF @CodigoHash IS NULL
       OR DATALENGTH(@CodigoHash) <> 32
        THROW 50102, 'Hash invalido.', 1;
    IF @Finalidad IS NULL
       OR @Finalidad NOT IN (N'PruebaAdministrativa', N'InicioSesionDestinatario', N'ConsentimientoWhatsApp')
        THROW 50109, 'Finalidad invalida.', 1;
    DECLARE @TelefonoRegistrado AS NVARCHAR (20);
    DECLARE @Ahora AS DATETIME2 (7);
    DECLARE @VerificacionId AS INT;
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @TelefonoRegistrado = TelefonoWhatsapp
        FROM   dbo.Personas WITH (UPDLOCK, HOLDLOCK)
        WHERE  Id = @PersonaId;
        IF @TelefonoRegistrado IS NULL
            THROW 50103, 'Persona sin telefono registrado.', 1;
        IF @TelefonoRegistrado <> @Telefono
            THROW 50104, 'Telefono no coincide.', 1;
        SET @Ahora = SYSUTCDATETIME();
        IF EXISTS (SELECT 1
                   FROM   dbo.VerificacionesTelefono
                   WHERE  PersonaId = @PersonaId
                          AND Telefono = @Telefono
                          AND Estado = N'Pendiente'
                          AND FechaExpiracion > @Ahora)
            THROW 50105, 'Ya existe una verificacion vigente.', 1;
        IF EXISTS (SELECT 1
                   FROM   dbo.VerificacionesTelefono
                   WHERE  PersonaId = @PersonaId
                          AND Telefono = @Telefono
                          AND FechaSolicitud > DATEADD(SECOND, -60, @Ahora))
            THROW 50106, 'Debe esperar antes de solicitar otro codigo.', 1;
        IF (SELECT COUNT(*)
            FROM   dbo.VerificacionesTelefono
            WHERE  PersonaId = @PersonaId
                   AND Telefono = @Telefono
                   AND FechaSolicitud > DATEADD(HOUR, -1, @Ahora)) >= 3
            THROW 50107, 'Limite de tres solicitudes por hora.', 1;
        IF (SELECT COUNT(*)
            FROM   dbo.VerificacionesTelefono
            WHERE  PersonaId = @PersonaId
                   AND Telefono = @Telefono
                   AND FechaSolicitud > DATEADD(HOUR, -24, @Ahora)) >= 6
            THROW 50108, 'Limite de seis solicitudes en 24 horas.', 1;
        UPDATE dbo.VerificacionesTelefono
        SET    Estado     = N'Vencido',
               CodigoHash = NULL
        WHERE  PersonaId = @PersonaId
               AND Telefono = @Telefono
               AND Estado = N'Pendiente'
               AND (FechaExpiracion IS NULL
                    OR FechaExpiracion <= @Ahora);
        INSERT  INTO dbo.VerificacionesTelefono (
            PersonaId,
            Telefono,
            Estado,
            FechaSolicitud,
            CodigoHash,
            FechaExpiracion,
            IntentosFallidos,
            Finalidad
        )
        VALUES                                  (@PersonaId, @Telefono, N'Pendiente', @Ahora, @CodigoHash, DATEADD(MINUTE, 5, @Ahora), 0, @Finalidad);
        SET @VerificacionId = CONVERT (INT, SCOPE_IDENTITY());
        COMMIT TRANSACTION;
        SELECT Id,
               PersonaId,
               Telefono,
               Estado,
               Finalidad,
               FechaSolicitud,
               FechaExpiracion,
               IntentosFallidos
        FROM   dbo.VerificacionesTelefono
        WHERE  Id = @VerificacionId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;
        THROW;
    END CATCH
END