using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Endpoints;

public sealed record PrepararCobroCargaSolicitud(string? TokenCobro);
public sealed record ConfirmarPagoCargaSolicitud(string? TokenCobro, string? MetodoPago);
public sealed record ConfirmarRecogidaCargaSolicitud(string? TokenCobro, bool DispositivoRetirado);

// Pago estrictamente simulado, exclusivo del entorno aislado.
// Ninguna ruta confirma un pago bancario real ni abre cerraduras.
public static class RetiroCargaCobroEndpoints
{
    private const string PropositoPin = "SYNCHRO.Carga.RetiroPinValidado.v1";

    private sealed record EstadoCarga(
        string Codigo, string Dispositivo, string Modalidad, DateTime InicioUtc,
        string Estado, DateTime? RetiroUtc, string EstadoEspacio);

    private sealed record CobroCarga(
        int Minutos, decimal Importe, string Descripcion,
        string Estado, string? MetodoPago);

    private static bool Permitido(WebApplication app, IConfiguration config)
    {
        if (!app.Environment.IsDevelopment() ||
            !config.GetValue<bool>("CargaDispositivos:RegistroHabilitado"))
            return false;

        var cadena = config.GetConnectionString("SynchroDB");
        if (string.IsNullOrWhiteSpace(cadena)) return false;
        try
        {
            return ConexionSqlAislada.EsWhatsAppPermitido(
                new SqlConnectionStringBuilder(cadena));
        }
        catch (ArgumentException) { return false; }
    }

    private static bool LeerId(string? token, IDataProtectionProvider proteccion, out Guid sesion)
    {
        sesion = Guid.Empty;
        if (token is null || token.Length is < 32 or > 4096) return false;
        try
        {
            var valor = proteccion.CreateProtector(PropositoPin)
                .ToTimeLimitedDataProtector().Unprotect(token);
            return Guid.TryParseExact(valor, "N", out sesion);
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
    }

    private static async Task<bool> EsquemaValido(
        SqlConnection db, CancellationToken cancel)
    {
        await using var cmd = new SqlCommand("""
            SELECT CASE WHEN DB_NAME() = N'SynchroDB_Pruebas'
              AND EXISTS (
                SELECT 1 FROM sys.extended_properties
                WHERE class = 0 AND name = N'SynchroFixtureDeposito'
                  AND CONVERT(nvarchar(10),value) = N'1'
              )
              AND OBJECT_ID(N'dbo.CobrosRetiroCargaSynchro', N'U') IS NOT NULL
              AND OBJECT_ID(N'dbo.TarifasCargaSynchro', N'U') IS NOT NULL
            THEN 1 ELSE 0 END;
            """, db);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancel)) == 1;
    }

    private static SqlCommand Comando(SqlConnection db, SqlTransaction tx,
        string consulta, Guid id)
    {
        var cmd = new SqlCommand(consulta, db, tx);
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        return cmd;
    }

    private static async Task<EstadoCarga?> LeerSesion(SqlConnection db,
        SqlTransaction tx, Guid id, CancellationToken cancel)
    {
        await using var cmd = Comando(db, tx, """
            SELECT s.CodigoEspacio, s.Dispositivo, s.Modalidad, s.InicioUtc,
                   s.Estado, s.RetiroUtc, e.Estado
            FROM dbo.SesionesCargaSynchro AS s WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.EspaciosCarga AS e WITH (UPDLOCK, HOLDLOCK)
              ON e.Codigo = s.CodigoEspacio
            WHERE s.Id = @id;
            """, id);
        await using var reader = await cmd.ExecuteReaderAsync(cancel);
        if (!await reader.ReadAsync(cancel)) return null;
        return new EstadoCarga(
            reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.GetDateTime(3), reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            reader.GetString(6));
    }

    private static async Task<CobroCarga?> LeerCobro(SqlConnection db,
        SqlTransaction tx, Guid id, CancellationToken cancel)
    {
        await using var cmd = Comando(db, tx, """
            SELECT MinutosUtilizados, ImporteHnl, DescripcionTarifa,
                   Estado, MetodoPago
            FROM dbo.CobrosRetiroCargaSynchro WITH (UPDLOCK, HOLDLOCK)
            WHERE SesionId = @id;
            """, id);
        await using var reader = await cmd.ExecuteReaderAsync(cancel);
        if (!await reader.ReadAsync(cancel)) return null;
        return new CobroCarga(
            reader.GetInt32(0), reader.GetDecimal(1), reader.GetString(2),
            reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private static bool EstaActiva(EstadoCarga? sesion) =>
        sesion is {
            Estado: "Activa",
            RetiroUtc: null,
            EstadoEspacio: "Ocupado"
        };

    public static void MapRetiroCargaCobroEndpoints(this WebApplication app)
    {
        var rutas = app.MapGroup("/api/terminales/carga/retiro/cobro")
            .AllowAnonymous()
            .RequireRateLimiting("RetiroPersonal");

        // La cotizacion queda persistida una sola vez por sesion. Reintentar no
        // altera el importe ni recalcula minutos después de prepararla.
        rutas.MapPost("/preparar", async (
            PrepararCobroCargaSolicitud solicitud, HttpContext http,
            IAntiforgery csrf, IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!Permitido(app, config)) return Results.NotFound();
            if (!http.Request.IsHttps || !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);
            if (!LeerId(solicitud.TokenCobro, proteccion, out var id))
                return Results.BadRequest(new { mensaje = "Verificacion vencida: repite rostro y PIN." });

            try
            {
                await using var db = new SqlConnection(config.GetConnectionString("SynchroDB"));
                await db.OpenAsync(http.RequestAborted);
                if (!await EsquemaValido(db, http.RequestAborted))
                    return Results.Problem(statusCode: 503,
                        title: "Falta preparar el modulo de cobro de carga en SQL de pruebas.");

                await using var tx = (SqlTransaction)await db.BeginTransactionAsync(
                    IsolationLevel.Serializable, http.RequestAborted);
                var sesion = await LeerSesion(db, tx, id, http.RequestAborted);
                if (!EstaActiva(sesion))
                    return Results.Conflict(new { mensaje = "La carga no esta activa para cobro." });

                var cobro = await LeerCobro(db, tx, id, http.RequestAborted);
                if (cobro is null)
                {
                    int minutosBase;
                    int minutosAdicionales;
                    decimal precioBase;
                    decimal precioAdicional;

                    await using (var tarifa = new SqlCommand("""
                        SELECT MinutosBase, PrecioBase,
                               MinutosAdicionales, PrecioAdicional
                        FROM dbo.TarifasCargaSynchro WITH (HOLDLOCK)
                        WHERE Modalidad = @modalidad;
                        """, db, tx))
                    {
                        tarifa.Parameters.Add("@modalidad", SqlDbType.NVarChar, 20)
                            .Value = sesion!.Modalidad;
                        await using var reader = await tarifa.ExecuteReaderAsync(http.RequestAborted);
                        if (!await reader.ReadAsync(http.RequestAborted))
                            return Results.Conflict(new { mensaje = "No hay tarifa de carga configurada." });
                        minutosBase = reader.GetInt32(0);
                        precioBase = reader.GetDecimal(1);
                        minutosAdicionales = reader.GetInt32(2);
                        precioAdicional = reader.GetDecimal(3);
                        if (await reader.ReadAsync(http.RequestAborted))
                            return Results.Conflict(new { mensaje = "Existe mas de una tarifa de carga." });
                    }

                    if (minutosBase < 1 || minutosAdicionales < 1 ||
                        precioBase <= 0 || precioAdicional <= 0)
                        return Results.Conflict(new { mensaje = "Tarifa de carga invalida." });

                    var transcurrido = DateTime.UtcNow - sesion!.InicioUtc;
                    var minutosLargos = Math.Max(1L,
                        (long)Math.Ceiling(Math.Max(0, transcurrido.TotalSeconds) / 60d));
                    if (minutosLargos > int.MaxValue)
                        return Results.Conflict(new { mensaje = "Duracion fuera de rango." });
                    int minutos = (int)minutosLargos;
                    long bloquesExtra = minutos > minutosBase
                        ? ((long)minutos - minutosBase + minutosAdicionales - 1) / minutosAdicionales
                        : 0;
                    decimal importe = checked(precioBase + bloquesExtra * precioAdicional);
                    if (importe <= 0 || importe > 9999999999.99m)
                        return Results.Conflict(new { mensaje = "Importe fuera de rango." });

                    string descripcion = $"{sesion.Dispositivo} ({sesion.Modalidad}): {minutos} min; " +
                        $"base {minutosBase} min y {bloquesExtra} bloque(s) adicionales de {minutosAdicionales} min.";
                    if (descripcion.Length > 160)
                        return Results.Conflict(new { mensaje = "Descripcion de tarifa invalida." });

                    await using var insertar = Comando(db, tx, """
                        INSERT dbo.CobrosRetiroCargaSynchro
                            (SesionId,CodigoEspacio,MinutosUtilizados,
                             ImporteHnl,DescripcionTarifa,Estado)
                        VALUES (@id,@espacio,@minutos,@importe,@descripcion,N'Pendiente');
                        """, id);
                    insertar.Parameters.Add("@espacio", SqlDbType.NVarChar, 10).Value = sesion.Codigo;
                    insertar.Parameters.Add("@minutos", SqlDbType.Int).Value = minutos;
                    var monto = insertar.Parameters.Add("@importe", SqlDbType.Decimal);
                    monto.Precision = 12;
                    monto.Scale = 2;
                    monto.Value = importe;
                    insertar.Parameters.Add("@descripcion", SqlDbType.NVarChar, 160).Value = descripcion;
                    if (await insertar.ExecuteNonQueryAsync(http.RequestAborted) != 1)
                        return Results.Conflict(new { mensaje = "No se pudo preparar el cobro." });
                    cobro = new CobroCarga(minutos, importe, descripcion, "Pendiente", null);
                }
                tx.Commit();
                return Results.Ok(new
                {
                    codigoEspacio = sesion!.Codigo,
                    dispositivo = sesion.Dispositivo,
                    minutosUtilizados = cobro.Minutos,
                    importeHnl = cobro.Importe,
                    descripcionTarifa = cobro.Descripcion,
                    estadoCobro = cobro.Estado,
                    metodoPago = cobro.MetodoPago switch
                    {
                        "EfectivoSimulado" => "Efectivo",
                        "TarjetaSimulada" => "Tarjeta",
                        "NfcSimulado" => "NFC",
                        _ => (string?)null
                    },
                    pagoSimulado = cobro.Estado is "ConfirmadoSimulado" or "Aplicado",
                    retiroConfirmado = false
                });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503, title: "No se pudo preparar el cobro de carga.");
            }
            catch (OverflowException)
            {
                return Results.Problem(statusCode: 503, title: "Importe fuera de rango.");
            }
        });

        // La tablet solo declara el metodo: NUNCA confirma el pago.
        // Un Operador o Administrador debe aprobarlo en /api/admin/carga/cobros.
        rutas.MapPost("/seleccionar-metodo", async (
            ConfirmarPagoCargaSolicitud solicitud, HttpContext http,
            IAntiforgery csrf, IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!Permitido(app, config)) return Results.NotFound();
            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);
            string? metodo = solicitud.MetodoPago switch
            {
                "Efectivo" => "EfectivoSimulado",
                "Tarjeta" => "TarjetaSimulada",
                "NFC" => "NfcSimulado",
                _ => null
            };
            if (metodo is null)
                return Results.BadRequest(new {
                    mensaje = "Metodo de pago invalido."
                });
            if (!LeerId(solicitud.TokenCobro, proteccion, out var id))
                return Results.BadRequest(new {
                    mensaje = "Verificacion vencida: repite rostro y PIN."
                });

            try
            {
                await using var db = new SqlConnection(
                    config.GetConnectionString("SynchroDB"));
                await db.OpenAsync(http.RequestAborted);
                if (!await EsquemaValido(db, http.RequestAborted))
                    return Results.Problem(statusCode: 503,
                        title: "Falta el modulo de cobro de carga.");
                await using var tx = (SqlTransaction)
                    await db.BeginTransactionAsync(
                        IsolationLevel.Serializable, http.RequestAborted);
                var sesion = await LeerSesion(db, tx, id, http.RequestAborted);
                if (!EstaActiva(sesion))
                    return Results.Conflict(new {
                        mensaje = "La sesion de carga no esta activa."
                    });
                var cobro = await LeerCobro(db, tx, id, http.RequestAborted);
                if (cobro is null)
                    return Results.Conflict(new {
                        mensaje = "Prepara primero el cobro."
                    });

                if (cobro.Estado == "Pendiente" &&
                    (cobro.MetodoPago is null || cobro.MetodoPago == metodo))
                {
                    await using var seleccionar = Comando(db, tx, """
                        UPDATE dbo.CobrosRetiroCargaSynchro
                        SET MetodoPago=@metodo
                        WHERE SesionId=@id AND Estado=N'Pendiente'
                          AND (MetodoPago IS NULL OR MetodoPago=@metodo);
                        """, id);
                    seleccionar.Parameters.Add("@metodo",
                        SqlDbType.NVarChar, 24).Value = metodo;
                    if (await seleccionar.ExecuteNonQueryAsync(
                        http.RequestAborted) != 1)
                        return Results.Conflict(new {
                            mensaje = "No se pudo registrar el metodo de pago."
                        });
                }
                else if (cobro.Estado != "ConfirmadoSimulado" ||
                    cobro.MetodoPago != metodo)
                    return Results.Conflict(new {
                        mensaje = "El cobro tiene otro metodo o estado."
                    });
                await tx.CommitAsync(http.RequestAborted);
                return Results.Ok(new
                {
                    metodoSeleccionado = true,
                    pagoConfirmado = cobro.Estado == "ConfirmadoSimulado",
                    pagoSimulado = true,
                    pagoRealProcesado = false,
                    retiroConfirmado = false,
                    codigoEspacio = sesion!.Codigo
                });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503,
                    title: "No se pudo seleccionar el metodo de pago.");
            }
        });

        // El estado es consultable con la autorizacion temporal de rostro+PIN.
        // La tablet no puede cambiar el estado a ConfirmadoSimulado.
        rutas.MapPost("/estado", async (
            PrepararCobroCargaSolicitud solicitud, HttpContext http,
            IAntiforgery csrf, IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!Permitido(app, config)) return Results.NotFound();
            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);
            if (!LeerId(solicitud.TokenCobro, proteccion, out var id))
                return Results.BadRequest(new {
                    mensaje = "Verificacion vencida: repite rostro y PIN."
                });
            try
            {
                await using var db = new SqlConnection(
                    config.GetConnectionString("SynchroDB"));
                await db.OpenAsync(http.RequestAborted);
                if (!await EsquemaValido(db, http.RequestAborted))
                    return Results.Problem(statusCode: 503,
                        title: "Falta el modulo de cobro de carga.");

                await using var cmd = new SqlCommand("""
                    SELECT c.Estado, c.MetodoPago, c.CodigoEspacio
                    FROM dbo.CobrosRetiroCargaSynchro AS c
                    INNER JOIN dbo.SesionesCargaSynchro AS s
                      ON s.Id = c.SesionId
                    INNER JOIN dbo.EspaciosCarga AS e
                      ON e.Codigo = c.CodigoEspacio
                    WHERE c.SesionId = @id
                      AND s.CodigoEspacio=c.CodigoEspacio
                      AND s.Estado=N'Activa'
                      AND s.RetiroUtc IS NULL
                      AND e.Estado=N'Ocupado'
                      AND c.Estado IN (
                        N'Pendiente',N'ConfirmadoSimulado');
                    """, db);
                cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value=id;
                await using var lector =
                    await cmd.ExecuteReaderAsync(http.RequestAborted);
                if (!await lector.ReadAsync(http.RequestAborted))
                    return Results.Conflict(new {
                        mensaje = "El cobro no esta disponible."
                    });
                var estado = lector.GetString(0);
                var metodo = lector.IsDBNull(1) ? null : lector.GetString(1);
                var codigo = lector.GetString(2);
                return Results.Ok(new
                {
                    estadoCobro = estado,
                    pagoConfirmado = estado == "ConfirmadoSimulado",
                    metodoPago = metodo switch
                    {
                        "EfectivoSimulado" => "Efectivo",
                        "TarjetaSimulada" => "Tarjeta",
                        "NfcSimulado" => "NFC",
                        _ => (string?)null
                    },
                    codigoEspacio = codigo,
                    retiroConfirmado = false,
                    pagoRealProcesado = false
                });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503,
                    title: "No se pudo consultar el estado del cobro.");
            }
        });

        // Solo tras pago simulado y declaracion explícita de recogida. Transacción
        // única: sesión cerrada, espacio liberado y cobro aplicado o nada.
        rutas.MapPost("/confirmar-recogida", async (
            ConfirmarRecogidaCargaSolicitud solicitud, HttpContext http,
            IAntiforgery csrf, IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!Permitido(app, config)) return Results.NotFound();
            if (!http.Request.IsHttps || !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);
            if (!solicitud.DispositivoRetirado)
                return Results.BadRequest(new { mensaje = "Confirma primero que has retirado el dispositivo." });
            if (!LeerId(solicitud.TokenCobro, proteccion, out var id))
                return Results.BadRequest(new { mensaje = "Verificacion vencida: repite rostro y PIN." });

            try
            {
                await using var db = new SqlConnection(config.GetConnectionString("SynchroDB"));
                await db.OpenAsync(http.RequestAborted);
                if (!await EsquemaValido(db, http.RequestAborted))
                    return Results.Problem(statusCode: 503, title: "Falta el modulo de cobro de carga.");

                await using var tx = (SqlTransaction)await db.BeginTransactionAsync(
                    IsolationLevel.Serializable, http.RequestAborted);
                var sesion = await LeerSesion(db, tx, id, http.RequestAborted);
                if (sesion is null)
                    return Results.Conflict(new { mensaje = "Sesion no encontrada." });
                var cobro = await LeerCobro(db, tx, id, http.RequestAborted);
                if (cobro is null)
                    return Results.Conflict(new { mensaje = "El cobro no existe." });

                if (sesion.Estado == "Retirada" && sesion.RetiroUtc is not null &&
                    cobro.Estado == "Aplicado" && sesion.EstadoEspacio == "Disponible")
                {
                    tx.Commit();
                    return Results.Ok(new {
                        retiroConfirmado = true, codigoEspacio = sesion.Codigo,
                        espacioDisponible = true, pagoSimulado = true
                    });
                }

                if (!EstaActiva(sesion) || cobro.Estado != "ConfirmadoSimulado")
                    return Results.Conflict(new { mensaje = "Falta el pago simulado o la sesion no esta activa." });

                await using (var cerrar = Comando(db, tx, """
                    UPDATE dbo.SesionesCargaSynchro
                    SET Estado=N'Retirada', RetiroUtc=SYSUTCDATETIME()
                    WHERE Id=@id AND Estado=N'Activa' AND RetiroUtc IS NULL;
                    """, id))
                {
                    if (await cerrar.ExecuteNonQueryAsync(http.RequestAborted) != 1)
                        return Results.Conflict(new { mensaje = "No se pudo finalizar la sesion." });
                }

                await using (var liberar = Comando(db, tx, """
                    UPDATE dbo.EspaciosCarga
                    SET Estado=N'Disponible'
                    WHERE Codigo=(SELECT CodigoEspacio FROM dbo.SesionesCargaSynchro WHERE Id=@id)
                      AND Estado=N'Ocupado';
                    """, id))
                {
                    if (await liberar.ExecuteNonQueryAsync(http.RequestAborted) != 1)
                        return Results.Conflict(new { mensaje = "No se pudo liberar el espacio." });
                }

                await using (var aplicar = Comando(db, tx, """
                    UPDATE dbo.CobrosRetiroCargaSynchro
                    SET Estado=N'Aplicado', FinalizadoUtc=SYSUTCDATETIME()
                    WHERE SesionId=@id AND Estado=N'ConfirmadoSimulado';
                    """, id))
                {
                    if (await aplicar.ExecuteNonQueryAsync(http.RequestAborted) != 1)
                        return Results.Conflict(new { mensaje = "No se pudo finalizar el registro del cobro." });
                }

                tx.Commit();
                return Results.Ok(new
                {
                    retiroConfirmado = true,
                    codigoEspacio = sesion.Codigo,
                    espacioDisponible = true, pagoSimulado = true,
                    pagoRealProcesado = false, importeHnl = cobro.Importe
                });
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503, title: "No se pudo finalizar el retiro de carga.");
            }
        });
    }
}
