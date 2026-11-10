SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56500,'Base no autorizada.',1;
IF OBJECT_ID(N'dbo.Personas',N'U') IS NULL THROW 56501,'Faltan personas.',1;
IF OBJECT_ID(N'dbo.ReferenciasRostroSimulado',N'U') IS NULL
    CREATE TABLE dbo.ReferenciasRostroSimulado
    (
        PersonaId INT NOT NULL PRIMARY KEY REFERENCES dbo.Personas(Id),
        PatronProtegido VARBINARY(MAX) NOT NULL,
        Modelo NVARCHAR(60) NOT NULL,
        VenceUtc DATETIME2(3) NOT NULL,
        ActualizadaUtc DATETIME2(3) NOT NULL,
        CHECK(VenceUtc>ActualizadaUtc)
    );
