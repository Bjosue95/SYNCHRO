namespace Synchro.Api.Services.Encomiendas;

internal sealed class LimpiezaBiometriaEncomiendasWorker(IServiceScopeFactory scopes,ILogger<LimpiezaBiometriaEncomiendasWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var reloj=new PeriodicTimer(TimeSpan.FromMinutes(1));
        try {
            while(await reloj.WaitForNextTickAsync(ct)) {
                try{using var scope=scopes.CreateScope();await scope.ServiceProvider.GetRequiredService<EncomiendasOperativasSql>().LimpiarBiometria(ct);}
                catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}
                catch(Exception e){logger.LogWarning("Limpieza biométrica de encomiendas pendiente ({Tipo}); sin datos biométricos en el registro.",e.GetType().Name);}
            }
        }catch(OperationCanceledException)when(ct.IsCancellationRequested){}
    }
}
