IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57800, 'Base incorrecta.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class=0
      AND name=N'SynchroFixtureDeposito'
      AND CONVERT(nvarchar(10),value)=N'1'
)
    THROW 57801, 'Base no autorizada.', 1;

IF OBJECT_ID(N'dbo.CobrosRetiroPersonal',N'U') IS NULL
    THROW 57802, 'Falta tabla de cobros.', 1;
GO

CREATE OR ALTER PROCEDURE dbo.PrepararCobroRetiroPersonalSynchro
    @UsoId INT,
    @ImporteHnl DECIMAL(12,2),
    @DescripcionTarifa NVARCHAR(160)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 57800, 'Base incorrecta.', 1;

    IF @UsoId IS NULL OR @UsoId <= 0
       OR @ImporteHnl IS NULL OR @ImporteHnl <= 0
       OR NULLIF(LTRIM(RTRIM(@DescripcionTarifa)),N'') IS NULL
        THROW 57803, 'Datos de cobro invalidos.', 1;

    DECLARE
        @Codigo NVARCHAR(10),
        @EstadoCasillero NVARCHAR(20),
        @EstadoUso NVARCHAR(20),
        @Ingreso DATETIME2(7),
        @Tipo NVARCHAR(30),
        @Origen NVARCHAR(20),
        @Retiro DATETIME2(7),
        @EstadoCobro NVARCHAR(24),
        @Segundos BIGINT,
        @Minutos INT,
        @Ahora DATETIME2(7) = SYSUTCDATETIME();

    SELECT @Codigo = CodigoCasillero
    FROM dbo.UsosCasillero
    WHERE Id=@UsoId;

    IF @Codigo IS NULL
        THROW 57804, 'Uso no disponible.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @EstadoCasillero=Estado
        FROM dbo.Casilleros WITH (UPDLOCK,HOLDLOCK)
        WHERE Codigo=@Codigo;

        SELECT
            @EstadoUso=Estado,
            @Ingreso=FechaIngreso,
            @Tipo=TipoServicio,
            @Origen=OrigenReserva,
            @Retiro=FechaRetiro
        FROM dbo.UsosCasillero WITH (UPDLOCK,HOLDLOCK)
        WHERE Id=@UsoId
          AND CodigoCasillero=@Codigo;

        IF @EstadoCasillero IS NULL
           OR @EstadoCasillero<>N'Ocupado'
           OR @EstadoUso IS NULL
           OR @EstadoUso<>N'Ocupado'
           OR @Ingreso IS NULL
           OR @Retiro IS NOT NULL
           OR @Tipo<>N'ResguardoPersonal'
           OR @Origen<>N'Terminal'
            THROW 57805, 'Uso no disponible para cobro.', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.AutorizacionesWhatsAppManuales
            WHERE UsoId=@UsoId
              AND Estado=N'Confirmado'
        )
            THROW 57806, 'Autorizacion no disponible.', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.CodigosRetiroPersonal WITH (UPDLOCK,HOLDLOCK)
            WHERE UsoId=@UsoId
              AND FechaUso IS NULL
              AND IntentosFallidos<5
              AND (
                FechaVencimiento IS NULL OR
                FechaVencimiento>@Ahora
              )
        )
            THROW 57807, 'PIN no disponible.', 1;

        SELECT @EstadoCobro=Estado
        FROM dbo.CobrosRetiroPersonal WITH (UPDLOCK,HOLDLOCK)
        WHERE UsoId=@UsoId;

        IF @EstadoCobro IS NOT NULL
        BEGIN
            IF @EstadoCobro<>N'Pendiente'
                THROW 57808, 'Cobro ya procesado.', 1;

            -- Reutilizar la orden pendiente existente.
            COMMIT TRANSACTION;

            SELECT
                CobroId,
                UsoId,
                MinutosUtilizados,
                ImporteHnl,
                DescripcionTarifa,
                Estado
            FROM dbo.CobrosRetiroPersonal
            WHERE UsoId=@UsoId;

            RETURN;
        END;

        SET @Segundos=DATEDIFF_BIG(
            SECOND,@Ingreso,@Ahora
        );

        IF @Segundos>128849016000
            THROW 57809, 'Tiempo de servicio fuera de rango.', 1;

        SET @Minutos=CONVERT(INT,
            CASE
                WHEN @Segundos<=0 THEN 1
                ELSE (@Segundos+59)/60
            END
        );

        INSERT dbo.CobrosRetiroPersonal
        (
            UsoId,
            MinutosUtilizados,
            ImporteHnl,
            DescripcionTarifa,
            Estado
        )
        VALUES
        (
            @UsoId,
            @Minutos,
            @ImporteHnl,
            @DescripcionTarifa,
            N'Pendiente'
        );

        COMMIT TRANSACTION;

        SELECT
            CobroId,
            UsoId,
            MinutosUtilizados,
            ImporteHnl,
            DescripcionTarifa,
            Estado
        FROM dbo.CobrosRetiroPersonal
        WHERE UsoId=@UsoId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;