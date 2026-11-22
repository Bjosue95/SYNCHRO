SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57800, 'Base no autorizada.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class = 0
      AND name = N'SynchroFixtureDeposito'
      AND CONVERT(nvarchar(10), value) = N'1'
)
    THROW 57801, 'Base sin marcador de pruebas.', 1;
GO
CREATE OR ALTER PROCEDURE
    dbo.PrepararCobroRetiroPersonalSynchro
    @UsoId INT,
    @MinutosPorBloque INT,
    @TarifaPequenoHnl DECIMAL(12,2) = NULL,
    @TarifaMedianoHnl DECIMAL(12,2) = NULL,
    @TarifaGrandeHnl DECIMAL(12,2) = NULL,
    @AdicionalPequenoHnl DECIMAL(12,2) = NULL,
    @AdicionalMedianoHnl DECIMAL(12,2) = NULL,
    @AdicionalGrandeHnl DECIMAL(12,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 57800, 'Base no autorizada.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE class=0
          AND name=N'SynchroFixtureDeposito'
          AND CONVERT(nvarchar(10),value)=N'1'
    )
        THROW 57801, 'Base sin marcador.', 1;

    IF @UsoId IS NULL OR @UsoId <= 0 OR
       @MinutosPorBloque IS NULL OR
       @MinutosPorBloque NOT BETWEEN 1 AND 1440
        THROW 57803, 'Parametros de cobro invalidos.', 1;

    DECLARE
        @Codigo NVARCHAR(10),
        @Tamano NVARCHAR(20),
        @EstadoCasillero NVARCHAR(20),
        @EstadoUso NVARCHAR(20),
        @Ingreso DATETIME2(7),
        @Retiro DATETIME2(7),
        @Tipo NVARCHAR(30),
        @Origen NVARCHAR(20),
        @EstadoCobro NVARCHAR(24),
        @Ahora DATETIME2(7),
        @Microsegundos BIGINT,
        @Minutos INT,
        @Bloques BIGINT,
        @Tarifa DECIMAL(12,2),
        @TarifaExtra DECIMAL(12,2),
        @ImporteAmplio DECIMAL(28,2),
        @Importe DECIMAL(12,2),
        @Descripcion NVARCHAR(160);

    SELECT @Codigo = CodigoCasillero
    FROM dbo.UsosCasillero
    WHERE Id=@UsoId;

    IF @Codigo IS NULL
        THROW 57804, 'Uso no disponible.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT
            @EstadoCasillero=Estado,
            @Tamano=Tamano
        FROM dbo.Casilleros WITH (UPDLOCK,HOLDLOCK)
        WHERE Codigo=@Codigo;

        SELECT
            @EstadoUso=Estado,
            @Ingreso=FechaIngreso,
            @Retiro=FechaRetiro,
            @Tipo=TipoServicio,
            @Origen=OrigenReserva
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

        -- Seleccionar la tarifa del tamano real, bajo bloqueo.
        SET @Tarifa = CASE REPLACE(@Tamano, N'ñ', N'n')
            WHEN N'Pequeno' THEN @TarifaPequenoHnl
            WHEN N'Mediano' THEN @TarifaMedianoHnl
            WHEN N'Grande' THEN @TarifaGrandeHnl
            ELSE NULL
        END;

        SET @TarifaExtra = CASE REPLACE(@Tamano, N'ñ', N'n')
            WHEN N'Pequeno' THEN @AdicionalPequenoHnl
            WHEN N'Mediano' THEN @AdicionalMedianoHnl
            WHEN N'Grande' THEN @AdicionalGrandeHnl
            ELSE NULL
        END;

        IF @TarifaExtra IS NULL OR
           @TarifaExtra <= 0 OR @TarifaExtra > 100000
            THROW 57810, 'Tarifa adicional no configurada.', 1;

        IF @Tarifa IS NULL OR
           @Tarifa <= 0 OR @Tarifa > 100000
            THROW 57810, 'Tarifa no configurada.', 1;

        SET @Ahora = SYSUTCDATETIME();

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
           AND @EstadoCobro<>N'Pendiente'
            THROW 57808, 'Cobro ya procesado.', 1;

        IF @EstadoCobro IS NULL
        BEGIN
            -- Una sola referencia de tiempo dentro de SQL.
            SET @Microsegundos=DATEDIFF_BIG(
                MICROSECOND,@Ingreso,@Ahora
            );

            IF @Microsegundos >
                CONVERT(BIGINT,2147483647)*60000000
                THROW 57809, 'Tiempo fuera de rango.', 1;

            SET @Minutos=CONVERT(INT,
                CASE
                    WHEN @Microsegundos<=0 THEN 1
                    ELSE (@Microsegundos+59999999)/60000000
                END
            );

            SET @Bloques=CASE
                WHEN @Minutos<=60 THEN 0
                ELSE (CONVERT(BIGINT,@Minutos)-60
                      +@MinutosPorBloque-1)/@MinutosPorBloque
            END;

            SET @ImporteAmplio=@Tarifa
                              +@Bloques*@TarifaExtra;

            IF @ImporteAmplio<=0 OR
               @ImporteAmplio>9999999999.99
                THROW 57811, 'Importe fuera de rango.', 1;

            SET @Importe=CONVERT(
                DECIMAL(12,2),@ImporteAmplio
            );

            SET @Descripcion=CONCAT(
                N'Resguardo ',@Tamano,
                N': primera hora y ',@Bloques,
                N' bloque(s) adicionales de ',@MinutosPorBloque,
                N' minutos.'
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
                @Importe,
                @Descripcion,
                N'Pendiente'
            );
        END;

        -- Si ya estaba pendiente se conserva el importe original.
        -- No se vuelve a calcular ni se crea otro cobro.
        COMMIT TRANSACTION;

        SELECT
            c.CobroId,
            c.UsoId,
            c.MinutosUtilizados,
            c.ImporteHnl,
            c.DescripcionTarifa,
            c.Estado,
            @Codigo AS CodigoCasillero,
            @Tamano AS TamanoCasillero
        FROM dbo.CobrosRetiroPersonal c
        WHERE c.UsoId=@UsoId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
