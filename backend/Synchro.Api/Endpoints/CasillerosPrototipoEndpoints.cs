using Synchro.Api.Services.Administracion;
using System.Data;
using Microsoft.Data.SqlClient;
using Synchro.Api.Models;

namespace Synchro.Api.Endpoints;

public static class CasillerosPrototipoEndpoints
{
    public static void MapCasillerosPrototipoEndpoints(this WebApplication app, bool definitiva = false)
    {
        var ruta = app.MapGet("/api/terminales/casilleros-prototipo",
            async (HttpContext http, IConfiguration configuracion) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!definitiva && !app.Environment.IsDevelopment())
                return Results.NotFound();

            string? cadena = configuracion.GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem(
                    title: "Conexion SQL no configurada.",
                    statusCode: 503);

            var datos = new SqlConnectionStringBuilder(cadena);

            if (definitiva ? !DestinoAdministrativoDefinitivo.Permitido(cadena) : !string.Equals(
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

            if (definitiva)
            {
                await using var comprobar = new SqlCommand(DestinoAdministrativoDefinitivo.ValidacionSql, db);
                if (Convert.ToInt32(await comprobar.ExecuteScalarAsync(http.RequestAborted)) != 1)
                    return Results.Problem(statusCode:503,title:"Base definitiva no autorizada.");
            }
            else
            await using (var verificar = new SqlCommand("SELECT DB_NAME()", db))
            {
                string? baseActual = await verificar.ExecuteScalarAsync(
                    http.RequestAborted) as string;

                if (baseActual != "SynchroDB_Pruebas")
                    return Results.Problem(
                        title: "Base de datos no autorizada.",
                        statusCode: 503);
            }

            // El catálogo de la tablet y el de reservas comparten exactamente
            // el mismo inventario de la estación. Otros fixtures quedan fuera.
            await using var comando = new SqlCommand("""
                SELECT
                    c.Codigo,
                    c.Tamano,
                    CASE
                        WHEN c.Codigo = N'M04' THEN N'Frio'
                        WHEN c.Codigo = N'M09' THEN N'Calor'
                        ELSE c.Tipo
                    END AS Tipo,
                    c.Estado
                FROM dbo.Casilleros AS c
                JOIN OPENJSON(@codigos) AS inventario
                    ON inventario.[value] = c.Codigo
                ORDER BY TRY_CONVERT(int, inventario.[key]);
                """, db);

            comando.Parameters.Add("@codigos", SqlDbType.NVarChar, -1)
                .Value = InventarioEstacionPrototipo.CodigosJson;

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

            if (lista.Count != InventarioEstacionPrototipo.Codigos.Length)
                return Results.Problem(
                    title: "Inventario fisico del prototipo incompleto.",
                    detail: $"Se encontraron {lista.Count} de los {InventarioEstacionPrototipo.Codigos.Length} casilleros esperados.",
                    statusCode: 503);

            return Results.Ok(lista);
        });
        if (definitiva) ruta.RequireAuthorization("ConsultarInventarioEstacion");
    }
}