using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Synchro.Api.Tests;

// Pruebas HTTP de integracion: no se envian tokens reales ni se abre SQL.
// Protegen el limite entre tablet y aprobacion administrativa.
public sealed class IntegracionCargaAdminHttpTests
{
    // El test verifica los limites HTTP, no el modulo WhatsApp.
    // No deshabilitar salvaguardas SQL ni alterar la API en produccion.
    private static WebApplicationFactory<Program> Fabrica() =>
        new FabricaTerminalCookieReal().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["WhatsAppManual:Habilitado"] = "false"
                    })));

    private static HttpClient Cliente(WebApplicationFactory<Program> fabrica) =>
        fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    [Theory]
    [InlineData("/api/admin/carga/espacios")]
    [InlineData("/api/admin/carga/cobros/pendientes")]
    public async Task InventarioYCobrosCargaRequierenSesionAdmin(string ruta)
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ConfirmarPagoCargaRequiereOperadorAutenticado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);
        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/admin/carga/cobros/confirmar",
            new {
                sesionId = Guid.NewGuid(),
                metodoPago = "Efectivo",
                pagoComprobado = true
            });
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task RutaAntiguaNoPermiteConfirmarPagoDesdeTablet()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);
        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/terminales/carga/retiro/cobro/confirmar-simulado",
            new { tokenCobro = new string('X', 80), metodoPago = "Efectivo" });
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
