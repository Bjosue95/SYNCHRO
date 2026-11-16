-- SYNCHRO / SOLO PROTOTIPO LOCAL / SynchroDB_Pruebas.
-- Guarda patrones visuales CIFRADOS, no fotografias. No representa autenticacion biometrica.
IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 57400, 'Solo SynchroDB_Pruebas.', 1;
GO
IF OBJECT_ID(N'dbo.UsosCasillero', N'U') IS NULL
    THROW 57401, 'Falta UsosCasillero.', 1;
GO
IF OBJECT_ID(N'dbo.RostrosRetiroPersonalManual', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RostrosRetiroPersonalManual
    (
        UsoId INT NOT NULL CONSTRAINT PK_RostrosRetiroPersonalManual PRIMARY KEY,
        PatronProtegido VARBINARY(MAX) NOT NULL,
        Modelo NVARCHAR(60) NOT NULL,
        RegistradoUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_RostrosRetiroPersonalManual_Registrado DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_RostrosRetiroPersonalManual_Uso
            FOREIGN KEY (UsoId) REFERENCES dbo.UsosCasillero(Id)
    );
END;
GO
