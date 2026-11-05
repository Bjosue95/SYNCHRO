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

public static class CasillerosDemoEndpoints
{
    public static void MapCasillerosDemoEndpoints(this WebApplication app)
    {
        var casilleros = new[]
        {
            new Casillero
            {
                Codigo = "S01",
                Tamano = "Pequeño",
                Tipo = "Normal",
                Estado ="Disponible"
            },
            new Casillero
            {
                Codigo = "M02",
                Tamano = "Mediano",
                Tipo ="Normal",
                Estado = "Disponible"
            }
        };

        app.MapGet("/api/casilleros", () => casilleros);

        app.MapGet("/api/casilleros/{codigo}", (string codigo) =>
        {
            var casillero = casilleros.FirstOrDefault(c =>
                c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

            if (casillero == null)
            {
                return Results.NotFound(new
                {
                    mensaje = "Casillero no encontrado"
                });
            }
            return Results.Ok(casillero);
        });

        #if false

        app.MapPost("/api/casilleros/{codigo}/reservar", (string codigo) =>
        {
            var casillero = casilleros.FirstOrDefault(c =>
                c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

            if (casillero == null)
            {
                return Results.NotFound(new
                {
                    mensaje = "Casillero no encontrado"
                });
            }

            if (casillero.Estado != "Disponible")
            {
                return Results.Conflict(new
                {
                    mensaje = "El casillero no está Disponible"
                });
            }

            casillero.Estado = "Reservado";

            return Results.Ok(casillero);
        });

        app.MapPost("/api/casilleros/{codigo}/depositar", (string codigo) =>
        {
            var casillero = casilleros.FirstOrDefault(c =>
                c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

            if (casillero == null)
            {
                return Results.NotFound(new
                {
                    mensaje = "Casillero no encontrado"
                });
            }

            if (casillero.Estado != "Reservado")
            {
                return Results.Conflict(new
                {
                    mensaje = "El casillero no está reservado"
                });
            }

            casillero.Estado = "Ocupado";

            return Results.Ok(new
            {
                mensaje = "Encomienda Depositada correctamente",
                codigo = casillero.Codigo,
                estado = casillero.Estado
            });
        });

        app.MapPost("/api/casilleros/{codigo}/retirar", (String codigo) =>
        {
            var casillero = casilleros.FirstOrDefault(c =>
                c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

            if (casillero == null)
            {
                return Results.NotFound(new
                {
                    mensaje = "Casillero no encontrado"
                });
            }

            if (casillero.Estado != "Ocupado")
            {
                return Results.Conflict(new
                {
                    mensaje = "El casillero no tiene encomiendas para retirar"
                });
            }

            casillero.Estado = "Disponible";

            return Results.Ok(new
            {
                mensaje = "Encomienda retirada correctamente",
                codigo = casillero.Codigo,
                estado = casillero.Estado
            });
        });

        #endif
    }
}
