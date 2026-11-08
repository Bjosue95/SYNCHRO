-- SYNCHRO
-- Actualizacion del procedimiento de reservas
-- Conserva las validaciones y bloqueos existentes
SET ANSI_NULLS ON;

SET QUOTED_IDENTIFIER ON;


GO
CREATE OR ALTER PROCEDURE dbo.ReservarCasilleroSynchro
@CodigoCasillero NVARCHAR (10), @TipoServicio NVARCHAR (20), @OrigenReserva NVARCHAR (20)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @CodigoCasillero IS NULL
       OR LTRIM(RTRIM(@CodigoCasillero)) = N''
        THROW 50020, 'Codigo de casillero obligatorio.', 1;
    IF @TipoServicio IS NULL
       OR @TipoServicio NOT IN (N'Encomienda', N'ResguardoPersonal')
        THROW 50021, 'Tipo de servicio no permitido.', 1;
    IF @OrigenReserva IS NOT NULL
       AND @OrigenReserva NOT IN (N'Terminal', N'Web')
        THROW 50026, 'Origen de reserva no permitido.', 1;
    DECLARE @TipoCasillero AS NVARCHAR (20);
    DECLARE @Estado AS NVARCHAR (20);
    DECLARE @UsoId AS INT;
    DECLARE @FechaReserva AS DATETIME2 (7) = SYSUTCDATETIME();
    DECLARE @FechaVencimiento AS DATETIME2 (7) = CASE WHEN @OrigenReserva = N'Terminal' THEN DATEADD(MINUTE, 10, @FechaReserva) ELSE NULL END;
    BEGIN TRY
        BEGIN TRANSACTION;
        SELECT @TipoCasillero = Tipo,
               @Estado = Estado
        FROM   dbo.Casilleros WITH (UPDLOCK, HOLDLOCK)
        WHERE  Codigo = @CodigoCasillero;
        IF @TipoCasillero IS NULL
            THROW 50022, 'Casillero no encontrado.', 1;
        IF @Estado <> N'Disponible'
            THROW 50023, 'Casillero no disponible.', 1;
        IF @TipoCasillero <> N'Normal'
           AND @TipoServicio <> N'Encomienda'
            THROW 50024, 'Casillero especial exclusivo para encomiendas.', 1;
        IF EXISTS (SELECT 1
                   FROM   dbo.UsosCasillero
                   WHERE  CodigoCasillero = @CodigoCasillero
                          AND Estado IN (N'Reservado', N'Ocupado')
                          AND FechaRetiro IS NULL)
            THROW 50025, 'El casillero tiene una operacion activa.', 1;
        UPDATE dbo.Casilleros
        SET    Estado = N'Reservado'
        WHERE  Codigo = @CodigoCasillero
               AND Estado = N'Disponible';
        IF @@ROWCOUNT <> 1
            THROW 50023, 'Casillero no disponible.', 1;
        INSERT  INTO dbo.UsosCasillero (
            CodigoCasillero,
            TipoServicio,
            FechaReserva,
            Estado,
            OrigenReserva,
            FechaVencimiento
        )
        VALUES                         (@CodigoCasillero, @TipoServicio, @FechaReserva, N'Reservado', @OrigenReserva, @FechaVencimiento);
        SET @UsoId = CONVERT (INT, SCOPE_IDENTITY());
        COMMIT TRANSACTION;
        SELECT u.Id AS IdUso,
               u.CodigoCasillero,
               u.TipoServicio,
               c.Tipo AS TipoCasillero,
               c.Tamano,
               u.Estado,
               u.FechaReserva,
               u.OrigenReserva,
               u.FechaVencimiento
        FROM   dbo.UsosCasillero AS u
               INNER JOIN
               dbo.Casilleros AS c
               ON c.Codigo = u.CodigoCasillero
        WHERE  u.Id = @UsoId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK;
        THROW;
    END CATCH
END