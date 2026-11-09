using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

// Solo se registra en Development con SynchroDB_Pruebas.
// NO ES un flujo de recuperacion de produccion: requiere verificacion manual del titular.
public record RecuperacionPinPersonalSolicitud(
    int UsoId,
    string? Motivo,
    bool TitularVerificado
);

public static class RecuperacionPinPersonalEndpoints
{
    public static void MapRecuperacionPinPersonalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/retiros-personales/recuperar-pin", async (
            RecuperacionPinPersonalSolicitud solicitud,
            HttpContext contexto,
            IAntiforgery antiforgery) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";

            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "Se requiere HTTPS." });

            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest(new { mensaje = "Token CSRF invalido o ausente." });

            if (!solicitud.TitularVerificado)
                return Results.BadRequest(new { mensaje = "Se requiere verificacion presencial previa del titular." });

            string motivo = solicitud.Motivo?.Trim() ?? "";
            if (solicitud.UsoId <= 0 || motivo.Length < 12 || motivo.Length > 200)
                return Results.BadRequest(new { mensaje = "Identificador o motivo invalidos (12 a 200 caracteres)." });

            string? administradorTexto = contexto.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(administradorTexto, out int administradorId) || administradorId <= 0)
                return Results.Unauthorized();

            string? cadena = app.Configuration.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena) ||
                !string.Equals(new SqlConnectionStringBuilder(cadena).InitialCatalog,
                    "SynchroDB_Pruebas", StringComparison.OrdinalIgnoreCase))
                return Results.Problem("Conexion de pruebas no configurada.");

            string pin = CodigoPinRetiroPersonal.Generar();
            byte[] salt = CodigoPinRetiroPersonal.GenerarSalt();
            byte[]? hash = null;

            try
            {
                hash = CodigoPinRetiroPersonal.CalcularHash(
                    pin, solicitud.UsoId, salt, app.Configuration);

                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync(contexto.RequestAborted);

                await using var comando = new SqlCommand(
                    "dbo.RecuperarPinPersonalSynchro", conexion)
                {
                    CommandType = CommandType.StoredProcedure
                };
                comando.Parameters.Add("@UsoId", SqlDbType.Int).Value = solicitud.UsoId;
                comando.Parameters.Add("@AdministradorId", SqlDbType.Int).Value = administradorId;
                comando.Parameters.Add("@PinHash", SqlDbType.VarBinary, 32).Value = hash;
                comando.Parameters.Add("@PinSalt", SqlDbType.VarBinary, 32).Value = salt;
                comando.Parameters.Add("@Motivo", SqlDbType.NVarChar, 200).Value = motivo;

                await using var lector = await comando.ExecuteReaderAsync(contexto.RequestAborted);
                if (!await lector.ReadAsync(contexto.RequestAborted))
                    return Results.Problem("No se obtuvo confirmacion de recuperacion.");

                return Results.Ok(new
                {
                    mensaje = "PIN de retiro personal emitido de forma autorizada.",
                    idUso = lector.GetInt32(lector.GetOrdinal("IdUso")),
                    codigoCasillero = lector.GetString(lector.GetOrdinal("CodigoCasillero")),
                    pinRetiro = pin, // SOLO esta respuesta, sin almacenamiento en logs.
                    vencimientoUtc = DateTime.SpecifyKind(
                        lector.GetDateTime(lector.GetOrdinal("FechaVencimiento")),
                        DateTimeKind.Utc),
                    auditoriaId = lector.GetInt32(lector.GetOrdinal("AuditoriaId"))
                });
            }
            catch (SqlException ex) when (ex.Number >= 50500 && ex.Number <= 50520)
            {
                return Results.Conflict(new
                {
                    mensaje = "La reserva no permite recuperar el PIN en su estado actual o el administrador no esta activo."
                });
            }
            catch (SqlException)
            {
                return Results.Problem("No se pudo registrar la recuperacion del PIN.");
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("La clave HMAC de retiro personal no esta configurada.");
            }
            catch (FormatException)
            {
                return Results.Problem("La configuracion HMAC es invalida.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(salt);
                if (hash is not null)
                    CryptographicOperations.ZeroMemory(hash);
            }
        })
        .RequireAuthorization("SoloAdministradores")
        .RequireRateLimiting("LoginLimitado");
    }
}
