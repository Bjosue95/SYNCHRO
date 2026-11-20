using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.RostroSimulado;

internal sealed class LimpiezaRostrosRetiroV3Worker(
    IConfiguration configuracion,
    ILogger<LimpiezaRostrosRetiroV3Worker> logger)
    : BackgroundService
{
    internal const string ConsultaLimpieza = """
        SET NOCOUNT ON;

        IF DB_NAME() <> N'SynchroDB_Pruebas'
            THROW 59010, 'Base de datos no permitida.', 1;

        DELETE TOP (200) r
        FROM dbo.RostrosRetiroPersonalV3 AS r
        INNER JOIN dbo.UsosCasillero AS u
            ON u.Id = r.UsoId
        WHERE u.FechaRetiro IS NOT NULL
           OR u.Estado IN (
               N'Finalizado', N'Finalizada',
               N'Cancelado', N'Cancelada',
               N'Vencido', N'Vencida',
               N'Anulado', N'Anulada'
           )
           OR (
               u.Estado = N'Reservado'
               AND u.FechaIngreso IS NULL
               AND u.FechaVencimiento IS NOT NULL
               AND u.FechaVencimiento <= SYSUTCDATETIME()
           );

        SELECT @@ROWCOUNT;
        """;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        string? cadena = configuracion
            .GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
            return;

        var destino = new SqlConnectionStringBuilder(cadena);

        if (!string.Equals(
                destino.InitialCatalog,
                "SynchroDB_Pruebas",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                destino.DataSource,
                @"(localdb)\SYNCHRO_Aislado",
                StringComparison.OrdinalIgnoreCase))
            return;

        using var temporizador = new PeriodicTimer(
            TimeSpan.FromMinutes(1));

        try
        {
            while (await temporizador.WaitForNextTickAsync(
                stoppingToken))
            {
                try
                {
                    await using var db = new SqlConnection(cadena);
                    await db.OpenAsync(stoppingToken);

                    await using var cmd = new SqlCommand(
                        ConsultaLimpieza, db)
                    {
                        CommandTimeout = 15
                    };

                    int eliminadas = Convert.ToInt32(
                        await cmd.ExecuteScalarAsync(stoppingToken));

                    if (eliminadas > 0)
                    {
                        logger.LogInformation(
                            "Limpieza V3: {Cantidad} referencias vencidas eliminadas.",
                            eliminadas);
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        "No fue posible limpiar referencias V3 ({Tipo}).",
                        ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Finalizacion normal.
        }
    }
}