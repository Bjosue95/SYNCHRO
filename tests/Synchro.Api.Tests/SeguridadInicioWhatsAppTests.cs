using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

public sealed class SeguridadInicioWhatsAppTests
{
    [Theory]
    [InlineData("WhatsApp:SolicitudesInicioHabilitadas")]
    [InlineData("WhatsApp:SolicitudesInicioWorker:Habilitado")]
    [InlineData("WhatsApp:VerificacionOutbox:Habilitado")]
    public void InicioAutomaticoRechazaOtraInstanciaAntesDeArrancar(string indicador)
    {
        using var fabrica=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>
        {
            b.UseEnvironment("Development");
            b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:SynchroDB"]="Server=localhost;Database=SynchroDB_Pruebas;Integrated Security=true",
                ["ProteccionDatos:DirectorioClaves"]=Path.GetTempPath(),
                [indicador]="true"
            }));
        });
        Assert.Throws<InvalidOperationException>(()=>fabrica.CreateClient());
    }

    [Fact]
    public void DependenciasResuelvenSinActivarTrabajadoresExternos()
    {
        using var fabrica=new WebApplicationFactory<Program>();
        using var scope=fabrica.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<EjecutorSolicitudesInicioWhatsApp>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProcesadorVerificacionTelefono>());
        var trabajadores=fabrica.Services.GetServices<IHostedService>();
        Assert.DoesNotContain(trabajadores,x=>x is VerificacionTelefonoWorker or SolicitudesInicioWhatsAppWorker or OutboxWhatsAppWorker);
    }

    [Fact]
    public async Task LoginLimitaIntentosAntesDeConsultarSql()
    {
        using var fabrica=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>
        {
            b.UseEnvironment("Development");
            b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:SynchroDB"]=@"Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=true",
                ["WhatsApp:Outbox:Habilitado"]="false",
                ["WhatsApp:VerificacionOutbox:Habilitado"]="false",
                ["WhatsApp:SolicitudesInicioWorker:Habilitado"]="false"
            }));
        });
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/destinatarios/csrf-token");
        for(int i=0;i<5;i++)
        {
            using var invalido=await cliente.PostAsJsonAsync("/api/destinatarios/login",new{verificacionId=0,codigo="invalido"});
            Assert.Equal(HttpStatusCode.Unauthorized,invalido.StatusCode);
        }
        using var limitado=await cliente.PostAsJsonAsync("/api/destinatarios/login",new{verificacionId=0,codigo="invalido"});
        Assert.Equal(HttpStatusCode.TooManyRequests,limitado.StatusCode);
    }

    [Fact]
    public void ClavesPersistidasProtegidasPermitenReabrirDesdeOtroProveedor()
    {
        if(!OperatingSystem.IsWindows()) return;
        var ruta=Path.Combine(Path.GetTempPath(),"Synchro-Claves-Prueba-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ruta);
        try
        {
            var directorio=new DirectoryInfo(ruta);
            var primero=DataProtectionProvider.Create(directorio,b=>
            {
                b.SetApplicationName("Synchro-Prueba");
                if(OperatingSystem.IsWindows()) b.ProtectKeysWithDpapi();
            });
            var id=Guid.NewGuid();
            var cifrado=new ProtectorVerificacionTelefono(primero).Proteger(id,7,new("+50499990001","inicio",["123456","7"]));
            var segundo=DataProtectionProvider.Create(directorio,b=>
            {
                b.SetApplicationName("Synchro-Prueba");
                if(OperatingSystem.IsWindows()) b.ProtectKeysWithDpapi();
            });
            Assert.Equal("7",new ProtectorVerificacionTelefono(segundo).Abrir(id,7,cifrado).Variables[1]);
            var xml=File.ReadAllText(Assert.Single(Directory.GetFiles(ruta,"key-*.xml")));
            Assert.Contains("encryptedSecret",xml);
            Assert.DoesNotContain("<masterKey>",xml);
        }
        finally
        {
            // Solo el directorio temporal aleatorio creado por esta prueba.
            Directory.Delete(ruta,true);
        }
    }
}
