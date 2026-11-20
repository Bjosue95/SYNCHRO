using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

public sealed class InicioFacialV3SeguridadHttpTests
{
    private const string Ruta =
        "/api/terminales/guardar/whatsapp/iniciar";

    private static WebApplicationFactory<Program> Fabrica(bool habilitado)
    {
        return new FabricaTerminalHttpPruebas()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration(
                (_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["WhatsAppManual:Habilitado"] = "true",
                        ["WhatsAppManual:NumeroE164"] = "50400000000",
                        ["RostroV3:RegistroHabilitado"] =
                            habilitado ? "true" : "false"
                    })));
    }

    private static async Task<HttpClient> Cliente(
        WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        await PreReservasWebTabletTests.CsrfAsync(
            cliente,
            "/api/terminales/guardar/whatsapp/csrf-token");

        return cliente;
    }

    private static HttpRequestMessage Solicitud(bool consentimiento)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, Ruta);

        solicitud.Headers.Add("X-SYNCHRO-CASILLERO", "M01");
        solicitud.Headers.Add("X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

        if (consentimiento)
            solicitud.Headers.Add(
                "X-SYNCHRO-CONSENTIMIENTO-BIOMETRICO", "ACEPTO");

        var descriptor = new double[1024];
        descriptor[0] = 1;

        solicitud.Content = JsonContent.Create(new
        {
            imagenPng = Convert.ToBase64String(RostroSimuladoTests.Png()),
            descriptor
        });

        return solicitud;
    }

    [Fact]
    public async Task V3Desactivado_NoIniciaDeposito()
    {
        using var fabrica = Fabrica(false);
        using var cliente = await Cliente(fabrica);
        using var solicitud = Solicitud(true);

        using var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task V3SinConsentimientoBiometrico_EsRechazado()
    {
        using var fabrica = Fabrica(true);
        using var cliente = await Cliente(fabrica);
        using var solicitud = Solicitud(false);

        using var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}