-- Esquema MINIMO para contrato de deposito. No representa todo SynchroDB.
-- Solo inicializa una base vacia, o una base marcada por este mismo script.
SET XACT_ABORT ON;
IF DB_NAME() <> N'SynchroDB_Pruebas' THROW 55700, 'Base incorrecta.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
        THROW 55701, 'No se inicializan bases existentes sin marca de fixture.', 1;
    BEGIN TRANSACTION;
    CREATE TABLE dbo.Terminales (Id INT IDENTITY PRIMARY KEY, Codigo NVARCHAR(30) NOT NULL UNIQUE, Nombre NVARCHAR(100) NOT NULL, Activa BIT NOT NULL);
    CREATE TABLE dbo.Personas (Id INT IDENTITY PRIMARY KEY);
    CREATE TABLE dbo.Casilleros (Codigo NVARCHAR(10) PRIMARY KEY, Tipo NVARCHAR(20) NOT NULL, Tamano NVARCHAR(12) NOT NULL, Estado NVARCHAR(20) NOT NULL);
    CREATE TABLE dbo.PreReservasEncomiendasWeb (
        Id INT IDENTITY PRIMARY KEY, TokenGestionHash VARBINARY(32) NOT NULL,
        TelefonoRemitente NVARCHAR(20) NOT NULL, TelefonoDestinatario NVARCHAR(20) NOT NULL,
        TamanoSolicitado NVARCHAR(12) NOT NULL, Estado NVARCHAR(30) NOT NULL DEFAULT N'PendienteConsentimiento',
        FechaCreacion DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(), FechaExpiracion DATETIME2(3) NOT NULL);
    CREATE TABLE dbo.UsosCasillero (
        Id INT IDENTITY PRIMARY KEY, CodigoCasillero NVARCHAR(10) NOT NULL REFERENCES dbo.Casilleros(Codigo),
        TipoServicio NVARCHAR(20) NOT NULL, FechaReserva DATETIME2(3) NOT NULL,
        FechaIngreso DATETIME2(3) NULL, FechaRetiro DATETIME2(3) NULL,
        Estado NVARCHAR(20) NOT NULL, OrigenReserva NVARCHAR(20) NULL);
    CREATE TABLE dbo.ParticipantesUso (
        UsoId INT NOT NULL REFERENCES dbo.UsosCasillero(Id), PersonaId INT NOT NULL REFERENCES dbo.Personas(Id),
        Rol NVARCHAR(20) NOT NULL, PRIMARY KEY (UsoId,Rol));
    EXEC sys.sp_addextendedproperty @name=N'SynchroFixtureDeposito', @value=N'1';
    COMMIT TRANSACTION;
END;
