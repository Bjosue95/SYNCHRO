-- SYNCHRO: Verificación telefónica para el prototipo aislado.
-- No ejecutar contra la base original SynchroDB.
SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 56640, 'Base de datos no autorizada.', 1;

IF NOT EXISTS (SELECT 1
               FROM   sys.extended_properties
               WHERE  class = 0
                      AND name = N'SynchroFixtureDeposito')
    THROW 56641, 'Falta la marca de la base aislada.', 1;

IF OBJECT_ID(N'dbo.Personas', N'U') IS NULL
    THROW 56642, 'No existe la tabla Personas.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    -- Completar Personas conservando sus registros.
    IF COL_LENGTH(N'dbo.Personas', N'Nombre') IS NULL
        ALTER TABLE dbo.Personas
            ADD Nombre NVARCHAR (120) NULL;
    IF COL_LENGTH(N'dbo.Personas', N'TelefonoWhatsapp') IS NULL
        ALTER TABLE dbo.Personas
            ADD TelefonoWhatsapp NVARCHAR (20) NULL;
    IF COL_LENGTH(N'dbo.Personas', N'TieneCuenta') IS NULL
        ALTER TABLE dbo.Personas
            ADD TieneCuenta BIT CONSTRAINT DF_Personas_TieneCuenta_Pruebas DEFAULT (0) WITH VALUES NOT NULL;
    IF COL_LENGTH(N'dbo.Personas', N'FechaCreacion') IS NULL
        ALTER TABLE dbo.Personas
            ADD FechaCreacion DATETIME2 (7) CONSTRAINT DF_Personas_FechaCreacion_Pruebas DEFAULT SYSUTCDATETIME() WITH VALUES NOT NULL;
    -- Verificaciones telefónicas con hash protegido.
    IF OBJECT_ID(N'dbo.VerificacionesTelefono', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.VerificacionesTelefono (
                Id                 INT            IDENTITY (1, 1) NOT NULL CONSTRAINT PK_VerificacionesTelefono_Pruebas PRIMARY KEY,
                PersonaId          INT            NOT NULL,
                Telefono           NVARCHAR (20)  NOT NULL,
                Estado             NVARCHAR (20)  DEFAULT N'Pendiente' NOT NULL,
                FechaSolicitud     DATETIME2 (7)  DEFAULT SYSUTCDATETIME() NOT NULL,
                FechaVerificacion  DATETIME2 (7)  NULL,
                CodigoHash         VARBINARY (32) NULL,
                FechaExpiracion    DATETIME2 (7)  NULL,
                IntentosFallidos   INT            DEFAULT (0) NOT NULL,
                FechaUltimoIntento DATETIME2 (7)  NULL,
                FechaSesionEmitida DATETIME2 (7)  NULL,
                Finalidad          NVARCHAR (40)  DEFAULT N'PruebaAdministrativa' NOT NULL,
                CONSTRAINT FK_VerificacionesTelefono_Personas_Pruebas FOREIGN KEY (PersonaId) REFERENCES dbo.Personas (Id),
                CONSTRAINT CK_VerificacionesTelefono_Estado_Pruebas CHECK (Estado IN (N'Pendiente', N'Verificado', N'Vencido', N'Bloqueado')),
                CONSTRAINT CK_VerificacionesTelefono_Finalidad_Pruebas CHECK (Finalidad IN (N'PruebaAdministrativa', N'InicioSesionDestinatario', N'ConsentimientoWhatsApp')),
                CONSTRAINT CK_VerificacionesTelefono_Sesion_Pruebas CHECK (FechaSesionEmitida IS NULL
                                                                           OR (Estado = N'Verificado'
                                                                               AND FechaVerificacion IS NOT NULL
                                                                               AND CodigoHash IS NULL
                                                                               AND FechaSesionEmitida >= FechaVerificacion))
            );
        END
    -- Índice compuesto requerido por las relaciones originales.
    IF NOT EXISTS (SELECT 1
                   FROM   sys.indexes
                   WHERE  object_id = OBJECT_ID(N'dbo.VerificacionesTelefono')
                          AND name = N'UX_VerificacionesTelefono_IdPersonaTelefono')
        BEGIN
            CREATE UNIQUE INDEX UX_VerificacionesTelefono_IdPersonaTelefono
                ON dbo.VerificacionesTelefono(Id, PersonaId, Telefono);
        END
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK;
    THROW;
END CATCH