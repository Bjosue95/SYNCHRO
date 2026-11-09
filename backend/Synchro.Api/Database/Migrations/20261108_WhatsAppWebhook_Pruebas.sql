IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 51001, 'Esta migracion solo se permite en SynchroDB_Pruebas.', 1;

IF OBJECT_ID(N'dbo.WhatsAppWebhookEventos', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.WhatsAppWebhookEventos (
            Id                   BIGINT         IDENTITY (1, 1) NOT NULL PRIMARY KEY,
            ClaveEvento          VARBINARY (32) NOT NULL,
            TipoEvento           NVARCHAR (16)  NOT NULL,
            MensajeId            NVARCHAR (255) NOT NULL,
            WhatsappId           NVARCHAR (32)  NOT NULL,
            EstadoMensaje        NVARCHAR (24)  NULL,
            RespuestaClasificada NVARCHAR (20)  NULL,
            FechaRecepcionUtc    DATETIME2 (3)  CONSTRAINT DF_WhatsAppWebhookEventos_Fecha DEFAULT SYSUTCDATETIME() NOT NULL,
            CONSTRAINT UQ_WhatsAppWebhookEventos_Clave UNIQUE (ClaveEvento),
            CONSTRAINT CK_WhatsAppWebhookEventos_Tipo CHECK (TipoEvento IN (N'Mensaje', N'Estado'))
        );
    END