namespace Synchro.Api.Services.WhatsApp;

internal sealed class OutboxWhatsAppWorker(IServiceScopeFactory scopes, ILogger<OutboxWhatsAppWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope=scopes.CreateAsyncScope();
                var procesador=scope.ServiceProvider.GetRequiredService<ProcesadorOutboxWhatsApp>();
                for (int i=0;i<20;i++)
                    if (!await procesador.ProcesarUnoAsync(stoppingToken))break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // Ni el contenido protegido, ni telefonos, PIN o excepciones
                // del proveedor deben terminar en los logs del trabajador.
                logger.LogWarning("No fue posible procesar la outbox WhatsApp ({Tipo}).",ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(10),stoppingToken); }
            catch (OperationCanceledException) when(stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
