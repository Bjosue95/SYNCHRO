using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

public sealed class ValidacionDescriptorV3HttpTests
{
    private const string Ruta =
        "/api/terminales/guardar/whatsapp/v3/validar";

    private static WebApplicationFactory<Program> Fabrica()
    {
        return new FabricaTerminalHttpPruebas()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration(
                (_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["WhatsAppManual:Habilitado"] = "true",
                        ["WhatsAppManual:NumeroE164"] = "50400000000"
                    })));
    }

    private static HttpClient Cliente(
        WebApplicationFactory<Program> fabrica)
    {
        return fabrica.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }

    private static double[] DescriptorValido()
    {
        var datos = new double[1024];
        datos[0] = 1;
        return datos;
    }

    [Fact]
    public async Task SinCsrfEsRechazado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        cliente.DefaultRequestHeaders.Add(
            "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

        using var respuesta = await cliente.PostAsJsonAsync(
            Ruta, new { descriptor = DescriptorValido() });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task SinConsentimientoEsRechazado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await PreReservasWebTabletTests.CsrfAsync(
            cliente, "/api/terminales/guardar/whatsapp/csrf-token");

        using var respuesta = await cliente.PostAsJsonAsync(
            Ruta, new { descriptor = DescriptorValido() });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task DescriptorValido_NoAutorizaRetiro()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await PreReservasWebTabletTests.CsrfAsync(
            cliente, "/api/terminales/guardar/whatsapp/csrf-token");

        cliente.DefaultRequestHeaders.Add(
            "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

        using var respuesta = await cliente.PostAsJsonAsync(
            Ruta, new { descriptor = DescriptorValido() });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        string contenido = await respuesta.Content.ReadAsStringAsync();

        Assert.Contains("\"valido\":true", contenido);
        Assert.DoesNotContain("tokenFacial", contenido);
        Assert.DoesNotContain("retiroConfirmado", contenido);
    }

    [Fact]
    public async Task DescriptorInvalidoEsRechazado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await PreReservasWebTabletTests.CsrfAsync(
            cliente, "/api/terminales/guardar/whatsapp/csrf-token");

        cliente.DefaultRequestHeaders.Add(
            "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

        using var respuesta = await cliente.PostAsJsonAsync(
            Ruta, new { descriptor = new double[1024] });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}