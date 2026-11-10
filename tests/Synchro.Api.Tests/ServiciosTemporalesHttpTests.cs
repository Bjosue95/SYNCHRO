using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synchro.Api.Services.Temporales;

namespace Synchro.Api.Tests;

public sealed class AdministradorTemporalPruebas(IOptionsMonitor<AuthenticationSchemeOptions> opciones,ILoggerFactory logger,UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opciones,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(!Request.Headers.ContainsKey("X-Test-Admin"))return Task.FromResult(AuthenticateResult.NoResult());
        string rol=Request.Headers["X-Test-Admin"].ToString() switch{"si"=>"Administrador","operador"=>"Operador","auditor"=>"Auditor",_=>"Lector"};
        var identidad=new ClaimsIdentity([new(ClaimTypes.NameIdentifier,"7"),new(ClaimTypes.Role,rol)],"AdminTemporalTest");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad),"AdminTemporalTest")));
    }
}

public sealed class ServiciosTemporalesHttpTests
{
    private sealed class Escenario : IDisposable
    {
        public RepositorioTemporalSimulado Repo {get;}=new();
        public RelojServicioTemporal Reloj {get;}=new();
        public WebApplicationFactory<Program> Servidor {get;}
        public Escenario(ClaseServicioTemporal clase=ClaseServicioTemporal.CargaCelular)
        {
            Repo.ConfigurarAsync(ServiciosTemporalesTests.Compartimento(clase),7,default).GetAwaiter().GetResult();
            Servidor=new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>
            {
                b.ConfigureAppConfiguration((_,c)=>c.AddConfiguration(ServiciosTemporalesTests.Config()).AddInMemoryCollection(new Dictionary<string,string?>
                {["ServiciosTemporales:Habilitado"]="true",["ServiciosTemporales:ProcesarVencimientos"]="false"}));
                b.ConfigureTestServices(s=>
                {
                    s.RemoveAll<IRepositorioServicioTemporal>();s.AddSingleton<IRepositorioServicioTemporal>(Repo);
                    s.RemoveAll<TimeProvider>();s.AddSingleton<TimeProvider>(Reloj);
                    s.AddAuthentication(o=>{o.DefaultScheme="SelectorTemporalTest";o.DefaultAuthenticateScheme="SelectorTemporalTest";o.DefaultChallengeScheme="SelectorTemporalTest";})
                        .AddPolicyScheme("SelectorTemporalTest",null,o=>o.ForwardDefaultSelector=h=>h.Request.Headers.ContainsKey("X-Test-Admin")?"AdminTemporalTest":AutenticacionTerminalHttpPruebas.Esquema)
                        .AddScheme<AuthenticationSchemeOptions,AdministradorTemporalPruebas>("AdminTemporalTest",_=>{});
                });
            });
        }
        public async Task<HttpClient> ClienteAsync(string? rol=null,bool csrf=true)
        {
            var c=Servidor.CreateClient(new(){BaseAddress=new Uri("https://localhost"),HandleCookies=false,AllowAutoRedirect=false});
            if(rol=="terminal")c.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
            else if(rol is not null)c.DefaultRequestHeaders.Add("X-Test-Admin",rol);
            if(csrf)
            {
                using var r=await c.GetAsync("/api/servicios/csrf-token");r.EnsureSuccessStatusCode();
                using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",json.RootElement.GetProperty("csrfToken").GetString());
                c.DefaultRequestHeaders.Add("Cookie",string.Join("; ",r.Headers.GetValues("Set-Cookie").Select(x=>x.Split(';')[0])));
            }
            return c;
        }
        public void Dispose()=>Servidor.Dispose();
    }
    [Theory]
    [InlineData(ClaseServicioTemporal.CargaCelular)]
    [InlineData(ClaseServicioTemporal.CargaLaptop)]
    [InlineData(ClaseServicioTemporal.Frio)]
    [InlineData(ClaseServicioTemporal.Calor)]
    [InlineData(ClaseServicioTemporal.Personal)]
    public async Task RecorridoWebATerminalPersisteEstadoYRecuperaContenidoTrasVencimientoSimulado(ClaseServicioTemporal clase)
    {
        using var e=new Escenario(clase);using var web=await e.ClienteAsync();using var terminal=await e.ClienteAsync("terminal");
        using var reserva=await web.PostAsJsonAsync("/api/web/servicios/C01/reservas",new{minutos=30});reserva.EnsureSuccessStatusCode();
        var r=(await reserva.Content.ReadFromJsonAsync<ReservaServicioTemporal>())!;
        web.DefaultRequestHeaders.Add("X-SYNCHRO-SERVICIO",r.Credencial);terminal.DefaultRequestHeaders.Add("X-SYNCHRO-SERVICIO",r.Credencial);
        using var pago=await web.PostAsJsonAsync($"/api/web/servicios/C01/reservas/{r.Operacion.Id}/pagar",new{idOperacion=Guid.NewGuid(),monto=0,aprobado=true});
        pago.EnsureSuccessStatusCode();var p=(await pago.Content.ReadFromJsonAsync<PagoServicioTemporal>())!;
        Assert.Equal(10,p.Operacion.Monto);Assert.NotNull(p.PinRetiro);Assert.True(pago.Headers.CacheControl?.NoStore);
        using var inicio=await terminal.PostAsJsonAsync($"/api/terminales/servicios/C01/reservas/{r.Operacion.Id}/iniciar",new{equipoCerrado=true,terminalId=5});
        inicio.EnsureSuccessStatusCode();e.Reloj.Avanzar(TimeSpan.FromMinutes(31));
        using var agotada=await web.GetAsync($"/api/servicios/C01/reservas/{r.Operacion.Id}");agotada.EnsureSuccessStatusCode();
        Assert.Equal(EstadoServicioTemporal.Agotada,(await agotada.Content.ReadFromJsonAsync<ResumenServicioTemporal>())!.Estado);
        Assert.Equal("Ocupado",e.Repo.Datos["C01"].EstadoInventario);
        using var retiro=await terminal.PostAsJsonAsync($"/api/terminales/servicios/C01/reservas/{r.Operacion.Id}/retirar",new{pin=p.PinRetiro});
        retiro.EnsureSuccessStatusCode();var resultado=(await retiro.Content.ReadFromJsonAsync<RetiroServicioTemporal>())!;
        Assert.True(resultado.Autorizado);Assert.True(resultado.Operacion.HardwareSimulado);Assert.Equal(10,resultado.Operacion.Monto);
        Assert.Equal("Disponible",e.Repo.Datos["C01"].EstadoInventario);
        using var repetida=await terminal.PostAsJsonAsync($"/api/terminales/servicios/C01/reservas/{r.Operacion.Id}/retirar",new{pin=p.PinRetiro});
        Assert.Equal(HttpStatusCode.Conflict,repetida.StatusCode);
    }
    [Fact]
    public async Task AdministradorConfiguraReportaYBloqueaPorIncidenciaMientrasOtrosRolesSeRechazan()
    {
        using var e=new Escenario();using var admin=await e.ClienteAsync("si");using var lector=await e.ClienteAsync("lector");
        using var rechazada=await lector.PutAsJsonAsync("/api/admin/servicios/compartimentos/C01",ServiciosTemporalesTests.Compartimento());
        Assert.Equal(HttpStatusCode.Forbidden,rechazada.StatusCode);
        using var config=await admin.PutAsJsonAsync("/api/admin/servicios/compartimentos/C01",ServiciosTemporalesTests.Compartimento());
        config.EnsureSuccessStatusCode();
        using var incidencia=await admin.PostAsJsonAsync("/api/admin/servicios/compartimentos/C01/incidencias",new{descripcion="Sensor simulado en revision"});
        incidencia.EnsureSuccessStatusCode();Assert.False(e.Repo.Datos["C01"].Configuracion.Habilitado);
        string desde=Uri.EscapeDataString(e.Reloj.GetUtcNow().AddHours(-1).ToString("O")),hasta=Uri.EscapeDataString(e.Reloj.GetUtcNow().ToString("O"));
        using var reporte=await admin.GetAsync($"/api/admin/servicios/reportes?desde={desde}&hasta={hasta}");reporte.EnsureSuccessStatusCode();
        Assert.Equal(1,(await reporte.Content.ReadFromJsonAsync<ReporteServiciosTemporales>())!.Incidencias);
    }
    [Fact]
    public async Task OperadorGestionaIncidenciasYAuditorReportesSinPoderCambiarPrecios()
    {
        using var e=new Escenario();using var operador=await e.ClienteAsync("operador");using var auditor=await e.ClienteAsync("auditor");
        using var precio=await operador.PutAsJsonAsync("/api/admin/servicios/compartimentos/C01",ServiciosTemporalesTests.Compartimento());
        Assert.Equal(HttpStatusCode.Forbidden,precio.StatusCode);
        using var incidencia=await operador.PostAsJsonAsync("/api/admin/servicios/compartimentos/C01/incidencias",new{descripcion="Revision del modulo simulado"});
        incidencia.EnsureSuccessStatusCode();
        string desde=Uri.EscapeDataString(e.Reloj.GetUtcNow().AddHours(-1).ToString("O")),hasta=Uri.EscapeDataString(e.Reloj.GetUtcNow().ToString("O"));
        using var reporte=await auditor.GetAsync($"/api/admin/servicios/reportes?desde={desde}&hasta={hasta}");reporte.EnsureSuccessStatusCode();
        using var cambio=await auditor.PutAsJsonAsync("/api/admin/servicios/compartimentos/C01",ServiciosTemporalesTests.Compartimento());
        Assert.Equal(HttpStatusCode.Forbidden,cambio.StatusCode);
    }
    [Fact]
    public async Task AdminSinCsrfNoCambiaPrecios()
    {
        using var e=new Escenario();using var admin=await e.ClienteAsync("si",false);
        using var r=await admin.PutAsJsonAsync("/api/admin/servicios/compartimentos/C01",ServiciosTemporalesTests.Compartimento()with{Habilitado=false});
        Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);Assert.True(e.Repo.Datos["C01"].Configuracion.Habilitado);
    }
    [Fact]
    public async Task RetiroSinTerminalOCredencialAjenaNoAccede()
    {
        using var e=new Escenario();using var web=await e.ClienteAsync();
        using var r=await web.PostAsJsonAsync("/api/web/servicios/C01/reservas",new{minutos=30});
        var reserva=(await r.Content.ReadFromJsonAsync<ReservaServicioTemporal>())!;
        using var noTerminal=await web.PostAsJsonAsync($"/api/terminales/servicios/C01/reservas/{reserva.Operacion.Id}/retirar",new{pin="123456"});
        Assert.Equal(HttpStatusCode.Unauthorized,noTerminal.StatusCode);
        web.DefaultRequestHeaders.Add("X-SYNCHRO-SERVICIO",new string('0',64));
        using var ajena=await web.GetAsync($"/api/servicios/C01/reservas/{reserva.Operacion.Id}");Assert.Equal(HttpStatusCode.NotFound,ajena.StatusCode);
    }
    [Fact]
    public async Task FlujoPostpagoLegadoNoPuedeEvitarElPrepagoActivo()
    {
        using var e=new Escenario();using var terminal=await e.ClienteAsync("terminal");
        using var r=await terminal.PostAsJsonAsync("/api/espacios-carga/C01/iniciar",new{tipoDispositivo="Celular"});
        Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);Assert.Empty(e.Repo.Datos["C01"].Operaciones);
    }
    [Fact]
    public async Task OpenApiSeGeneraConLosEndpointsNuevosYSoloLoConsultaAdministrador()
    {
        using var e=new Escenario();using var anonimo=await e.ClienteAsync();using var admin=await e.ClienteAsync("si");
        using var noAutorizado=await anonimo.GetAsync("/openapi/v1.json");Assert.Equal(HttpStatusCode.Unauthorized,noAutorizado.StatusCode);
        using var documento=await admin.GetAsync("/openapi/v1.json");documento.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await documento.Content.ReadAsStringAsync());
        Assert.StartsWith("3.",json.RootElement.GetProperty("openapi").GetString());
        var paths=json.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/terminales/encomiendas/usos/{id}/retirar",out _));
        Assert.True(paths.TryGetProperty("/api/web/servicios/{codigo}/reservas",out _));
        Assert.True(paths.TryGetProperty("/api/admin/servicios/reportes",out _));
    }

    [Fact]
    public void ProduccionNoPuedeActivarElHardwareSimulado()
    {
        using var fabrica=new WebApplicationFactory<Program>().WithWebHostBuilder(b=>
        {
            b.UseEnvironment("Production");b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>
            {["ServiciosTemporales:Habilitado"]="true",["ConnectionStrings:SynchroDB"]="Server=.;Database=SynchroDB_Pruebas;Integrated Security=True"}));
        });
        Assert.Throws<InvalidOperationException>(()=>fabrica.CreateClient());
    }
}
