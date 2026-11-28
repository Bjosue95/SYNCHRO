using Synchro.Api.Services.Administracion;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

/// <summary>
/// Historial administrativo de espacios realmente utilizados.
/// No emite PIN ni altera reservas, pagos o estados de casilleros.
/// </summary>
public static class AdminMovimientosEndpoints
{
    private const string Fuente = """
        ;WITH Registro AS (
            SELECT
                CONCAT(N'uso:', CONVERT(nvarchar(20), u.Id)) AS MovimientoId,
                u.CodigoCasillero AS Codigo,
                CASE WHEN u.TipoServicio = N'Encomienda'
                    THEN N'Encomienda' ELSE N'Resguardo' END AS Servicio,
                u.Estado AS Estado,
                u.FechaIngreso AS InicioUtc,
                u.FechaRetiro AS FinUtc,
                CONCAT(COALESCE(c.Tamano, N'Casillero'), N' · ', COALESCE(c.Tipo, N'Normal')) AS Detalle,
                u.Id AS UsoId,
                o.ReservaId AS EncomiendaId,
                CAST(NULL AS uniqueidentifier) AS SesionId
            FROM dbo.UsosCasillero AS u
            LEFT JOIN dbo.Casilleros AS c ON c.Codigo = u.CodigoCasillero
            LEFT JOIN dbo.EncomiendasOperativas AS o ON o.UsoId = u.Id
            WHERE u.TipoServicio IN (N'Encomienda', N'ResguardoPersonal')
              AND u.FechaIngreso IS NOT NULL

            UNION ALL

            SELECT
                CONCAT(N'carga:', CONVERT(nvarchar(36), s.Id)),
                s.CodigoEspacio,
                N'Carga',
                s.Estado,
                s.InicioUtc,
                s.RetiroUtc,
                CONCAT(
                    CASE s.Modalidad
                      WHEN N'Inalambrica' THEN N'Inalámbrica'
                      WHEN N'Cable' THEN N'Por cable'
                      WHEN N'Laptop' THEN N'Laptop'
                      ELSE s.Modalidad END,
                    N' · ', s.Dispositivo
                ),
                CAST(NULL AS int),
                CAST(NULL AS int),
                s.Id
            FROM dbo.SesionesCargaSynchro AS s
            WHERE s.InicioUtc IS NOT NULL
        )
        """;

    public static void MapAdminMovimientosEndpoints(this WebApplication app, bool definitiva = false)
    {
        app.MapGet("/api/admin/movimientos", async (
            int? pagina, int? tamanoPagina, string? servicio, string? codigo,
            HttpContext http, IConfiguration configuracion) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!definitiva && !app.Environment.IsDevelopment()) return Results.NotFound();
            if (!http.Request.IsHttps) return Results.StatusCode(403);

            int numeroPagina = pagina ?? 1;
            int cantidad = tamanoPagina ?? 20;
            if (numeroPagina < 1 || numeroPagina > 100000 || cantidad < 1 || cantidad > 100)
                return Results.BadRequest(new { mensaje = "Paginación inválida." });

            string tipo = string.IsNullOrWhiteSpace(servicio) ? "Todos" : servicio.Trim();
            if (tipo is not ("Todos" or "Resguardo" or "Encomienda" or "Carga"))
                return Results.BadRequest(new { mensaje = "Servicio inválido." });

            string? buscarCodigo = string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim();
            if (buscarCodigo is { Length: > 20 })
                return Results.BadRequest(new { mensaje = "Código de espacio inválido." });

            string? cadena = configuracion.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem(statusCode: 503, title: "Conexión SQL no configurada.");

            try
            {
                var cs = new SqlConnectionStringBuilder(cadena);
                if (definitiva ? !DestinoAdministrativoDefinitivo.Permitido(cadena) : (!cs.DataSource.Equals(@"(localdb)\SYNCHRO_Aislado", StringComparison.OrdinalIgnoreCase) ||
                    !cs.InitialCatalog.Equals("SynchroDB_Pruebas", StringComparison.OrdinalIgnoreCase) ||
                    !cs.IntegratedSecurity))
                    return Results.NotFound();

                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);

                await using (var preparar = new SqlCommand(definitiva ? DestinoAdministrativoDefinitivo.ValidacionSql : """
                    SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas'
                      AND EXISTS (SELECT 1 FROM sys.extended_properties
                          WHERE class=0 AND name=N'SynchroFixtureDeposito'
                            AND CONVERT(nvarchar(10), value)=N'1')
                      AND OBJECT_ID(N'dbo.UsosCasillero',N'U') IS NOT NULL
                      AND OBJECT_ID(N'dbo.Casilleros',N'U') IS NOT NULL
                      AND OBJECT_ID(N'dbo.EncomiendasOperativas',N'U') IS NOT NULL
                      AND OBJECT_ID(N'dbo.SesionesCargaSynchro',N'U') IS NOT NULL
                      THEN 1 ELSE 0 END;
                    """, db))
                {
                    int esquema = Convert.ToInt32(await preparar.ExecuteScalarAsync(http.RequestAborted));
                    if (esquema != 1)
                        return Results.Problem(statusCode: 503,
                            title: "El historial necesita las tablas de operaciones y carga.");
                }

                const string filtros = """
                    WHERE (@servicio=N'Todos' OR Servicio=@servicio)
                      AND (@codigo IS NULL OR CHARINDEX(@codigo, Codigo)>0)
                    """;

                // Las cadenas raw de C# no conservan un espacio final de
                // forma implícita. Separar siempre los bloques SQL para evitar
                // concatenaciones como "RegistroWHERE" y ")SELECT".
                string consulta = Fuente + "\n" +
                    "SELECT COUNT(*) FROM Registro\n" + filtros + ";\n" +
                    Fuente + "\n" +
                    """
                    SELECT MovimientoId,Codigo,Servicio,Estado,InicioUtc,FinUtc,
                           Detalle,UsoId,EncomiendaId,SesionId
                    FROM Registro
                    """ + "\n" + filtros + "\n" +
                    "ORDER BY InicioUtc DESC, MovimientoId DESC\n" +
                    "OFFSET @salto ROWS FETCH NEXT @cantidad ROWS ONLY;";

                await using var cmd = new SqlCommand(consulta, db);
                cmd.CommandTimeout = 20;
                cmd.Parameters.Add("@servicio", SqlDbType.NVarChar, 20).Value = tipo;
                cmd.Parameters.Add("@codigo", SqlDbType.NVarChar, 20).Value =
                    (object?)buscarCodigo ?? DBNull.Value;
                cmd.Parameters.Add("@salto", SqlDbType.Int).Value = (numeroPagina - 1) * cantidad;
                cmd.Parameters.Add("@cantidad", SqlDbType.Int).Value = cantidad;

                await using var lector = await cmd.ExecuteReaderAsync(http.RequestAborted);
                int total = await lector.ReadAsync(http.RequestAborted) ? lector.GetInt32(0) : 0;
                await lector.NextResultAsync(http.RequestAborted);

                var movimientos = new List<object>();
                while (await lector.ReadAsync(http.RequestAborted))
                {
                    DateTime? fin = lector.IsDBNull(5) ? null :
                        DateTime.SpecifyKind(lector.GetDateTime(5), DateTimeKind.Utc);
                    DateTime inicio = DateTime.SpecifyKind(lector.GetDateTime(4), DateTimeKind.Utc);
                    movimientos.Add(new {
                        id = lector.GetString(0),
                        codigo = lector.GetString(1),
                        servicio = lector.GetString(2),
                        estado = lector.GetString(3),
                        inicioUtc = inicio,
                        finUtc = fin,
                        detalle = lector.GetString(6),
                        usoId = lector.IsDBNull(7) ? (int?)null : lector.GetInt32(7),
                        encomiendaId = lector.IsDBNull(8) ? (int?)null : lector.GetInt32(8),
                        sesionId = lector.IsDBNull(9) ? (Guid?)null : lector.GetGuid(9),
                        duracionMinutos = fin.HasValue
                            ? (int?)Math.Max(0, (int)Math.Ceiling((fin.Value - inicio).TotalMinutes))
                            : null
                    });
                }

                return Results.Ok(new {
                    movimientos, total, pagina = numeroPagina,
                    tamanoPagina = cantidad,
                    totalPaginas = (int)Math.Ceiling((double)total / cantidad)
                });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503,
                    title: "No se pudieron consultar los movimientos.");
            }
            catch (ArgumentException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization("OperarServicios")
        .RequireRateLimiting("EncomiendasOperativas");
    }
}
