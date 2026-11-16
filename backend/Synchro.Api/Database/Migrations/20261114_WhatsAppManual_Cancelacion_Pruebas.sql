SET XACT_ABORT ON;
-- LocalDB publica un nombre interno LOCALDB#..., no el alias SYNCHRO_Aislado.
IF DB_NAME()<>N'SynchroDB_Pruebas' OR CONVERT(int,SERVERPROPERTY('IsLocalDB'))<>1
    OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito' AND CONVERT(nvarchar(10),value)=N'1')
    THROW 57300,'Solo se permite la instancia aislada de pruebas.',1;
IF OBJECT_ID(N'dbo.AutorizacionesWhatsAppManuales',N'U') IS NULL
    THROW 57301,'Instalar primero el modulo WhatsApp manual.',1;
BEGIN TRANSACTION;
ALTER TABLE dbo.AutorizacionesWhatsAppManuales DROP CONSTRAINT CK_WAM_Estado;
ALTER TABLE dbo.AutorizacionesWhatsAppManuales ADD CONSTRAINT CK_WAM_Estado
    CHECK(Estado IN(N'Pendiente',N'Reservado',N'PinEmitido',N'Enviado',N'Confirmado',N'Cancelado'));
COMMIT;
GO
CREATE OR ALTER PROCEDURE dbo.CancelarReservaPersonalManualSynchro @IntentoId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 57300,'Base no autorizada.',1;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @Uso INT,@Codigo NVARCHAR(10),@Estado NVARCHAR(20);
        SELECT @Uso=UsoId,@Codigo=CodigoCasillero,@Estado=Estado
        FROM dbo.AutorizacionesWhatsAppManuales WITH(UPDLOCK,HOLDLOCK) WHERE Id=@IntentoId;
        IF @Estado IS NULL OR @Estado IN(N'Confirmado',N'Cancelado') THROW 57302,'Estado no cancelable.',1;
        IF @Uso IS NOT NULL
        BEGIN
            -- Mismo orden de bloqueo que el deposito: autorizacion, casillero, uso.
            IF NOT EXISTS(SELECT 1 FROM dbo.Casilleros WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@Codigo AND Estado IN(N'Reservado',N'Disponible'))
                THROW 57303,'Casillero ocupado o bloqueado.',1;
            IF NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK) WHERE Id=@Uso AND CodigoCasillero=@Codigo
                AND Estado IN(N'Reservado',N'Cancelado') AND FechaIngreso IS NULL AND FechaRetiro IS NULL
                AND TipoServicio=N'ResguardoPersonal' AND OrigenReserva=N'Terminal')
                THROW 57304,'Uso ocupado o incompatible.',1;
            IF EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK) WHERE CodigoCasillero=@Codigo AND Id<>@Uso
                AND Estado IN(N'Reservado',N'Ocupado') AND FechaRetiro IS NULL) THROW 57305,'Otra ocupacion activa.',1;
            UPDATE dbo.UsosCasillero SET Estado=N'Cancelado' WHERE Id=@Uso AND Estado=N'Reservado';
            UPDATE dbo.CodigosRetiroPersonal SET FechaUso=SYSUTCDATETIME() WHERE UsoId=@Uso AND FechaUso IS NULL;
            UPDATE dbo.Casilleros SET Estado=N'Disponible' WHERE Codigo=@Codigo AND Estado=N'Reservado';
        END;
        UPDATE dbo.AutorizacionesWhatsAppManuales SET Estado=N'Cancelado' WHERE Id=@IntentoId;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF XACT_STATE()<>0 ROLLBACK;
        THROW;
    END CATCH
END;
GO
