IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 59020, 'Base no autorizada.', 1;

IF OBJECT_ID(N'dbo.RostrosRetiroPersonalV3', N'U') IS NULL
    THROW 59021, 'La tabla de referencias V3 no existe.', 1;

IF OBJECT_ID(N'dbo.ConsentimientosRostroPersonalV3', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConsentimientosRostroPersonalV3
    (
        UsoId INT NOT NULL,

        VersionTexto NVARCHAR(60) NOT NULL,

        DeclaradoUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_ConsentimientoRostroV3_Fecha
            DEFAULT (SYSUTCDATETIME()),

        Canal NVARCHAR(24) NOT NULL,

        CONSTRAINT PK_ConsentimientosRostroPersonalV3
            PRIMARY KEY (UsoId),

        CONSTRAINT FK_ConsentimientoRostroV3_Referencia
            FOREIGN KEY (UsoId)
            REFERENCES dbo.RostrosRetiroPersonalV3(UsoId)
            ON DELETE CASCADE
    );
END;