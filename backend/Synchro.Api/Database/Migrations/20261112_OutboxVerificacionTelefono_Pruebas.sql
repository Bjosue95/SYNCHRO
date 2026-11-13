-- SYNCHRO
-- Cola independiente para verificaciones telefonicas.
-- Exclusiva de la base aislada de pruebas.
SET QUOTED_IDENTIFIER ON;

SET ANSI_NULLS ON;

SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56940, 'Base de datos no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 56941, 'Falta la marca del prototipo.', 1;

IF OBJECT_ID(N'dbo.VerificacionesTelefono', N'U') IS NULL
    THROW 56942, 'Falta la tabla de verificaciones.', 1;

IF OBJECT_ID(N'dbo.OutboxVerificacionTelefono', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.OutboxVerificacionTelefono (
            Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OutboxVerificacionTelefono PRIMARY KEY,
            VerificacionId        INT              NOT NULL CONSTRAINT FK_OutboxVerificacionTelefono_Verificacion FOREIGN KEY REFERENCES dbo.VerificacionesTelefono (Id),
            TelefonoHash          VARBINARY (32)   NOT NULL,
            ContenidoProtegido    VARBINARY (MAX)  NULL,
            Estado                NVARCHAR (20)    CONSTRAINT DF_OutboxVerificacionTelefono_Estado DEFAULT N'Pendiente' NOT NULL,
            Intentos              INT              CONSTRAINT DF_OutboxVerificacionTelefono_Intentos DEFAULT 0 NOT NULL,
            CreadoUtc             DATETIME2 (3)    CONSTRAINT DF_OutboxVerificacionTelefono_Creado DEFAULT SYSUTCDATETIME() NOT NULL,
            DisponibleUtc         DATETIME2 (3)    CONSTRAINT DF_OutboxVerificacionTelefono_Disponible DEFAULT SYSUTCDATETIME() NOT NULL,
            ExpiraUtc             DATETIME2 (3)    NOT NULL,
            Arrendamiento         UNIQUEIDENTIFIER NULL,
            ArrendamientoHastaUtc DATETIME2 (3)    NULL,
            MensajeMetaId         NVARCHAR (255)   NULL,
            CodigoError           NVARCHAR (64)    NULL,
            CONSTRAINT UQ_OutboxVerificacionTelefono_Verificacion UNIQUE (VerificacionId),
            CONSTRAINT CK_OutboxVerificacionTelefono_Hash CHECK (DATALENGTH(TelefonoHash) = 32),
            CONSTRAINT CK_OutboxVerificacionTelefono_Estado CHECK (Estado IN (N'Pendiente', N'Enviando', N'Reintento', N'Aceptado', N'Entregado', N'Leido', N'Fallido', N'Indeterminado', N'Caducado', N'Cancelado')),
            CONSTRAINT CK_OutboxVerificacionTelefono_Intentos CHECK (Intentos BETWEEN 0 AND 5),
            CONSTRAINT CK_OutboxVerificacionTelefono_Fechas CHECK (ExpiraUtc > CreadoUtc),
            CONSTRAINT CK_OutboxVerificacionTelefono_Arrendamiento CHECK ((Estado = N'Enviando'
                                                                           AND Arrendamiento IS NOT NULL
                                                                           AND ArrendamientoHastaUtc IS NOT NULL)
                                                                          OR (Estado <> N'Enviando'
                                                                              AND Arrendamiento IS NULL
                                                                              AND ArrendamientoHastaUtc IS NULL))
        );
        CREATE INDEX IX_OutboxVerificacionTelefono_Pendientes
            ON dbo.OutboxVerificacionTelefono(Estado, DisponibleUtc);
        CREATE UNIQUE INDEX UX_OutboxVerificacionTelefono_Meta
            ON dbo.OutboxVerificacionTelefono(MensajeMetaId) WHERE MensajeMetaId IS NOT NULL;
    END

PRINT 'Cola de verificacion telefonica preparada.';