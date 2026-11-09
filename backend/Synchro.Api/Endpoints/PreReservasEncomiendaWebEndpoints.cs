using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

// Pre-reserva, no asignacion de casillero. No autoriza deposito.
public record CrearPreReservaEncomiendaWebSolicitud(
    string? TelefonoRemitente,
    string? TelefonoDestinatario,
    string? TamanoSolicitado);

public static class PreReservasEncomiendaWebEndpoints
{
    private static readonly Regex TelefonoRegex = new(
        @"^\+?[1-9][0-9]{7,14}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static void MapPreReservasEncomiendaWebEndpoints(this WebApplication app)
    {
        // El modulo solo se registra desde Program.cs en Development + SynchroDB_Pruebas.
        app.MapGet("/api/web/encomiendas/csrf-token", (
            HttpContext contexto, IAntiforgery antiforgery) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "HTTPS obligatorio." });

            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new {
                csrfToken = antiforgery.GetAndStoreTokens(contexto).RequestToken
            });
        }).RequireRateLimiting("LoginLimitado");

        app.MapGet("/api/web/encomiendas/integraciones", (HttpContext contexto) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new {
                whatsappRealConectado = false,
                reconocimientoFacialConectado = false,
                depositoHabilitado = false,
                mensaje = "Pre-reservas de prueba. Falta consentimiento y verificacion reales por WhatsApp."
            });
        });

        app.MapPost("/api/web/encomiendas/pre-reservas", async (
            CrearPreReservaEncomiendaWebSolicitud solicitud,
            HttpContext contexto,
            IAntiforgery antiforgery) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "HTTPS obligatorio." });

            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest(new { mensaje = "CSRF invalido o ausente." });

            var remitente = solicitud.TelefonoRemitente?.Trim();
            var destinatario = solicitud.TelefonoDestinatario?.Trim();
            var tamano = solicitud.TamanoSolicitado?.Trim();

            if (remitente is null || destinatario is null ||
                remitente.Length > 20 || destinatario.Length > 20 ||
                !TelefonoRegex.IsMatch(remitente) || !TelefonoRegex.IsMatch(destinatario) ||
                tamano is not ("Pequeno" or "Mediano" or "Grande"))
                return Results.BadRequest(new {
                    mensaje = "Telefonos o tamano invalidos. Indica telefonos con codigo de pais cuando corresponda."
                });

            var cadena = app.Configuration.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem("Conexion SQL no configurada.");

            // Se entrega este token UNA vez; solo su SHA-256 se guarda en SQL.
            byte[] bytesToken = RandomNumberGenerator.GetBytes(32);
            string token = Convert.ToHexString(bytesToken);
            byte[] hash = SHA256.HashData(bytesToken);
            CryptographicOperations.ZeroMemory(bytesToken);

            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync(contexto.RequestAborted);
                await using var comando = new SqlCommand("""
                    INSERT INTO dbo.PreReservasEncomiendasWeb
                    (TokenGestionHash,TelefonoRemitente,TelefonoDestinatario,
                     TamanoSolicitado,FechaExpiracion)
                    OUTPUT INSERTED.Id, INSERTED.FechaExpiracion
                    VALUES (@hash,@remitente,@destinatario,@tamano,
                            DATEADD(HOUR,24,SYSUTCDATETIME()));
                    """, conexion);
                comando.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = hash;
                comando.Parameters.Add("@remitente", SqlDbType.NVarChar, 20).Value = remitente;
                comando.Parameters.Add("@destinatario", SqlDbType.NVarChar, 20).Value = destinatario;
                comando.Parameters.Add("@tamano", SqlDbType.NVarChar, 12).Value = tamano;

                await using var lector = await comando.ExecuteReaderAsync(contexto.RequestAborted);
                if (!await lector.ReadAsync(contexto.RequestAborted))
                    return Results.Problem("No se pudo crear la pre-reserva.");

                contexto.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new {
                    id = lector.GetInt32(0),
                    tokenGestion = token,
                    estado = "PendienteConsentimiento",
                    fechaExpiracion = DateTime.SpecifyKind(lector.GetDateTime(1),DateTimeKind.Utc),
                    depositoHabilitado = false,
                    mensaje = "Pre-reserva creada. No es un deposito ni una autorizacion de WhatsApp."
                });
            }
            catch (SqlException)
            {
                return Results.Problem("No se pudo registrar la pre-reserva en SQL Server.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }).RequireRateLimiting("LoginLimitado");

        app.MapGet("/api/web/encomiendas/pre-reservas/{id:int}", async (
            int id, HttpContext contexto) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "HTTPS obligatorio." });
            if (id <= 0)
                return Results.BadRequest(new { mensaje = "Referencia invalida." });

            // Se evita incluir el token secreto en rutas o parametros de URL.
            string? token = contexto.Request.Headers["X-SYNCHRO-RESERVA"].FirstOrDefault();
            if (token is null || token.Length != 64 || !Regex.IsMatch(token, "\\A[0-9A-Fa-f]{64}\\z"))
                return Results.NotFound(new { mensaje = "Pre-reserva no encontrada." });

            byte[] bytesToken = Convert.FromHexString(token);
            byte[] hash = SHA256.HashData(bytesToken);
            CryptographicOperations.ZeroMemory(bytesToken);
            var cadena = app.Configuration.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem("Conexion SQL no configurada.");

            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync(contexto.RequestAborted);
                await using var comando = new SqlCommand("""
                    SELECT TamanoSolicitado, Estado, FechaExpiracion
                    FROM dbo.PreReservasEncomiendasWeb
                    WHERE Id = @id AND TokenGestionHash = @hash;
                    """, conexion);
                comando.Parameters.Add("@id", SqlDbType.Int).Value = id;
                comando.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = hash;
                await using var lector = await comando.ExecuteReaderAsync(contexto.RequestAborted);
                if (!await lector.ReadAsync(contexto.RequestAborted))
                    return Results.NotFound(new { mensaje = "Pre-reserva no encontrada." });

                var estado = lector.GetString(1);
                var vence = DateTime.SpecifyKind(lector.GetDateTime(2), DateTimeKind.Utc);
                contexto.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new {
                    id,
                    tamanoSolicitado = lector.GetString(0),
                    estado = vence <= DateTime.UtcNow ? "Vencida" : estado,
                    fechaExpiracion = vence,
                    depositoHabilitado = false,
                    mensaje = "Pendiente de integracion real de WhatsApp y verificacion facial."
                });
            }
            catch (SqlException)
            {
                return Results.Problem("No se pudo consultar la pre-reserva.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }).RequireRateLimiting("LoginLimitado");

        app.MapGet("/api/admin/encomiendas/pre-reservas", async (
            HttpContext contexto) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "HTTPS obligatorio." });
            var cadena = app.Configuration.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem("Conexion SQL no configurada.");
            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync(contexto.RequestAborted);
                await using var comando = new SqlCommand("""
                    SELECT TOP (30) Id, TamanoSolicitado, Estado,
                           FechaCreacion, FechaExpiracion,
                           RIGHT(TelefonoDestinatario, 4) AS UltimosCuatro
                    FROM dbo.PreReservasEncomiendasWeb
                    ORDER BY Id DESC;
                    """, conexion);
                var lista = new List<object>();
                await using var lector = await comando.ExecuteReaderAsync(contexto.RequestAborted);
                while (await lector.ReadAsync(contexto.RequestAborted))
                {
                    lista.Add(new {
                        id = lector.GetInt32(0),
                        tamanoSolicitado = lector.GetString(1),
                        estado = lector.GetString(2),
                        fechaCreacion = DateTime.SpecifyKind(lector.GetDateTime(3),DateTimeKind.Utc),
                        fechaExpiracion = DateTime.SpecifyKind(lector.GetDateTime(4),DateTimeKind.Utc),
                        destinatarioUltimos4 = lector.GetString(5)
                    });
                }
                contexto.Response.Headers.CacheControl = "no-store";
                return Results.Ok(lista);
            }
            catch (SqlException)
            {
                return Results.Problem("No se pudieron consultar las pre-reservas.");
            }
        }).RequireAuthorization("SoloAdministradores");
    }
}
