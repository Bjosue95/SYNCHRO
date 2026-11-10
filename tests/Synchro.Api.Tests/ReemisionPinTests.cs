using Synchro.Api.Services;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Synchro.Api.Tests;
public sealed class ReemisionPinTests
{
    [Theory]
    [InlineData(-1,0,(int)ResultadoReemisionPin.RequiereVerificacionReciente)]
    [InlineData(-180,0,(int)ResultadoReemisionPin.RequiereVerificacionReciente)]
    [InlineData(1,0,(int)ResultadoReemisionPin.RequiereVerificacionReciente)]
    [InlineData(0,3,(int)ResultadoReemisionPin.LimiteAlcanzado)]
    public void ExigeVerificacionPosteriorAlPinRecienteYLimitaReemisiones(int segundos,int cantidad,int esperado)
    {
        var ahora=DateTimeOffset.UtcNow;var creado=ahora.AddMinutes(-3);
        // El caso -1 representa una sesion anterior al PIN.
        var verificado=segundos==-1?creado.AddSeconds(-1):ahora.AddSeconds(segundos);
        Assert.Equal((ResultadoReemisionPin)esperado,PoliticaReemisionPin.Evaluar(ahora,creado,verificado,cantidad));
    }
    [Fact]
    public void SesionNuevaPermiteRecuperarPinBloqueadoSinAbrirNiReusarSesionAnterior()
    {
        var ahora=DateTimeOffset.UtcNow;
        Assert.Null(PoliticaReemisionPin.Evaluar(ahora,ahora.AddMinutes(-3),ahora.AddSeconds(-20),0));
        Assert.Equal(ResultadoReemisionPin.LimiteAlcanzado,PoliticaReemisionPin.Evaluar(ahora,ahora.AddSeconds(-40),ahora.AddSeconds(-20),0));
        Assert.DoesNotContain("+50499990002",new SolicitudReemisionPin(1,2,3,Guid.NewGuid(),"+50499990002",ahora).ToString());
    }
    [Fact]
    public void PinRotadoTieneOtroValorYSalt()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {["RetiroPin:ClaveHmac"]=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))}).Build();
        var pines=new PreparadorPinEncomienda(config,TimeProvider.System);
        using var anterior=pines.Crear(1,42);using var nuevo=pines.CrearDistinto(1,42,anterior.Hash,anterior.Salt);
        Assert.NotEqual(anterior.Pin,nuevo.Pin);Assert.NotEqual(anterior.Salt,nuevo.Salt);Assert.DoesNotContain(nuevo.Pin,nuevo.ToString());
    }
    private sealed class Reemision : IReemisionPinEncomienda
    {
        public SolicitudReemisionPin? Ultima {get;private set;}
        public Task<ResultadoReemisionPin> ReemitirAsync(SolicitudReemisionPin s,CancellationToken t)
        {Ultima=s;return Task.FromResult(ResultadoReemisionPin.Encolado);}
    }
    [Fact]
    public async Task HttpReemisionExigeAmbasSesionesYVerificacionFirmadaSinAceptarIdentidadesDelJson()
    {
        var servicio=new Reemision();using var fabrica=new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>
        {
            s.RemoveAll<IReemisionPinEncomienda>();s.AddSingleton<IReemisionPinEncomienda>(servicio);
            s.PostConfigure<CookieAuthenticationOptions>("Destinatarios",o=>o.Events.OnValidatePrincipal=_=>Task.CompletedTask);
        }));
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        const string ruta="/api/terminales/encomiendas/usos/17/pin/reemitir";
        using var sinTerminal=await cliente.PostAsJsonAsync(ruta,new{});Assert.Equal(HttpStatusCode.Unauthorized,sinTerminal.StatusCode);
        cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
        await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/terminales/encomiendas/csrf-token");
        cliente.DefaultRequestHeaders.Add("Idempotency-Key",Guid.NewGuid().ToString());
        using var sinDestinatario=await cliente.PostAsJsonAsync(ruta,new{});Assert.Equal(HttpStatusCode.Unauthorized,sinDestinatario.StatusCode);Assert.Null(servicio.Ultima);
        var opciones=fabrica.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Destinatarios");
        var principal=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,"42"),new("tipo_usuario","Destinatario"),
            new("telefono_verificado","+50499990002"),new("sesion_verificada_utc",DateTimeOffset.UtcNow.ToString("O",System.Globalization.CultureInfo.InvariantCulture))],"Destinatarios"));
        string ticket=opciones.TicketDataFormat.Protect(new AuthenticationTicket(principal,new(),"Destinatarios"));
        cliente.DefaultRequestHeaders.Add("Cookie",opciones.Cookie.Name+"="+ticket);
        using var respuesta=await cliente.PostAsJsonAsync(ruta,new{destinatarioId=999,terminalId=888,telefono="+50400000000",verificadoUtc=DateTimeOffset.MaxValue});
        Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);Assert.NotNull(servicio.Ultima);Assert.Equal(42,servicio.Ultima.DestinatarioId);
        Assert.Equal(99999,servicio.Ultima.TerminalId);Assert.Equal("+50499990002",servicio.Ultima.TelefonoVerificado);
        string json=await respuesta.Content.ReadAsStringAsync();Assert.Contains("\"retiroAutorizado\":false",json);Assert.DoesNotContain("99990002",json);
    }

}
