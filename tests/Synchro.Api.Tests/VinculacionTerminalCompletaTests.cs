
using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Synchro.Api.Tests;

[CollectionDefinition(
    "SQL Vinculacion Terminal",
    DisableParallelization = true)]
public sealed class ColeccionVinculacionTerminal
{
}

[Collection("SQL Vinculacion Terminal")]
public class VinculacionTerminalCompletaTests
{
    private const string Cadena =
        @"Server=(localdb)\MSSQLLocalDB;" +
        "Database=SynchroDB_Pruebas;" +
        "Integrated Security=True;" +
        "Encrypt=True;" +
        "TrustServerCertificate=True;";

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient cliente,
        string ruta,
        object datos,
        string? cookies = null,
        string? csrf = null)
    {
        using var solicitud =
            new HttpRequestMessage(HttpMethod.Post, ruta);

        solicitud.Content = JsonContent.Create(datos);

        if (!string.IsNullOrEmpty(cookies))
            solicitud.Headers.Add("Cookie", cookies);

        if (!string.IsNullOrEmpty(csrf))
            solicitud.Headers.Add("X-CSRF-TOKEN", csrf);

        return await cliente.SendAsync(solicitud);
    }

    private static string ObtenerCookie(
        HttpResponseMessage respuesta,
        string nombre)
    {
        Assert.True(respuesta.Headers.TryGetValues(
            "Set-Cookie", out var encabezados));


        string? cookie = encabezados!
            .Select(x => x.Split(';')[0])
            .FirstOrDefault(x =>
                x.StartsWith(
                    nombre + "=",
                    StringComparison.Ordinal)
                ||
                x.StartsWith(
                    nombre + ".",
                    StringComparison.Ordinal));


        Assert.False(string.IsNullOrWhiteSpace(cookie));

        return cookie!;
    }

    private static string CrearCookieAdministrador(
        FabricaTerminalCookieReal fabrica,
        int administradorId)
    {
        var opciones = fabrica.Services
            .GetRequiredService<
                IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        var identidad = new ClaimsIdentity(
            new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    administradorId.ToString()),
                new Claim(
                    ClaimTypes.Role,
                    "Administrador")
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        var propiedades = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identidad),
            propiedades,
            CookieAuthenticationDefaults.AuthenticationScheme);

        return opciones.Cookie.Name + "=" +
            opciones.TicketDataFormat.Protect(ticket);
    }

    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task VinculacionCompleta_ImpideReutilizarToken()
    {
        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();

        Assert.Equal("SynchroDB_Pruebas", conexion.Database);

        string codigoTerminal =
            "TST-" + Guid.NewGuid().ToString("N")[..20];

        int terminalId = 0;

        try
        {
            // 1. Crear solamente una terminal ficticia.
            await using (var crear = new SqlCommand("""
                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 55101, 'Base incorrecta.', 1;

                INSERT INTO dbo.Terminales
                    (Codigo, Nombre, Activa)
                OUTPUT INSERTED.Id
                VALUES
                    (@Codigo, N'SYNCHRO_TEST_AUTOMATICO', 0);
                """, conexion))
            {
                crear.Parameters.Add(
                    "@Codigo", SqlDbType.NVarChar, 30
                ).Value = codigoTerminal;

                terminalId = Convert.ToInt32(
                    await crear.ExecuteScalarAsync());
            }

            using var fabrica =
                new FabricaTerminalCookieReal();

            using var cliente = fabrica.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });

            // 2. Solicitar vinculacion por HTTP.
            using var solicitar = await PostAsync(
                cliente,
                "/api/terminales/vinculacion/solicitar",
                new { CodigoTerminal = codigoTerminal });

            Assert.Equal(HttpStatusCode.OK, solicitar.StatusCode);

            using var datosSolicitud = JsonDocument.Parse(
                await solicitar.Content.ReadAsStringAsync());

            int solicitudId = datosSolicitud.RootElement
                .GetProperty("solicitudId").GetInt32();

            string codigoVisual = datosSolicitud.RootElement
                .GetProperty("codigoVisual").GetString()!;

            string token = datosSolicitud.RootElement
                .GetProperty("token").GetString()!;

            Assert.Matches(@"^[0-9]{8}$", codigoVisual);
            Assert.False(string.IsNullOrWhiteSpace(token));

            // 3. Sin aprobacion debe rechazar la vinculacion.
            using (var sinAprobacion = await PostAsync(
                cliente,
                "/api/terminales/vinculacion/confirmar",
                new { Token = token }))
            {
                Assert.Equal(
                    HttpStatusCode.Unauthorized,
                    sinAprobacion.StatusCode);
            }

            // 4. Localizar un administrador activo de pruebas.
            await using var buscarAdmin = new SqlCommand("""
                SELECT TOP (1) Id
                FROM dbo.Administradores
                WHERE Activo = 1
                ORDER BY Id;
                """, conexion);

            int administradorId = Convert.ToInt32(
                await buscarAdmin.ExecuteScalarAsync());

            Assert.True(administradorId > 0);

            // Cookie de administrador creada dentro del test.
            string cookieAdmin =
                CrearCookieAdministrador(fabrica, administradorId);

            // 5. Obtener CSRF con esa misma identidad.
            using var peticionCsrf = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/admin/csrf-token");

            peticionCsrf.Headers.Add("Cookie", cookieAdmin);

            using var respuestaCsrf =
                await cliente.SendAsync(peticionCsrf);

            Assert.Equal(
                HttpStatusCode.OK,
                respuestaCsrf.StatusCode);

            using var datosCsrf = JsonDocument.Parse(
                await respuestaCsrf.Content.ReadAsStringAsync());

            string csrf = datosCsrf.RootElement
                .GetProperty("csrfToken").GetString()!;

            string cookieCsrf = ObtenerCookie(
                respuestaCsrf,
                ".AspNetCore.Antiforgery");

            string cookiesAdmin =
                cookieAdmin + "; " + cookieCsrf;

            // 6. Aprobar la solicitud mediante el endpoint.
            using var aprobar = await PostAsync(
                cliente,
                "/api/admin/terminales/vinculacion/aprobar",
                new
                {
                    SolicitudId = solicitudId,
                    CodigoVisual = codigoVisual
                },
                cookiesAdmin,
                csrf);

            Assert.Equal(HttpStatusCode.OK, aprobar.StatusCode);

            // 7. Confirmar vinculacion.
            using var confirmar = await PostAsync(
                cliente,
                "/api/terminales/vinculacion/confirmar",
                new { Token = token });

            Assert.Equal(
                HttpStatusCode.OK,
                confirmar.StatusCode);

            string cookieTerminal = ObtenerCookie(
                confirmar, "Synchro.Terminal");

            // 8. La nueva terminal ya puede consultar su sesion.
            using var peticionSesion = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/terminales/mi-sesion");

            peticionSesion.Headers.Add(
                "Cookie", cookieTerminal);

            using var respuestaSesion =
                await cliente.SendAsync(peticionSesion);

            Assert.Equal(
                HttpStatusCode.OK,
                respuestaSesion.StatusCode);

            using var datosSesion = JsonDocument.Parse(
                await respuestaSesion.Content.ReadAsStringAsync());

            Assert.Equal(
                terminalId.ToString(),
                datosSesion.RootElement
                    .GetProperty("terminalId").GetString());

            // 9. Comprobar los estados guardados en SQL.
            await using var verificar = new SqlCommand("""
                SELECT COUNT(*)
                FROM dbo.Terminales t
                INNER JOIN dbo.SolicitudesVinculacionTerminal s
                    ON s.TerminalId = t.Id
                WHERE t.Id = @TerminalId
                  AND t.Codigo = @Codigo
                  AND t.Activa = 1
                  AND s.Id = @SolicitudId
                  AND s.Estado = N'Consumida'
                  AND s.FechaConsumo IS NOT NULL;
                """, conexion);

            verificar.Parameters.Add(
                "@TerminalId", SqlDbType.Int
            ).Value = terminalId;

            verificar.Parameters.Add(
                "@Codigo", SqlDbType.NVarChar, 30
            ).Value = codigoTerminal;

            verificar.Parameters.Add(
                "@SolicitudId", SqlDbType.Int
            ).Value = solicitudId;

            Assert.Equal(
                1,
                Convert.ToInt32(
                    await verificar.ExecuteScalarAsync()));

            // 10. No permitir utilizar el token por segunda vez.
            using var repetir = await PostAsync(
                cliente,
                "/api/terminales/vinculacion/confirmar",
                new { Token = token });

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                repetir.StatusCode);
        }
        finally
        {
            // Limpiar solamente nuestra terminal ficticia.
            if (terminalId > 0)
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 55102, 'Base incorrecta.', 1;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.Terminales
                        WHERE Id = @Id
                          AND Codigo = @Codigo
                          AND Nombre = N'SYNCHRO_TEST_AUTOMATICO'
                    )
                        THROW 55103, 'Terminal de prueba no coincide.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DELETE FROM dbo.SolicitudesVinculacionTerminal
                        WHERE TerminalId = @Id;

                        DELETE FROM dbo.Terminales
                        WHERE Id = @Id
                          AND Codigo = @Codigo
                          AND Nombre = N'SYNCHRO_TEST_AUTOMATICO';

                        IF @@ROWCOUNT <> 1
                            THROW 55104, 'Limpieza incompleta.', 1;

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0 ROLLBACK;
                        THROW;
                    END CATCH;
                    """, conexion);

                limpiar.Parameters.Add(
                    "@Id", SqlDbType.Int
                ).Value = terminalId;

                limpiar.Parameters.Add(
                    "@Codigo", SqlDbType.NVarChar, 30
                ).Value = codigoTerminal;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}
