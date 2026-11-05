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

public static class DestinatariosEncomiendasEndpoints
{
    public static void MapDestinatariosEncomiendasEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/api/destinatarios/mis-encomiendas",
            async (HttpContext contexto) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                // Obtener el destinatario desde su sesion.
                string? personaTexto = contexto.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(personaTexto, out int personaId)
                    || personaId <= 0)
                {
                    return Results.Unauthorized();
                }

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.Problem(
                        "Conexion SQL no configurada.");
                }

                try
                {
                    await using var conexion = new SqlConnection(cadena);
                    await conexion.OpenAsync();

                    await using var comando = new SqlCommand(
                        @"SELECT TOP (20)
                            u.Id,
                            u.CodigoCasillero,
                            u.TipoServicio,
                            u.Estado,
                            COALESCE(
                                cw.Estado,
                                N'Sin asociar'
                            ) AS EstadoConsentimiento
                          FROM dbo.UsosCasillero u
                          INNER JOIN dbo.ParticipantesUso p
                              ON p.UsoId = u.Id
                             AND p.Rol = N'Destinatario'
                          LEFT JOIN dbo.ConsentimientosUso cu
                              ON cu.UsoId = u.Id
                          LEFT JOIN dbo.ConsentimientosWhatsApp cw
                              ON cw.Id = cu.ConsentimientoId
                          WHERE p.PersonaId = @PersonaId
                            AND u.TipoServicio = N'Encomienda'
                          ORDER BY u.Id DESC",
                        conexion);

                    comando.Parameters.Add(
                        "@PersonaId",
                        System.Data.SqlDbType.Int
                    ).Value = personaId;

                    await using var lector =
                        await comando.ExecuteReaderAsync();

                    var encomiendas = new List<object>();

                    while (await lector.ReadAsync())
                    {
                        encomiendas.Add(new
                        {
                            usoId = lector.GetInt32(0),
                            codigoCasillero = lector.GetString(1),
                            tipoServicio = lector.GetString(2),
                            estado = lector.GetString(3),
                            estadoConsentimiento = lector.GetString(4)
                        });
                    }

                    contexto.Response.Headers.CacheControl = "no-store";

                    return Results.Ok(new
                    {
                        encomiendas
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No se pudieron consultar las encomiendas.");
                }
            }
        )
        .RequireAuthorization("SoloDestinatarios");
    }
}
