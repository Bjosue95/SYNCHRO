namespace Synchro.Api.Services.WhatsApp;

// Se registran solo mediante indicadores explicitos, deshabilitados por defecto.
internal sealed class SolicitudesInicioWhatsAppWorker(IServiceScopeFactory scopes,
    ILogger<SolicitudesInicioWhatsAppWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var ejecutor = scope.ServiceProvider.GetRequiredService<EjecutorSolicitudesInicioWhatsApp>();
                for (int i = 0; i < 20; i++)
                    if (await ejecutor.EjecutarUnaAsync(stoppingToken) == ResultadoEjecucionInicioWhatsApp.SinSolicitudes) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("No fue posible procesar solicitudes de inicio WhatsApp."); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}

internal sealed class VerificacionTelefonoWorker(IServiceScopeFactory scopes,
    ILogger<VerificacionTelefonoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var procesador = scope.ServiceProvider.GetRequiredService<ProcesadorVerificacionTelefono>();
                for (int i = 0; i < 20; i++)
                    if (!await procesador.ProcesarUnoAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logger.LogWarning("No fue posible procesar la outbox telefonica."); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
