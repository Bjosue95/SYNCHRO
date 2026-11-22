-- SYNCHRO: cobro simulado y retiro seguro de CARGA (no pertenencias).
-- Solo base aislada SynchroDB_Pruebas, sin tarifas inventadas.
SET XACT_ABORT ON;
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57920, 'Base no autorizada.', 1;
IF NOT EXISTS (
  SELECT 1 FROM sys.extended_properties
  WHERE class = 0 AND name = N'SynchroFixtureDeposito'
    AND CONVERT(nvarchar(10), value) = N'1'
)
    THROW 57921, 'Falta marcador de base de pruebas.', 1;
IF OBJECT_ID(N'dbo.SesionesCargaSynchro', N'U') IS NULL
    THROW 57922, 'Falta SesionesCargaSynchro.', 1;
IF OBJECT_ID(N'dbo.EspaciosCarga', N'U') IS NULL
    THROW 57923, 'Falta EspaciosCarga.', 1;
-- El prototipo actual no tiene la antigua dbo.TarifasCarga.
-- Crear catalogo propio de precios para los servicios de CARGA.
-- NO insertar valores predeterminados ni copiar precios de resguardo.

BEGIN TRY
    BEGIN TRANSACTION;
    -- La base del prototipo posee el catalogo por modalidad:
    -- Inalambrica, Cable o Laptop (no por TipoDispositivo).
    IF OBJECT_ID(N'dbo.TarifasCargaSynchro', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TarifasCargaSynchro (
            Modalidad NVARCHAR(20) NOT NULL
                CONSTRAINT PK_TarifasCargaSynchro PRIMARY KEY,
            MinutosBase INT NOT NULL,
            PrecioBase DECIMAL(12,2) NOT NULL,
            MinutosAdicionales INT NOT NULL,
            PrecioAdicional DECIMAL(12,2) NOT NULL,
            CONSTRAINT CK_TarifaCargaV3Modalidad CHECK
                (Modalidad IN (N'Inalambrica', N'Cable', N'Laptop')),
            CONSTRAINT CK_TarifaCargaV3Minutos CHECK
                (MinutosBase BETWEEN 1 AND 1440
                 AND MinutosAdicionales BETWEEN 1 AND 1440),
            CONSTRAINT CK_TarifaCargaV3Precios CHECK
                (PrecioBase > 0 AND PrecioAdicional > 0)
        );
    END;

    IF COL_LENGTH(N'dbo.TarifasCargaSynchro',N'Modalidad') IS NULL
        OR COL_LENGTH(N'dbo.TarifasCargaSynchro',N'MinutosBase') IS NULL
        OR COL_LENGTH(N'dbo.TarifasCargaSynchro',N'PrecioBase') IS NULL
        OR COL_LENGTH(N'dbo.TarifasCargaSynchro',N'MinutosAdicionales') IS NULL
        OR COL_LENGTH(N'dbo.TarifasCargaSynchro',N'PrecioAdicional') IS NULL
        THROW 57925, 'El catalogo de tarifas existente no es compatible.', 1;

    IF OBJECT_ID(N'dbo.CobrosRetiroCargaSynchro', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CobrosRetiroCargaSynchro (
            SesionId UNIQUEIDENTIFIER NOT NULL
                CONSTRAINT PK_CobrosRetiroCargaSynchro PRIMARY KEY,
            CodigoEspacio NVARCHAR(10) NOT NULL,
            MinutosUtilizados INT NOT NULL,
            ImporteHnl DECIMAL(12,2) NOT NULL,
            DescripcionTarifa NVARCHAR(160) NOT NULL,
            Estado NVARCHAR(24) NOT NULL
                CONSTRAINT DF_CobroCargaEstado DEFAULT N'Pendiente',
            MetodoPago NVARCHAR(24) NULL,
            CreadoUtc DATETIME2(3) NOT NULL
                CONSTRAINT DF_CobroCargaCreadoUtc DEFAULT SYSUTCDATETIME(),
            ConfirmadoUtc DATETIME2(3) NULL,
            FinalizadoUtc DATETIME2(3) NULL,
            CONSTRAINT FK_CobroCargaSesion
                FOREIGN KEY (SesionId) REFERENCES dbo.SesionesCargaSynchro(Id),
            CONSTRAINT CK_CobroCargaMinutos CHECK (MinutosUtilizados >= 1),
            CONSTRAINT CK_CobroCargaImporte CHECK (ImporteHnl > 0),
            CONSTRAINT CK_CobroCargaEstado CHECK
                (Estado IN (N'Pendiente', N'ConfirmadoSimulado', N'Aplicado')),
            CONSTRAINT CK_CobroCargaMetodo CHECK
                (MetodoPago IS NULL OR
                 MetodoPago IN (N'EfectivoSimulado', N'TarjetaSimulada', N'NfcSimulado'))
        );
    END;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
SELECT N'CobrosRetiroCargaSynchro' AS Modulo,
       COUNT(*) AS CobrosRegistrados
FROM dbo.CobrosRetiroCargaSynchro;
SELECT N'TarifasCargaSynchro' AS Catalogo,
       COUNT(*) AS TarifasConfiguradas
FROM dbo.TarifasCargaSynchro;
