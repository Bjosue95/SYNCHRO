using System.Data;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class RegistroFacialV3RechazosSqlTests
{
    [SqlDepositoFact]
    public async Task V3_RechazaSolicitudesInvalidasSinCrearOperaciones()
    {
        await using var db = new SqlConnection(
            PruebaHistoricaAislada.CadenaAislada);

        await db.OpenAsync();
        await PruebaHistoricaAislada.VerificarAsync(db);

        string codigo = "V3R" + Guid.NewGuid()
            .ToString("N")[..7].ToUpperInvariant();

        using var fabrica = new FabricaTerminalHttpPruebas()
            .WithWebHostBuilder(b =>
                b.ConfigureAppConfiguration(
                    (_, config) => config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["WhatsAppManual:Habilitado"] = "true",
                            ["WhatsAppManual:NumeroE164"] = "50400000000",
                            ["RostroV3:RegistroHabilitado"] = "true"
                        })));

        using var cliente = fabrica.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        await PreReservasWebTabletTests.CsrfAsync(
            cliente,
            "/api/terminales/guardar/whatsapp/csrf-token");

        async Task VerificarRechazo(
            byte[] imagen,
            double[] descriptor,
            bool consentimientoBiometrico,
            string? version = "SYNCHRO-FACIAL-V3-2026-11")
        {
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/terminales/guardar/whatsapp/iniciar");

            solicitud.Headers.Add(
                "X-SYNCHRO-CASILLERO", codigo);

            solicitud.Headers.Add(
                "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

            if (consentimientoBiometrico)
            {
                solicitud.Headers.Add(
                    "X-SYNCHRO-CONSENTIMIENTO-BIOMETRICO",
                    "ACEPTO");
            }

            solicitud.Content = JsonContent.Create(new
            {
                imagenPng = imagen,
                descriptor,
                versionConsentimiento = version
            });

            using var respuesta =
                await cliente.SendAsync(solicitud);

            Assert.Equal(
                HttpStatusCode.BadRequest,
                respuesta.StatusCode);
        }

        var descriptorValido = new double[1024];
        descriptorValido[0] = 1;

        // Caso 1: sin consentimiento biometrico.
        await VerificarRechazo(
            RostroSimuladoTests.Png(),
            descriptorValido,
            false);

        // Caso 2: descriptor sin informacion util.
        await VerificarRechazo(
            RostroSimuladoTests.Png(),
            new double[1024],
            true);

        // Caso 3: imagen incorrecta.
        await VerificarRechazo(
            new byte[] { 1, 2, 3, 4 },
            descriptorValido,
            true);

        // Caso 4: version ausente.
        await VerificarRechazo(
            RostroSimuladoTests.Png(),
            descriptorValido,
            true,
            null);

        // Caso 5: version incorrecta.
        await VerificarRechazo(
            RostroSimuladoTests.Png(),
            descriptorValido,
            true,
            "VERSION-DESCONOCIDA");
        // Comprobar que no se haya creado ninguna operacion.
        await using var consulta = new SqlCommand("""
            SELECT
                (SELECT COUNT(*)
                 FROM dbo.UsosCasillero
                 WHERE CodigoCasillero = @Codigo)
                +
                (SELECT COUNT(*)
                 FROM dbo.RostrosRetiroPersonalV3 AS v
                 INNER JOIN dbo.UsosCasillero AS u
                    ON u.Id = v.UsoId
                 WHERE u.CodigoCasillero = @Codigo);
            """, db);

        consulta.Parameters.Add(
            "@Codigo", SqlDbType.NVarChar, 10)
            .Value = codigo;

        int registros = Convert.ToInt32(
            await consulta.ExecuteScalarAsync());

        Assert.Equal(0, registros);
    }
}