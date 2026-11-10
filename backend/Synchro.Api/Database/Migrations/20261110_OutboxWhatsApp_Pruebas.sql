SET XACT_ABORT ON;
IF DB_NAME() <> N'SynchroDB_Pruebas' THROW 55800,'Base no autorizada.',1;
IF OBJECT_ID(N'dbo.PreReservasEncomiendasWeb',N'U') IS NULL
    THROW 55804,'Faltan pre-reservas.',1;
IF OBJECT_ID(N'dbo.OutboxWhatsApp',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutboxWhatsApp
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ReservaId INT NOT NULL REFERENCES dbo.PreReservasEncomiendasWeb(Id),
        TelefonoHash VARBINARY(32) NOT NULL CHECK (DATALENGTH(TelefonoHash)=32),
        ContenidoProtegido VARBINARY(MAX) NULL,
        Estado NVARCHAR(20) NOT NULL DEFAULT N'Pendiente'
            CHECK (Estado IN (N'Pendiente',N'Enviando',N'Reintento',N'Aceptado',N'Entregado',N'Leido',N'Fallido',N'Indeterminado',N'Caducado',N'Cancelado')),
        Intentos INT NOT NULL DEFAULT 0 CHECK (Intentos BETWEEN 0 AND 5),
        CreadoUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        DisponibleUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
        ExpiraUtc DATETIME2(3) NOT NULL,
        Arrendamiento UNIQUEIDENTIFIER NULL,
        ArrendamientoHastaUtc DATETIME2(3) NULL,
        MensajeMetaId NVARCHAR(255) NULL,
        CodigoError NVARCHAR(64) NULL,
        CONSTRAINT CK_Outbox_Fechas CHECK (ExpiraUtc>CreadoUtc),
        CONSTRAINT CK_Outbox_Arrendamiento CHECK
          ((Estado=N'Enviando' AND Arrendamiento IS NOT NULL AND ArrendamientoHastaUtc IS NOT NULL)
           OR (Estado<>N'Enviando' AND Arrendamiento IS NULL AND ArrendamientoHastaUtc IS NULL))
    );
    CREATE UNIQUE INDEX UX_Outbox_Meta ON dbo.OutboxWhatsApp(MensajeMetaId) WHERE MensajeMetaId IS NOT NULL;
    CREATE INDEX IX_Outbox_Disponible ON dbo.OutboxWhatsApp(Estado,DisponibleUtc);
END;
GO
CREATE OR ALTER PROCEDURE dbo.TomarOutboxWhatsAppSynchro
AS
BEGIN
    SET XACT_ABORT ON;
    SET NOCOUNT ON;
    IF DB_NAME() <> N'SynchroDB_Pruebas' THROW 55800,'Base no autorizada.',1;
    DECLARE @Ahora DATETIME2(3)=SYSUTCDATETIME();
    -- Un proceso caido puede haber enviado: conservar trazabilidad, NO reintentar.
    UPDATE dbo.OutboxWhatsApp SET Estado=N'Indeterminado', CodigoError=N'ArrendamientoVencido',
        ContenidoProtegido=NULL,Arrendamiento=NULL,ArrendamientoHastaUtc=NULL
    WHERE Estado=N'Enviando' AND ArrendamientoHastaUtc<=@Ahora;
    UPDATE dbo.OutboxWhatsApp SET Estado=N'Caducado',CodigoError=N'MensajeVencido',ContenidoProtegido=NULL
    WHERE Estado IN (N'Pendiente',N'Reintento') AND ExpiraUtc<=@Ahora;
    UPDATE o SET Estado=N'Cancelado',CodigoError=N'ConsentimientoNoValido',ContenidoProtegido=NULL
    FROM dbo.OutboxWhatsApp o
    WHERE o.Estado IN (N'Pendiente',N'Reintento') AND NOT EXISTS
    (
        SELECT 1 FROM dbo.ConsentimientosPreReservaWhatsApp c
        JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=c.ReservaId
        JOIN dbo.DepositosPreReservaEncomienda d ON d.ReservaId=r.Id
        WHERE c.ReservaId=o.ReservaId AND c.Estado=N'Confirmado'
          AND HASHBYTES('SHA2_256',CONVERT(VARBINARY(MAX),c.TelefonoWhatsApp))=o.TelefonoHash
          AND c.TelefonoWhatsApp=CASE WHEN LEFT(r.TelefonoDestinatario,1)=N'+'
              THEN r.TelefonoDestinatario ELSE N'+'+r.TelefonoDestinatario END
          AND NULLIF(c.MensajeIdConfirmacion,N'') IS NOT NULL
          AND c.FechaConfirmacionUtc BETWEEN c.FechaCreacionUtc AND c.FechaExpiracionUtc
          AND c.FechaConfirmacionUtc<=@Ahora
    );
    ;WITH Candidato AS
    (
        SELECT TOP(1) * FROM dbo.OutboxWhatsApp WITH (UPDLOCK,READPAST,ROWLOCK)
        WHERE Estado IN (N'Pendiente',N'Reintento') AND DisponibleUtc<=@Ahora
          AND ExpiraUtc>@Ahora AND Intentos<5 AND ContenidoProtegido IS NOT NULL
        ORDER BY DisponibleUtc,Id
    )
    UPDATE Candidato SET Estado=N'Enviando',Intentos=Intentos+1,
        Arrendamiento=NEWID(),ArrendamientoHastaUtc=DATEADD(MINUTE,1,@Ahora)
    OUTPUT INSERTED.Id,INSERTED.Arrendamiento,INSERTED.ReservaId,INSERTED.ContenidoProtegido,INSERTED.Intentos,INSERTED.ExpiraUtc;
END;
