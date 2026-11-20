SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 59003, 'Base incorrecta.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Codigo NVARCHAR(10) =
        N'ZV3' + LEFT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), '-', ''), 6);

    INSERT dbo.Casilleros (Codigo, Tamano, Tipo, Estado)
    VALUES (@Codigo, N'Mediano', N'Normal', N'Ocupado');

    INSERT dbo.UsosCasillero
        (CodigoCasillero, TipoServicio, FechaReserva,
         FechaIngreso, Estado, OrigenReserva)
    VALUES
        (@Codigo, N'ResguardoPersonal', SYSUTCDATETIME(),
         SYSUTCDATETIME(), N'Ocupado', N'Terminal');

    DECLARE @UsoId INT = CONVERT(INT, SCOPE_IDENTITY());

    INSERT dbo.RostrosRetiroPersonalV3
        (UsoId, PatronProtegido, Modelo)
    VALUES
        (@UsoId, CRYPT_GEN_RANDOM(128),
         N'SYNCHRO-HUMANJS-EMBEDDING-1024-v3');

    IF NOT EXISTS (
        SELECT 1 FROM dbo.RostrosRetiroPersonalV3
        WHERE UsoId = @UsoId
    )
        THROW 59004, 'No se registro la referencia ficticia.', 1;

    UPDATE dbo.UsosCasillero
    SET Estado = N'Finalizado',
        FechaRetiro = SYSUTCDATETIME()
    WHERE Id = @UsoId;

    IF EXISTS (
        SELECT 1 FROM dbo.RostrosRetiroPersonalV3
        WHERE UsoId = @UsoId
    )
        THROW 59005, 'La limpieza V3 no funciono.', 1;

    SELECT N'PRUEBA CORRECTA: V3 eliminado al finalizar' AS Resultado;

    ROLLBACK TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;