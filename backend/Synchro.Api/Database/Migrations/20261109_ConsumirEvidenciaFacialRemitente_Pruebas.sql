-- SYNCHRO
-- Consumo unico de evidencias faciales.
-- Exclusivo para SynchroDB_Pruebas.
-- No autoriza depositos de encomiendas.
SET NOCOUNT ON;

SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 55401, 'Base de datos incorrecta.', 1;

IF OBJECT_ID(N'dbo.EvidenciasFacialesRemitente', N'U') IS NULL
    THROW 55402, 'Falta la tabla de evidencias.', 1;


GO
CREATE OR ALTER PROCEDURE dbo.ConsumirEvidenciaFacialRemitenteSynchro
@EvidenciaId UNIQUEIDENTIFIER, @ReservaId INT, @RemitenteId INT, @TerminalId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    -- Proteccion adicional.
    IF DB_NAME() <> N'SynchroDB_Pruebas'
        THROW 55403, 'Base no autorizada.', 1;
    IF @EvidenciaId IS NULL
       OR @EvidenciaId = '00000000-0000-0000-0000-000000000000'
       OR @ReservaId IS NULL
       OR @ReservaId <= 0
       OR @RemitenteId IS NULL
       OR @RemitenteId <= 0
       OR @TerminalId IS NULL
       OR @TerminalId <= 0
        BEGIN
            THROW 55404, 'Identificadores invalidos.', 1;
        END
    DECLARE @Ahora AS DATETIME2 (3) = SYSUTCDATETIME();
    DECLARE @Resultado TABLE (
        EvidenciaId     UNIQUEIDENTIFIER NOT NULL,
        FechaConsumoUtc DATETIME2 (3)    NOT NULL);
    -- UPDATE atomico: una sola solicitud puede
    -- cambiar Consumida de 0 a 1.
    UPDATE e
    SET    Consumida       = 1,
           FechaConsumoUtc = @Ahora
    OUTPUT INSERTED.EvidenciaId, INSERTED.FechaConsumoUtc INTO @Resultado (EvidenciaId, FechaConsumoUtc)
    FROM   dbo.EvidenciasFacialesRemitente AS e WITH (UPDLOCK)
           INNER JOIN
           dbo.Terminales AS t
           ON t.Id = e.TerminalId
           INNER JOIN
           dbo.PreReservasEncomiendasWeb AS r
           ON r.Id = e.ReservaId
    WHERE  e.EvidenciaId = @EvidenciaId
           AND e.ReservaId = @ReservaId
           AND e.RemitenteId = @RemitenteId
           AND e.TerminalId = @TerminalId
           AND -- Evidencia no utilizada.
           e.Consumida = 0
           AND e.FechaConsumoUtc IS NULL
           AND -- Evidencia vigente.
           e.FechaEmisionUtc <= @Ahora
           AND e.FechaExpiracionUtc > @Ahora
           AND DATEDIFF_BIG(MILLISECOND, e.FechaEmisionUtc, e.FechaExpiracionUtc) BETWEEN 1 AND 120000
           AND -- Resultado facial aprobado.
           e.RostroCoincide = 1
           AND e.PruebaDeVidaSuperada = 1
           AND -- Terminal activa.
           t.Activa = 1
           AND -- Reserva disponible.
           r.Estado = N'PendienteConsentimiento'
           AND r.FechaExpiracion > @Ahora;
    -- Si ninguna fila se actualizo, no se
    -- permite consumir la evidencia.
    IF @@ROWCOUNT <> 1
        BEGIN
            THROW 55405, 'Evidencia no valida, vencida o consumida.', 1;
        END
    SELECT EvidenciaId,
           FechaConsumoUtc,
           N'Consumida' AS Estado
    FROM   @Resultado;
END