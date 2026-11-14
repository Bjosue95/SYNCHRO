
using System.Data;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public class RevocacionTerminalTests : PruebaHistoricaAislada
{
    private const string Cadena =
        @"Server=(localdb)\SYNCHRO_Aislado;" +
        "Database=SynchroDB_Pruebas;" +
        "Integrated Security=True;" +
        "Encrypt=True;" +
        "TrustServerCertificate=True;";

    private const string RutaSesion =
        "/api/terminales/mi-sesion";

    // Crear una cookie con el esquema real de ASP.NET Core.
    // Solo se utiliza dentro del servidor de pruebas.
    private static string CrearCookie(
        FabricaTerminalCookieReal fabrica,
        int terminalId)
    {
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

        var propiedades = new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        };

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identidad),
            propiedades,
            "Terminal");

        return opciones.Cookie.Name + "=" +
            opciones.TicketDataFormat.Protect(ticket);
    }

    private static async Task<HttpResponseMessage>
        ConsultarSesionAsync(
            HttpClient cliente,
            string cookie)
    {
        using var solicitud = new HttpRequestMessage(
            HttpMethod.Get,
            RutaSesion);

        solicitud.Headers.Add("Cookie", cookie);

        return await cliente.SendAsync(solicitud);
    }

    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task TerminalDesactivada_PierdeAccesoConCookieVigente()
    {
        string codigo =
            "TST-REV-" + Guid.NewGuid().ToString("N")[..18];

        int terminalId = 0;

        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();
        await PruebaHistoricaAislada.VerificarAsync(conexion);

        // Nunca ejecutar contra la base principal.
        Assert.Equal(
            "SynchroDB_Pruebas",
            conexion.Database);

        try
        {
            // 1. Crear una terminal ficticia activa.
            await using (var crear = new SqlCommand(
                """
                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 55201, 'Base incorrecta.', 1;

                INSERT INTO dbo.Terminales
                    (Codigo, Nombre, Activa)
                OUTPUT INSERTED.Id
                VALUES
                    (@Codigo, N'SYNCHRO_TEST_REVOCACION', 1);
                """,
                conexion))
            {
                crear.Parameters.Add(
                    "@Codigo",
                    SqlDbType.NVarChar,
                    30).Value = codigo;

                terminalId = Convert.ToInt32(
                    await crear.ExecuteScalarAsync());
            }

            // 2. Utilizar la autenticacion real del backend.
            using var fabrica =
                new FabricaTerminalCookieReal();

            using var cliente = fabrica.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });

            string cookie = CrearCookie(fabrica, terminalId);

            // 3. La terminal activa debe tener acceso.
            using (var activa = await ConsultarSesionAsync(
                cliente, cookie))
            {
                Assert.Equal(
                    HttpStatusCode.OK,
                    activa.StatusCode);
            }

            // 4. Desactivar solamente la terminal ficticia.
            await using (var desactivar = new SqlCommand(
                """
                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 55202, 'Base incorrecta.', 1;

                UPDATE dbo.Terminales
                SET Activa = 0
                WHERE Id = @Id
                  AND Codigo = @Codigo
                  AND Nombre = N'SYNCHRO_TEST_REVOCACION'
                  AND Activa = 1;

                IF @@ROWCOUNT <> 1
                    THROW 55203, 'No se desactivo la terminal.', 1;
                """,
                conexion))
            {
                desactivar.Parameters.Add(
                    "@Id",
                    SqlDbType.Int).Value = terminalId;

                desactivar.Parameters.Add(
                    "@Codigo",
                    SqlDbType.NVarChar,
                    30).Value = codigo;

                await desactivar.ExecuteNonQueryAsync();
            }

            // 5. Reutilizar la MISMA cookie.
            // Debe ser rechazada aunque no haya vencido.
            using var desactivada = await ConsultarSesionAsync(
                cliente, cookie);

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                desactivada.StatusCode);
        }
        finally
        {
            // 6. Eliminar unicamente nuestra terminal ficticia.
            if (terminalId > 0)
            {
                await using var limpiar = new SqlCommand(
                    """
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 55204, 'Base incorrecta.', 1;

                    DELETE FROM dbo.Terminales
                    WHERE Id = @Id
                      AND Codigo = @Codigo
                      AND Nombre = N'SYNCHRO_TEST_REVOCACION';

                    IF @@ROWCOUNT <> 1
                        THROW 55205, 'Limpieza incompleta.', 1;
                    """,
                    conexion);

                limpiar.Parameters.Add(
                    "@Id",
                    SqlDbType.Int).Value = terminalId;

                limpiar.Parameters.Add(
                    "@Codigo",
                    SqlDbType.NVarChar,
                    30).Value = codigo;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}