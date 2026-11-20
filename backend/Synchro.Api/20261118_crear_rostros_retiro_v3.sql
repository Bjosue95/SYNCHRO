IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 59010, 'Base no autorizada para V3.', 1;

IF OBJECT_ID(N'dbo.RostrosRetiroPersonalV3', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RostrosRetiroPersonalV3
    (
        UsoId INT NOT NULL,

        PatronProtegido VARBINARY(MAX) NOT NULL,

        Modelo NVARCHAR(60) NOT NULL,

        RegistradoUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_RostrosV3_Registrado
            DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_RostrosRetiroPersonalV3
            PRIMARY KEY (UsoId),

        CONSTRAINT FK_RostrosV3_Usos
            FOREIGN KEY (UsoId)
            REFERENCES dbo.UsosCasillero(Id)
            ON DELETE CASCADE,

        CONSTRAINT CK_RostrosV3_Modelo
            CHECK (
                Modelo = N'SYNCHRO-HUMANJS-EMBEDDING-1024-v3'
            ),

        CONSTRAINT CK_RostrosV3_Tamano
            CHECK (
                DATALENGTH(PatronProtegido)
                BETWEEN 64 AND 65536
            )
    );
END;