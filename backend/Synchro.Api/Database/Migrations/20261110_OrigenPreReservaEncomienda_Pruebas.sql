SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56100,'Base no autorizada.',1;
IF OBJECT_ID(N'dbo.PreReservasEncomiendasWeb',N'U') IS NULL
    THROW 56101,'Falta la tabla de pre-reservas.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.PreReservasEncomiendasWeb',N'OrigenPreReserva') IS NULL
        ALTER TABLE dbo.PreReservasEncomiendasWeb ADD OrigenPreReserva NVARCHAR(10) NOT NULL
            CONSTRAINT DF_PreReserva_Origen DEFAULT N'Web' WITH VALUES;
    IF COL_LENGTH(N'dbo.PreReservasEncomiendasWeb',N'TerminalCreacionId') IS NULL
        ALTER TABLE dbo.PreReservasEncomiendasWeb ADD TerminalCreacionId INT NULL
            CONSTRAINT FK_PreReserva_TerminalCreacion REFERENCES dbo.Terminales(Id);
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
