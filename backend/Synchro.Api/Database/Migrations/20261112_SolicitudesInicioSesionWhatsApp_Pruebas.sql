-- SYNCHRO
-- Solicitudes de inicio de sesion recibidas por WhatsApp.
-- Exclusivo de SynchroDB_Pruebas.
-- No envia mensajes ni genera codigos.
SET QUOTED_IDENTIFIER ON;

SET ANSI_NULLS ON;

SET ANSI_PADDING ON;

SET ANSI_WARNINGS ON;

SET ARITHABORT ON;

SET CONCAT_NULL_YIELDS_NULL ON;

SET NUMERIC_ROUNDABORT OFF;

SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57010, 'Base no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 57011, 'Falta la marca del prototipo.', 1;

IF OBJECT_ID(N'dbo.WhatsAppWebhookEventos', N'U') IS NULL
    THROW 57012, 'Falta la tabla del webhook.', 1;

IF OBJECT_ID(N'dbo.PreReservasEncomiendasWeb', N'U') IS NULL
    THROW 57013, 'Falta la tabla de reservas.', 1;

IF OBJECT_ID(N'dbo.VerificacionesTelefono', N'U') IS NULL
    THROW 57014, 'Falta la tabla de verificaciones.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.SolicitudesInicioSesionWhatsApp', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SolicitudesInicioSesionWhatsApp (
                EventoId              BIGINT           NOT NULL CONSTRAINT PK_SolicitudesInicioSesionWhatsApp PRIMARY KEY,
                ReservaId             INT              NOT NULL,
                Estado                NVARCHAR (20)    CONSTRAINT DF_SolicitudesInicio_Estado DEFAULT N'Pendiente' NOT NULL,
                Intentos              INT              CONSTRAINT DF_SolicitudesInicio_Intentos DEFAULT 0 NOT NULL,
                CreadoUtc             DATETIME2 (3)    CONSTRAINT DF_SolicitudesInicio_Creado DEFAULT SYSUTCDATETIME() NOT NULL,
                DisponibleUtc         DATETIME2 (3)    CONSTRAINT DF_SolicitudesInicio_Disponible DEFAULT SYSUTCDATETIME() NOT NULL,
                ExpiraUtc             DATETIME2 (3)    NOT NULL,
                Arrendamiento         UNIQUEIDENTIFIER NULL,
                ArrendamientoHastaUtc DATETIME2 (3)    NULL,
                VerificacionId        INT              NULL,
                CodigoError           NVARCHAR (64)    NULL,
                CONSTRAINT FK_SolicitudesInicio_Evento FOREIGN KEY (EventoId) REFERENCES dbo.WhatsAppWebhookEventos (Id),
                CONSTRAINT FK_SolicitudesInicio_Reserva FOREIGN KEY (ReservaId) REFERENCES dbo.PreReservasEncomiendasWeb (Id),
                CONSTRAINT FK_SolicitudesInicio_Verificacion FOREIGN KEY (VerificacionId) REFERENCES dbo.VerificacionesTelefono (Id),
                CONSTRAINT CK_SolicitudesInicio_Estado CHECK (Estado IN (N'Pendiente', N'Procesando', N'Reintento', N'Preparada', N'Rechazada', N'Indeterminado', N'Caducada')),
                CONSTRAINT CK_SolicitudesInicio_Intentos CHECK (Intentos BETWEEN 0 AND 3),
                CONSTRAINT CK_SolicitudesInicio_Fechas CHECK (ExpiraUtc > CreadoUtc),
                CONSTRAINT CK_SolicitudesInicio_Arrendamiento CHECK ((Estado = N'Procesando'
                                                                      AND Arrendamiento IS NOT NULL
                                                                      AND ArrendamientoHastaUtc IS NOT NULL)
                                                                     OR (Estado <> N'Procesando'
                                                                         AND Arrendamiento IS NULL
                                                                         AND ArrendamientoHastaUtc IS NULL)),
                CONSTRAINT CK_SolicitudesInicio_Preparada CHECK (Estado <> N'Preparada'
                                                                 OR VerificacionId IS NOT NULL)
            );
        END
    IF NOT EXISTS (SELECT 1
                   FROM   sys.indexes
                   WHERE  object_id = OBJECT_ID(N'dbo.SolicitudesInicioSesionWhatsApp')
                          AND name = N'IX_SolicitudesInicio_Pendientes')
        BEGIN
            CREATE INDEX IX_SolicitudesInicio_Pendientes
                ON dbo.SolicitudesInicioSesionWhatsApp(Estado, DisponibleUtc);
        END
    IF NOT EXISTS (SELECT 1
                   FROM   sys.indexes
                   WHERE  object_id = OBJECT_ID(N'dbo.SolicitudesInicioSesionWhatsApp')
                          AND name = N'UX_SolicitudesInicio_Verificacion')
        BEGIN
            CREATE UNIQUE INDEX UX_SolicitudesInicio_Verificacion
                ON dbo.SolicitudesInicioSesionWhatsApp(VerificacionId) WHERE VerificacionId IS NOT NULL;
        END
    COMMIT TRANSACTION;
    PRINT 'Cola de solicitudes WhatsApp preparada.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK;
    THROW;
END CATCH