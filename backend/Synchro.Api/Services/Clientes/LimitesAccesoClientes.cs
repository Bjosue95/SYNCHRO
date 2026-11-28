using System.Threading.RateLimiting;

namespace Synchro.Api.Services.Clientes;

// Límite global para las operaciones costosas; la cola es finita.
// La cuota por nombre no depende de IP ni de cabeceras del visitante.
internal sealed class LimitesAccesoClientes : IDisposable
{
    private readonly ConcurrencyLimiter concurrencia = new(new ConcurrencyLimiterOptions
    {
        PermitLimit = 8, QueueLimit = 240, QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });
    private readonly PartitionedRateLimiter<string> intentos =
        PartitionedRateLimiter.Create<string, string>(nombre =>
            RateLimitPartition.GetFixedWindowLimiter(nombre, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5, Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0, AutoReplenishment = true
            }));

    public static bool ModoFeria(WebApplication app) =>
        app.Environment.IsDevelopment() &&
        app.Configuration.GetValue<bool>("ClientesWeb:ModoFeria");

    public ValueTask<RateLimitLease> EntrarAsync(CancellationToken ct) =>
        concurrencia.AcquireAsync(1, ct);

    public RateLimitLease IntentoLogin(string? nombre)
    {
        string clave;
        try { clave = Synchro.Api.Services.ValidacionCredencialesCuenta.Nombre(nombre).ToUpperInvariant(); }
        catch (ArgumentException) { clave = "!invalido"; }
        return intentos.AttemptAcquire(clave);
    }

    public void Dispose() { concurrencia.Dispose(); intentos.Dispose(); }
}
