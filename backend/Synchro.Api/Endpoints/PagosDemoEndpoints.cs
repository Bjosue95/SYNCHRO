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

public static class PagosDemoEndpoints
{
    public static void MapPagosDemoEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/demo/pagos/{id:int}/confirmar",
                async (
                    int id,
                    ConfirmarPagoSimuladoSolicitud solicitud,
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

                    string? metodo = solicitud.CanalPago switch
                    {
                        "Tablet" => "NFC",
                        "Web" => "TarjetaWeb",
                        _ => null
                    };

                    if (metodo is null)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "El canal debe ser Tablet o Web."
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
                        await using var conexion =
                            new SqlConnection(cadena);

                        await conexion.OpenAsync();

                        await using var comando =
                            new SqlCommand(
                                "dbo.ConfirmarPagoSimulado",
                                conexion);

                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@PagoId",
                            System.Data.SqlDbType.Int
                        ).Value = id;

                        comando.Parameters.Add(
                            "@CanalPago",
                            System.Data.SqlDbType.NVarChar,
                            20
                        ).Value = solicitud.CanalPago;

                        comando.Parameters.Add(
                            "@MetodoPago",
                            System.Data.SqlDbType.NVarChar,
                            30
                        ).Value = metodo;

                        await using var lector =
                            await comando.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                        {
                            return Results.Problem(
                                "No se obtuvo respuesta del pago.");
                        }

                        return Results.Ok(new
                        {
                            id = lector["Id"],
                            monto = lector["Monto"],
                            estado = lector["Estado"],
                            metodoPago = lector["MetodoPago"],
                            canalPago = lector["CanalPago"],
                            referenciaPago = lector["ReferenciaPago"],
                            esSimulado = lector["EsSimulado"],
                            fechaPago = lector["FechaPago"]
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 50002)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "El pago no existe, ya fue confirmado o no es simulado."
                        });
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/demo/pagos/{id:int}/rechazar",
                async (
                    int id,
                    RegistrarRechazoPagoSimuladoSolicitud solicitud,
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

                    if (solicitud.CanalPago is not ("Tablet" or "Web"))
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "El canal debe ser Tablet o Web."
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
                            "dbo.RegistrarIntentoPagoRechazado",
                            conexion);

                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@PagoId",
                            System.Data.SqlDbType.Int
                        ).Value = id;

                        comando.Parameters.Add(
                            "@CanalPago",
                            System.Data.SqlDbType.NVarChar,
                            20
                        ).Value = solicitud.CanalPago;

                        await using var lector =
                            await comando.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                        {
                            return Results.Problem(
                                "No se obtuvo respuesta del intento de pago.");
                        }

                        return Results.Ok(new
                        {
                            idIntento = lector["IdIntento"],
                            pagoId = lector["PagoId"],
                            canalPago = lector["CanalPago"],
                            metodoPago = lector["MetodoPago"],
                            resultado = lector["Resultado"],
                            referenciaIntento = lector["ReferenciaIntento"],
                            fechaIntento = lector["FechaIntento"],
                            estadoPago = lector["EstadoPago"]
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 50004)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "El pago no existe, no esta pendiente o no es simulado."
                        });
                    }
                    catch (SqlException)
                    {
                        return Results.Problem(
                            "Ocurrio un error al registrar el intento de pago.");
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }
    }
}
