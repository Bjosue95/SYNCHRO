using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Synchro.Api.Tests;

// Pruebas HTTP de integracion: no se envian tokens reales ni se abre SQL.
// Protegen el limite entre tablet y aprobacion administrativa.
public sealed class IntegracionCargaAdminHttpTests
{
    private static HttpClient Cliente(FabricaTerminalCookieReal fabrica) =>
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
        using var fabrica = new FabricaTerminalCookieReal();
        using var cliente = Cliente(fabrica);
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ConfirmarPagoCargaRequiereOperadorAutenticado()
    {
        using var fabrica = new FabricaTerminalCookieReal();
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
        using var fabrica = new FabricaTerminalCookieReal();
        using var cliente = Cliente(fabrica);
        using var respuesta = await cliente.PostAsJsonAsync(
            "/api/terminales/carga/retiro/cobro/confirmar-simulado",
            new { tokenCobro = new string('X', 80), metodoPago = "Efectivo" });
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
