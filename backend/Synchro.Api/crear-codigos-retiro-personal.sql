/*
 SYNCHRO
 Credenciales para retiro de pertenencias personales
 Exclusivamente para SynchroDB_Pruebas
*/
IF DB_NAME() <> N'SynchroDB_Pruebas'
    BEGIN
        THROW 50300, 'Esta operacion solo se permite en SynchroDB_Pruebas.', 1;
    END

IF OBJECT_ID(N'dbo.CodigosRetiroPersonal', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.CodigosRetiroPersonal (
            UsoId            INT            NOT NULL,
            PinHash          VARBINARY (32) NOT NULL,
            PinSalt          VARBINARY (32) NOT NULL,
            FechaCreacion    DATETIME2 (7)  CONSTRAINT DF_RetiroPersonal_FechaCreacion DEFAULT SYSUTCDATETIME() NOT NULL,
            FechaVencimiento DATETIME2 (7)  NULL,
            IntentosFallidos INT            CONSTRAINT DF_RetiroPersonal_Intentos DEFAULT (0) NOT NULL,
            FechaUso         DATETIME2 (7)  NULL,
            CONSTRAINT PK_CodigosRetiroPersonal PRIMARY KEY (UsoId),
            CONSTRAINT FK_RetiroPersonal_Usos FOREIGN KEY (UsoId) REFERENCES dbo.UsosCasillero (Id),
            CONSTRAINT CK_RetiroPersonal_Intentos CHECK (IntentosFallidos BETWEEN 0 AND 5),
            CONSTRAINT CK_RetiroPersonal_Hashes CHECK (DATALENGTH(PinHash) = 32
                                                       AND DATALENGTH(PinSalt) = 32),
            CONSTRAINT CK_RetiroPersonal_Vencimiento CHECK (FechaVencimiento IS NULL
                                                            OR FechaVencimiento > FechaCreacion)
        );
        PRINT 'Tabla CodigosRetiroPersonal creada.';
    END
ELSE
    BEGIN
        PRINT 'La tabla CodigosRetiroPersonal ya existe.';
    END