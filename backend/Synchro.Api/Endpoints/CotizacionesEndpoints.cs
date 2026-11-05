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

public static class CotizacionesEndpoints
{
    public static void MapCotizacionesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/casilleros/{codigo}/cotizar",
            async (string codigo, string tipoServicio, int minutos) =>
            {
                if (minutos <= 0)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "La duracion debe ser mayor que cero."
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
                        "dbo.CotizarAlmacenamiento", conexion);

                    comando.CommandType =
                        System.Data.CommandType.StoredProcedure;

                    comando.Parameters.Add(
                        "@CodigoCasillero",
                        System.Data.SqlDbType.NVarChar, 10
                    ).Value = codigo;

                    comando.Parameters.Add(
                        "@TipoServicio",
                        System.Data.SqlDbType.NVarChar, 20
                    ).Value = tipoServicio;

                    comando.Parameters.Add(
                        "@MinutosSolicitados",
                        System.Data.SqlDbType.Int
                    ).Value = minutos;

                    await using var lector =
                        await comando.ExecuteReaderAsync();

                    if (!await lector.ReadAsync())
                    {
                        return Results.Problem(
                            "No se obtuvo la cotizacion.");
                    }

                    return Results.Ok(new
                    {
                        codigoCasillero = lector["CodigoCasillero"],
                        tipoServicio = lector["TipoServicio"],
                        tipoCasillero = lector["TipoCasillero"],
                        tamano = lector["Tamano"],
                        minutosSolicitados = lector["MinutosSolicitados"],
                        precioBase = lector["PrecioBase"],
                        bloquesAdicionales = lector["BloquesAdicionales"],
                        precioAdicional = lector["PrecioAdicional"],
                        montoTotal = lector["MontoTotal"]
                    });
                }
                catch (SqlException ex) when (ex.Number == 50012)
                {
                    return Results.NotFound(new
                    {
                        mensaje = "Casillero no encontrado."
                    });
                }
                catch (SqlException ex) when (
                    ex.Number == 50010 ||
                    ex.Number == 50011 ||
                    ex.Number == 50013 ||
                    ex.Number == 50015)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = ex.Message
                    });
                }
                catch (SqlException ex) when (ex.Number == 50014)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "No existe una tarifa activa unica."
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "Error al consultar la tarifa.");
                }
            });
    }
}
