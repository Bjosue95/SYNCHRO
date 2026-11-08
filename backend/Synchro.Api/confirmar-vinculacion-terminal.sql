-- SYNCHRO
-- Confirmacion segura de vinculacion
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 51090, 'Solo ejecutar en la base de pruebas.', 1;


GO
CREATE OR ALTER PROCEDURE dbo.ConfirmarVinculacionTerminalSynchro
@TokenHash VARBINARY (32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @TokenHash IS NULL
       OR DATALENGTH(@TokenHash) <> 32
        THROW 51020, 'Token invalido.', 1;
    DECLARE @TerminalId AS INT;
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @TerminalId = s.TerminalId
        FROM   dbo.SolicitudesVinculacionTerminal AS s WITH (UPDLOCK, HOLDLOCK)
               INNER JOIN
               dbo.Terminales AS t WITH (UPDLOCK, HOLDLOCK)
               ON t.Id = s.TerminalId
        WHERE  s.TokenHash = @TokenHash
               AND s.Estado = N'Aprobada'
               AND s.FechaExpiracion > SYSUTCDATETIME()
               AND t.Activa = 0;
        IF @TerminalId IS NULL
            THROW 51021, 'Vinculacion no autorizada.', 1;
        UPDATE dbo.Terminales
        SET    Activa            = 1,
               FechaAutorizacion = SYSUTCDATETIME()
        WHERE  Id = @TerminalId
               AND Activa = 0;
        IF @@ROWCOUNT <> 1
            THROW 51022, 'No se pudo activar la terminal.', 1;
        UPDATE dbo.SolicitudesVinculacionTerminal
        SET    Estado       = N'Consumida',
               FechaConsumo = SYSUTCDATETIME()
        WHERE  TokenHash = @TokenHash
               AND Estado = N'Aprobada'
               AND FechaExpiracion > SYSUTCDATETIME();
        IF @@ROWCOUNT <> 1
            THROW 51023, 'No se pudo consumir la solicitud.', 1;
        COMMIT TRANSACTION;
        SELECT @TerminalId AS TerminalId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK;
        THROW;
    END CATCH
END