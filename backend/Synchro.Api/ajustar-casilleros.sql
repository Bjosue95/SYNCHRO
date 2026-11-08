-- SYNCHRO
-- Migracion de codigos del prototipo fisico
-- Conserva el historial de usos y sus Id
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;
    -- Comprobar la cantidad de casilleros
    IF (SELECT COUNT(*)
        FROM   dbo.Casilleros) <> 10
        THROW 51001, 'Se esperaban 10 casilleros. No se realizaron cambios.', 1;
    -- Comprobar los codigos originales
    IF EXISTS (SELECT 1
               FROM   dbo.Casilleros
               WHERE  Codigo NOT IN ('S01', 'S03', 'S06', 'S08', 'M02', 'M04', 'M07', 'M09', 'L05', 'L10'))
        THROW 51002, 'Los codigos actuales son diferentes a los revisados.', 1;
    -- Proteger los casilleros que se modificaran
    IF (SELECT COUNT(*)
        FROM   dbo.Casilleros
        WHERE  Codigo IN ('S03', 'S06', 'S08', 'M02')
               AND Tipo = 'Normal'
               AND Estado = 'Disponible') <> 4
        THROW 51003, 'Alguno de los casilleros cambio de estado.', 1;
    -- Comprobar el uso historico de M02
    IF (SELECT COUNT(*)
        FROM   dbo.UsosCasillero
        WHERE  CodigoCasillero = 'M02'
               AND Estado = 'Finalizado') <> 1
       OR (SELECT COUNT(*)
           FROM   dbo.UsosCasillero
           WHERE  CodigoCasillero = 'M02') <> 1
        THROW 51004, 'El historial de M02 ha cambiado.', 1;
    -- Crear M03 conservando las propiedades de M02
    INSERT INTO dbo.Casilleros (
        Codigo,
        Tamano,
        Tipo,
        Estado
    )
    SELECT 'M03',
           Tamano,
           Tipo,
           Estado
    FROM   dbo.Casilleros
    WHERE  Codigo = 'M02';
    -- Transferir la referencia historica, sin cambiar el Id
    UPDATE dbo.UsosCasillero
    SET    CodigoCasillero = 'M03'
    WHERE  CodigoCasillero = 'M02';
    -- Eliminar solamente el codigo antiguo M02
    DELETE dbo.Casilleros
    WHERE  Codigo = 'M02';
    -- Ajustar los demas casilleros
    UPDATE dbo.Casilleros
    SET    Codigo = 'S02'
    WHERE  Codigo = 'S03';
    UPDATE dbo.Casilleros
    SET    Codigo = 'M06',
           Tamano = 'Mediano'
    WHERE  Codigo = 'S06';
    UPDATE dbo.Casilleros
    SET    Codigo = 'M08',
           Tamano = 'Mediano'
    WHERE  Codigo = 'S08';
    -- Validar la distribucion final
    IF (SELECT COUNT(*)
        FROM   dbo.Casilleros) <> 10
        THROW 51005, 'La cantidad final de casilleros es incorrecta.', 1;
    IF (SELECT COUNT(*)
        FROM   dbo.Casilleros
        WHERE  Codigo IN ('M03', 'M06', 'M07', 'M08')
               AND Tamano = 'Mediano'
               AND Tipo = 'Normal') <> 4
        THROW 51006, 'La distribucion de casilleros medianos es incorrecta.', 1;
    IF (SELECT COUNT(*)
        FROM   dbo.Casilleros
        WHERE  Codigo IN ('S01', 'S02')
               AND Tipo = 'Normal') <> 2
        THROW 51007, 'La distribucion de casilleros pequenos es incorrecta.', 1;
    IF (SELECT COUNT(*)
        FROM   dbo.UsosCasillero
        WHERE  CodigoCasillero = 'M03') <> 1
        THROW 51008, 'No se conservo el historial de M02.', 1;
    COMMIT TRANSACTION;
    PRINT 'Migracion de SYNCHRO completada correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;
    THROW;
END CATCH

-- Mostrar el resultado
SELECT   Codigo,
         Tamano,
         Tipo,
         Estado
FROM     dbo.Casilleros
ORDER BY Codigo;

SELECT CodigoCasillero,
       Estado
FROM   dbo.UsosCasillero
WHERE  CodigoCasillero = 'M03';