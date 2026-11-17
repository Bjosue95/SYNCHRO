-- SYNCHRO: registro de cobros por retiro personal
-- EXCLUSIVAMENTE PARA LA BASE AISLADA DEL PROTOTIPO.

SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57500, 'Base de datos no autorizada.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.extended_properties
    WHERE class = 0
      AND name = N'SynchroFixtureDeposito'
)
    THROW 57501, 'No es una base de pruebas autorizada.', 1;

IF OBJECT_ID(N'dbo.UsosCasillero', N'U') IS NULL
    THROW 57502, 'Falta la tabla de usos.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.CobrosRetiroPersonal', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CobrosRetiroPersonal
        (
            UsoId INT NOT NULL
                CONSTRAINT PK_CobrosRetiroPersonal
                PRIMARY KEY,

            CobroId UNIQUEIDENTIFIER NOT NULL
                CONSTRAINT DF_CobroPersonal_Id
                DEFAULT NEWID(),

            MinutosUtilizados INT NOT NULL,

            ImporteHnl DECIMAL(12,2) NOT NULL,

            DescripcionTarifa NVARCHAR(160) NOT NULL,

            Estado NVARCHAR(24) NOT NULL
                CONSTRAINT DF_CobroPersonal_Estado
                DEFAULT N'Pendiente',

            MetodoPago NVARCHAR(24) NULL,

            CreadoUtc DATETIME2(3) NOT NULL
                CONSTRAINT DF_CobroPersonal_Fecha
                DEFAULT SYSUTCDATETIME(),

            ConfirmadoUtc DATETIME2(3) NULL,

            FinalizadoUtc DATETIME2(3) NULL,

            CONSTRAINT FK_CobroPersonal_Uso
                FOREIGN KEY (UsoId)
                REFERENCES dbo.UsosCasillero(Id),

            CONSTRAINT UQ_CobroPersonal_Id
                UNIQUE (CobroId),

            CONSTRAINT CK_CobroPersonal_Minutos
                CHECK (MinutosUtilizados >= 1),

            CONSTRAINT CK_CobroPersonal_Importe
                CHECK (ImporteHnl >= 0),

            CONSTRAINT CK_CobroPersonal_Estado
                CHECK (Estado IN (
                    N'Pendiente',
                    N'ConfirmadoSimulado',
                    N'Aplicado',
                    N'Cancelado'
                ))
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    OBJECT_NAME(OBJECT_ID(N'dbo.CobrosRetiroPersonal'))
        AS TablaCreada,
    COUNT(*) AS CobrosRegistrados
FROM dbo.CobrosRetiroPersonal;