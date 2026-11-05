using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Synchro.Api.Models;
using Synchro.Api.Endpoints;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Synchro.Api.Endpoints;

public static class ReservasDemoEndpoints
{
    public static void MapReservasDemoEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/demo/reservas/casilleros",
                async (
                    ReservarCasilleroSolicitud solicitud,
                    HttpContext contexto,
                    IAntiforgery antiforgery) =>
                {
                    if (!contexto.Request.IsHttps)
                        return Results.BadRequest(new
                        {
                            mensaje = "Se requiere HTTPS."
                        });

                    if (!await antiforgery.IsRequestValidAsync(contexto))
                        return Results.BadRequest(new
                        {
                            mensaje = "Token CSRF invalido o ausente."
                        });

                    if (string.IsNullOrWhiteSpace(solicitud.CodigoCasillero)
                        || string.IsNullOrWhiteSpace(solicitud.TipoServicio))
                        return Results.BadRequest(new
                        {
                            mensaje = "Codigo y servicio son obligatorios."
                        });

                    var cadena = app.Configuration
                        .GetConnectionString("SynchroDB");

                    if (string.IsNullOrWhiteSpace(cadena))
                        return Results.Problem(
                            "No se encontro la conexion a SynchroDB.");

                    try
                    {
                        await using var conexion = new SqlConnection(cadena);
                        await conexion.OpenAsync();

                        await using var comando = new SqlCommand(
                            "dbo.ReservarCasilleroSynchro", conexion);

                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@CodigoCasillero",
                            System.Data.SqlDbType.NVarChar, 10
                        ).Value = solicitud.CodigoCasillero;

                        comando.Parameters.Add(
                            "@TipoServicio",
                            System.Data.SqlDbType.NVarChar, 20
                        ).Value = solicitud.TipoServicio;

                        await using var lector =
                            await comando.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                            return Results.Problem(
                                "No se obtuvo la reserva.");

                        return Results.Ok(new
                        {
                            idUso = lector["IdUso"],
                            codigoCasillero = lector["CodigoCasillero"],
                            tipoServicio = lector["TipoServicio"],
                            tipoCasillero = lector["TipoCasillero"],
                            tamano = lector["Tamano"],
                            estado = lector["Estado"],
                            fechaReserva = lector["FechaReserva"]
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
                            "Error al registrar la reserva.");
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/demo/reservas/{id:int}/cancelar",
                async (
                    int id,
                    HttpContext contexto,
                    IAntiforgery antiforgery) =>
                {
                    if (!contexto.Request.IsHttps)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Se requiere HTTPS."
                        });
                    }

                    if (!await antiforgery.IsRequestValidAsync(contexto))
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Token CSRF invalido o ausente."
                        });
                    }

                    if (id <= 0)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Identificador de reserva invalido."
                        });
                    }

                    var cadena = app.Configuration
                        .GetConnectionString("SynchroDB");

                    if (string.IsNullOrWhiteSpace(cadena))
                    {
                        return Results.Problem(
                            "No se encontro la conexion a SynchroDB.");
                    }

                    try
                    {
                        await using var conexion = new SqlConnection(cadena);
                        await conexion.OpenAsync();

                        await using var comando = new SqlCommand(
                            "dbo.CancelarReservaCasilleroSynchro",
                            conexion);

                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@UsoId",
                            System.Data.SqlDbType.Int
                        ).Value = id;

                        await using var lector =
                            await comando.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                        {
                            return Results.Problem(
                                "No se obtuvo respuesta de la cancelacion.");
                        }

                        return Results.Ok(new
                        {
                            idUso = lector["IdUso"],
                            codigoCasillero = lector["CodigoCasillero"],
                            tipoServicio = lector["TipoServicio"],
                            estadoOperacion = lector["EstadoOperacion"],
                            estadoCasillero = lector["EstadoCasillero"]
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 50031)
                    {
                        return Results.NotFound(new
                        {
                            mensaje = "Reserva no encontrada."
                        });
                    }
                    catch (SqlException ex) when (
                        ex.Number == 50033 ||
                        ex.Number == 50034 ||
                        ex.Number == 50035)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "La reserva no puede cancelarse en su estado actual."
                        });
                    }
                    catch (SqlException ex) when (
                        ex.Number == 50030)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Identificador invalido."
                        });
                    }
                    catch (SqlException)
                    {
                        return Results.Problem(
                            "Error al cancelar la reserva.");
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/demo/reservas/{id:int}/depositar",
                async (
                    int id,
                    HttpContext contexto,
                    IAntiforgery antiforgery) =>
                {
                    if (!contexto.Request.IsHttps)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Se requiere HTTPS."
                        });
                    }

                    if (!await antiforgery.IsRequestValidAsync(contexto))
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Token CSRF invalido o ausente."
                        });
                    }

                    if (id <= 0)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Identificador de operacion invalido."
                        });
                    }

                    var cadena = app.Configuration
                        .GetConnectionString("SynchroDB");

                    if (string.IsNullOrWhiteSpace(cadena))
                    {
                        return Results.Problem(
                            "No se encontro la conexion a SynchroDB.");
                    }

                    try
                    {
                        await using var conexion = new SqlConnection(cadena);
                        await conexion.OpenAsync();

                        await using var comando = new SqlCommand(
                            "dbo.RegistrarDepositoEncomiendaSynchro",
                            conexion);

                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@UsoId",
                            System.Data.SqlDbType.Int
                        ).Value = id;

                        await using var lector =
                            await comando.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                        {
                            return Results.Problem(
                                "No se obtuvo respuesta del deposito.");
                        }

                        return Results.Ok(new
                        {
                            idUso = lector["IdUso"],
                            codigoCasillero = lector["CodigoCasillero"],
                            tipoServicio = lector["TipoServicio"],
                            tipoCasillero = lector["TipoCasillero"],
                            estadoOperacion = lector["EstadoOperacion"],
                            estadoCasillero = lector["EstadoCasillero"],
                            fechaIngreso = lector["FechaIngreso"]
                        });
                    }

                    catch (SqlException ex) when (ex.Number == 50140)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "No se puede depositar la encomienda porque faltan participantes."
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 50141)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "No se puede depositar la encomienda porque el consentimiento del destinatario no es valido."
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 50142)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "No se puede depositar la encomienda porque falta verificar el telefono del destinatario."
                        });
                    }

                    catch (SqlException ex) when (ex.Number == 50041)
                    {
                        return Results.NotFound(new
                        {
                            mensaje = "Reserva no encontrada."
                        });
                    }
                    catch (SqlException ex) when (
                        ex.Number == 50043 ||
                        ex.Number == 50044 ||
                        ex.Number == 50045 ||
                        ex.Number == 50046 ||
                        ex.Number == 50047 ||
                        ex.Number == 50048)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "No se puede registrar el deposito en el estado actual."
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 50040)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Identificador invalido."
                        });
                    }
                    catch (SqlException)
                    {
                        return Results.Problem(
                            "Error al registrar el deposito.");
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/demo/reservas/{id:int}/retirar",
                async (
                    int id,
                    HttpContext contexto,
                    IAntiforgery antiforgery) =>
                {
                    if (!contexto.Request.IsHttps)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Se requiere HTTPS."
                        });
                    }

                    if (!await antiforgery.IsRequestValidAsync(contexto))
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Token CSRF invalido o ausente."
                        });
                    }

                    if (id <= 0)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Identificador de operacion invalido."
                        });
                    }

                    var cadena = app.Configuration
                        .GetConnectionString("SynchroDB");

                    if (string.IsNullOrWhiteSpace(cadena))
                    {
                        return Results.Problem(
                            "No se encontro la conexion a SynchroDB.");
                    }

                    try
                    {
                        await using var conexion = new SqlConnection(cadena);
                        await conexion.OpenAsync();

                        await using var comando = new SqlCommand(
                            "dbo.RegistrarRetiroEncomiendaSynchro",
                            conexion);

                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@UsoId",
                            System.Data.SqlDbType.Int
                        ).Value = id;

                        await using var lector =
                            await comando.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                        {
                            return Results.Problem(
                                "No se obtuvo respuesta del retiro.");
                        }

                        return Results.Ok(new
                        {
                            idUso = lector["IdUso"],
                            codigoCasillero = lector["CodigoCasillero"],
                            tipoServicio = lector["TipoServicio"],
                            tipoCasillero = lector["TipoCasillero"],
                            estadoOperacion = lector["EstadoOperacion"],
                            estadoCasillero = lector["EstadoCasillero"],
                            fechaIngreso = lector["FechaIngreso"],
                            fechaRetiro = lector["FechaRetiro"]
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 50051)
                    {
                        return Results.NotFound(new
                        {
                            mensaje = "Operacion no encontrada."
                        });
                    }
                    catch (SqlException ex) when (
                        ex.Number >= 50053 &&
                        ex.Number <= 50058)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "No se puede retirar la encomienda en su estado actual."
                        });
                    }
                    catch (SqlException)
                    {
                        return Results.Problem(
                            "Error al registrar el retiro.");
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }
    }
}
