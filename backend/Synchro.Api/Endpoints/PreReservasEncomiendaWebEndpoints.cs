using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Endpoints;

// Pre-reserva, no asignacion de casillero. No autoriza deposito.
public record CrearPreReservaEncomiendaWebSolicitud(
    string? TelefonoDestinatario,
    string? TamanoSolicitado);

public static class PreReservasEncomiendaWebEndpoints
{
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

        app.MapGet("/api/web/encomiendas/integraciones", (HttpContext contexto, IVerificadorFacial verificador) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new {
                whatsappRealConectado = false,
                reconocimientoFacialConectado = verificador is VerificadorFacialSimulado,
                reconocimientoFacialSimulado = true,
                reservasSoloClientesWeb = true,
                depositoHabilitado = false,
                mensaje = "Prototipo: rostro simulado; cada deposito exige reserva web de cliente, consentimiento WhatsApp y referencias de identidad."
            });
        });

        app.MapPost("/api/web/encomiendas/pre-reservas", async (
            CrearPreReservaEncomiendaWebSolicitud solicitud,
            HttpContext contexto,
            IAntiforgery antiforgery,
            IPreReservasEncomienda servicio) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "HTTPS obligatorio." });

            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest(new { mensaje = "CSRF invalido o ausente." });

            contexto.Response.Headers.CacheControl = "no-store";
            try
            {
                int cliente=int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var creada=await servicio.CrearAsync(cliente,solicitud.TelefonoDestinatario,
                    solicitud.TamanoSolicitado,contexto.RequestAborted);
                return Results.Ok(new
                {
                    id=creada.Id,tokenGestion=creada.TokenGestion,estado="PendienteConsentimiento",
                    fechaExpiracion=creada.FechaExpiracion,depositoHabilitado=false,
                    mensaje="Pre-reserva creada. No es un deposito ni una autorizacion de WhatsApp."
                });
            }
            catch(EncomiendaNoDisponibleException){return Results.NotFound();}
            catch(ArgumentException){return Results.BadRequest(new{mensaje="Telefono destinatario o tamano invalidos."});}
            catch(SqlException){return Results.Problem(statusCode:503,title:"No se pudo registrar la pre-reserva.");}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Servicio de pre-reservas no disponible.");}
        }).RequireAuthorization("SoloClientes").RequireRateLimiting("LoginLimitado");

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
                    WHERE Id = @id AND TokenGestionHash = @hash AND ClientePersonaId=@cliente;
                    """, conexion);
                comando.Parameters.Add("@cliente",SqlDbType.Int).Value=int.Parse(contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
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
                    mensaje = "El deposito requiere consentimiento WhatsApp e identidad del remitente comprobada mediante captura simulada."
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
        }).RequireAuthorization("SoloClientes").RequireRateLimiting("LoginLimitado");

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
