using Synchro.Api.Services.Administracion;
using System.Data;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Endpoints;

// Consulta solo lectura para mostrar espacios reales de carga
// al lado de los casilleros de resguardo del administrador.
public static class AdminEspaciosCargaEndpoints
{
    public static void MapAdminEspaciosCargaEndpoints(this WebApplication app, bool definitiva = false)
    {
        app.MapGet("/api/admin/carga/espacios", async (
            HttpContext http, IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!definitiva && (!app.Environment.IsDevelopment() ||
                !config.GetValue<bool>("CargaDispositivos:RegistroHabilitado")))
                return Results.NotFound();
            if (!http.Request.IsHttps)
                return Results.StatusCode(403);

            var cadena = config.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem(statusCode: 503,
                    title: "Conexion de carga no configurada.");

            try
            {
                if (definitiva ? !DestinoAdministrativoDefinitivo.Permitido(cadena) : !ConexionSqlAislada.EsWhatsAppPermitido(
                    new SqlConnectionStringBuilder(cadena)))
                    return Results.NotFound();

                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using (var validar = new SqlCommand(definitiva ? DestinoAdministrativoDefinitivo.ValidacionSql : """
                    SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas'
                      AND EXISTS (SELECT 1 FROM sys.extended_properties
                        WHERE class=0 AND name=N'SynchroFixtureDeposito'
                          AND CONVERT(nvarchar(10),value)=N'1')
                      AND OBJECT_ID(N'dbo.EspaciosCarga',N'U') IS NOT NULL
                      AND OBJECT_ID(N'dbo.SesionesCargaSynchro',N'U') IS NOT NULL
                      THEN 1 ELSE 0 END;
                    """, db))
                {
                    if (Convert.ToInt32(await validar.ExecuteScalarAsync(
                        http.RequestAborted)) != 1)
                        return Results.Problem(statusCode: 503,
                            title: "Esquema de espacios de carga no preparado.");
                }

                // No crear espacios ni inventar estados: mostrar solo
                // los existentes en dbo.EspaciosCarga.
                await using var cmd = new SqlCommand("""
                    SELECT e.Codigo, e.Estado,
                        (SELECT COUNT(*)
                         FROM dbo.SesionesCargaSynchro AS s
                         WHERE s.CodigoEspacio=e.Codigo
                           AND s.Estado=N'Activa'
                           AND s.RetiroUtc IS NULL) AS SesionesActivas
                    FROM dbo.EspaciosCarga AS e
                    WHERE e.Codigo IN (
                        N'W1',N'W2',N'C1',N'C2',N'C3',N'C4',N'L1',N'L2')
                    ORDER BY CASE
                        WHEN e.Codigo=N'W1' THEN 1
                        WHEN e.Codigo=N'W2' THEN 2
                        WHEN e.Codigo=N'C1' THEN 3
                        WHEN e.Codigo=N'C2' THEN 4
                        WHEN e.Codigo=N'C3' THEN 5
                        WHEN e.Codigo=N'C4' THEN 6
                        WHEN e.Codigo=N'L1' THEN 7
                        WHEN e.Codigo=N'L2' THEN 8
                        ELSE 9 END;
                    """, db);
                var espacios = new List<object>();
                await using var lector = await cmd.ExecuteReaderAsync(
                    http.RequestAborted);
                while (await lector.ReadAsync(http.RequestAborted))
                {
                    var codigo = lector.GetString(0);
                    var modalidad = codigo.StartsWith("W", StringComparison.Ordinal)
                        ? "Inalambrica"
                        : codigo.StartsWith("C", StringComparison.Ordinal)
                            ? "Cable"
                            : "Laptop";
                    espacios.Add(new
                    {
                        codigo,
                        estado = lector.GetString(1),
                        modalidad,
                        dispositivo = modalidad == "Laptop" ? "Laptop" : "Celular",
                        sesionesActivas = lector.GetInt32(2)
                    });
                }
                return Results.Ok(new { espacios });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503,
                    title: "No se pudieron consultar los espacios de carga.");
            }
            catch (ArgumentException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization("OperarServicios")
        .RequireRateLimiting("OperacionesTerminal");
    }
}
