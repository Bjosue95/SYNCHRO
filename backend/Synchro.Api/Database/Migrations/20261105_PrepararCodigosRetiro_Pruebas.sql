
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Seguridad: ejecutar solo sobre la base de pruebas.
IF DB_NAME() <> N'SynchroDB_Detalle_Pruebas'
    THROW 51090, 'Base de datos no autorizada.', 1;

IF OBJECT_ID(N'dbo.CodigosRetiro', N'U') IS NULL
    THROW 51091, 'No existe CodigosRetiro.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Evitar varios registros de PIN por encomienda.
    IF EXISTS (
        SELECT UsoId
        FROM dbo.CodigosRetiro
        GROUP BY UsoId
        HAVING COUNT(*) > 1
    )
        THROW 51092, 'Existen codigos duplicados.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.CodigosRetiro')
          AND name = N'UX_CodigosRetiro_UsoId'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_CodigosRetiro_UsoId
        ON dbo.CodigosRetiro(UsoId);
    END;

    -- Exigir hashes HMAC-SHA256 de 32 bytes.
    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.CodigosRetiro')
          AND name = N'CK_CodigosRetiro_Hash'
    )
    BEGIN
        ALTER TABLE dbo.CodigosRetiro WITH CHECK
        ADD CONSTRAINT CK_CodigosRetiro_Hash
        CHECK (DATALENGTH(PinHash) = 32);
    END;

    -- Exigir salt de 32 bytes.
    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.CodigosRetiro')
          AND name = N'CK_CodigosRetiro_Salt'
    )
    BEGIN
        ALTER TABLE dbo.CodigosRetiro WITH CHECK
        ADD CONSTRAINT CK_CodigosRetiro_Salt
        CHECK (DATALENGTH(PinSalt) = 32);
    END;

    -- El vencimiento debe ser posterior a la creacion.
    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.CodigosRetiro')
          AND name = N'CK_CodigosRetiro_Fechas'
    )
    BEGIN
        ALTER TABLE dbo.CodigosRetiro WITH CHECK
        ADD CONSTRAINT CK_CodigosRetiro_Fechas
        CHECK (
            FechaVencimiento > FechaCreacion
            AND (
                FechaUso IS NULL
                OR FechaUso >= FechaCreacion
            )
        );
    END;

    COMMIT TRANSACTION;

    PRINT 'ESTRUCTURA PIN RETIRO PREPARADA CORRECTAMENTE';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
