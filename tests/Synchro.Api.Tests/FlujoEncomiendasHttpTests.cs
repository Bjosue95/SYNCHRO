using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Synchro.Api.Services;

namespace Synchro.Api.Tests;

// Solo pruebas: ni este repositorio ni el verificador simulado se distribuyen con la API.
internal sealed class RepositorioEncomiendasSimulado : IFlujoEncomiendasTerminal
{
    private readonly object _sync=new();
    public bool Consentimiento {get;set;}=true;
    public bool Ocupado {get;private set;}
    public bool Retirado {get;private set;}
    public bool PinConsumido {get;private set;}
    public int Notificaciones {get;private set;}
    public int IntentosPin {get;private set;}
    public Dictionary<Guid,EvidenciaFacialRemitente> Remitentes {get;}=[];
    public Dictionary<Guid,EvidenciaRetiroEncomienda> Destinatarios {get;}=[];
    private static readonly byte[] TokenHash=SHA256.HashData(Convert.FromHexString(new string('A',64)));
    public Task<ContextoReservaEncomienda?> ObtenerReservaAsync(int reserva,int terminal,byte[] hash,CancellationToken token)
    {
        lock(_sync)return Task.FromResult(reserva==1&&terminal==99999&&Consentimiento&&!Ocupado&&!Retirado&&CryptographicOperations.FixedTimeEquals(hash,TokenHash)
            ?new ContextoReservaEncomienda(1,41):null);
    }
    public Task GuardarEvidenciaRemitenteAsync(EvidenciaFacialRemitente e,byte[] hash,CancellationToken token)
    {lock(_sync)Remitentes.Add(e.EvidenciaId,e);return Task.CompletedTask;}
    public Task<DepositoEncomiendaResultado> DepositarAsync(DepositoEncomiendaComando c,CancellationToken token)
    {
        lock(_sync)
        {
            if(!Consentimiento||Ocupado||Retirado||c.TokenGestionHash is null||!CryptographicOperations.FixedTimeEquals(c.TokenGestionHash,TokenHash)||
                !Remitentes.TryGetValue(c.EvidenciaId,out var e)||
                !ValidacionEvidenciaFacialRemitente.EsValidaParaContinuar(e,c.ReservaId,41,c.TerminalId,DateTimeOffset.UtcNow))
                throw new EncomiendaNoDisponibleException();
            Remitentes[e.EvidenciaId]=e with{Consumida=true};Ocupado=true;Notificaciones++;
            return Task.FromResult(new DepositoEncomiendaResultado(17,c.CodigoCasillero));
        }
    }
    public Task<bool> PuedeRetirarAsync(int uso,int persona,int terminal,CancellationToken token)
    {lock(_sync)return Task.FromResult(uso==17&&persona==42&&terminal==99999&&Ocupado&&!Retirado);}
    public Task GuardarEvidenciaRetiroAsync(EvidenciaRetiroEncomienda e,CancellationToken token)
    {lock(_sync)Destinatarios.Add(e.Id,e);return Task.CompletedTask;}
    public Task<ResultadoRetiroEncomienda> RetirarAsync(RetirarEncomiendaComando c,CancellationToken token)
    {
        lock(_sync)
        {
            if(c.UsoId!=17||c.DestinatarioId!=42||c.TerminalId!=99999||!Ocupado||Retirado||PinConsumido)return Task.FromResult(ResultadoRetiroEncomienda.NoDisponible);
            if(IntentosPin>=5)return Task.FromResult(ResultadoRetiroEncomienda.PinBloqueado);
            if(!Destinatarios.TryGetValue(c.EvidenciaId,out var e)||e.TerminalId!=c.TerminalId||e.DestinatarioId!=c.DestinatarioId||
                e.UsoId!=c.UsoId||e.VenceUtc<=DateTimeOffset.UtcNow)return Task.FromResult(ResultadoRetiroEncomienda.EvidenciaNoValida);
            if(c.Pin!="654321")return Task.FromResult(++IntentosPin>=5?ResultadoRetiroEncomienda.PinBloqueado:ResultadoRetiroEncomienda.PinIncorrecto);
            Destinatarios.Remove(e.Id);PinConsumido=true;Ocupado=false;Retirado=true;
            return Task.FromResult(ResultadoRetiroEncomienda.Retirado);
        }
    }
}
internal sealed class VerificadorFacialSoloPruebas(bool aprobar=true) : IVerificadorFacial
{
    public async Task<ResultadoFacial> VerificarAsync(int persona,Stream captura,CancellationToken token=default)
    {
        using var lector=new StreamReader(captura,leaveOpen:true);
        bool coincide=aprobar&&await lector.ReadToEndAsync(token)=="CAPTURA-SIMULADA:"+persona;
        return new(coincide,coincide);
    }
}

public sealed class FlujoEncomiendasHttpTests
{
    private sealed class Escenario : IDisposable
    {
        public RepositorioEncomiendasSimulado Repo {get;}=new();
        public WebApplicationFactory<Program> Servidor {get;}
        public HttpClient Cliente {get;}
        private string _csrfCookie="";
        public Escenario(bool verificador=true,bool aprobar=true,string esquema="https")
        {
            Servidor=new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>
            {
                s.RemoveAll<IFlujoEncomiendasTerminal>();s.AddSingleton<IFlujoEncomiendasTerminal>(Repo);
                if(verificador){s.RemoveAll<IVerificadorFacial>();s.AddSingleton<IVerificadorFacial>(new VerificadorFacialSoloPruebas(aprobar));}
                s.PostConfigure<CookieAuthenticationOptions>("Destinatarios",o=>o.Events.OnValidatePrincipal=_=>Task.CompletedTask);
            }));
            Cliente=Servidor.CreateClient(new(){BaseAddress=new Uri(esquema+"://localhost"),HandleCookies=false,AllowAutoRedirect=false});
            Cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
            Cliente.DefaultRequestHeaders.Add("X-SYNCHRO-RESERVA",new string('A',64));
        }
        public async Task CsrfAsync()
        {
            using var r=await Cliente.GetAsync("/api/web/encomiendas/csrf-token");r.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            Cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN",json.RootElement.GetProperty("csrfToken").GetString());
            _csrfCookie=string.Join("; ",r.Headers.GetValues("Set-Cookie").Select(c=>c.Split(';')[0]));
            Cliente.DefaultRequestHeaders.Add("Cookie",_csrfCookie);
        }
        public void Destinatario(int persona)
        {
            var opciones=Servidor.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Destinatarios");
            var identidad=new ClaimsIdentity([new(ClaimTypes.NameIdentifier,persona.ToString()),new("tipo_usuario","Destinatario")],"Destinatarios");
            var cookie=opciones.TicketDataFormat.Protect(new AuthenticationTicket(new ClaimsPrincipal(identidad),new AuthenticationProperties(),"Destinatarios"));
            Cliente.DefaultRequestHeaders.Remove("Cookie");
            Cliente.DefaultRequestHeaders.Add("Cookie",_csrfCookie+"; "+opciones.Cookie.Name+"="+cookie);
        }
        public async Task<Guid> CapturaAsync(string ruta,int persona)
        {
            using var r=await Cliente.PostAsync(ruta,Captura(persona));
            Assert.Equal(HttpStatusCode.OK,r.StatusCode);
            using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync());return json.RootElement.GetProperty("evidenciaId").GetGuid();
        }
        public async Task<Guid> DepositarAsync()
        {
            var evidencia=await CapturaAsync("/api/terminales/encomiendas/reservas/1/evidencias-remitente",41);
            using var r=await Cliente.PostAsJsonAsync("/api/terminales/encomiendas/reservas/1/depositar",new{evidenciaId=evidencia,codigoCasillero="S01"});
            Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.DoesNotContain("654321",await r.Content.ReadAsStringAsync());return evidencia;
        }
        public void Dispose(){Cliente.Dispose();Servidor.Dispose();}
    }
    private static HttpContent Captura(int persona)
    {
        var contenido=new ByteArrayContent(Encoding.UTF8.GetBytes("CAPTURA-SIMULADA:"+persona));
        contenido.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");return contenido;
    }
    [Theory]
    [InlineData("reservas/1/evidencias-remitente")]
    [InlineData("reservas/1/depositar")]
    [InlineData("usos/17/evidencias-destinatario")]
    [InlineData("usos/17/retirar")]
    public async Task SinSesionTerminalNoAccede(string ruta)
    {
        using var e=new Escenario();e.Cliente.DefaultRequestHeaders.Remove("X-Synchro-Test-Terminal");
        using var r=await e.Cliente.PostAsJsonAsync("/api/terminales/encomiendas/"+ruta,new{evidenciaId=Guid.NewGuid(),codigoCasillero="S01",pin="654321"});
        Assert.Equal(HttpStatusCode.Unauthorized,r.StatusCode);Assert.False(e.Repo.Ocupado);
    }
    [Theory]
    [InlineData("reservas/1/evidencias-remitente")]
    [InlineData("reservas/1/depositar")]
    [InlineData("usos/17/evidencias-destinatario")]
    [InlineData("usos/17/retirar")]
    public async Task SinCsrfNoModifica(string ruta)
    {
        using var e=new Escenario();
        using var r=await e.Cliente.PostAsJsonAsync("/api/terminales/encomiendas/"+ruta,new{evidenciaId=Guid.NewGuid(),codigoCasillero="S01",pin="654321"});
        Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);Assert.False(e.Repo.Ocupado);
    }
    [Fact]
    public async Task HttpNoPuedeCapturarNiDepositar()
    {
        using var e=new Escenario(esquema:"http");
        using var r=await e.Cliente.PostAsync("/api/terminales/encomiendas/reservas/1/evidencias-remitente",Captura(41));
        Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);
    }
    [Fact]
    public async Task ProveedorNoDisponibleMantieneBloqueo()
    {
        using var e=new Escenario(verificador:false);await e.CsrfAsync();
        using var r=await e.Cliente.PostAsync("/api/terminales/encomiendas/reservas/1/evidencias-remitente",Captura(41));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,r.StatusCode);Assert.Empty(e.Repo.Remitentes);
    }
    [Fact]
    public async Task RecorridoHttpSimuladoDepositaNotificaYRetiraUnaSolaVez()
    {
        using var e=new Escenario();await e.CsrfAsync();var deposito=await e.DepositarAsync();
        Assert.True(e.Repo.Remitentes[deposito].Consumida);Assert.Equal(1,e.Repo.Notificaciones);
        e.Destinatario(42);var facial=await e.CapturaAsync("/api/terminales/encomiendas/usos/17/evidencias-destinatario",42);
        using var r=await e.Cliente.PostAsJsonAsync("/api/terminales/encomiendas/usos/17/retirar",new{evidenciaId=facial,pin="654321"});
        Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.True(e.Repo.PinConsumido);Assert.True(e.Repo.Retirado);Assert.False(e.Repo.Ocupado);
        using var repetida=await e.Cliente.PostAsJsonAsync("/api/terminales/encomiendas/usos/17/retirar",new{evidenciaId=facial,pin="654321"});
        Assert.Equal(HttpStatusCode.NotFound,repetida.StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservaSinConsentimientoOCredencialNoEmiteEvidencia(bool credencialAjena)
    {
        using var e=new Escenario();await e.CsrfAsync();
        if(credencialAjena){e.Cliente.DefaultRequestHeaders.Remove("X-SYNCHRO-RESERVA");e.Cliente.DefaultRequestHeaders.Add("X-SYNCHRO-RESERVA",new string('B',64));}
        else e.Repo.Consentimiento=false;
        using var r=await e.Cliente.PostAsync("/api/terminales/encomiendas/reservas/1/evidencias-remitente",Captura(41));
        Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);Assert.Empty(e.Repo.Remitentes);
    }
    [Fact]
    public async Task AprobacionJsonDelNavegadorNoEsCaptura()
    {
        using var e=new Escenario();await e.CsrfAsync();
        using var r=await e.Cliente.PostAsJsonAsync("/api/terminales/encomiendas/reservas/1/evidencias-remitente",new{aprobado=true,pruebaDeVida=true});
        Assert.Equal(HttpStatusCode.UnsupportedMediaType,r.StatusCode);Assert.Empty(e.Repo.Remitentes);
    }
    [Fact]
    public async Task CapturaDemasiadoGrandeSeRechaza()
    {
        using var e=new Escenario();await e.CsrfAsync();using var captura=new ByteArrayContent(new byte[256*1024+1]);
        captura.Headers.ContentType=new("application/octet-stream");
        using var r=await e.Cliente.PostAsync("/api/terminales/encomiendas/reservas/1/evidencias-remitente",captura);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,r.StatusCode);Assert.Empty(e.Repo.Remitentes);
    }
    [Fact]
    public async Task RostroRechazadoNoGeneraEvidencia()
    {
        using var e=new Escenario(aprobar:false);await e.CsrfAsync();
        using var r=await e.Cliente.PostAsync("/api/terminales/encomiendas/reservas/1/evidencias-remitente",Captura(41));
        Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode);Assert.Empty(e.Repo.Remitentes);
    }
    [Fact]
    public async Task RetiroExigeSesionDelDestinatarioYProtegeEncomiendaAjena()
    {
        using var e=new Escenario();await e.CsrfAsync();await e.DepositarAsync();
        using var sinSesion=await e.Cliente.PostAsync("/api/terminales/encomiendas/usos/17/evidencias-destinatario",Captura(42));
        Assert.Equal(HttpStatusCode.Unauthorized,sinSesion.StatusCode);
        e.Destinatario(43);
        using var ajena=await e.Cliente.PostAsync("/api/terminales/encomiendas/usos/17/evidencias-destinatario",Captura(43));
        Assert.Equal(HttpStatusCode.NotFound,ajena.StatusCode);Assert.True(e.Repo.Ocupado);
    }
    [Fact]
    public async Task CincoPinsIncorrectosBloqueanSinConsumirRostroNiLiberarCasillero()
    {
        using var e=new Escenario();await e.CsrfAsync();await e.DepositarAsync();e.Destinatario(42);
        var rostro=await e.CapturaAsync("/api/terminales/encomiendas/usos/17/evidencias-destinatario",42);
        for(int i=0;i<5;i++)
        {
            using var r=await e.Cliente.PostAsJsonAsync("/api/terminales/encomiendas/usos/17/retirar",new{evidenciaId=rostro,pin="000000"});
            Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);
        }
        using var correcto=await e.Cliente.PostAsJsonAsync("/api/terminales/encomiendas/usos/17/retirar",new{evidenciaId=rostro,pin="654321"});
        Assert.Equal(HttpStatusCode.Conflict,correcto.StatusCode);Assert.False(e.Repo.PinConsumido);Assert.True(e.Repo.Ocupado);Assert.True(e.Repo.Destinatarios.ContainsKey(rostro));
    }
    [Fact]
    public async Task EndpointsDePrototipoNoSeExponenEnProduccion()
    {
        using var servidor=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>
        {
            b.UseEnvironment("Production");
            b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>
                {["ConnectionStrings:SynchroDB"]=""}));
        });
        using var cliente=servidor.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        using var r=await cliente.PostAsJsonAsync("/api/terminales/encomiendas/reservas/1/depositar",new{evidenciaId=Guid.NewGuid(),codigoCasillero="S01"});
        Assert.Equal(HttpStatusCode.NotFound,r.StatusCode);
    }

    [Fact]
    public async Task DepositosConcurrentesNoDuplicanLaOperacionSimulada()
    {
        using var e=new Escenario();await e.CsrfAsync();var facial=await e.CapturaAsync("/api/terminales/encomiendas/reservas/1/evidencias-remitente",41);
        var resultados=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>e.Cliente.PostAsJsonAsync("/api/terminales/encomiendas/reservas/1/depositar",new{evidenciaId=facial,codigoCasillero="S01"})));
        try{Assert.Single(resultados,r=>r.StatusCode==HttpStatusCode.OK);Assert.Equal(1,e.Repo.Notificaciones);}
        finally{foreach(var r in resultados)r.Dispose();}
    }
}
