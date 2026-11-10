using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Synchro.Api.Tests;

public sealed class SeguridadCargaHttpTests
{
    [Theory]
    [InlineData("/api/espacios-carga/C01/reservar")]
    [InlineData("/api/espacios-carga/C01/iniciar")]
    [InlineData("/api/sesiones-carga/1/finalizar")]
    public async Task SinTerminalAutenticadaNoModificaCarga(string ruta)
    {
        using var fabrica = new FabricaTerminalHttpPruebas();
        using var cliente = CrearCliente(fabrica,"https");
        using var respuesta = await cliente.PostAsJsonAsync(ruta,new { TipoDispositivo="Celular" });
        Assert.Equal(HttpStatusCode.Unauthorized,respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/api/espacios-carga/C01/reservar")]
    [InlineData("/api/espacios-carga/C01/iniciar")]
    [InlineData("/api/sesiones-carga/1/finalizar")]
    public async Task TerminalSinCsrfNoModificaCarga(string ruta)
    {
        using var fabrica = new FabricaTerminalHttpPruebas();
        using var cliente = CrearCliente(fabrica,"https");
        cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
        using var respuesta = await cliente.PostAsJsonAsync(ruta,new { TipoDispositivo="Celular" });
        Assert.Equal(HttpStatusCode.BadRequest,respuesta.StatusCode);
        Assert.Contains("CSRF",await respuesta.Content.ReadAsStringAsync());
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("/api/espacios-carga/C01/reservar")]
    [InlineData("/api/espacios-carga/C01/iniciar")]
    [InlineData("/api/sesiones-carga/1/finalizar")]
    public async Task TerminalSobreHttpNoModificaCarga(string ruta)
    {
        using var fabrica = new FabricaTerminalHttpPruebas();
        using var cliente = CrearCliente(fabrica,"http");
        cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
        using var respuesta = await cliente.PostAsJsonAsync(ruta,new { TipoDispositivo="Celular" });
        Assert.Equal(HttpStatusCode.BadRequest,respuesta.StatusCode);
        Assert.Contains("HTTPS",await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LimitaIntentosSinLlegarALaBaseDeDatos()
    {
        using var fabrica = new FabricaTerminalHttpPruebas();
        using var cliente = CrearCliente(fabrica,"https");
        cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");
        for (int i=0;i<30;i++)
        {
            using var rechazada=await cliente.PostAsJsonAsync("/api/espacios-carga/C01/reservar",new {});
            Assert.Equal(HttpStatusCode.BadRequest,rechazada.StatusCode);
        }
        using var limitada=await cliente.PostAsJsonAsync("/api/espacios-carga/C01/reservar",new {});
        Assert.Equal(HttpStatusCode.TooManyRequests,limitada.StatusCode);
    }

    private static HttpClient CrearCliente(FabricaTerminalHttpPruebas fabrica,string esquema)
        => fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress=new Uri($"{esquema}://localhost"),AllowAutoRedirect=false,HandleCookies=false
        });
}
