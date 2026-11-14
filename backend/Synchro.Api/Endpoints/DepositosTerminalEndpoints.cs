using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public record DepositoPersonalSolicitud(int UsoId, string? TokenDeposito);

public static class DepositosTerminalEndpoints
{
    public static void MapDepositosTerminalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/terminales/depositos", async (
            DepositoPersonalSolicitud solicitud,
            HttpContext contexto,
            IAntiforgery antiforgery) =>
        {
            // Una respuesta que incluye un PIN temporal no debe guardarse en cache.
            contexto.Response.Headers.CacheControl = "no-store";

            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "Se requiere HTTPS." });

            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest(new { mensaje = "Token CSRF invalido o ausente." });

            if (app.Configuration.GetValue<bool>("ServiciosTemporales:Habilitado"))
                return Results.Conflict(new { mensaje = "Utilice el flujo de servicios temporales con prepago." });

            if (solicitud.UsoId <= 0 ||
                solicitud.TokenDeposito is null ||
                solicitud.TokenDeposito.Length != 64)
                return Results.BadRequest(new { mensaje = "Datos de deposito invalidos." });

            byte[] token;
            try
            {
                token = Convert.FromHexString(solicitud.TokenDeposito);
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { mensaje = "Token de deposito invalido." });
            }

            var cadena = app.Configuration.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena) ||
                !string.Equals(new SqlConnectionStringBuilder(cadena).InitialCatalog,
                    "SynchroDB_Pruebas", StringComparison.OrdinalIgnoreCase))
            {
                CryptographicOperations.ZeroMemory(token);
                return Results.Problem("La conexion de pruebas no esta configurada.");
            }

            // El PIN solo se devuelve al navegador cuando SQL confirma el deposito.
            // SQL recibe el hash HMAC y un salt, nunca el PIN en texto claro.
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
                    "dbo.RegistrarDepositoPersonalSynchro", conexion)
                {
                    CommandType = CommandType.StoredProcedure
                };

                comando.Parameters.Add("@UsoId", SqlDbType.Int).Value = solicitud.UsoId;
                comando.Parameters.Add("@TokenDeposito", SqlDbType.VarBinary, 32).Value = token;
                comando.Parameters.Add("@PinHash", SqlDbType.VarBinary, 32).Value = hash;
                comando.Parameters.Add("@PinSalt", SqlDbType.VarBinary, 32).Value = salt;

                await using var lector = await comando.ExecuteReaderAsync(contexto.RequestAborted);
                if (!await lector.ReadAsync(contexto.RequestAborted))
                    return Results.Problem("No se obtuvo confirmacion del deposito.");

                return Results.Ok(new
                {
                    idUso = lector.GetInt32(lector.GetOrdinal("IdUso")),
                    codigoCasillero = lector.GetString(lector.GetOrdinal("CodigoCasillero")),
                    tipoServicio = lector.GetString(lector.GetOrdinal("TipoServicio")),
                    estadoOperacion = lector.GetString(lector.GetOrdinal("EstadoOperacion")),
                    estadoCasillero = lector.GetString(lector.GetOrdinal("EstadoCasillero")),
                    fechaIngreso = DateTime.SpecifyKind(
                        lector.GetDateTime(lector.GetOrdinal("FechaIngreso")),
                        DateTimeKind.Utc),
                    pinRetiro = pin // Mostrar solo una vez; NO registrar en logs.
                });
            }
            catch (SqlException ex) when (ex.Number == 50202)
            {
                return Results.NotFound(new { mensaje = "Reserva no encontrada." });
            }
            catch (SqlException ex) when (
                (ex.Number >= 50203 && ex.Number <= 50212) ||
                (ex.Number >= 50301 && ex.Number <= 50304))
            {
                return Results.Conflict(new
                {
                    mensaje = "No se pudo confirmar el deposito. Verifica la reserva y su vigencia."
                });
            }
            catch (SqlException)
            {
                return Results.Problem("Error al registrar el deposito.");
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("No se pudo preparar el PIN de retiro.");
            }
            catch (FormatException)
            {
                return Results.Problem("Configuracion HMAC invalida.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(token);
                CryptographicOperations.ZeroMemory(salt);
                if (hash is not null)
                    CryptographicOperations.ZeroMemory(hash);
            }
        })
        .RequireAuthorization("SoloTerminal")
        .RequireRateLimiting("LoginLimitado");
    }
}
