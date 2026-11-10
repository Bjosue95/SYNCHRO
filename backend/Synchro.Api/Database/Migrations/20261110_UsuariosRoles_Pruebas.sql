SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56300,'Base no autorizada.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Administradores',N'U') IS NULL
        CREATE TABLE dbo.Administradores
        (
            Id INT IDENTITY PRIMARY KEY,NombreUsuario NVARCHAR(80) NOT NULL UNIQUE,
            PasswordHash NVARCHAR(500) NOT NULL,Activo BIT NOT NULL DEFAULT 1,
            FechaCreacion DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),UltimoAcceso DATETIME2(3) NULL
        );
    IF OBJECT_ID(N'dbo.AccesosAdministrativosGestionados',N'U') IS NULL
        CREATE TABLE dbo.AccesosAdministrativosGestionados
        (
            AdministradorId INT NOT NULL PRIMARY KEY REFERENCES dbo.Administradores(Id),
            VersionSesion INT NOT NULL CHECK(VersionSesion>=1)
        );
    IF OBJECT_ID(N'dbo.RolesAdministrativosPrototipo',N'U') IS NULL
        CREATE TABLE dbo.RolesAdministrativosPrototipo
        (
            AdministradorId INT NOT NULL REFERENCES dbo.Administradores(Id),
            Rol NVARCHAR(20) COLLATE Latin1_General_100_BIN2 NOT NULL CHECK(Rol IN(N'Administrador',N'Operador',N'Auditor')),
            PRIMARY KEY(AdministradorId,Rol)
        );
    IF OBJECT_ID(N'dbo.AuditoriaUsuariosAdministrativos',N'U') IS NULL
        CREATE TABLE dbo.AuditoriaUsuariosAdministrativos
        (
            Id BIGINT IDENTITY PRIMARY KEY,AdministradorId INT NOT NULL,ActorId INT NOT NULL,
            Accion NVARCHAR(80) NOT NULL,FechaUtc DATETIME2(3) NOT NULL
        );
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
