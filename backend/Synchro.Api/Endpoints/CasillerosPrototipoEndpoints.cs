using Microsoft.Data.SqlClient;
using Synchro.Api.Models;

namespace Synchro.Api.Endpoints;

public static class CasillerosPrototipoEndpoints
{
    public static void MapCasillerosPrototipoEndpoints(this WebApplication app)
    {
        app.MapGet("/api/terminales/casilleros-prototipo",
            async (HttpContext http, IConfiguration configuracion) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!app.Environment.IsDevelopment())
                return Results.NotFound();

            string? cadena = configuracion.GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem(
                    title: "Conexion SQL no configurada.",
                    statusCode: 503);

            var datos = new SqlConnectionStringBuilder(cadena);

            if (!string.Equals(
                    datos.InitialCatalog,
                    "SynchroDB_Pruebas",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    title: "La tablet requiere la base aislada del prototipo.",
                    statusCode: 503);
            }

            await using var db = new SqlConnection(cadena);
            await db.OpenAsync(http.RequestAborted);

            await using (var verificar = new SqlCommand("SELECT DB_NAME()", db))
            {
                string? baseActual = await verificar.ExecuteScalarAsync(
                    http.RequestAborted) as string;

                if (baseActual != "SynchroDB_Pruebas")
                    return Results.Problem(
                        title: "Base de datos no autorizada.",
                        statusCode: 503);
            }

            await using var comando = new SqlCommand("""
                SELECT
                    Codigo,
                    Tamano,
                    CASE
                        WHEN Codigo = N'M04' THEN N'Frio'
                        WHEN Codigo = N'M09' THEN N'Calor'
                        ELSE Tipo
                    END AS Tipo,
                    Estado
                FROM dbo.Casilleros
                WHERE Codigo IN
                (
                    N'S01', N'S02',
                    N'M03', N'M04',
                    N'L05', N'M06',
                    N'M07', N'M08',
                    N'M09', N'L10'
                )
                ORDER BY CASE Codigo
                    WHEN N'S01' THEN 1
                    WHEN N'S02' THEN 2
                    WHEN N'M03' THEN 3
                    WHEN N'M04' THEN 4
                    WHEN N'L05' THEN 5
                    WHEN N'M06' THEN 6
                    WHEN N'M07' THEN 7
                    WHEN N'M08' THEN 8
                    WHEN N'M09' THEN 9
                    WHEN N'L10' THEN 10
                    ELSE 99
                END;
                """, db);

            var lista = new List<Casillero>();

            await using var lector = await comando.ExecuteReaderAsync(
                http.RequestAborted);

            while (await lector.ReadAsync(http.RequestAborted))
            {
                lista.Add(new Casillero
                {
                    Codigo = lector.GetString(0),
                    Tamano = lector.GetString(1),
                    Tipo = lector.GetString(2),
                    Estado = lector.GetString(3)
                });
            }

            if (lista.Count != 10)
                return Results.Problem(
                    title: "Inventario fisico del prototipo incompleto.",
                    detail: $"Se encontraron {lista.Count} de los 10 casilleros esperados.",
                    statusCode: 503);

            return Results.Ok(lista);
        });
    }
}