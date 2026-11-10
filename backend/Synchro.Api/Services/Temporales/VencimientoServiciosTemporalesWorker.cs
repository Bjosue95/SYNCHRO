namespace Synchro.Api.Services.Temporales;

internal sealed class VencimientoServiciosTemporalesWorker(IServiceScopeFactory scopes,ILogger<VencimientoServiciosTemporalesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope=scopes.CreateAsyncScope();
                // El mismo motor que atiende consultas aplica vencimientos bajo la
                // transaccion del repositorio. Nunca libera una operacion ocupada.
                await scope.ServiceProvider.GetRequiredService<ServicioTemporal>().DisponibilidadAsync(stoppingToken);
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
            catch(Exception ex){logger.LogWarning("No se pudo revisar vencimientos de servicios temporales ({Tipo}).",ex.GetType().Name);}
            try{await Task.Delay(TimeSpan.FromSeconds(30),stoppingToken);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
        }
    }
}
