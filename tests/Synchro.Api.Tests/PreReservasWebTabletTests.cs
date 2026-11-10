using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Synchro.Api.Services;
using Synchro.Api.Services.Clientes;

namespace Synchro.Api.Tests;

public sealed class PreReservasWebTabletTests
{
    private sealed class PersistenciaSimulada : IPreReservasEncomienda
    {
        public int Guardadas {get;private set;}
        public int Cliente {get;private set;}
        public Task<PreReservaEncomiendaCreada> CrearAsync(int cliente,string? destino,string? tamano,CancellationToken token)
        {
            PreReservaEncomiendaService.Validar(cliente,destino,tamano);Guardadas++;Cliente=cliente;
            return Task.FromResult(new PreReservaEncomiendaCreada(Guardadas,new string('A',64),DateTimeOffset.UtcNow.AddHours(24)));
        }
    }
    private static WebApplicationFactory<Program> Fabrica(PersistenciaSimulada datos,ClientesWebSimulados cuentas)
        =>new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>
        {s.RemoveAll<IPreReservasEncomienda>();s.AddSingleton<IPreReservasEncomienda>(datos);s.RemoveAll<IRepositorioClientesWeb>();s.AddSingleton<IRepositorioClientesWeb>(cuentas);}));
    internal static async Task CsrfAsync(HttpClient cliente,string ruta)
    {
        using var respuesta=await cliente.GetAsync(ruta);respuesta.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        cliente.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN",json.RootElement.GetProperty("csrfToken").GetString());
    }
    internal static async Task RegistrarAsync(HttpClient cliente,string nombre="cliente_prueba",string telefono="+50499990001")
    {
        await CsrfAsync(cliente,"/api/web/clientes/acceso/csrf-token");
        using var respuesta=await cliente.PostAsJsonAsync("/api/web/clientes/acceso/registro",new{nombreUsuario=nombre,contrasena="Clave-cliente-solo-pruebas-123",telefono});
        respuesta.EnsureSuccessStatusCode();await CsrfAsync(cliente,"/api/web/clientes/csrf-token");
    }
    [Fact]
    public async Task SoloClienteRegistradoCreaReservaWebSinAutorizarDeposito()
    {
        var datos=new PersistenciaSimulada();var cuentas=new ClientesWebSimulados();using var fabrica=Fabrica(datos,cuentas);
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});await RegistrarAsync(cliente);
        using var respuesta=await cliente.PostAsJsonAsync("/api/web/encomiendas/pre-reservas",new{telefonoDestinatario="+50499990002",tamanoSolicitado="Mediano",clientePersonaId=999,telefonoRemitente="+50400000000"});
        Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);Assert.Equal(1,datos.Guardadas);Assert.Equal(41,datos.Cliente);
        using var contenido=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.False(contenido.RootElement.GetProperty("depositoHabilitado").GetBoolean());
        Assert.Equal("PendienteConsentimiento",contenido.RootElement.GetProperty("estado").GetString());
        Assert.True(respuesta.Headers.CacheControl?.NoStore);Assert.DoesNotContain("99990002",await respuesta.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task AnonimoNiSesionTerminalPuedenCrearReservasWeb()
    {
        var datos=new PersistenciaSimulada();using var fabrica=Fabrica(datos,new());
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        using var anonimo=await cliente.PostAsJsonAsync("/api/web/encomiendas/pre-reservas",new{telefonoDestinatario="+50499990002",tamanoSolicitado="Mediano"});
        Assert.Equal(HttpStatusCode.Unauthorized,anonimo.StatusCode);
        cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
        using var terminal=await cliente.PostAsJsonAsync("/api/web/encomiendas/pre-reservas",new{telefonoDestinatario="+50499990002",tamanoSolicitado="Mediano"});
        Assert.Equal(HttpStatusCode.Unauthorized,terminal.StatusCode);Assert.Equal(0,datos.Guardadas);
    }
    [Theory]
    [InlineData("visitas")]
    [InlineData("reservas")]
    [InlineData("pre-reservas")]
    public async Task TerminalNoExponeCreacionDeEncomiendas(string ruta)
    {
        var datos=new PersistenciaSimulada();using var fabrica=Fabrica(datos,new());
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
        using var respuesta=await cliente.PostAsJsonAsync("/api/terminales/encomiendas/"+ruta,new{telefonoRemitente="+50499990001",telefonoDestinatario="+50499990002",tamanoSolicitado="Mediano"});
        Assert.Equal(HttpStatusCode.NotFound,respuesta.StatusCode);Assert.Equal(0,datos.Guardadas);
    }
    [Theory]
    [InlineData(0,"+50499990002","Mediano")]
    [InlineData(41,null,"Mediano")]
    [InlineData(41,"+50499990002",null)]
    [InlineData(41,"+50499990002","Otro")]
    [InlineData(41,"+5049999\n0002","Mediano")]
    [InlineData(41,"+00000000","Mediano")]
    public void ValidacionRequiereClienteYDatosCorrectos(int cliente,string? d,string? t)
        =>Assert.Throws<ArgumentException>(()=>PreReservaEncomiendaService.Validar(cliente,d,t));
    [Fact]
    public void CredencialDeCreacionNoSeImprime()
    {
        var creada=new PreReservaEncomiendaCreada(1,new string('A',64),DateTimeOffset.UtcNow);Assert.DoesNotContain(creada.TokenGestion,creada.ToString());
    }
}
