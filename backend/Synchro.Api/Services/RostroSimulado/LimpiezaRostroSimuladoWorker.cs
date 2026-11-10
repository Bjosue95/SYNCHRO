namespace Synchro.Api.Services.RostroSimulado;

internal sealed class LimpiezaRostroSimuladoWorker(IReferenciasRostroSimulado referencias,ILogger<LimpiezaRostroSimuladoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try{await referencias.EliminarVencidasAsync(stoppingToken);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
            catch(Exception e){logger.LogWarning("No fue posible limpiar referencias simuladas ({Tipo}).",e.GetType().Name);}
            try{await Task.Delay(TimeSpan.FromMinutes(5),stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
        }
    }
}
