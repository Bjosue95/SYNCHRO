SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56200,'Base no autorizada.',1;
IF OBJECT_ID(N'dbo.Casilleros',N'U') IS NULL OR OBJECT_ID(N'dbo.Terminales',N'U') IS NULL
    THROW 56201,'Falta el inventario base.',1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.EspaciosCarga',N'U') IS NULL
        CREATE TABLE dbo.EspaciosCarga(Codigo NVARCHAR(10) NOT NULL PRIMARY KEY,Estado NVARCHAR(20) NOT NULL);
    IF OBJECT_ID(N'dbo.CompartimentosServiciosTemporales',N'U') IS NULL
        CREATE TABLE dbo.CompartimentosServiciosTemporales
        (
            Codigo NVARCHAR(10) NOT NULL PRIMARY KEY,
            ConfiguracionJson NVARCHAR(MAX) NOT NULL CHECK(ISJSON(ConfiguracionJson)=1)
        );
    IF OBJECT_ID(N'dbo.OperacionesServiciosTemporales',N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.OperacionesServiciosTemporales
        (
            Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
            Codigo NVARCHAR(10) NOT NULL REFERENCES dbo.CompartimentosServiciosTemporales(Codigo),
            TerminalId INT NULL REFERENCES dbo.Terminales(Id),
            Estado NVARCHAR(20) NOT NULL CHECK(Estado IN(N'Reservada',N'Pagada',N'Activa',N'Agotada',N'Finalizada',N'Cancelada')),
            Monto DECIMAL(18,2) NOT NULL CHECK(Monto>=0),
            Pagado BIT NOT NULL,
            PagoAnulado BIT NOT NULL,
            PagoId UNIQUEIDENTIFIER NULL,
            PagoUtc DATETIME2(3) NULL,
            CreadaUtc DATETIME2(3) NOT NULL,
            InicioUtc DATETIME2(3) NULL,
            RetiroUtc DATETIME2(3) NULL,
            DatosJson NVARCHAR(MAX) NOT NULL CHECK(ISJSON(DatosJson)=1)
        );
        CREATE UNIQUE INDEX UX_Servicio_Pago ON dbo.OperacionesServiciosTemporales(PagoId) WHERE PagoId IS NOT NULL;
        CREATE UNIQUE INDEX UX_Servicio_Activo ON dbo.OperacionesServiciosTemporales(Codigo)
            WHERE Estado IN(N'Reservada',N'Pagada',N'Activa',N'Agotada');
        CREATE INDEX IX_Servicio_Fecha ON dbo.OperacionesServiciosTemporales(CreadaUtc);
    END;
    IF OBJECT_ID(N'dbo.AuditoriaServiciosTemporales',N'U') IS NULL
        CREATE TABLE dbo.AuditoriaServiciosTemporales
        (
            Id BIGINT IDENTITY PRIMARY KEY,Codigo NVARCHAR(10) NOT NULL,
            OperacionId UNIQUEIDENTIFIER NULL,Accion NVARCHAR(80) NOT NULL,
            Actor NVARCHAR(60) NOT NULL,FechaUtc DATETIME2(3) NOT NULL
        );
    IF OBJECT_ID(N'dbo.IncidenciasServiciosTemporales',N'U') IS NULL
        CREATE TABLE dbo.IncidenciasServiciosTemporales
        (
            Id BIGINT IDENTITY PRIMARY KEY,Codigo NVARCHAR(10) NOT NULL,
            Descripcion NVARCHAR(1000) NOT NULL,CreadaUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),ResueltaUtc DATETIME2(3) NULL
        );
    COMMIT;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK;
    THROW;
END CATCH;
