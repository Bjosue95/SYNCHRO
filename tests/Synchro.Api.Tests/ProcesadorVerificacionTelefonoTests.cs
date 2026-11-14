using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

internal sealed class ClienteVerificacionFalso : IWhatsAppCloudService
{
    public int Envios { get; private set; }
    public IReadOnlyList<string>? Variables { get; private set; }
    public WhatsAppResultado Resultado { get; set; } = new(true,"wamid.falso."+Guid.NewGuid(),"aceptado");
    public bool Fallar { get; set; }
    public Task<WhatsAppResultado> EnviarPlantillaAsync(string telefono, string plantilla,
        IReadOnlyList<string> variables, CancellationToken token=default)
    {
        Envios++; Variables=variables;
        if (Fallar) throw new HttpRequestException("Error ficticio sensible que no debe registrarse");
        return Task.FromResult(Resultado);
    }
}

public sealed class ProcesadorVerificacionTelefonoTests
{
    private sealed class Almacen(EnvioVerificacionTelefono envio, bool autorizado) : IOutboxVerificacionTelefono
    {
        private bool tomado;
        public CierreEnvioWhatsApp? Cierre { get; private set; }
        public TaskCompletionSource<CierreEnvioWhatsApp> Terminado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<EnvioVerificacionTelefono?> TomarAsync(CancellationToken token)
        { var siguiente=tomado?null:envio; tomado=true; return Task.FromResult(siguiente); }
        public Task<bool> AutorizarAsync(EnvioVerificacionTelefono e,CancellationToken token)=>Task.FromResult(autorizado);
        public Task CerrarAsync(EnvioVerificacionTelefono e,CierreEnvioWhatsApp cierre,CancellationToken token)
        { Cierre=cierre; Terminado.TrySetResult(cierre); return Task.CompletedTask; }
    }

    [Fact]
    public async Task TrabajadorAutomaticoUsaElConsumidorSeparadoYSeDetiene()
    {
        var protector=new ProtectorVerificacionTelefono(new EphemeralDataProtectionProvider());
        var id=Guid.NewGuid();
        var envio=new EnvioVerificacionTelefono(id,Guid.NewGuid(),42,
            protector.Proteger(id,42,new("+50499990001","inicio",["123456","42"])),
            1,DateTimeOffset.UtcNow.AddMinutes(5));
        var almacen=new Almacen(envio,true);
        var cliente=new ClienteVerificacionFalso();
        var servicios=new ServiceCollection();
        servicios.AddSingleton<IOutboxVerificacionTelefono>(almacen);
        servicios.AddSingleton(protector);
        servicios.AddSingleton<IWhatsAppCloudService>(cliente);
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddScoped<ProcesadorVerificacionTelefono>();
        await using var proveedor=servicios.BuildServiceProvider();
        using var trabajador=new VerificacionTelefonoWorker(proveedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<VerificacionTelefonoWorker>.Instance);
        await trabajador.StartAsync(default);
        try
        {
            var cierre=await almacen.Terminado.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(EstadoEnvioWhatsApp.Aceptado,cierre.Estado);
            Assert.Equal(1,cliente.Envios);
        }
        finally { await trabajador.StopAsync(default); }
    }

    [Theory]
    [InlineData("aceptado", "Aceptado",1)]
    [InlineData("revocado", "Cancelado",0)]
    [InlineData("vencido", "Caducado",0)]
    [InlineData("alterado", "Fallido",0)]
    [InlineData("incierto", "Indeterminado",1)]
    [InlineData("limite", "Reintento",1)]
    [InlineData("rechazado", "Fallido",1)]
    public async Task ControlaEnvioYNoDuplica(string caso,string esperado,int envios)
    {
        var protector=new ProtectorVerificacionTelefono(new EphemeralDataProtectionProvider());
        var ahora=DateTimeOffset.UtcNow;
        var id=Guid.NewGuid();
        var cifrado=protector.Proteger(id,42,new("+50499990001","inicio_sesion",["123456","42"]));
        if(caso=="alterado") cifrado[^1]^=1;
        var envio=new EnvioVerificacionTelefono(id,Guid.NewGuid(),42,cifrado,1,
            caso=="vencido"?ahora.AddMinutes(-1):ahora.AddMinutes(5));
        var almacen=new Almacen(envio,caso!="revocado");
        var cliente=new ClienteVerificacionFalso { Fallar=caso=="incierto" };
        if(caso=="limite") cliente.Resultado=new(false,null,"limite",FalloWhatsApp.Reintentable);
        if(caso=="rechazado") cliente.Resultado=new(false,null,"rechazo",FalloWhatsApp.Rechazado);
        var procesador=new ProcesadorVerificacionTelefono(almacen,protector,cliente,TimeProvider.System);
        Assert.True(await procesador.ProcesarUnoAsync());
        Assert.False(await procesador.ProcesarUnoAsync());
        Assert.Equal(esperado,almacen.Cierre!.Estado.ToString());
        Assert.Equal(envios,cliente.Envios);
        Assert.All(cifrado,b=>Assert.Equal((byte)0,b));
    }
}
