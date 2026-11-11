using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synchro.Api.Services;
using Xunit;

namespace Synchro.Api.Tests;

public sealed class FabricaTerminalHttpPruebas
    : WebApplicationFactory<Program>
{
    // Las pruebas que acceden a SQL exigen esta variable y nunca usan la base principal.
    internal static string CadenaSqlAislada => ConexionSqlAislada.Exigir(
        Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST"),
        "SynchroDB_Pruebas");

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            // Las pruebas de 401/400 no requieren SQL ni credenciales externas.
            // Sin variable, la cadena de respaldo no se usa para consultas SQL.
            string? configurada = Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST");
            string cadena = string.IsNullOrWhiteSpace(configurada)
                ? @"Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;"
                : ConexionSqlAislada.Exigir(configurada, "SynchroDB_Pruebas");
            configuracion.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SynchroDB"] = cadena
                });
        });

        builder.ConfigureTestServices(servicios =>
        {
            // Desactivar el proceso automatico de vencimiento
            // mientras se ejecutan estas pruebas HTTP.
            for (int i = servicios.Count - 1; i >= 0; i--)
            {
                var registro = servicios[i];

                if (registro.ServiceType == typeof(IHostedService)
                    && registro.ImplementationType ==
                        typeof(VencimientoReservasService))
                {
                    servicios.RemoveAt(i);
                }
            }

            // Autenticacion ficticia exclusiva de las pruebas.
            servicios.AddAuthentication(
                AutenticacionTerminalHttpPruebas.Esquema)
                .AddScheme<
                    AuthenticationSchemeOptions,
                    AutenticacionTerminalHttpPruebas>(
                    AutenticacionTerminalHttpPruebas.Esquema,
                    _ => { });

            servicios.PostConfigure<AuthorizationOptions>(opciones =>
            {
                opciones.AddPolicy(
                    "SoloTerminal",
                    new AuthorizationPolicyBuilder(
                        AutenticacionTerminalHttpPruebas.Esquema)
                        .RequireAuthenticatedUser()
                        .Build());
            });
        });
    }
}

public sealed class AutenticacionTerminalHttpPruebas
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Esquema = "TerminalHttpPruebas";

    public AutenticacionTerminalHttpPruebas(
        IOptionsMonitor<AuthenticationSchemeOptions> opciones,
        ILoggerFactory logger,
        UrlEncoder codificador)
        : base(opciones, logger, codificador)
    {
    }

    protected override Task<AuthenticateResult>
        HandleAuthenticateAsync()
    {
        if (Request.Headers["X-Synchro-Test-Terminal"].ToString()
            != "autorizada")
        {
            return Task.FromResult(
                AuthenticateResult.NoResult());
        }

        var identidad = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "99999")
            },
            Esquema);

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identidad),
            Esquema);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}

[Collection("SQL Deposito Aislado")]
public class ConsultaReservaTerminalHttpTests
{
    private const string Ruta =
        "/api/terminales/encomiendas/reservas/consultar";

    private static HttpClient CrearCliente(
        FabricaTerminalHttpPruebas fabrica)
    {
        return fabrica.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
    }

    private static HttpRequestMessage CrearSolicitud(
        string tokenReserva)
    {
        return new HttpRequestMessage(HttpMethod.Post, Ruta)
        {
            Content = JsonContent.Create(new
            {
                Referencia = int.MaxValue,
                TokenGestion = tokenReserva
            })
        };
    }

    [Fact]
    public async Task SinSesion_DebeDevolver401()
    {
        using var fabrica = new FabricaTerminalHttpPruebas();
        using var cliente = CrearCliente(fabrica);

        using var solicitud =
            CrearSolicitud(new string('A', 64));

        using var respuesta =
            await cliente.SendAsync(solicitud);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);
    }

    [Fact]
    public async Task SinCsrf_DebeDevolver400()
    {
        using var fabrica = new FabricaTerminalHttpPruebas();
        using var cliente = CrearCliente(fabrica);

        using var solicitud =
            CrearSolicitud(new string('A', 64));

        solicitud.Headers.Add(
            "X-Synchro-Test-Terminal", "autorizada");

        using var respuesta =
            await cliente.SendAsync(solicitud);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            respuesta.StatusCode);
    }

    private static async Task<HttpResponseMessage>
        EnviarConCsrfAsync(
            HttpClient cliente,
            string tokenReserva)
    {
        using var solicitudCsrf = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/web/encomiendas/csrf-token");

        solicitudCsrf.Headers.Add(
            "X-Synchro-Test-Terminal", "autorizada");

        using var respuestaCsrf =
            await cliente.SendAsync(solicitudCsrf);

        Assert.Equal(
            HttpStatusCode.OK,
            respuestaCsrf.StatusCode);

        using var documento = JsonDocument.Parse(
            await respuestaCsrf.Content.ReadAsStringAsync());

        string? csrfToken = documento.RootElement
            .GetProperty("csrfToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(csrfToken));

        Assert.True(respuestaCsrf.Headers.TryGetValues(
            "Set-Cookie", out var cookies));

        string cookie = string.Join("; ",
            cookies.Select(x => x.Split(';')[0]));

        using var solicitud = CrearSolicitud(tokenReserva);

        solicitud.Headers.Add(
            "X-Synchro-Test-Terminal", "autorizada");

        solicitud.Headers.Add(
            "X-CSRF-TOKEN", csrfToken);

        solicitud.Headers.Add("Cookie", cookie);

        return await cliente.SendAsync(solicitud);
    }

    [Fact]
    public async Task TokenMalFormado_DebeDevolver400()
    {
        using var fabrica = new FabricaTerminalHttpPruebas();
        using var cliente = CrearCliente(fabrica);

        using var respuesta =
            await EnviarConCsrfAsync(
                cliente, "TOKEN-INVALIDO");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            respuesta.StatusCode);
    }

    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task ReservaInexistente_DebeDevolver404()
    {
        // Usa la misma fixture idempotente que las pruebas SQL del deposito.
        await DepositoEncomiendaSqlTests.PrepararAsync();
        using var fabrica = new FabricaTerminalHttpPruebas();
        using var cliente = CrearCliente(fabrica);

        // Credencial ficticia con formato correcto.
        using var respuesta =
            await EnviarConCsrfAsync(
                cliente, new string('A', 64));

        Assert.Equal(
            HttpStatusCode.NotFound,
            respuesta.StatusCode);
    }
}