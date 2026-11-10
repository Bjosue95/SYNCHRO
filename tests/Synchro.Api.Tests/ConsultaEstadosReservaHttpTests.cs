using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Synchro.Api.Tests;

public class ConsultaEstadosReservaHttpTests
{
    private const string Cadena =
        @"Server=(localdb)\MSSQLLocalDB;" +
        "Database=SynchroDB_Pruebas;" +
        "Integrated Security=True;" +
        "Encrypt=True;" +
        "TrustServerCertificate=True;";

    [Theory]
    [InlineData("SinSolicitud")]
    [InlineData("Pendiente")]
    [InlineData("Revocado")]
    [InlineData("Confirmado")]
    [InlineData("Vencida")]
    [InlineData("TelefonoDistinto")]
    [InlineData("Rechazado")]
    [Trait("Category", "LocalDb")]
    public async Task ConsultaDebeRespetarEstadoReal(
        string escenario)
    {
        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();

        Assert.Equal("SynchroDB_Pruebas", conexion.Database);

        byte[] secreto = RandomNumberGenerator.GetBytes(32);
        byte[] hash = SHA256.HashData(secreto);
        string token = Convert.ToHexString(secreto);

        int reservaId = 0;

        try
        {
            // Crear una pre-reserva ficticia.
            await using (var crear = new SqlCommand("""
                INSERT INTO dbo.PreReservasEncomiendasWeb
                (
                    TokenGestionHash,
                    TelefonoRemitente,
                    TelefonoDestinatario,
                    TamanoSolicitado,
                    Estado,
                    FechaCreacion,
                    FechaExpiracion
                )
                OUTPUT INSERTED.Id
                VALUES
                (
                    @Hash,
                    N'+50499990001',
                    N'+50499990002',
                    N'Mediano',
                    N'PendienteConsentimiento',
                    DATEADD(HOUR,
                        CASE WHEN @Vencida = 1 THEN -48 ELSE -1 END,
                        SYSUTCDATETIME()),
                    DATEADD(HOUR,
                        CASE WHEN @Vencida = 1 THEN -24 ELSE 24 END,
                        SYSUTCDATETIME())
                );
                """, conexion))
            {
                crear.Parameters.Add(
                    "@Hash", SqlDbType.VarBinary, 32
                ).Value = hash;

                crear.Parameters.Add(
                    "@Vencida", SqlDbType.Bit
                ).Value = escenario == "Vencida";

                reservaId = Convert.ToInt32(
                    await crear.ExecuteScalarAsync());
            }

            // Crear un consentimiento ficticio,
            // salvo en el escenario SinSolicitud.
            if (escenario != "SinSolicitud")
            {
                bool confirmado =
                    escenario is "Confirmado" or "TelefonoDistinto";

                string estado = escenario switch
                {
                    "Vencida" => "Pendiente",
                    "TelefonoDistinto" => "Confirmado",
                    _ => escenario
                };

                byte[] codigoHash =
                    RandomNumberGenerator.GetBytes(32);

                try
                {
                    await using var crearConsentimiento =
                        new SqlCommand("""
                        INSERT INTO dbo.ConsentimientosPreReservaWhatsApp
                        (
                            ReservaId,
                            TelefonoWhatsApp,
                            CodigoHash,
                            Estado,
                            FechaCreacionUtc,
                            FechaExpiracionUtc,
                            FechaConfirmacionUtc,
                            MensajeIdConfirmacion
                        )
                        VALUES
                        (
                            @Id,
                           @TelefonoWhatsApp,
                            @CodigoHash,
                            @Estado,
                            DATEADD(MINUTE,-5,SYSUTCDATETIME()),
                            DATEADD(MINUTE,10,SYSUTCDATETIME()),
                            CASE WHEN @Confirmado = 1
                                THEN SYSUTCDATETIME()
                                ELSE NULL END,
                            @Mensaje
                        );
                        """, conexion);

                    crearConsentimiento.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = reservaId;

                    crearConsentimiento.Parameters.Add(
                        "@TelefonoWhatsApp",
                        SqlDbType.NVarChar,
                        20
                    ).Value = escenario == "TelefonoDistinto"
                        ? "+50499990003"
                        : "+50499990002";

                    crearConsentimiento.Parameters.Add(
                        "@CodigoHash", SqlDbType.VarBinary, 32
                    ).Value = codigoHash;

                    crearConsentimiento.Parameters.Add(
                        "@Estado", SqlDbType.NVarChar, 20
                    ).Value = estado;

                    crearConsentimiento.Parameters.Add(
                        "@Confirmado", SqlDbType.Bit
                    ).Value = confirmado;

                    crearConsentimiento.Parameters.Add(
                        "@Mensaje", SqlDbType.NVarChar, 255
                    ).Value = confirmado
                        ? "PRUEBA-" + Guid.NewGuid().ToString("N")
                        : DBNull.Value;

                    await crearConsentimiento.ExecuteNonQueryAsync();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(codigoHash);
                }
            }

            // Utilizar la fabrica HTTP ya creada.
            using var fabrica = new FabricaTerminalHttpPruebas();

            using var cliente = fabrica.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false
                });


            // Obtener CSRF con la misma identidad
            // de terminal que usaremos en la consulta.
            using var solicitudCsrf = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/web/encomiendas/csrf-token");

            solicitudCsrf.Headers.Add(
                "X-Synchro-Test-Terminal",
                "autorizada");

            using var respuestaCsrf =
                await cliente.SendAsync(solicitudCsrf);


            Assert.Equal(HttpStatusCode.OK,
                respuestaCsrf.StatusCode);

            using var documentoCsrf = JsonDocument.Parse(
                await respuestaCsrf.Content.ReadAsStringAsync());

            string csrf = documentoCsrf.RootElement
                .GetProperty("csrfToken")
                .GetString()!;

            string cookie = string.Join("; ",
                respuestaCsrf.Headers.GetValues("Set-Cookie")
                    .Select(x => x.Split(';')[0]));

            // Consultar la pre-reserva por la API.
            using var solicitud = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/terminales/encomiendas/reservas/consultar")
            {
                Content = JsonContent.Create(new
                {
                    Referencia = reservaId,
                    TokenGestion = token
                })
            };

            solicitud.Headers.Add(
                "X-Synchro-Test-Terminal", "autorizada");

            solicitud.Headers.Add("X-CSRF-TOKEN", csrf);
            solicitud.Headers.Add("Cookie", cookie);

            using var respuesta =
                await cliente.SendAsync(solicitud);

            Assert.Equal(HttpStatusCode.OK,
                respuesta.StatusCode);

            using var documento = JsonDocument.Parse(
                await respuesta.Content.ReadAsStringAsync());

            var resultado = documento.RootElement;

            bool reservaVigente = escenario != "Vencida";
            bool consentimientoValido = escenario == "Confirmado";

            Assert.Equal(
                reservaVigente,
                resultado.GetProperty("reservaVigente").GetBoolean());

            Assert.Equal(
                consentimientoValido,
                resultado.GetProperty("consentimientoConfirmado")
                    .GetBoolean());

            Assert.Equal(
                reservaVigente && consentimientoValido,
                resultado.GetProperty("puedeContinuarVerificacion")
                    .GetBoolean());

            // El deposito nunca queda habilitado.
            Assert.False(
                resultado.GetProperty("depositoHabilitado")
                    .GetBoolean());

            if (escenario == "Vencida")
                Assert.Equal(
                    "Vencida",
                    resultado.GetProperty("estado").GetString());
        }
        finally
        {
            // Eliminar exclusivamente los registros ficticios.
            if (reservaId > 0)
            {
                await using var limpiar = new SqlCommand("""
                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 55001, 'Base incorrecta.', 1;

                    BEGIN TRANSACTION;

                    DELETE FROM dbo.ConsentimientosPreReservaWhatsApp
                    WHERE ReservaId = @Id;

                    DELETE FROM dbo.PreReservasEncomiendasWeb
                    WHERE Id = @Id AND TokenGestionHash = @Hash;

                    COMMIT TRANSACTION;
                    """, conexion);

                limpiar.Parameters.Add(
                    "@Id", SqlDbType.Int
                ).Value = reservaId;

                limpiar.Parameters.Add(
                    "@Hash", SqlDbType.VarBinary, 32
                ).Value = hash;

                await limpiar.ExecuteNonQueryAsync();
            }

            CryptographicOperations.ZeroMemory(secreto);
            CryptographicOperations.ZeroMemory(hash);
        }
    }
}
