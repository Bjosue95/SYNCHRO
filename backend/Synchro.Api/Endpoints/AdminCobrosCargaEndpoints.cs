using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Endpoints;

// Confirmacion manual, exclusivamente por Operador o Administrador autenticado.
// No se recibe ni procesa dinero real.
public sealed record ConfirmarCobroCargaAdminSolicitud(
    Guid SesionId, string? MetodoPago, bool PagoComprobado);

public static class AdminCobrosCargaEndpoints
{
    private static string? NormalizarMetodo(string? valor) => valor switch
    {
        "Efectivo" => "EfectivoSimulado",
        "Tarjeta" => "TarjetaSimulada",
        "NFC" => "NfcSimulado",
        _ => null
    };

    private static string? MetodoVisual(string valor) => valor switch
    {
        "EfectivoSimulado" => "Efectivo",
        "TarjetaSimulada" => "Tarjeta",
        "NfcSimulado" => "NFC",
        _ => null
    };

    private static bool Permitido(WebApplication app, IConfiguration config,bool definitiva)
    {
        if ((!definitiva && !app.Environment.IsDevelopment()) ||
            !config.GetValue<bool>("CargaDispositivos:RegistroHabilitado"))
            return false;
        var cadena = config.GetConnectionString("SynchroDB");
        if (string.IsNullOrWhiteSpace(cadena)) return false;
        try
        {
            return definitiva ? Synchro.Api.Services.Administracion.DestinoAdministrativoDefinitivo.Permitido(cadena) : ConexionSqlAislada.EsWhatsAppPermitido(
                new SqlConnectionStringBuilder(cadena));
        }
        catch (ArgumentException) { return false; }
    }

    private static async Task<bool> EsquemaValido(
        SqlConnection db, CancellationToken ct,bool definitiva)
    {
        if(definitiva)return await VerificarBaseCargaDefinitiva.ValidarAsync(db,ct);
        await using var cmd = new SqlCommand("""
            SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas'
              AND EXISTS (SELECT 1 FROM sys.extended_properties
                WHERE class=0 AND name=N'SynchroFixtureDeposito'
                  AND CONVERT(nvarchar(10),value)=N'1')
              AND OBJECT_ID(N'dbo.CobrosRetiroCargaSynchro',N'U') IS NOT NULL
              AND OBJECT_ID(N'dbo.SesionesCargaSynchro',N'U') IS NOT NULL
              AND OBJECT_ID(N'dbo.EspaciosCarga',N'U') IS NOT NULL
              THEN 1 ELSE 0 END;
            """, db);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
    }

    public static void MapAdminCobrosCargaEndpoints(this WebApplication app, bool definitiva = false)
    {
        var rutas = app.MapGroup("/api/admin/carga/cobros")
            .RequireAuthorization("OperarServicios")
            .RequireRateLimiting("OperacionesTerminal");

        rutas.MapGet("/pendientes", async (
            HttpContext http, IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!Permitido(app, config, definitiva)) return Results.NotFound();
            if (!http.Request.IsHttps) return Results.StatusCode(403);
            try
            {
                await using var db = new SqlConnection(
                    config.GetConnectionString("SynchroDB"));
                await db.OpenAsync(http.RequestAborted);
                if (!await EsquemaValido(db, http.RequestAborted,definitiva))
                    return Results.Problem(statusCode: 503,
                        title: "Esquema de cobro de carga no preparado.");

                await using var cmd = new SqlCommand("""
                    SELECT TOP (50) c.SesionId, c.CodigoEspacio,
                           s.Dispositivo, s.Modalidad, c.MinutosUtilizados,
                           c.ImporteHnl, c.DescripcionTarifa,
                           c.CreadoUtc, c.MetodoPago
                    FROM dbo.CobrosRetiroCargaSynchro AS c
                    INNER JOIN dbo.SesionesCargaSynchro AS s
                      ON s.Id=c.SesionId
                    INNER JOIN dbo.EspaciosCarga AS e
                      ON e.Codigo=c.CodigoEspacio
                    WHERE c.Estado=N'Pendiente'
                      AND c.MetodoPago IS NOT NULL
                      AND s.CodigoEspacio=c.CodigoEspacio
                      AND s.Estado=N'Activa'
                      AND s.RetiroUtc IS NULL
                      AND e.Estado=N'Ocupado'
                    ORDER BY c.CreadoUtc DESC;
                    """, db);

                var cobros = new List<object>();
                await using var lector =
                    await cmd.ExecuteReaderAsync(http.RequestAborted);
                while (await lector.ReadAsync(http.RequestAborted))
                {
                    var metodo = MetodoVisual(lector.GetString(8));
                    if (metodo is null) continue;
                    cobros.Add(new
                    {
                        sesionId = lector.GetGuid(0),
                        codigoEspacio = lector.GetString(1),
                        dispositivo = lector.GetString(2),
                        modalidad = lector.GetString(3),
                        minutosUtilizados = lector.GetInt32(4),
                        importeHnl = lector.GetDecimal(5),
                        descripcionTarifa = lector.GetString(6),
                        creadoUtc = DateTime.SpecifyKind(
                            lector.GetDateTime(7), DateTimeKind.Utc),
                        metodoPago = metodo,
                        estadoCobro = "Pendiente",
                        pagoSimulado = true
                    });
                }
                return Results.Ok(new { cobros });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503,
                    title: "No se pudieron consultar los cobros de carga.");
            }
        });

        rutas.MapPost("/confirmar", async (
            ConfirmarCobroCargaAdminSolicitud datos,
            HttpContext http, IAntiforgery csrf,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!Permitido(app, config, definitiva)) return Results.NotFound();
            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);
            if (datos.SesionId == Guid.Empty || !datos.PagoComprobado)
                return Results.BadRequest(new {
                    mensaje = "Comprueba primero el pago simulado."
                });
            var metodo = NormalizarMetodo(datos.MetodoPago);
            if (metodo is null)
                return Results.BadRequest(new {
                    mensaje = "Metodo de pago invalido."
                });
            if (!int.TryParse(http.User.FindFirstValue(
                    ClaimTypes.NameIdentifier), out var operadorId) ||
                operadorId <= 0)
                return Results.Unauthorized();

            try
            {
                await using var db = new SqlConnection(
                    config.GetConnectionString("SynchroDB"));
                await db.OpenAsync(http.RequestAborted);
                if (!await EsquemaValido(db, http.RequestAborted,definitiva))
                    return Results.Problem(statusCode: 503,
                        title: "Esquema de cobro de carga no preparado.");

                await using var tx = (SqlTransaction)
                    await db.BeginTransactionAsync(
                        IsolationLevel.Serializable, http.RequestAborted);

                // La eleccion de metodo se realizo primero en la tablet.
                // No permitir que el operador la altere al confirmar.
                await using var cmd = new SqlCommand("""
                    UPDATE c
                    SET Estado=N'ConfirmadoSimulado',
                        ConfirmadoUtc=SYSUTCDATETIME()
                    OUTPUT inserted.SesionId, inserted.CodigoEspacio,
                           inserted.ImporteHnl
                    FROM dbo.CobrosRetiroCargaSynchro AS c
                        WITH (UPDLOCK,HOLDLOCK)
                    INNER JOIN dbo.SesionesCargaSynchro AS s
                        WITH (UPDLOCK,HOLDLOCK)
                      ON s.Id=c.SesionId
                    INNER JOIN dbo.EspaciosCarga AS e
                        WITH (UPDLOCK,HOLDLOCK)
                      ON e.Codigo=c.CodigoEspacio
                    WHERE c.SesionId=@id
                      AND c.Estado=N'Pendiente'
                      AND c.MetodoPago=@metodo
                      AND s.CodigoEspacio=c.CodigoEspacio
                      AND s.Estado=N'Activa'
                      AND s.RetiroUtc IS NULL
                      AND e.Estado=N'Ocupado';
                    """, db, tx);
                cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier)
                    .Value = datos.SesionId;
                cmd.Parameters.Add("@metodo", SqlDbType.NVarChar, 24)
                    .Value = metodo;

                Guid id;
                string espacio;
                decimal importe;
                await using (var lector =
                    await cmd.ExecuteReaderAsync(http.RequestAborted))
                {
                    if (!await lector.ReadAsync(http.RequestAborted))
                        return Results.Conflict(new {
                            mensaje = "Cobro ya confirmado, cancelado o metodo distinto."
                        });
                    id = lector.GetGuid(0);
                    espacio = lector.GetString(1);
                    importe = lector.GetDecimal(2);
                }
                await tx.CommitAsync(http.RequestAborted);

                app.Logger.LogInformation(
                    "SYNCHRO carga: pago simulado aprobado por operador {OperadorId}, sesion {SesionId}, espacio {Espacio}",
                    operadorId, id, espacio);
                return Results.Ok(new
                {
                    sesionId = id,
                    codigoEspacio = espacio,
                    importeHnl = importe,
                    metodoPago = datos.MetodoPago,
                    pagoConfirmado = true,
                    pagoSimulado = true,
                    pagoRealProcesado = false,
                    retiroConfirmado = false
                });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503,
                    title: "No se pudo aprobar el cobro de carga.");
            }
        });
    }
}
