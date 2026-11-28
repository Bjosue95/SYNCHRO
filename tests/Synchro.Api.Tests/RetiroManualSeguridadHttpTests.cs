using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Synchro.Api.Tests;

public sealed class RetiroManualSeguridadHttpTests
{
    private const string RutaRostro =
        "/api/terminales/guardar/whatsapp/retiro/rostro";

    private const string RutaPin =
        "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar";

    private const string RutaCsrf =
        "/api/terminales/guardar/whatsapp/csrf-token";

    private const string ContextoToken =
        "SYNCHRO.RostroManual.RetiroAutorizado.v1";

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

    private static HttpClient Cliente(WebApplicationFactory<Program> fabrica)
    {
        return fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    private static Task CsrfAsync(HttpClient cliente)
    {
        return PreReservasWebTabletTests.CsrfAsync(cliente, RutaCsrf);
    }

    [Fact]
    public async Task PinSinCsrfEsRechazado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaPin,
            new { pin = "123456", tokenFacial = new string('A', 64) });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task PinConFormatoIncorrectoEsRechazado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await CsrfAsync(cliente);

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaPin,
            new { pin = "12A456", tokenFacial = new string('A', 64) });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task TokenFacialAlteradoEsRechazado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await CsrfAsync(cliente);

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaPin,
            new { pin = "123456", tokenFacial = new string('A', 80) });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        string contenido = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("123456", contenido);
    }

    [Fact]
    public async Task TokenFacialVencidoEsRechazado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await CsrfAsync(cliente);

        var proveedor = fabrica.Services
            .GetRequiredService<IDataProtectionProvider>();

        string token = proveedor.CreateProtector(ContextoToken)
            .ToTimeLimitedDataProtector()
            .Protect("123", DateTimeOffset.UtcNow.AddMinutes(-1));

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaPin,
            new { pin = "123456", tokenFacial = token });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task TokenCifradoPeroNoEmitidoEsRechazado()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await CsrfAsync(cliente);

        var proveedor = fabrica.Services
            .GetRequiredService<IDataProtectionProvider>();

        string token = proveedor.CreateProtector(ContextoToken)
            .ToTimeLimitedDataProtector()
            .Protect("123", TimeSpan.FromMinutes(2));

        using var respuesta = await cliente.PostAsJsonAsync(
            RutaPin,
            new { pin = "123456", tokenFacial = token });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CapturaSinConsentimientoEsRechazada()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await CsrfAsync(cliente);

        using var contenido = new ByteArrayContent(RostroSimuladoTests.Png());
        contenido.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/octet-stream");

        using var respuesta = await cliente.PostAsync(RutaRostro, contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CapturaPngIncompletaEsRechazada()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        await CsrfAsync(cliente);
        cliente.DefaultRequestHeaders.Add(
            "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

        using var contenido = new ByteArrayContent(
            new byte[] { 137,80,78,71,13,10,26,10,0,0,0,0,0,0,0,0 });

        contenido.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/octet-stream");

        using var respuesta = await cliente.PostAsync(RutaRostro, contenido);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task CapturaSinCsrfEsRechazada()
    {
        using var fabrica = Fabrica();
        using var cliente = Cliente(fabrica);

        cliente.DefaultRequestHeaders.Add(
            "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

        using var contenido = new ByteArrayContent(RostroSimuladoTests.Png());

        contenido.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/octet-stream");

        using var respuesta = await cliente.PostAsync(RutaRostro, contenido);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}