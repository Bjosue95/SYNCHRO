-- SYNCHRO
-- Solicitudes de vinculacion de terminales
-- Ejecutar primero en SynchroDB_Pruebas
SET QUOTED_IDENTIFIER ON;

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.SolicitudesVinculacionTerminal', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SolicitudesVinculacionTerminal (
                Id              INT            IDENTITY (1, 1) NOT NULL CONSTRAINT PK_SolicitudesVinculacion PRIMARY KEY,
                TerminalId      INT            NOT NULL,
                CodigoVisual    NVARCHAR (8)   NOT NULL,
                TokenHash       VARBINARY (32) NOT NULL,
                Estado          NVARCHAR (20)  CONSTRAINT DF_SolicitudesVinculacion_Estado DEFAULT N'Pendiente' NOT NULL,
                FechaSolicitud  DATETIME2 (7)  CONSTRAINT DF_SolicitudesVinculacion_Fecha DEFAULT SYSUTCDATETIME() NOT NULL,
                FechaExpiracion DATETIME2 (7)  NOT NULL,
                FechaAprobacion DATETIME2 (7)  NULL,
                AdministradorId INT            NULL,
                FechaConsumo    DATETIME2 (7)  NULL,
                CONSTRAINT FK_Solicitud_Terminal FOREIGN KEY (TerminalId) REFERENCES dbo.Terminales (Id),
                CONSTRAINT FK_Solicitud_Administrador FOREIGN KEY (AdministradorId) REFERENCES dbo.Administradores (Id),
                CONSTRAINT UQ_Solicitud_TokenHash UNIQUE (TokenHash),
                CONSTRAINT CK_Solicitud_Estado CHECK (Estado IN (N'Pendiente', N'Aprobada', N'Rechazada', N'Consumida', N'Expirada'))
            );
        END
    COMMIT TRANSACTION;
    PRINT 'Solicitudes de vinculacion preparadas.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;
    THROW;
END CATCH