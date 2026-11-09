SET NOCOUNT ON;

SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    BEGIN
        THROW 54001, 'Solo se permite ejecutar en SynchroDB_Pruebas.', 1;
    END

IF OBJECT_ID(N'dbo.PreReservasEncomiendasWeb', N'U') IS NULL
    BEGIN
        THROW 54002, 'Primero debe existir la tabla de pre-reservas.', 1;
    END

IF OBJECT_ID(N'dbo.ConsentimientosPreReservaWhatsApp', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ConsentimientosPreReservaWhatsApp (
            ReservaId             INT            NOT NULL,
            TelefonoWhatsApp      NVARCHAR (20)  NOT NULL,
            CodigoHash            VARBINARY (32) NOT NULL,
            Estado                NVARCHAR (20)  CONSTRAINT DF_ConsentimientoReserva_Estado DEFAULT N'Pendiente' NOT NULL,
            IntentosFallidos      INT            CONSTRAINT DF_ConsentimientoReserva_Intentos DEFAULT 0 NOT NULL,
            FechaCreacionUtc      DATETIME2 (3)  CONSTRAINT DF_ConsentimientoReserva_Fecha DEFAULT SYSUTCDATETIME() NOT NULL,
            FechaExpiracionUtc    DATETIME2 (3)  NOT NULL,
            FechaConfirmacionUtc  DATETIME2 (3)  NULL,
            MensajeIdConfirmacion NVARCHAR (255) NULL,
            CONSTRAINT PK_ConsentimientosPreReservaWhatsApp PRIMARY KEY (ReservaId),
            CONSTRAINT FK_Consentimiento_PreReserva FOREIGN KEY (ReservaId) REFERENCES dbo.PreReservasEncomiendasWeb (Id),
            CONSTRAINT CK_ConsentimientoReserva_Hash CHECK (DATALENGTH(CodigoHash) = 32),
            CONSTRAINT CK_ConsentimientoReserva_Estado CHECK (Estado IN (N'Pendiente', N'Confirmado', N'Rechazado', N'Revocado')),
            CONSTRAINT CK_ConsentimientoReserva_Intentos CHECK (IntentosFallidos BETWEEN 0 AND 5),
            CONSTRAINT CK_ConsentimientoReserva_Fechas CHECK (FechaExpiracionUtc > FechaCreacionUtc)
        );
        CREATE INDEX IX_ConsentimientoReserva_Telefono
            ON dbo.ConsentimientosPreReservaWhatsApp(TelefonoWhatsApp, Estado, FechaExpiracionUtc);
        CREATE UNIQUE INDEX UX_ConsentimientoReserva_Mensaje
            ON dbo.ConsentimientosPreReservaWhatsApp(MensajeIdConfirmacion) WHERE MensajeIdConfirmacion IS NOT NULL;
    END

SELECT DB_NAME() AS BaseDeDatos,
       OBJECT_ID(N'dbo.ConsentimientosPreReservaWhatsApp', N'U') AS TablaConsentimientos,
       N'Preparada' AS Resultado;