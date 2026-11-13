
using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class WhatsAppWebhookInicioHttpTests
{
    [SqlDepositoFact]
    public async Task FirmaValidaRegistraUnaSolaSolicitud()
    {
        string cadena = ConexionSqlAislada.Exigir(
            Environment.GetEnvironmentVariable(
                "SYNCHRO_SQL_DEPOSITO_TEST"),
            "SynchroDB_Pruebas");

        const string secreto = "secreto-ficticio-exclusivo-pruebas";
        const string numeroMeta = "123456789012345";

        string numeroOrigen = "999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000,
                2_000_000_000).ToString();

        string telefono = "+" + numeroOrigen;

        string usuario = "test_webhook_" +
            Guid.NewGuid().ToString("N");

        string mensajeId = "wamid.test." +
            Guid.NewGuid().ToString("N");

        int remitenteId = 0;
        int reservaId = 0;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        try
        {
            // Preparar datos ficticios en la base aislada.
            await using (var preparar = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57080, 'Base incorrecta.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57081, 'Falta marca de pruebas.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    INSERT dbo.Personas
                        (TelefonoWhatsapp, TieneCuenta)
                    VALUES (N'+999000000001', 1);

                    DECLARE @RemitenteId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ClientesWebPrototipo
                        (PersonaId, NombreUsuario,
                         Telefono, PasswordHash)
                    VALUES
                        (@RemitenteId, @Usuario,
                         N'+999000000001',
                         N'HASH_FICTICIO_NO_AUTENTICABLE');

                    INSERT dbo.PreReservasEncomiendasWeb
                        (TokenGestionHash,
                         TelefonoRemitente,
                         TelefonoDestinatario,
                         TamanoSolicitado,
                         FechaExpiracion,
                         ClientePersonaId)
                    VALUES
                        (CRYPT_GEN_RANDOM(32),
                         N'+999000000001',
                         @Telefono,
                         N'Mediano',
                         DATEADD(HOUR, 1, SYSUTCDATETIME()),
                         @RemitenteId);

                    DECLARE @ReservaId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ConsentimientosPreReservaWhatsApp
                        (ReservaId,
                         TelefonoWhatsApp,
                         CodigoHash,
                         Estado,
                         FechaCreacionUtc,
                         FechaExpiracionUtc,
                         FechaConfirmacionUtc,
                         MensajeIdConfirmacion)
                    VALUES
                        (@ReservaId,
                         @Telefono,
                         CRYPT_GEN_RANDOM(32),
                         N'Confirmado',
                         DATEADD(MINUTE, -2, SYSUTCDATETIME()),
                         DATEADD(MINUTE, 30, SYSUTCDATETIME()),
                         DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                         N'CONS-' + @Usuario);

                    COMMIT TRANSACTION;

                    SELECT @RemitenteId, @ReservaId;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                """, conexion))
            {
                preparar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                preparar.Parameters.Add(
                    "@Telefono", SqlDbType.NVarChar, 20
                ).Value = telefono;

                await using var lector =
                    await preparar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                remitenteId = lector.GetInt32(0);
                reservaId = lector.GetInt32(1);
            }

            // Simular el formato de un evento recibido de Meta.
            var evento = new
            {
                @object = "whatsapp_business_account",
                entry = new[]
                {
                    new
                    {
                        changes = new[]
                        {
                            new
                            {
                                field = "messages",
                                value = new
                                {
                                    metadata = new
                                    {
                                        phone_number_id = numeroMeta
                                    },
                                    messages = new[]
                                    {
                                        new
                                        {
                                            id = mensajeId,
                                            from = numeroOrigen,
                                            type = "text",
                                            text = new
                                            {
                                                body =
                                                    $"SYNCHRO INICIAR {reservaId}"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            byte[] cuerpo = JsonSerializer.SerializeToUtf8Bytes(
                evento);

            byte[] firmaBytes = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secreto),
                cuerpo);

            string firma = "sha256=" +
                Convert.ToHexString(firmaBytes)
                    .ToLowerInvariant();

            CryptographicOperations.ZeroMemory(firmaBytes);

            // Utilizar la aplicacion real, pero con
            // configuracion exclusiva de las pruebas.
            using var fabrica =
                new FabricaTerminalHttpPruebas()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration(
                        (_, configuracion) =>
                        {
                            configuracion.AddInMemoryCollection(
                                new Dictionary<string, string?>
                                {
                                    ["WhatsApp:AppSecret"] =
                                        secreto,
                                    ["WhatsApp:PhoneNumberId"] =
                                        numeroMeta,
                                    ["WhatsApp:SolicitudesInicioHabilitadas"] =
                                        "true",
                                    ["WhatsApp:Outbox:Habilitado"] =
                                        "false"
                                });
                        });
                });

            using var cliente = fabrica.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    BaseAddress =
                        new Uri("https://localhost"),
                    AllowAutoRedirect = false
                });

            async Task<HttpStatusCode> EnviarAsync(
                string firmaSolicitud)
            {
                using var solicitud = new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/webhooks/whatsapp");

                solicitud.Content = new ByteArrayContent(cuerpo);

                solicitud.Content.Headers.ContentType =
                    new MediaTypeHeaderValue(
                        "application/json");

                solicitud.Headers.TryAddWithoutValidation(
                    "X-Hub-Signature-256",
                    firmaSolicitud);

                using var respuesta =
                    await cliente.SendAsync(solicitud);

                return respuesta.StatusCode;
            }

            // 1. Una firma incorrecta no se acepta.
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                await EnviarAsync(
                    "sha256=" + new string('0', 64)));

            // 2. Una firma correcta permite registrar.
            Assert.Equal(
                HttpStatusCode.OK,
                await EnviarAsync(firma));

            // 3. Repetir el mismo evento no lo duplica.
            Assert.Equal(
                HttpStatusCode.OK,
                await EnviarAsync(firma));

            // Comprobar directamente SQL Server.
            await using var comprobar = new SqlCommand("""
                SELECT
                    (
                        SELECT COUNT(*)
                        FROM dbo.WhatsAppWebhookEventos
                        WHERE MensajeId = @MensajeId
                    ) AS Eventos,
                    (
                        SELECT COUNT(*)
                        FROM dbo.SolicitudesInicioSesionWhatsApp s
                        INNER JOIN dbo.WhatsAppWebhookEventos e
                            ON e.Id = s.EventoId
                        WHERE e.MensajeId = @MensajeId
                          AND s.ReservaId = @ReservaId
                    ) AS Solicitudes;
                """, conexion);

            comprobar.Parameters.Add(
                "@MensajeId", SqlDbType.NVarChar, 255
            ).Value = mensajeId;

            comprobar.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaId;

            await using var resultado =
                await comprobar.ExecuteReaderAsync();

            Assert.True(await resultado.ReadAsync());

            Assert.Equal(1, resultado.GetInt32(0));
            Assert.Equal(1, resultado.GetInt32(1));
        }
        finally
        {
            // Limpiar exclusivamente los datos de esta prueba.
            if (remitenteId > 0 && reservaId > 0)
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 57082, 'Base incorrecta.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DELETE s
                        FROM dbo.SolicitudesInicioSesionWhatsApp s
                        INNER JOIN dbo.WhatsAppWebhookEventos e
                            ON e.Id = s.EventoId
                        WHERE e.MensajeId = @MensajeId;

                        DELETE dbo.WhatsAppWebhookEventos
                        WHERE MensajeId = @MensajeId;

                        DELETE dbo.ConsentimientosPreReservaWhatsApp
                        WHERE ReservaId = @ReservaId;

                        DELETE dbo.PreReservasEncomiendasWeb
                        WHERE Id = @ReservaId
                          AND ClientePersonaId = @RemitenteId;

                        DELETE dbo.ClientesWebPrototipo
                        WHERE PersonaId = @RemitenteId
                          AND NombreUsuario = @Usuario;

                        DELETE dbo.Personas
                        WHERE Id = @RemitenteId
                          AND TelefonoWhatsapp = N'+999000000001';

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF XACT_STATE() <> 0
                            ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH;
                    """, conexion);

                limpiar.Parameters.Add(
                    "@MensajeId", SqlDbType.NVarChar, 255
                ).Value = mensajeId;

                limpiar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                limpiar.Parameters.Add(
                    "@RemitenteId", SqlDbType.Int
                ).Value = remitenteId;

                limpiar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}
