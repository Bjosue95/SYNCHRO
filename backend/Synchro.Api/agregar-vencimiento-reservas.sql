-- SYNCHRO
-- Agregar campos para controlar el vencimiento de reservas
-- Conserva los registros y estados existentes
SET QUOTED_IDENTIFIER ON;

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;
    -- Origen de la reserva: Terminal o Web
    IF COL_LENGTH('dbo.UsosCasillero', 'OrigenReserva') IS NULL
        BEGIN
            ALTER TABLE dbo.UsosCasillero
                ADD OrigenReserva NVARCHAR (20) NULL;
        END
    -- Fecha de vencimiento de la reserva
    IF COL_LENGTH('dbo.UsosCasillero', 'FechaVencimiento') IS NULL
        BEGIN
            ALTER TABLE dbo.UsosCasillero
                ADD FechaVencimiento DATETIME2 (7) NULL;
        END
    COMMIT TRANSACTION;
    PRINT 'Estructura preparada correctamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;
    THROW;
END CATCH