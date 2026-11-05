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

public static class ParticipantesDemoEndpoints
{
    public static void MapParticipantesDemoEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/demo/reservas/{id:int}/participantes",
                async (
                    int id,
                    AsignarParticipantesPeticion datos,
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

                    if (id <= 0 ||
                        datos.RemitenteId <= 0 ||
                        datos.DestinatarioId <= 0)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Identificadores invalidos."
                        });
                    }

                    if (datos.RemitenteId == datos.DestinatarioId)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Remitente y destinatario deben ser distintos."
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
                            "dbo.AsignarParticipantesEncomiendaSynchro",
                            conexion);

                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@UsoId",
                            System.Data.SqlDbType.Int
                        ).Value = id;

                        comando.Parameters.Add(
                            "@RemitenteId",
                            System.Data.SqlDbType.Int
                        ).Value = datos.RemitenteId;

                        comando.Parameters.Add(
                            "@DestinatarioId",
                            System.Data.SqlDbType.Int
                        ).Value = datos.DestinatarioId;

                        var participantes = new List<object>();

                        await using var lector =
                            await comando.ExecuteReaderAsync();

                        while (await lector.ReadAsync())
                        {
                            participantes.Add(new
                            {
                                usoId = Convert.ToInt32(lector["UsoId"]),
                                personaId = Convert.ToInt32(lector["PersonaId"]),
                                rol = lector["Rol"].ToString()
                            });
                        }

                        return Results.Ok(new
                        {
                            mensaje = "Participantes asignados correctamente.",
                            idUso = id,
                            participantes
                        });
                    }
                    catch (SqlException ex) when (
                        ex.Number == 50060 ||
                        ex.Number == 50061)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Datos de participantes invalidos."
                        });
                    }
                    catch (SqlException ex) when (
                        ex.Number == 50062 ||
                        ex.Number == 50066 ||
                        ex.Number == 50067)
                    {
                        return Results.NotFound(new
                        {
                            mensaje = "Operacion o persona no encontrada."
                        });
                    }
                    catch (SqlException ex) when (
                        ex.Number == 50063 ||
                        ex.Number == 50064 ||
                        ex.Number == 50065 ||
                        ex.Number == 50068 ||
                        ex.Number == 2601 ||
                        ex.Number == 2627)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "No se pueden asignar participantes a esta reserva."
                        });
                    }
                    catch (SqlException)
                    {
                        return Results.Problem(
                            "Error al asignar participantes.");
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }
    }
}
