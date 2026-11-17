SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57900, 'Base no autorizada.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class=0
      AND name=N'SynchroFixtureDeposito'
      AND CONVERT(nvarchar(10),value)=N'1'
)
    THROW 57901, 'Base de pruebas no autorizada.', 1;

IF OBJECT_ID(N'dbo.CobrosRetiroPersonal',N'U') IS NULL
    THROW 57902, 'Falta tabla de cobros.', 1;

IF OBJECT_ID(N'dbo.AuditoriaCobrosRetiroPersonal',N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditoriaCobrosRetiroPersonal
    (
        AuditoriaId BIGINT IDENTITY(1,1) NOT NULL
            PRIMARY KEY,

        CobroId UNIQUEIDENTIFIER NOT NULL
            UNIQUE,

        UsoId INT NOT NULL,

        CodigoCasillero NVARCHAR(10) NOT NULL,

        OperadorId INT NOT NULL
            CHECK (OperadorId > 0),

        ImporteHnl DECIMAL(12,2) NOT NULL
            CHECK (ImporteHnl > 0),

        MetodoPago NVARCHAR(24) NOT NULL
            CHECK (
                MetodoPago IN (
                    N'EfectivoSimulado',
                    N'TarjetaSimulada'
                )
            ),

        ConfirmadoUtc DATETIME2(3) NOT NULL,

        CONSTRAINT FK_AuditoriaCobro_Cobro
            FOREIGN KEY (CobroId)
            REFERENCES dbo.CobrosRetiroPersonal(CobroId)
    );
END;
GO

CREATE OR ALTER PROCEDURE
    dbo.ConfirmarCobroRetiroPersonalAuditableSynchro
    @CobroId UNIQUEIDENTIFIER,
    @Metodo NVARCHAR(24),
    @OperadorId INT,
    @PagoComprobado BIT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 57900, 'Base no autorizada.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE class=0
          AND name=N'SynchroFixtureDeposito'
          AND CONVERT(nvarchar(10),value)=N'1'
    )
        THROW 57901, 'Falta marcador de pruebas.', 1;

    IF @CobroId IS NULL OR
       @OperadorId IS NULL OR @OperadorId <= 0 OR
       @PagoComprobado IS NULL OR @PagoComprobado <> 1 OR
       @Metodo IS NULL OR
       @Metodo NOT IN (
           N'EfectivoSimulado',
           N'TarjetaSimulada'
       )
        THROW 57911, 'Confirmacion invalida.', 1;

    DECLARE @Actualizados TABLE
    (
        CobroId UNIQUEIDENTIFIER,
        UsoId INT,
        ImporteHnl DECIMAL(12,2),
        MetodoPago NVARCHAR(24),
        ConfirmadoUtc DATETIME2(3)
    );

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE c
        SET Estado=N'ConfirmadoSimulado',
            MetodoPago=@Metodo,
            ConfirmadoUtc=SYSUTCDATETIME()
        OUTPUT
            INSERTED.CobroId,
            INSERTED.UsoId,
            INSERTED.ImporteHnl,
            INSERTED.MetodoPago,
            INSERTED.ConfirmadoUtc
        INTO @Actualizados
        FROM dbo.CobrosRetiroPersonal c
            WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.UsosCasillero u
            ON u.Id=c.UsoId
        JOIN dbo.Casilleros cas
            ON cas.Codigo=u.CodigoCasillero
        WHERE c.CobroId=@CobroId
          AND c.Estado=N'Pendiente'
          AND c.ConfirmadoUtc IS NULL
          AND c.FinalizadoUtc IS NULL
          AND c.ImporteHnl>0
          AND c.MinutosUtilizados>0
          AND u.TipoServicio=N'ResguardoPersonal'
          AND u.OrigenReserva=N'Terminal'
          AND u.Estado=N'Ocupado'
          AND u.FechaIngreso IS NOT NULL
          AND u.FechaRetiro IS NULL
          AND cas.Estado=N'Ocupado'
          AND EXISTS (
              SELECT 1 FROM dbo.CodigosRetiroPersonal p
              WHERE p.UsoId=u.Id
                AND p.FechaUso IS NULL
                AND p.IntentosFallidos<5
                AND (
                    p.FechaVencimiento IS NULL OR
                    p.FechaVencimiento>SYSUTCDATETIME()
                )
          )
          AND EXISTS (
              SELECT 1
              FROM dbo.AutorizacionesWhatsAppManuales a
              WHERE a.UsoId=u.Id
                AND a.Estado=N'Confirmado'
          );

        IF (SELECT COUNT(*) FROM @Actualizados) <> 1
            THROW 57910, 'Cobro no disponible.', 1;

        INSERT dbo.AuditoriaCobrosRetiroPersonal
        (
            CobroId,
            UsoId,
            CodigoCasillero,
            OperadorId,
            ImporteHnl,
            MetodoPago,
            ConfirmadoUtc
        )
        SELECT
            x.CobroId,
            x.UsoId,
            u.CodigoCasillero,
            @OperadorId,
            x.ImporteHnl,
            x.MetodoPago,
            x.ConfirmadoUtc
        FROM @Actualizados x
        JOIN dbo.UsosCasillero u
            ON u.Id=x.UsoId;

        IF @@ROWCOUNT<>1
            THROW 57910, 'Auditoria no registrada.', 1;

        COMMIT TRANSACTION;

        SELECT
            x.CobroId,
            x.ImporteHnl,
            N'ConfirmadoSimulado' AS Estado,
            u.CodigoCasillero
        FROM @Actualizados x
        JOIN dbo.UsosCasillero u
            ON u.Id=x.UsoId;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO