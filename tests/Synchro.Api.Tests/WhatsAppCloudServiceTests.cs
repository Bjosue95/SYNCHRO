using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

public sealed class WhatsAppCloudServiceTests
{
    private static IConfiguration Config(string clave="",string? valor=null)
    {
        var datos=new Dictionary<string,string?>
        {
            ["WhatsApp:PhoneNumberId"]="123456789",["WhatsApp:AccessToken"]="token-exclusivo-prueba"
        };
        if (clave!="")datos[clave]=valor;
        return new ConfigurationBuilder().AddInMemoryCollection(datos).Build();
    }
    private sealed class Transporte(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> enviar) : HttpMessageHandler
    {
        public int Intentos { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        { Intentos++;return enviar(request,token); }
    }
    private static HttpResponseMessage Respuesta(string cuerpo,HttpStatusCode status=HttpStatusCode.OK)
        => new(status){Content=new StringContent(cuerpo,Encoding.UTF8,"application/json")};

    [Fact]
    public async Task EnviaSoloCloudApiYNoConfundeAceptacionConEntrega()
    {
        using var transporte=new Transporte(async (r,_)=>
        {
            Assert.Equal("https://graph.facebook.com/v26.0/123456789/messages",r.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post,r.Method);Assert.Equal("Bearer",r.Headers.Authorization!.Scheme);
            using var json=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());
            var raiz=json.RootElement;Assert.Equal("whatsapp",raiz.GetProperty("messaging_product").GetString());
            Assert.Equal("50499990001",raiz.GetProperty("to").GetString());
            Assert.Equal("pin_retiro",raiz.GetProperty("template").GetProperty("name").GetString());
            Assert.Equal("123456",raiz.GetProperty("template").GetProperty("components")[0].GetProperty("parameters")[0].GetProperty("text").GetString());
            return Respuesta("{\"messages\":[{\"id\":\"wamid.test\"}]}");
        });
        using var http=new HttpClient(transporte);
        var resultado=await new WhatsAppCloudService(http,Config()).EnviarPlantillaAsync("+50499990001","pin_retiro",["123456"]);
        Assert.True(resultado.AceptadoPorMeta);Assert.Equal("wamid.test",resultado.IdMensaje);
        Assert.Contains("pendiente",resultado.Mensaje);Assert.Equal(1,transporte.Intentos);
    }

    [Theory]
    [InlineData("50499990001","pin_retiro")]
    [InlineData("+50499990001\n","pin_retiro")]
    [InlineData("+50499990001","pin_retiro\n")]
    [InlineData("+50499990001","../pin")]
    [InlineData("+50499990001","PIN")]
    [InlineData("+00000000","pin_retiro")]
    public async Task DatosInvalidosNoRealizanPeticiones(string telefono,string plantilla)
    {
        using var transporte=new Transporte((_,_)=>throw new InvalidOperationException("No debe enviar."));
        using var http=new HttpClient(transporte);
        var resultado=await new WhatsAppCloudService(http,Config()).EnviarPlantillaAsync(telefono,plantilla,[]);
        Assert.Equal(FalloWhatsApp.DatosInvalidos,resultado.Fallo);Assert.Equal(0,transporte.Intentos);
    }

    [Theory]
    [InlineData("WhatsApp:AccessToken",null)]
    [InlineData("WhatsApp:AccessToken","token\r\nmalicioso")]
    [InlineData("WhatsApp:ApiVersion","v26.0\n")]
    [InlineData("WhatsApp:PhoneNumberId","../messages")]
    [InlineData("WhatsApp:Idioma","es\n")]
    public async Task ConfiguracionInvalidaNoRealizaPeticiones(string clave,string? valor)
    {
        using var transporte=new Transporte((_,_)=>throw new InvalidOperationException("No debe enviar."));
        using var http=new HttpClient(transporte);
        var resultado=await new WhatsAppCloudService(http,Config(clave,valor)).EnviarPlantillaAsync("+50499990001","pin_retiro",[]);
        Assert.Equal(FalloWhatsApp.Configuracion,resultado.Fallo);Assert.Equal(0,transporte.Intentos);
    }

    [Theory]
    [InlineData(400,FalloWhatsApp.Rechazado)]
    [InlineData(401,FalloWhatsApp.Rechazado)]
    [InlineData(429,FalloWhatsApp.Reintentable)]
    [InlineData(500,FalloWhatsApp.Indeterminado)]
    [InlineData(503,FalloWhatsApp.Indeterminado)]
    public async Task ClasificaRechazoSinReintentarDentroDelCliente(int codigo,FalloWhatsApp esperado)
    {
        using var transporte=new Transporte((_,_)=>
        {
            var respuesta=Respuesta("datos-privados-que-no-deben-regresarse",(HttpStatusCode)codigo);
            respuesta.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));
            return Task.FromResult(respuesta);
        });
        using var http=new HttpClient(transporte);
        var resultado=await new WhatsAppCloudService(http,Config()).EnviarPlantillaAsync("+50499990001","pin_retiro",[]);
        Assert.False(resultado.AceptadoPorMeta);Assert.Equal(esperado,resultado.Fallo);
        Assert.Equal(TimeSpan.FromMinutes(2),resultado.ReintentarDespues);
        Assert.DoesNotContain("datos-privados",resultado.Mensaje);Assert.Equal(1,transporte.Intentos);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"messages\":[null]}")]
    [InlineData("{\"messages\":[{\"id\":\"\"}]}")]
    [InlineData("{\"messages\":[{\"id\":\"wamid.test\\n\"}]}")]
    [InlineData("{\"messages\":[]}")]
    [InlineData("no-json")]
    public async Task RespuestasNoConfirmadasNoSeReenvianAutomaticamente(string cuerpo)
    {
        using var transporte=new Transporte((_,_)=>Task.FromResult(Respuesta(cuerpo)));
        using var http=new HttpClient(transporte);
        var resultado=await new WhatsAppCloudService(http,Config()).EnviarPlantillaAsync("+50499990001","pin_retiro",[]);
        Assert.Equal(FalloWhatsApp.Indeterminado,resultado.Fallo);Assert.Null(resultado.IdMensaje);
        Assert.Equal(1,transporte.Intentos);
    }

    [Fact]
    public async Task RespuestaExcesivaQuedaIndeterminada()
    {
        using var transporte=new Transporte((_,_)=>Task.FromResult(Respuesta(new string('x',65537))));
        using var http=new HttpClient(transporte);
        var resultado=await new WhatsAppCloudService(http,Config()).EnviarPlantillaAsync("+50499990001","pin_retiro",[]);
        Assert.Equal(FalloWhatsApp.Indeterminado,resultado.Fallo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FalloDeTransporteOTimeoutNoConfirmaNiReenvia(bool timeout)
    {
        using var transporte=new Transporte((_,_)=> { if (timeout)throw new TaskCanceledException();throw new HttpRequestException(); });
        using var http=new HttpClient(transporte);
        var resultado=await new WhatsAppCloudService(http,Config()).EnviarPlantillaAsync("+50499990001","pin_retiro",[]);
        Assert.Equal(FalloWhatsApp.Indeterminado,resultado.Fallo);Assert.Equal(1,transporte.Intentos);
    }

    [Fact]
    public async Task CancelacionDelLlamadorSePropaga()
    {
        using var transporte=new Transporte((_,_)=>throw new InvalidOperationException("No debe enviar."));
        using var http=new HttpClient(transporte);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new WhatsAppCloudService(http,Config())
            .EnviarPlantillaAsync("+50499990001","pin_retiro",[],new CancellationToken(true)));
        Assert.Equal(0,transporte.Intentos);
    }
}
