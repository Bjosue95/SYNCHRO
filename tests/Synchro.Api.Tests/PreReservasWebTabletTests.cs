using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Synchro.Api.Services;

namespace Synchro.Api.Tests;

public sealed class PreReservasWebTabletTests
{
    private sealed class PersistenciaSimulada : IPreReservasEncomienda
    {
        public int Guardadas {get;private set;}
        public int? Terminal {get;private set;}
        public (string Remitente,string Destinatario,string Tamano) Ultima {get;private set;}
        public Task<PreReservaEncomiendaCreada> CrearAsync(string? r,string? d,string? t,CancellationToken token,int? terminalId=null)
        {
            Ultima=PreReservaEncomiendaService.Validar(r,d,t);Guardadas++;Terminal=terminalId;
            return Task.FromResult(new PreReservaEncomiendaCreada(Guardadas,new string('A',64),DateTimeOffset.UtcNow.AddHours(24)));
        }
    }
    [Theory]
    [InlineData("/api/web/encomiendas/pre-reservas")]
    [InlineData("/api/terminales/encomiendas/visitas")]
    public async Task WebYTabletCompartenCreacionSinAutorizarDeposito(string ruta)
    {
        var datos=new PersistenciaSimulada();
        using var fabrica=new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>
        {s.RemoveAll<IPreReservasEncomienda>();s.AddSingleton<IPreReservasEncomienda>(datos);}));
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),HandleCookies=false});
        cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
        using var csrf=await cliente.GetAsync("/api/web/encomiendas/csrf-token");csrf.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN",json.RootElement.GetProperty("csrfToken").GetString());
        cliente.DefaultRequestHeaders.Add("Cookie",string.Join("; ",csrf.Headers.GetValues("Set-Cookie").Select(c=>c.Split(';')[0])));
        using var respuesta=await cliente.PostAsJsonAsync(ruta,new{telefonoRemitente="50499990001",telefonoDestinatario="+50499990002",tamanoSolicitado="Mediano"});
        Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);Assert.Equal(1,datos.Guardadas);
        Assert.Equal(ruta.Contains("/visitas")?99999:(int?)null,datos.Terminal);
        Assert.Equal("+50499990001",datos.Ultima.Remitente);Assert.Equal("+50499990002",datos.Ultima.Destinatario);
        using var contenido=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.False(contenido.RootElement.GetProperty("depositoHabilitado").GetBoolean());
        Assert.Equal("PendienteConsentimiento",contenido.RootElement.GetProperty("estado").GetString());
        Assert.Equal(64,contenido.RootElement.GetProperty("tokenGestion").GetString()!.Length);
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("99990002",await respuesta.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task VisitaSinTerminalNoSeRegistra()
    {
        using var fabrica=new FabricaTerminalHttpPruebas();
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        using var respuesta=await cliente.PostAsJsonAsync("/api/terminales/encomiendas/visitas",new{telefonoRemitente="50499990001",telefonoDestinatario="50499990002",tamanoSolicitado="Mediano"});
        Assert.Equal(HttpStatusCode.Unauthorized,respuesta.StatusCode);
    }
    [Theory]
    [InlineData(null,"+50499990002","Mediano")]
    [InlineData("+50499990001",null,"Mediano")]
    [InlineData("+50499990001","+50499990002",null)]
    [InlineData("+50499990001","+50499990002","Otro")]
    [InlineData("+50499990001","+5049999\n0002","Mediano")]
    [InlineData("+50499990001","+00000000","Mediano")]
    public void ValidacionComunRechazaDatosIncompletos(string? r,string? d,string? t)
        =>Assert.Throws<ArgumentException>(()=>PreReservaEncomiendaService.Validar(r,d,t));
    [Fact]
    public void CredencialDeCreacionNoSeImprime()
    {
        var creada=new PreReservaEncomiendaCreada(1,new string('A',64),DateTimeOffset.UtcNow);
        Assert.DoesNotContain(creada.TokenGestion,creada.ToString());
    }
}
