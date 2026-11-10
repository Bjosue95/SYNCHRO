SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56500,'Base no autorizada.',1;
IF OBJECT_ID(N'dbo.CodigosRetiroEncomiendaPrototipo',N'U')IS NULL
    THROW 56502,'Instalar primero PIN y retiro.',1;
IF OBJECT_ID(N'dbo.ReemisionesPinEncomienda',N'U')IS NULL
BEGIN
    CREATE TABLE dbo.ReemisionesPinEncomienda
    (
        OperacionId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UsoId INT NOT NULL REFERENCES dbo.UsosCasillero(Id),
        DestinatarioId INT NOT NULL REFERENCES dbo.Personas(Id),
        TerminalId INT NOT NULL REFERENCES dbo.Terminales(Id),
        CreadaUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_ReemisionesPin_Uso ON dbo.ReemisionesPinEncomienda(UsoId);
END;
