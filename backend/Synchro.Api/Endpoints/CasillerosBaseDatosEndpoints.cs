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
        app.MapGet("/api/prueba-bd", async () =>
        {
            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);

            await conexion.OpenAsync();

            using var comando = new SqlCommand("SELECT COUNT(*) FROM Casilleros", conexion);
            var total = Convert.ToInt32(await comando.ExecuteScalarAsync());
            return Results.Ok(new
            {
                mensaje = "Conexión correcta a SychroDB",
                totalCasilleros = total
            });
        });

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

        #if false

        app.MapPost("/api/casilleros-bd/{codigo}/reservar", async (string codigo) =>
        {
            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            using var comando = new SqlCommand(@"UPDATE Casilleros Set Estado = N'Reservado' WHERE Codigo = @codigo AND Estado = N'Disponible'", conexion);

            comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

            var filasActualizadas = await comando.ExecuteNonQueryAsync();

            if (filasActualizadas == 0)
            {
                return Results.Conflict(new
                {
                    mensaje = "Casillero no encontrado o no disponible"
                });
            }

            return Results.Ok(new
            {
                mensaje = "Resrva Guardada en SQL Server",
                codigo = codigo,
                estado = "Reservado"
            });
        });

        app.MapPost("/api/casilleros-bd/{codigo}/depositar", async (string codigo) =>
        {
            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            using var comando = new SqlCommand(@"UPDATE Casilleros SET Estado = N'Ocupado' WHERE Codigo = @codigo And Estado = N'Reservado'", conexion);
            comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

            var filasActualizadas = await comando.ExecuteNonQueryAsync();

            if (filasActualizadas == 0)
            {
                return Results.Conflict(new
                {
                    mensaje = "Casillero no encontrado o no reservado"
                });
            }

            return Results.Ok(new
            {
                mensaje = "Deposito Guardador en SQL Server",
                codigo = codigo,
                estado = "Ocupado"
            });
        });

        app.MapPost("/api/casilleros-bd/{codigo}/retirar", async (string codigo) =>
        {
            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            using var comando = new SqlCommand(@"UPDATE Casilleros SET Estado =N'Disponible' WHERE Codigo = @codigo AND EStado = N'Ocupado'", conexion);

            comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

            var filasActualizadas = await comando.ExecuteNonQueryAsync();

            if (filasActualizadas == 0)
            {
                return Results.Conflict(new
                {
                    mensaje = "Casillero no encontrado o no ocupado"
                });
            }

            return Results.Ok(new
            {
                mensaje = "Retiro guardado en SQL Server",
                codigo = codigo,
                estado = "Disponible"
            });
        });

        #endif
    }
}
