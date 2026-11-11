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

public static class CasillerosBaseDatosEndpoints
{
    public static void MapCasillerosBaseDatosEndpoints(this WebApplication app)
    {
        app.MapGet("/api/casilleros-bd", async () =>
        {
            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            var lista = new List<Casillero>();

            using var comando = new SqlCommand("SELECT Codigo , Tamano, Tipo, Estado FROM Casilleros ORDER BY Codigo", conexion);

            using var lector = await comando.ExecuteReaderAsync();

            while (await lector.ReadAsync())
            {
                lista.Add(new Casillero
                {
                    Codigo = lector.GetString(0),
                    Tamano = lector.GetString(1),
                    Tipo = lector.GetString(2),
                    Estado = lector.GetString(3)
                });
            }

            return Results.Ok(lista);
        });
    }
}
