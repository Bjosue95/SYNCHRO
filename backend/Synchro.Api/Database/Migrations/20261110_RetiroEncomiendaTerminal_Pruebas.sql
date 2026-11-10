SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56000,'Base no autorizada.',1;
IF OBJECT_ID(N'dbo.CodigosRetiroEncomiendaPrototipo',N'U') IS NULL
    THROW 56002,'Instalar primero la migracion de PIN.',1;
IF OBJECT_ID(N'dbo.EvidenciasFacialesRetiroEncomienda',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EvidenciasFacialesRetiroEncomienda
    (
        EvidenciaId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UsoId INT NOT NULL REFERENCES dbo.UsosCasillero(Id),
        DestinatarioId INT NOT NULL REFERENCES dbo.Personas(Id),
        TerminalId INT NOT NULL REFERENCES dbo.Terminales(Id),
        EmitidaUtc DATETIME2(3) NOT NULL,
        VenceUtc DATETIME2(3) NOT NULL,
        ConsumidaUtc DATETIME2(3) NULL,
        CHECK(DATEDIFF_BIG(MILLISECOND,EmitidaUtc,VenceUtc) BETWEEN 1 AND 120000),
        CHECK(ConsumidaUtc IS NULL OR ConsumidaUtc>=EmitidaUtc)
    );
END;
IF OBJECT_ID(N'dbo.AuditoriaEncomiendasPrototipo',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditoriaEncomiendasPrototipo
    (
        Id BIGINT IDENTITY PRIMARY KEY,
        UsoId INT NOT NULL REFERENCES dbo.UsosCasillero(Id),
        TerminalId INT NOT NULL REFERENCES dbo.Terminales(Id),
        Accion NVARCHAR(30) NOT NULL,
        FechaUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
GO
CREATE OR ALTER PROCEDURE dbo.RetirarEncomiendaTerminalSynchro
    @UsoId INT,@DestinatarioId INT,@TerminalId INT,@EvidenciaId UNIQUEIDENTIFIER,
    @PinHash VARBINARY(32),@PinSaltEsperado VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56000,'Base no autorizada.',1;
    IF @@TRANCOUNT<>0 THROW 56003,'No se admite transaccion externa.',1;
    IF @UsoId IS NULL OR @UsoId<=0 OR @DestinatarioId IS NULL OR @DestinatarioId<=0
       OR @TerminalId IS NULL OR @TerminalId<=0 OR @EvidenciaId IS NULL
       OR @EvidenciaId='00000000-0000-0000-0000-000000000000'
       OR @PinHash IS NULL OR DATALENGTH(@PinHash)<>32
       OR @PinSaltEsperado IS NULL OR DATALENGTH(@PinSaltEsperado)<>32
        THROW 56004,'Datos invalidos.',1;
    DECLARE @Codigo NVARCHAR(10),@Ahora DATETIME2(3),@HashActual VARBINARY(32),@Intentos INT;
    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS(SELECT 1 FROM dbo.Terminales WITH(UPDLOCK,HOLDLOCK) WHERE Id=@TerminalId AND Activa=1)
        BEGIN
            COMMIT;SELECT N'NoDisponible';RETURN;
        END;
        SELECT @Codigo=CodigoCasillero FROM dbo.UsosCasillero WHERE Id=@UsoId;
        IF @Codigo IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.Casilleros WITH(UPDLOCK,HOLDLOCK)
            WHERE Codigo=@Codigo AND Estado=N'Ocupado')
        BEGIN
            COMMIT;SELECT N'NoDisponible';RETURN;
        END;
        IF NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK)
            WHERE Id=@UsoId AND CodigoCasillero=@Codigo AND TipoServicio=N'Encomienda' AND Estado=N'Ocupado'
                AND FechaIngreso IS NOT NULL AND FechaRetiro IS NULL)
           OR NOT EXISTS(SELECT 1 FROM dbo.ParticipantesUso WITH(UPDLOCK,HOLDLOCK)
                WHERE UsoId=@UsoId AND Rol=N'Destinatario' AND PersonaId=@DestinatarioId)
        BEGIN
            COMMIT;SELECT N'NoDisponible';RETURN;
        END;
        -- Orden de bloqueo constante y hora posterior a todas las esperas.
        SELECT @HashActual=PinHash,@Intentos=IntentosFallidos
        FROM dbo.CodigosRetiroEncomiendaPrototipo WITH(UPDLOCK,HOLDLOCK)
        WHERE UsoId=@UsoId AND DestinatarioId=@DestinatarioId AND PinSalt=@PinSaltEsperado AND Consumido=0;
        IF @HashActual IS NULL
        BEGIN
            COMMIT;SELECT N'NoDisponible';RETURN;
        END;
        IF @Intentos>=5
        BEGIN
            COMMIT;SELECT N'PinBloqueado';RETURN;
        END;
        DECLARE @UsoEvidencia INT;
        SELECT @UsoEvidencia=UsoId FROM dbo.EvidenciasFacialesRetiroEncomienda WITH(UPDLOCK,HOLDLOCK)
        WHERE EvidenciaId=@EvidenciaId;
        SET @Ahora=SYSUTCDATETIME();
        IF NOT EXISTS(SELECT 1 FROM dbo.CodigosRetiroEncomiendaPrototipo
            WHERE UsoId=@UsoId AND VenceUtc>@Ahora)
        BEGIN
            COMMIT;SELECT N'NoDisponible';RETURN;
        END;
        IF @UsoEvidencia IS NULL OR NOT EXISTS(
            SELECT 1 FROM dbo.EvidenciasFacialesRetiroEncomienda e
            JOIN dbo.UsosCasillero u ON u.Id=e.UsoId
            WHERE e.EvidenciaId=@EvidenciaId AND e.UsoId=@UsoId AND e.DestinatarioId=@DestinatarioId
                AND e.TerminalId=@TerminalId AND e.ConsumidaUtc IS NULL
                AND e.EmitidaUtc>=u.FechaIngreso AND e.EmitidaUtc<=@Ahora AND e.VenceUtc>@Ahora)
        BEGIN
            COMMIT;SELECT N'EvidenciaNoValida';RETURN;
        END;
        IF @HashActual<>@PinHash
        BEGIN
            UPDATE dbo.CodigosRetiroEncomiendaPrototipo SET IntentosFallidos=IntentosFallidos+1 WHERE UsoId=@UsoId;
            INSERT dbo.AuditoriaEncomiendasPrototipo(UsoId,TerminalId,Accion)VALUES(@UsoId,@TerminalId,N'PIN rechazado');
            COMMIT;
            SELECT CASE WHEN @Intentos+1>=5 THEN N'PinBloqueado' ELSE N'PinIncorrecto' END;
            RETURN;
        END;
        UPDATE dbo.EvidenciasFacialesRetiroEncomienda SET ConsumidaUtc=@Ahora
        WHERE EvidenciaId=@EvidenciaId AND ConsumidaUtc IS NULL;
        IF @@ROWCOUNT<>1 THROW 56005,'No se pudo consumir la evidencia.',1;
        UPDATE dbo.CodigosRetiroEncomiendaPrototipo SET Consumido=1,FechaUsoUtc=@Ahora
        WHERE UsoId=@UsoId AND Consumido=0;
        IF @@ROWCOUNT<>1 THROW 56006,'No se pudo consumir el PIN.',1;
        UPDATE dbo.UsosCasillero SET Estado=N'Finalizado',FechaRetiro=@Ahora WHERE Id=@UsoId AND Estado=N'Ocupado';
        IF @@ROWCOUNT<>1 THROW 56007,'No se pudo finalizar la operacion.',1;
        UPDATE dbo.Casilleros SET Estado=N'Disponible' WHERE Codigo=@Codigo AND Estado=N'Ocupado';
        IF @@ROWCOUNT<>1 THROW 56008,'No se pudo liberar el casillero.',1;
        UPDATE dbo.OutboxWhatsApp SET Estado=N'Cancelado',CodigoError=N'EncomiendaRetirada',ContenidoProtegido=NULL
        WHERE ReservaId IN(SELECT ReservaId FROM dbo.DepositosPreReservaEncomienda WHERE UsoId=@UsoId)
            AND Estado IN(N'Pendiente',N'Reintento');
        INSERT dbo.AuditoriaEncomiendasPrototipo(UsoId,TerminalId,Accion)VALUES(@UsoId,@TerminalId,N'Retiro confirmado');
        COMMIT;
        SELECT N'Retirado';
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH;
END;
