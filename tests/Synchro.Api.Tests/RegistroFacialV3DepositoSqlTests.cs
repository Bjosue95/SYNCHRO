using System.Data;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Synchro.Api.Services.RostroSimulado;
using Xunit;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class RegistroFacialV3DepositoSqlTests
{
    [SqlDepositoFact]
    public async Task DepositoV3_ProtegeReferenciaYSigueGuardandoV2()
    {
        await using var db = new SqlConnection(
            PruebaHistoricaAislada.CadenaAislada);

        await db.OpenAsync();
        await PruebaHistoricaAislada.VerificarAsync(db);

        string codigo = "V3H" + Guid.NewGuid()
            .ToString("N")[..7].ToUpperInvariant();

        try
        {
            await using (var crear = new SqlCommand("""
                INSERT dbo.Casilleros
                    (Codigo, Tamano, Tipo, Estado)
                VALUES
                    (@Codigo, N'Mediano', N'Normal', N'Disponible');
                """, db))
            {
                crear.Parameters.Add("@Codigo", SqlDbType.NVarChar, 10)
                    .Value = codigo;

                await crear.ExecuteNonQueryAsync();
            }

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

            var descriptor = new double[1024];
            descriptor[0] = 3;
            descriptor[1] = 4;

            using var peticion = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/terminales/guardar/whatsapp/iniciar");

            peticion.Headers.Add(
                "X-SYNCHRO-CASILLERO", codigo);
            peticion.Headers.Add(
                "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");
            peticion.Headers.Add(
                "X-SYNCHRO-CONSENTIMIENTO-BIOMETRICO", "ACEPTO");

            peticion.Content = JsonContent.Create(new
            {
                imagenPng = RostroSimuladoTests.Png(),
                versionConsentimiento = "SYNCHRO-FACIAL-V3-2026-11",
                descriptor
            });

            using var respuesta = await cliente.SendAsync(peticion);

            Assert.True(
                respuesta.IsSuccessStatusCode,
                "Inicio V3: " +
                await respuesta.Content.ReadAsStringAsync());

            await using var consulta = new SqlCommand("""
                SELECT u.Id,
                       v.PatronProtegido,
                       v.Modelo,
                       m.Modelo
                FROM dbo.UsosCasillero AS u
                INNER JOIN dbo.RostrosRetiroPersonalV3 AS v
                    ON v.UsoId = u.Id
                INNER JOIN dbo.RostrosRetiroPersonalManual AS m
                    ON m.UsoId = u.Id
                WHERE u.CodigoCasillero = @Codigo;
                """, db);

            consulta.Parameters.Add("@Codigo", SqlDbType.NVarChar, 10)
                .Value = codigo;

            int usoId;
            byte[] cifrado;

            await using (var lector = await consulta.ExecuteReaderAsync())
            {
                Assert.True(await lector.ReadAsync());

                usoId = lector.GetInt32(0);
                cifrado = (byte[])lector.GetValue(1);

                Assert.Equal(DescriptorFacialV3.Modelo,
                    lector.GetString(2));

                Assert.Equal(PatronVisualRetiroV2.Modelo,
                    lector.GetString(3));

                Assert.False(await lector.ReadAsync());
            }

            // El consentimiento debe pertenecer al mismo UsoId.
            await using (var consentimiento = new SqlCommand("""
                SELECT VersionTexto, DeclaradoUtc, Canal
                FROM dbo.ConsentimientosRostroPersonalV3
                WHERE UsoId = @UsoId;
                """, db))
            {
                consentimiento.Parameters.Add(
                    "@UsoId", SqlDbType.Int).Value = usoId;

                await using var registro =
                    await consentimiento.ExecuteReaderAsync();

                Assert.True(await registro.ReadAsync());

                Assert.Equal(
                    "SYNCHRO-FACIAL-V3-2026-11",
                    registro.GetString(0));

                Assert.True(
                    registro.GetDateTime(1) >
                    DateTime.UtcNow.AddMinutes(-10));

                Assert.Equal("TabletV3", registro.GetString(2));

                Assert.False(await registro.ReadAsync());
            }

            byte[] plano = fabrica.Services
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(
                    "SYNCHRO.RostroManual.V3.v1",
                    usoId.ToString(CultureInfo.InvariantCulture))
                .Unprotect(cifrado);

            try
            {
                double[]? recuperado =
                    JsonSerializer.Deserialize<double[]>(plano);

                Assert.NotNull(recuperado);
                Assert.Equal(1024, recuperado.Length);

                Assert.InRange(recuperado[0], 0.599, 0.601);
                Assert.InRange(recuperado[1], 0.799, 0.801);

                Array.Clear(recuperado);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plano);
                CryptographicOperations.ZeroMemory(cifrado);
                Array.Clear(descriptor);
            }
        }
        finally
        {
            // Exclusivamente datos ficticios del casillero creado.
            await PruebaHistoricaAislada.VerificarAsync(db);

            await using var limpiar = new SqlCommand("""
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                DELETE v
                FROM dbo.RostrosRetiroPersonalV3 AS v
                JOIN dbo.UsosCasillero AS u
                    ON u.Id = v.UsoId
                WHERE u.CodigoCasillero = @Codigo;

                IF EXISTS (
                    SELECT 1
                    FROM dbo.ConsentimientosRostroPersonalV3 AS c
                    INNER JOIN dbo.UsosCasillero AS u
                        ON u.Id = c.UsoId
                    WHERE u.CodigoCasillero = @Codigo
                )
                    THROW 59024, 'El consentimiento V3 no fue eliminado.', 1;

                DELETE m
                FROM dbo.RostrosRetiroPersonalManual AS m
                JOIN dbo.UsosCasillero AS u
                    ON u.Id = m.UsoId
                WHERE u.CodigoCasillero = @Codigo;

                DELETE p
                FROM dbo.CodigosRetiroPersonal AS p
                JOIN dbo.UsosCasillero AS u
                    ON u.Id = p.UsoId
                WHERE u.CodigoCasillero = @Codigo;

                DELETE dbo.AutorizacionesWhatsAppManuales
                WHERE CodigoCasillero = @Codigo;

                DELETE dbo.UsosCasillero
                WHERE CodigoCasillero = @Codigo;

                DELETE dbo.Casilleros
                WHERE Codigo = @Codigo;

                COMMIT;
                """, db);

            limpiar.Parameters.Add("@Codigo", SqlDbType.NVarChar, 10)
                .Value = codigo;

            await limpiar.ExecuteNonQueryAsync();
        }
    }
}