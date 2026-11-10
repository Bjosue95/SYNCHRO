SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56400,'Base no autorizada.',1;
IF OBJECT_ID(N'dbo.Personas',N'U') IS NULL OR OBJECT_ID(N'dbo.PreReservasEncomiendasWeb',N'U') IS NULL
    THROW 56401,'Faltan personas o pre-reservas.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.ClientesWebPrototipo',N'U') IS NULL
        CREATE TABLE dbo.ClientesWebPrototipo
        (
            PersonaId INT NOT NULL PRIMARY KEY REFERENCES dbo.Personas(Id),
            NombreUsuario NVARCHAR(80) NOT NULL UNIQUE,Telefono NVARCHAR(20) NOT NULL,
            PasswordHash NVARCHAR(500) NOT NULL,Activa BIT NOT NULL DEFAULT 1,
            VersionSesion INT NOT NULL DEFAULT 1 CHECK(VersionSesion>=1),
            CreadaUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
        );
    IF COL_LENGTH(N'dbo.PreReservasEncomiendasWeb',N'ClientePersonaId') IS NULL
        ALTER TABLE dbo.PreReservasEncomiendasWeb ADD ClientePersonaId INT NULL
            CONSTRAINT FK_PreReserva_ClienteWeb REFERENCES dbo.ClientesWebPrototipo(PersonaId);
    -- No inventar cuentas ni convertir visitantes antiguos en clientes. Los
    -- registros sin cuenta permanecen en el historial pero no autorizan deposito.
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
