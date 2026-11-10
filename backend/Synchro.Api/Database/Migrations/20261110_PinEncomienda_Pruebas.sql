SET XACT_ABORT ON;
IF DB_NAME() <> N'SynchroDB_Pruebas' THROW 55900,'Base no autorizada.',1;
IF OBJECT_ID(N'dbo.UsosCasillero',N'U') IS NULL OR OBJECT_ID(N'dbo.Personas',N'U') IS NULL
    THROW 55901,'Faltan usos o personas.',1;
IF OBJECT_ID(N'dbo.CodigosRetiroEncomiendaPrototipo',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodigosRetiroEncomiendaPrototipo
    (
        UsoId INT NOT NULL PRIMARY KEY REFERENCES dbo.UsosCasillero(Id),
        DestinatarioId INT NOT NULL REFERENCES dbo.Personas(Id),
        PinHash VARBINARY(32) NOT NULL CHECK (DATALENGTH(PinHash)=32),
        PinSalt VARBINARY(32) NOT NULL CHECK (DATALENGTH(PinSalt)=32),
        CreadoUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        VenceUtc DATETIME2(3) NOT NULL,
        IntentosFallidos INT NOT NULL DEFAULT 0 CHECK (IntentosFallidos BETWEEN 0 AND 5),
        Consumido BIT NOT NULL DEFAULT 0,
        FechaUsoUtc DATETIME2(3) NULL,
        CONSTRAINT CK_PinEncomienda_Vigencia CHECK (VenceUtc>CreadoUtc),
        CONSTRAINT CK_PinEncomienda_Consumo CHECK
            ((Consumido=0 AND FechaUsoUtc IS NULL) OR (Consumido=1 AND FechaUsoUtc>=CreadoUtc))
    );
END;
