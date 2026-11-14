-- Complemento aditivo de la fixture; nunca reconstruye ni borra una base.
SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB_Pruebas' OR NOT EXISTS
    (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito'
     AND CONVERT(nvarchar(10),value)=N'1')
    THROW 57301,'Base o marcador no autorizado.',1;
IF COL_LENGTH(N'dbo.Personas',N'Nombre') IS NULL
    ALTER TABLE dbo.Personas ADD Nombre nvarchar(120) NULL;
IF COL_LENGTH(N'dbo.Personas',N'TelefonoWhatsapp') IS NULL
    ALTER TABLE dbo.Personas ADD TelefonoWhatsapp nvarchar(20) NULL;
IF COL_LENGTH(N'dbo.Personas',N'TieneCuenta') IS NULL
    ALTER TABLE dbo.Personas ADD TieneCuenta bit NOT NULL DEFAULT 0;
IF COL_LENGTH(N'dbo.Terminales',N'FechaAutorizacion') IS NULL
    ALTER TABLE dbo.Terminales ADD FechaAutorizacion datetime2 NULL;
IF COL_LENGTH(N'dbo.Terminales',N'FechaUltimaActividad') IS NULL
    ALTER TABLE dbo.Terminales ADD FechaUltimaActividad datetime2 NULL;
IF OBJECT_ID(N'dbo.CodigosRetiro',N'U') IS NULL
    CREATE TABLE dbo.CodigosRetiro(
        Id int IDENTITY PRIMARY KEY,UsoId int NOT NULL REFERENCES dbo.UsosCasillero(Id),
        PinHash varbinary(64) NOT NULL,PinSalt varbinary(32) NOT NULL,
        FechaCreacion datetime2 NOT NULL,FechaVencimiento datetime2 NOT NULL,
        IntentosFallidos int NOT NULL DEFAULT 0,FechaUso datetime2 NULL);
IF OBJECT_ID(N'dbo.VerificacionesRetiro',N'U') IS NULL
    CREATE TABLE dbo.VerificacionesRetiro(
        Id int IDENTITY PRIMARY KEY,UsoId int NOT NULL REFERENCES dbo.UsosCasillero(Id),
        PersonaId int NOT NULL REFERENCES dbo.Personas(Id),Metodo nvarchar(20) NOT NULL,
        Resultado nvarchar(20) NOT NULL,FechaVerificacion datetime2 NOT NULL);
IF OBJECT_ID(N'dbo.ConsentimientosWhatsApp',N'U') IS NULL
    CREATE TABLE dbo.ConsentimientosWhatsApp(Id int IDENTITY PRIMARY KEY,Estado nvarchar(30) NOT NULL);
IF OBJECT_ID(N'dbo.ConsentimientosUso',N'U') IS NULL
    CREATE TABLE dbo.ConsentimientosUso(UsoId int NOT NULL REFERENCES dbo.UsosCasillero(Id),
        ConsentimientoId int NOT NULL REFERENCES dbo.ConsentimientosWhatsApp(Id));
