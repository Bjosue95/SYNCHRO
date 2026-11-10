
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Synchro.Api.Services;
using Xunit;

namespace Synchro.Api.Tests;

// Servidor que utiliza la autenticacion real
// de ASP.NET Core, exclusivamente con la base de pruebas.
public sealed class FabricaTerminalCookieReal
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SynchroDB"] =
                        @"Server=(localdb)\MSSQLLocalDB;" +
                        "Database=SynchroDB_Pruebas;" +
                        "Integrated Security=True;" +
                        "Encrypt=True;" +
                        "TrustServerCertificate=True;"
                });
        });

        builder.ConfigureTestServices(servicios =>
        {
            // Evitar que el proceso automatico de vencimiento
            // modifique reservas mientras hacemos las pruebas.
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
        });
    }
}

public class AutenticacionTerminalRealTests
{
    private const string CadenaPruebas =
        @"Server=(localdb)\MSSQLLocalDB;" +
        "Database=SynchroDB_Pruebas;" +
        "Integrated Security=True;" +
        "Encrypt=True;" +
        "TrustServerCertificate=True;";

    private const string RutaSesion =
        "/api/terminales/mi-sesion";

    private static HttpClient CrearCliente(
        FabricaTerminalCookieReal fabrica)
    {
        return fabrica.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
    }

    private static async Task<int> ObtenerTerminalActivaAsync()
    {
        await using var conexion =
            new SqlConnection(CadenaPruebas);

        await conexion.OpenAsync();

        Assert.Equal(
            "SynchroDB_Pruebas",
            conexion.Database);

        await using var comando = new SqlCommand(
            """
            SELECT TOP (1) Id
            FROM dbo.Terminales
            WHERE Activa = 1
            ORDER BY Id;
            """,
            conexion);

        object? resultado = await comando.ExecuteScalarAsync();

        Assert.True(
            resultado is int,
            "No se encontro una terminal activa en la base de pruebas.");

        return (int)resultado!;
    }

    private static string CrearCookieDePrueba(
        FabricaTerminalCookieReal fabrica,
        int terminalId)
    {
        // Usar el esquema y protector real de cookies.
        var opciones = fabrica.Services
            .GetRequiredService<
                IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get("Terminal");

        var identidad = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    terminalId.ToString()),

                new Claim(
                    ClaimTypes.Role,
                    "Terminal")
            },
            "Terminal");

        var principal = new ClaimsPrincipal(identidad);

        var propiedades = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        var ticket = new AuthenticationTicket(
            principal,
            propiedades,
            "Terminal");

        string valor = opciones.TicketDataFormat.Protect(ticket);

        return $"{opciones.Cookie.Name}={valor}";
    }

    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task TerminalActiva_PuedeConsultarSesion()
    {
        int terminalId = await ObtenerTerminalActivaAsync();

        using var fabrica = new FabricaTerminalCookieReal();
        using var cliente = CrearCliente(fabrica);

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get,
            RutaSesion);

        solicitud.Headers.Add(
            "Cookie",
            CrearCookieDePrueba(fabrica, terminalId));

        using var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(
            HttpStatusCode.OK,
            respuesta.StatusCode);

        using var documento = JsonDocument.Parse(
            await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(
            terminalId.ToString(),
            documento.RootElement
                .GetProperty("terminalId")
                .GetString());
    }

    [Fact]
    public async Task SinCookie_DebeDevolver401()
    {
        using var fabrica = new FabricaTerminalCookieReal();
        using var cliente = CrearCliente(fabrica);

        using var respuesta = await cliente.GetAsync(RutaSesion);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);
    }

    [Fact]
    public async Task CookieFalsa_DebeDevolver401()
    {
        using var fabrica = new FabricaTerminalCookieReal();
        using var cliente = CrearCliente(fabrica);

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get,
            RutaSesion);

        solicitud.Headers.Add(
            "Cookie",
            "Synchro.Terminal=cookie-invalida");

        using var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);
    }

    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task TerminalInexistente_DebeDevolver401()
    {
        using var fabrica = new FabricaTerminalCookieReal();
        using var cliente = CrearCliente(fabrica);

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get,
            RutaSesion);

        // Identificador ficticio que no existe.
        solicitud.Headers.Add(
            "Cookie",
            CrearCookieDePrueba(fabrica, int.MaxValue));

        using var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);
    }
}
