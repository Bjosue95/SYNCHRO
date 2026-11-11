-- SYNCHRO
-- Consumo unico de verificacion telefonica.
-- Exclusivo de SynchroDB_Pruebas.
SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56670, 'Base de datos no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 56671, 'Falta la marca del prototipo.', 1;

IF OBJECT_ID(N'dbo.VerificacionesTelefono', N'U') IS NULL
    THROW 56672, 'Falta VerificacionesTelefono.', 1;


GO
CREATE OR ALTER PROCEDURE dbo.ConsumirVerificacionInicioSesionSynchro
@VerificacionId INT, @PersonaId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 56673, 'Base no autorizada.', 1;
    IF @VerificacionId IS NULL
       OR @VerificacionId <= 0
        THROW 50220, 'Verificacion invalida.', 1;
    IF @PersonaId IS NULL
       OR @PersonaId <= 0
        THROW 50221, 'Persona invalida.', 1;
    DECLARE @Ahora AS DATETIME2 (7) = SYSUTCDATETIME();
    BEGIN TRY
        BEGIN TRANSACTION;
        -- Consumir atomicamente una verificacion confirmada.
        UPDATE dbo.VerificacionesTelefono
        SET    FechaSesionEmitida = @Ahora
        WHERE  Id = @VerificacionId
               AND PersonaId = @PersonaId
               AND Finalidad = N'InicioSesionDestinatario'
               AND Estado = N'Verificado'
               AND FechaVerificacion IS NOT NULL
               AND CodigoHash IS NULL
               AND FechaSesionEmitida IS NULL
               AND FechaVerificacion <= @Ahora
               AND FechaVerificacion >= DATEADD(MINUTE, -10, @Ahora)
               AND FechaExpiracion IS NOT NULL
               AND FechaVerificacion <= FechaExpiracion;
        -- Impedir verificaciones inexistentes o reutilizadas.
        IF @@ROWCOUNT <> 1
            THROW 50222, 'Verificacion no disponible para iniciar sesion.', 1;
        COMMIT TRANSACTION;
        -- Devolver informacion validada al backend.
        SELECT Id AS VerificacionId,
               PersonaId,
               Telefono,
               Finalidad,
               Estado,
               FechaVerificacion,
               FechaSesionEmitida
        FROM   dbo.VerificacionesTelefono
        WHERE  Id = @VerificacionId
               AND PersonaId = @PersonaId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;
        THROW;
    END CATCH
END