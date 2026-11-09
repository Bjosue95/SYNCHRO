using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public record RetiroPersonalSolicitud(int UsoId, string? Pin);

public static class RetirosPersonalesTerminalEndpoints
{
    public static void MapRetirosPersonalesTerminalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/terminales/retiros-personales", async (
            RetiroPersonalSolicitud solicitud,
            HttpContext contexto,
            IAntiforgery antiforgery) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "Se requiere HTTPS." });

            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest(new { mensaje = "Token CSRF invalido o ausente." });

            if (solicitud.UsoId <= 0 || solicitud.Pin is null ||
                solicitud.Pin.Length != 6 ||
                solicitud.Pin.Any(c => c < '0' || c > '9'))
                return Results.BadRequest(new { mensaje = "Datos de retiro invalidos." });

            var cadena = app.Configuration.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena) ||
                !string.Equals(new SqlConnectionStringBuilder(cadena).InitialCatalog,
                    "SynchroDB_Pruebas", StringComparison.OrdinalIgnoreCase))
                return Results.Problem("La conexion de pruebas no esta configurada.");

            byte[]? salt = null;
            byte[]? hash = null;
            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync(contexto.RequestAborted);

                // El salt NO es un secreto. Su lectura no cambia el contador de intentos.
                // El procedimiento SQL valida de nuevo todos los estados bajo bloqueo.
                await using (var buscar = new SqlCommand(@"
                    SELECT p.PinSalt
                    FROM dbo.CodigosRetiroPersonal AS p
                    INNER JOIN dbo.UsosCasillero AS u ON u.Id = p.UsoId
                    WHERE p.UsoId = @UsoId
                      AND u.TipoServicio = N'ResguardoPersonal'
                      AND u.OrigenReserva = N'Terminal'
                      AND u.Estado = N'Ocupado'
                      AND u.FechaRetiro IS NULL", conexion))
                {
                    buscar.Parameters.Add("@UsoId", SqlDbType.Int).Value = solicitud.UsoId;
                    salt = await buscar.ExecuteScalarAsync(contexto.RequestAborted) as byte[];
                }

                if (salt is null || salt.Length != 32)
                    return Results.BadRequest(new
                    {
                        mensaje = "El retiro no esta disponible o los datos son invalidos."
                    });

                hash = CodigoPinRetiroPersonal.CalcularHash(
                    solicitud.Pin, solicitud.UsoId, salt, app.Configuration);

                await using var comando = new SqlCommand(
                    "dbo.RegistrarRetiroPersonalSynchro", conexion)
                {
                    CommandType = CommandType.StoredProcedure
                };
                comando.Parameters.Add("@UsoId", SqlDbType.Int).Value = solicitud.UsoId;
                comando.Parameters.Add("@PinHashCandidato", SqlDbType.VarBinary, 32).Value = hash;
                comando.Parameters.Add("@PinSaltLeido", SqlDbType.VarBinary, 32).Value = salt;

                await using var lector = await comando.ExecuteReaderAsync(contexto.RequestAborted);
                if (!await lector.ReadAsync(contexto.RequestAborted))
                    return Results.Problem("No se obtuvo respuesta del retiro.");

                if (!lector.GetBoolean(lector.GetOrdinal("RetiroConfirmado")))
                    return Results.BadRequest(new
                    {
                        mensaje = "El retiro no esta disponible o el PIN es incorrecto o esta bloqueado."
                    });

                return Results.Ok(new
                {
                    idUso = lector.GetInt32(lector.GetOrdinal("IdUso")),
                    codigoCasillero = lector.GetString(lector.GetOrdinal("CodigoCasillero")),
                    estadoCasillero = lector.GetString(lector.GetOrdinal("EstadoCasillero")),
                    fechaRetiro = DateTime.SpecifyKind(
                        lector.GetDateTime(lector.GetOrdinal("FechaRetiro")),
                        DateTimeKind.Utc)
                });
            }
            catch (SqlException ex) when (ex.Number >= 50401 && ex.Number <= 50412)
            {
                return Results.BadRequest(new
                {
                    mensaje = "El retiro no esta disponible o los datos son invalidos."
                });
            }
            catch (SqlException)
            {
                return Results.Problem("Error al registrar el retiro.");
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("No se pudo verificar el PIN de retiro.");
            }
            catch (FormatException)
            {
                return Results.Problem("Configuracion HMAC invalida.");
            }
            finally
            {
                if (salt is not null)
                    CryptographicOperations.ZeroMemory(salt);
                if (hash is not null)
                    CryptographicOperations.ZeroMemory(hash);
            }
        })
        .RequireAuthorization("SoloTerminal")
        .RequireRateLimiting("LoginLimitado");
    }
}
