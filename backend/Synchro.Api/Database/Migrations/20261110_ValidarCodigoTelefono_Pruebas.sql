-- SYNCHRO
-- Validacion de codigos telefonicos.
-- Exclusivo para SynchroDB_Pruebas.
-- No contiene codigos ni datos personales reales.
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56660, 'Base de datos no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 56661, 'Falta la marca del prototipo.', 1;

IF OBJECT_ID(N'dbo.VerificacionesTelefono', N'U') IS NULL
    THROW 56662, 'Falta VerificacionesTelefono.', 1;


GO
-- PROCEDIMIENTO 1:
-- Validar hash, vencimiento e intentos.
CREATE OR ALTER PROCEDURE dbo.ValidarCodigoTelefonoSynchro
@VerificacionId INT, @PersonaId INT, @CodigoHash VARBINARY (32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 56663, 'Base no autorizada.', 1;
    IF @VerificacionId IS NULL
       OR @VerificacionId <= 0
       OR @PersonaId IS NULL
       OR @PersonaId <= 0
        THROW 50120, 'Identificadores invalidos.', 1;
    IF @CodigoHash IS NULL
       OR DATALENGTH(@CodigoHash) <> 32
        THROW 50121, 'Hash invalido.', 1;
    DECLARE @Estado AS NVARCHAR (20);
    DECLARE @Intentos AS INT;
    DECLARE @FechaExpiracion AS DATETIME2;
    DECLARE @HashGuardado AS VARBINARY (32);
    DECLARE @Ahora AS DATETIME2 = SYSUTCDATETIME();
    DECLARE @Resultado AS NVARCHAR (30);
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @Estado = Estado,
               @Intentos = IntentosFallidos,
               @FechaExpiracion = FechaExpiracion,
               @HashGuardado = CodigoHash
        FROM   dbo.VerificacionesTelefono WITH (UPDLOCK, HOLDLOCK)
        WHERE  Id = @VerificacionId
               AND PersonaId = @PersonaId;
        IF @Estado IS NULL
            THROW 50122, 'Verificacion no encontrada.', 1;
        IF @Estado <> N'Pendiente'
            BEGIN
                SET @Resultado = N'NoDisponible';
            END
        ELSE
            IF @FechaExpiracion IS NULL
               OR @FechaExpiracion <= @Ahora
                BEGIN
                    UPDATE dbo.VerificacionesTelefono
                    SET    Estado     = N'Vencido',
                           CodigoHash = NULL
                    WHERE  Id = @VerificacionId;
                    SET @Resultado = N'Vencido';
                END
            ELSE
                IF @Intentos >= 5
                    BEGIN
                        UPDATE dbo.VerificacionesTelefono
                        SET    Estado     = N'Bloqueado',
                               CodigoHash = NULL
                        WHERE  Id = @VerificacionId;
                        SET @Resultado = N'Bloqueado';
                    END
                ELSE
                    IF @HashGuardado IS NOT NULL
                       AND @HashGuardado = @CodigoHash
                        BEGIN
                            UPDATE dbo.VerificacionesTelefono
                            SET    Estado             = N'Verificado',
                                   FechaVerificacion  = @Ahora,
                                   FechaUltimoIntento = @Ahora,
                                   CodigoHash         = NULL
                            WHERE  Id = @VerificacionId;
                            SET @Resultado = N'Verificado';
                        END
                    ELSE
                        BEGIN
                            UPDATE dbo.VerificacionesTelefono
                            SET    IntentosFallidos   = @Intentos + 1,
                                   FechaUltimoIntento = @Ahora,
                                   Estado             = CASE WHEN @Intentos + 1 >= 5 THEN N'Bloqueado' ELSE N'Pendiente' END,
                                   CodigoHash         = CASE WHEN @Intentos + 1 >= 5 THEN NULL ELSE CodigoHash END
                            WHERE  Id = @VerificacionId;
                            SET @Resultado = CASE WHEN @Intentos + 1 >= 5 THEN N'Bloqueado' ELSE N'Incorrecto' END;
                        END
        COMMIT TRANSACTION;
        SELECT Id AS VerificacionId,
               PersonaId,
               Estado,
               IntentosFallidos,
               FechaExpiracion,
               FechaVerificacion,
               @Resultado AS Resultado
        FROM   dbo.VerificacionesTelefono
        WHERE  Id = @VerificacionId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;
        THROW;
    END CATCH
END


GO
-- PROCEDIMIENTO 2:
-- Comprobar la finalidad antes de validar.
CREATE OR ALTER PROCEDURE dbo.ValidarCodigoTelefonoConFinalidadSynchro
@VerificacionId INT, @PersonaId INT, @CodigoHash VARBINARY (32), @FinalidadEsperada NVARCHAR (40)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 56664, 'Base no autorizada.', 1;
    IF @VerificacionId IS NULL
       OR @VerificacionId <= 0
        THROW 50270, 'Verificacion invalida.', 1;
    IF @PersonaId IS NULL
       OR @PersonaId <= 0
        THROW 50271, 'Persona invalida.', 1;
    IF @CodigoHash IS NULL
       OR DATALENGTH(@CodigoHash) <> 32
        THROW 50272, 'Hash invalido.', 1;
    IF @FinalidadEsperada IS NULL
       OR @FinalidadEsperada NOT IN (N'PruebaAdministrativa', N'InicioSesionDestinatario', N'ConsentimientoWhatsApp')
        THROW 50273, 'Finalidad esperada invalida.', 1;
    DECLARE @FinalidadReal AS NVARCHAR (40);
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @FinalidadReal = Finalidad
        FROM   dbo.VerificacionesTelefono WITH (UPDLOCK, HOLDLOCK)
        WHERE  Id = @VerificacionId
               AND PersonaId = @PersonaId;
        IF @FinalidadReal IS NULL
            THROW 50274, 'Verificacion no encontrada.', 1;
        IF @FinalidadReal <> @FinalidadEsperada
            THROW 50275, 'La finalidad de la verificacion no coincide.', 1;
        EXECUTE dbo.ValidarCodigoTelefonoSynchro @VerificacionId = @VerificacionId, @PersonaId = @PersonaId, @CodigoHash = @CodigoHash;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
            ROLLBACK;
        THROW;
    END CATCH
END