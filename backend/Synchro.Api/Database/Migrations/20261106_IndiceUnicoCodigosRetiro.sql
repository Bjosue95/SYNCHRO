SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Migracion exclusiva de la base local de desarrollo.
IF DB_NAME() <> N'SynchroDB'
    THROW 51200, 'Base de datos no autorizada.', 1;

IF OBJECT_ID(N'dbo.CodigosRetiro', N'U') IS NULL
    THROW 51201, 'No existe la tabla CodigosRetiro.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Evitar crear el indice si hay registros duplicados.
    IF EXISTS (
        SELECT UsoId
        FROM dbo.CodigosRetiro WITH (TABLOCKX, HOLDLOCK)
        GROUP BY UsoId
        HAVING COUNT(*) > 1
    )
        THROW 51202, 'Existen UsoId duplicados.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.CodigosRetiro')
          AND name = N'UX_CodigosRetiro_UsoId'
    )
    BEGIN
        CREATE UNIQUE INDEX UX_CodigosRetiro_UsoId
        ON dbo.CodigosRetiro (UsoId);
    END;

    -- Comprobar que exista un indice unico sobre UsoId.
    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic
            ON i.object_id = ic.object_id
           AND i.index_id = ic.index_id
        INNER JOIN sys.columns c
            ON ic.object_id = c.object_id
           AND ic.column_id = c.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.CodigosRetiro')
          AND i.name = N'UX_CodigosRetiro_UsoId'
          AND i.is_unique = 1
          AND i.is_disabled = 0
          AND i.has_filter = 0
          AND ic.key_ordinal = 1
          AND c.name = N'UsoId'
          AND (
              SELECT COUNT(*)
              FROM sys.index_columns ic2
              WHERE ic2.object_id = i.object_id
                AND ic2.index_id = i.index_id
                AND ic2.key_ordinal > 0
          ) = 1
    )
        THROW 51203, 'No se verifico el indice unico.', 1;

    COMMIT TRANSACTION;

    PRINT 'Indice unico de PIN instalado correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
