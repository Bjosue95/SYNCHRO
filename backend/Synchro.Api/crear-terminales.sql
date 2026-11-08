-- SYNCHRO
-- Registro de terminales de autoservicio
-- Preparacion del sistema de vinculacion
SET QUOTED_IDENTIFIER ON;

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Terminales', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Terminales (
                Id                   INT            IDENTITY (1, 1) NOT NULL CONSTRAINT PK_Terminales PRIMARY KEY,
                Codigo               NVARCHAR (30)  NOT NULL CONSTRAINT UQ_Terminales_Codigo UNIQUE,
                Nombre               NVARCHAR (100) NOT NULL,
                Activa               BIT            CONSTRAINT DF_Terminales_Activa DEFAULT (0) NOT NULL,
                FechaRegistro        DATETIME2 (7)  CONSTRAINT DF_Terminales_FechaRegistro DEFAULT (SYSUTCDATETIME()) NOT NULL,
                FechaAutorizacion    DATETIME2 (7)  NULL,
                FechaUltimaActividad DATETIME2 (7)  NULL
            );
        END
    COMMIT TRANSACTION;
    PRINT 'Registro de terminales preparado correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;
    THROW;
END CATCH