-- SYNCHRO
-- Preparacion de seguridad para deposito personal
-- Exclusivamente para SynchroDB_Pruebas
SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 51090, 'Base de datos incorrecta.', 1;

IF OBJECT_ID(N'dbo.UsosCasillero', N'U') IS NULL
    THROW 51091, 'No existe la tabla UsosCasillero.', 1;

IF COL_LENGTH(N'dbo.UsosCasillero', N'TokenDepositoHash') IS NULL
    BEGIN
        ALTER TABLE dbo.UsosCasillero
            ADD TokenDepositoHash VARBINARY (32) NULL;
        PRINT 'Columna TokenDepositoHash agregada.';
    END
ELSE
    BEGIN
        PRINT 'La columna ya existe. No se realizaron cambios.';
    END