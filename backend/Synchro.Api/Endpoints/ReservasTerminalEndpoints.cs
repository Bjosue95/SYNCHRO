
using System.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public record ReservaTerminalSolicitud(
    string? CodigoCasillero
);

public static class ReservasTerminalEndpoints
{
    public static void MapReservasTerminalEndpoints(
        this WebApplication app)
    {
        app.MapPost(
            "/api/terminales/reservas",
            async (
                ReservaTerminalSolicitud solicitud,
                HttpContext contexto,
                IAntiforgery antiforgery) =>
            {
                // Requerir una conexion segura
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                // Proteger contra solicitudes CSRF
                if (!await antiforgery.IsRequestValidAsync(contexto))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido o ausente."
                    });
                }

                var codigo = solicitud.CodigoCasillero?.Trim();

                if (string.IsNullOrWhiteSpace(codigo)
                    || codigo.Length > 10)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Codigo de casillero invalido."
                    });
                }

                var cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.Problem(
                        "Conexion a la base de datos no configurada.");
                }

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync();

                    await using var comando = new SqlCommand(
                        "dbo.ReservarCasilleroSynchro",
                        conexion);

                    comando.CommandType = CommandType.StoredProcedure;

                    comando.Parameters.Add(
                        "@CodigoCasillero",
                        SqlDbType.NVarChar,
                        10).Value = codigo;

                    // Por ahora, solo resguardo personal
                    comando.Parameters.Add(
                        "@TipoServicio",
                        SqlDbType.NVarChar,
                        20).Value = "ResguardoPersonal";

                    // Activa el vencimiento de 10 minutos
                    comando.Parameters.Add(
                        "@OrigenReserva",
                        SqlDbType.NVarChar,
                        20).Value = "Terminal";

                    await using var lector =
                        await comando.ExecuteReaderAsync();

                    if (!await lector.ReadAsync())
                    {
                        return Results.Problem(
                            "No se obtuvo la reserva.");
                    }


                    return Results.Ok(new
                    {
                        idUso = lector["IdUso"],
                        codigoCasillero = lector["CodigoCasillero"],
                        tipoServicio = lector["TipoServicio"],
                        tamano = lector["Tamano"],
                        estado = lector["Estado"],
                        fechaReserva = lector["FechaReserva"],

                        fechaVencimiento = DateTime.SpecifyKind(
                            lector.GetDateTime(
                                lector.GetOrdinal("FechaVencimiento")
                            ),
                            DateTimeKind.Utc
                        )
                    });

                }
                catch (SqlException ex) when (
                    ex.Number == 50020 ||
                    ex.Number == 50021 ||
                    ex.Number == 50024)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = ex.Message
                    });
                }
                catch (SqlException ex) when (ex.Number == 50022)
                {
                    return Results.NotFound(new
                    {
                        mensaje = "Casillero no encontrado."
                    });
                }
                catch (SqlException ex) when (
                    ex.Number == 50023 ||
                    ex.Number == 50025)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "Casillero no disponible."
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No se pudo registrar la reserva.");
                }
            })
            .RequireAuthorization("SoloTerminal")
            .RequireRateLimiting("LoginLimitado");
    }
}