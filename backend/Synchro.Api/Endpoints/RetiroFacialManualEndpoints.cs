using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Endpoints;

// Una solicitud de PIN nunca debe volcar su valor en logs diagnosticos.
public sealed record RetiroManualPinSolicitud(string? Pin, string? TokenFacial)
{
    public override string ToString() => "Solicitud de retiro protegida";
}

// SOLO prototipo en Development + SynchroDB_Pruebas + modulo manual habilitado.
// Human.js detecta/captura; este servidor compara PATRONES VISUALES SIMULADOS.
// No hay verificacion biometrica real ni prueba de vida.
internal static class RetiroFacialManualEndpoints
{
    private const int MaxPngBytes = 1024 * 1024;
    private const int MaxCandidatos = 100;
    private const int MaxCoincidencias = 12;
    private const string ContextoPatron = "SYNCHRO.RostroManual.Uso.v1";
    private const string ContextoAutorizacion = "SYNCHRO.RostroManual.RetiroAutorizado.v1";
    private const string MensajeRechazo = "No fue posible autorizar el retiro con los datos proporcionados.";

    // SYNCHRO-RETO-PIN-V1: control exclusivo del prototipo local.
    private sealed class ControlRetiro
    {
        internal int Intentos;
        internal int Consumido;
        internal DateTimeOffset VenceUtc;

        internal ControlRetiro(DateTimeOffset venceUtc)
        {
            VenceUtc = venceUtc;
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ControlRetiro>
        ControlesRetiro = new();

    private static string ClaveDeToken(string token) =>
        Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token)));

    internal static byte[] ProtegerPatron(IDataProtectionProvider proveedor, int usoId, double[] patron)
    {
        byte[] plano = JsonSerializer.SerializeToUtf8Bytes(patron);
        try
        {
            return proveedor.CreateProtector(ContextoPatron, usoId.ToString(CultureInfo.InvariantCulture)).Protect(plano);
        }
        finally { CryptographicOperations.ZeroMemory(plano); }
    }

    private static double[]? RecuperarPatron(IDataProtectionProvider proveedor, int usoId, byte[] cifrado)
    {
        byte[] plano = proveedor.CreateProtector(ContextoPatron, usoId.ToString(CultureInfo.InvariantCulture)).Unprotect(cifrado);
        try { return JsonSerializer.Deserialize<double[]>(plano); }
        finally { CryptographicOperations.ZeroMemory(plano); }
    }

    internal static void MapRetiroFacialManualEndpoints(this WebApplication app, string cadena)
    {
        var grupo = app.MapGroup("/api/terminales/guardar/whatsapp/retiro")
            .AllowAnonymous()
            .RequireRateLimiting("RetiroPersonal");

        grupo.MapPost("/rostro", async (HttpContext http, IAntiforgery csrf,
            IDataProtectionProvider proveedor, IConfiguration config,
            ILogger<Program> logger) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!http.Request.IsHttps || !await csrf.IsRequestValidAsync(http)) return Results.StatusCode(403);
            if (http.Request.ContentType?.Split(';')[0] != "application/octet-stream" ||
                http.Request.Headers["X-SYNCHRO-CONSENTIMIENTO"].ToString() != "ACEPTO")
                return Results.BadRequest(new { mensaje = "Captura o consentimiento no disponible." });
            if (http.Request.ContentLength > MaxPngBytes) return Results.StatusCode(413);

            byte[]? imagen = null;
            double[]? muestra = null;
            double[]? muestraV2 = null;
            try
            {
                using var memoria = new MemoriaProtegida();
                byte[] buffer = new byte[8192];
                try
                {
                    int n;
                    while ((n = await http.Request.Body.ReadAsync(buffer, http.RequestAborted)) > 0)
                    {
                        if (memoria.Length + n > MaxPngBytes) return Results.StatusCode(413);
                        memoria.Write(buffer, 0, n);
                    }
                    imagen = memoria.ToArray();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(buffer);
                    if (memoria.TryGetBuffer(out var datos))
                        CryptographicOperations.ZeroMemory(datos.AsSpan());
                }

                muestra = PatronVisualSimulado.Extraer(imagen);
                if (muestra is null) return Results.BadRequest(new { mensaje = "Imagen PNG de demostracion invalida." });
                muestraV2 = PatronVisualRetiroV2.Transformar(muestra);
                if (muestraV2 is null) return Results.BadRequest(new { mensaje = "Captura visual no utilizable." });

                double umbral = config.GetValue("RostroSimulado:UmbralDistancia", 0.25d);
                if (!double.IsFinite(umbral) || umbral < 0 || umbral > 1)
                    return Results.Problem(statusCode: 503, title: "Comparacion simulada no configurada.");

                var coincidencias = new List<int>();
                // Solo contadores y una categoría de diferencia para diagnóstico
                // local. Nunca registrar imágenes, descriptores, PIN o UsoId.
                int referenciasExaminadas = 0;
                int distanciasValidas = 0;
                double minimoExceso = double.PositiveInfinity;
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using var consulta = new SqlCommand("""
                    SELECT TOP (101) u.Id, r.PatronProtegido, r.Modelo
                    FROM dbo.UsosCasillero AS u
                    INNER JOIN dbo.AutorizacionesWhatsAppManuales AS a
                        ON a.UsoId = u.Id AND a.Estado = N'Confirmado'
                    INNER JOIN dbo.RostrosRetiroPersonalManual AS r
                        ON r.UsoId = u.Id
                        AND r.Modelo IN (@ModeloV1, @ModeloV2)
                    WHERE u.Estado = N'Ocupado' AND u.FechaRetiro IS NULL
                      AND u.TipoServicio = N'ResguardoPersonal'
                      AND u.OrigenReserva = N'Terminal'
                    ORDER BY u.Id DESC
                    """, db);
                consulta.Parameters.Add("@ModeloV1", SqlDbType.NVarChar, 60).Value = PatronVisualSimulado.Modelo;
                consulta.Parameters.Add("@ModeloV2", SqlDbType.NVarChar, 60).Value = PatronVisualRetiroV2.Modelo;
                await using var lector = await consulta.ExecuteReaderAsync(http.RequestAborted);
                int examinados = 0;
                while (await lector.ReadAsync(http.RequestAborted))
                {
                    if (++examinados > MaxCandidatos)
                        return Results.Problem(statusCode: 503, title: "Demasiadas operaciones para la demostracion.");
                    referenciasExaminadas++;
                    int usoId = lector.GetInt32(0);
                    byte[] cifrado = (byte[])lector.GetValue(1);
                    bool esV2 = string.Equals(lector.GetString(2), PatronVisualRetiroV2.Modelo,
                        StringComparison.Ordinal);
                    double[]? guardado = null;
                    try
                    {
                        guardado = RecuperarPatron(proveedor, usoId, cifrado);
                        // Referencias v1 existentes (como M07) siguen usando
                        // EXACTAMENTE su comparador y umbral anteriores.
                        double limite = esV2 ? PatronVisualRetiroV2.UmbralDistancia : umbral;
                        double? distancia = esV2
                            ? PatronVisualRetiroV2.CalcularDistancia(guardado, muestraV2)
                            : ComparadorDescriptoresFaciales.CalcularDistancia(guardado, muestra);
                        if (distancia.HasValue)
                        {
                            distanciasValidas++;
                            minimoExceso = Math.Min(minimoExceso, distancia.Value - limite);
                        }
                        if (distancia.HasValue && distancia.Value <= limite)
                        {
                            coincidencias.Add(usoId);
                            if (coincidencias.Count > MaxCoincidencias)
                                return Results.Problem(statusCode: 409, title: "Demasiadas coincidencias simuladas.");
                        }
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(cifrado);
                        if (guardado is not null) Array.Clear(guardado);
                    }
                }
                if (coincidencias.Count == 0)
                {
                    // Opt-in y solo Development en la BD aislada: el cliente sigue
                    // recibiendo el mismo rechazo, sin exposición de similitudes.
                    if (app.Environment.IsDevelopment() &&
                        config.GetValue<bool>("RetiroPersonal:DiagnosticoFacial"))
                    {
                        string categoria = double.IsPositiveInfinity(minimoExceso)
                            ? "sin-distancia-calculable"
                            : minimoExceso <= 0.05d
                                ? "cerca-del-umbral"
                                : minimoExceso <= 0.25d
                                    ? "diferencia-moderada"
                                    : "diferencia-alta";
                        logger.LogInformation(
                            "SYNCHRO diagnóstico rostro rechazo: referencias={Referencias}, distanciasValidas={Distancias}, categoria={Categoria}. No se autorizó retiro.",
                            referenciasExaminadas, distanciasValidas, categoria);
                    }
                    return Results.BadRequest(new { mensaje = MensajeRechazo });
                }

                // Token protegido temporal: no se envian IDs de reservas al navegador.
                string texto = string.Join(',', coincidencias);
                string token = proveedor.CreateProtector(ContextoAutorizacion)
                    .ToTimeLimitedDataProtector().Protect(texto, TimeSpan.FromMinutes(2));

                foreach (var entrada in ControlesRetiro)
                {
                    if (entrada.Value.VenceUtc <= DateTimeOffset.UtcNow)
                        ControlesRetiro.TryRemove(entrada.Key, out _);
                }

                ControlesRetiro[ClaveDeToken(token)] =
                    new ControlRetiro(DateTimeOffset.UtcNow.AddMinutes(2));
                return Results.Ok(new { verificacionSimulada = true, tokenFacial = token });
            }
            catch (SqlException) { return Results.Problem(statusCode: 503, title: "Servicio facial no disponible."); }
            catch (CryptographicException) { return Results.Problem(statusCode: 503, title: "Referencias faciales no disponibles."); }
            catch (JsonException) { return Results.Problem(statusCode: 503, title: "Referencia simulada no disponible."); }
            catch (InvalidOperationException) { return Results.Problem(statusCode: 503, title: "Verificacion simulada no disponible."); }
            finally
            {
                if (imagen is not null) CryptographicOperations.ZeroMemory(imagen);
                if (muestra is not null) Array.Clear(muestra);
                if (muestraV2 is not null) Array.Clear(muestraV2);
            }
        });

        // SYNCHRO-COBRO-POSTERIOR-PREVALIDAR-V1
        // No registra retiro, no consume PIN y no confirma pagos.
        grupo.MapPost("/pin/prevalidar", async (
            RetiroManualPinSolicitud solicitud,
            HttpContext http,
            IAntiforgery csrf,
            IDataProtectionProvider proveedor,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);

            if (solicitud.Pin is not { Length: 6 } ||
                !solicitud.Pin.All(char.IsAsciiDigit) ||
                solicitud.TokenFacial is null ||
                solicitud.TokenFacial.Length is < 32 or > 4096)
                return Results.BadRequest(new { mensaje = MensajeRechazo });

            int[] candidatos;

            try
            {
                string texto = proveedor
                    .CreateProtector(ContextoAutorizacion)
                    .ToTimeLimitedDataProtector()
                    .Unprotect(solicitud.TokenFacial);

                candidatos = texto
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.Parse(x, CultureInfo.InvariantCulture))
                    .ToArray();

                if (candidatos.Length is < 1 or > MaxCoincidencias ||
                    candidatos.Any(x => x <= 0) ||
                    candidatos.Distinct().Count() != candidatos.Length)
                    return Results.BadRequest(new { mensaje = MensajeRechazo });
            }
            catch (Exception ex) when (
                ex is CryptographicException or FormatException or OverflowException)
            {
                return Results.BadRequest(new {
                    mensaje = "La autorizacion facial vencio. Repite el escaneo."
                });
            }

            if (!ControlesRetiro.TryGetValue(
                    ClaveDeToken(solicitud.TokenFacial), out var control) ||
                control.VenceUtc <= DateTimeOffset.UtcNow ||
                Volatile.Read(ref control.Consumido) != 0)
                return Results.BadRequest(new { mensaje = MensajeRechazo });

            if (Interlocked.Increment(ref control.Intentos) > 5)
                return Results.StatusCode(429);

            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);

                var coincidenciasPin = new List<int>();

                foreach (int usoId in candidatos)
                {
                    byte[]? sal = null;
                    byte[]? guardado = null;
                    byte[]? calculado = null;

                    try
                    {
                        await using var buscar = new SqlCommand("""
                            SELECT p.PinSalt, p.PinHash
                            FROM dbo.CodigosRetiroPersonal p
                            JOIN dbo.UsosCasillero u
                                ON u.Id = p.UsoId
                            JOIN dbo.AutorizacionesWhatsAppManuales a
                                ON a.UsoId = u.Id
                                AND a.Estado = N'Confirmado'
                            JOIN dbo.RostrosRetiroPersonalManual r
                                ON r.UsoId = u.Id
                            WHERE u.Id = @Uso
                              AND u.Estado = N'Ocupado'
                              AND u.FechaRetiro IS NULL
                              AND u.TipoServicio = N'ResguardoPersonal'
                              AND u.OrigenReserva = N'Terminal'
                              AND p.FechaUso IS NULL
                              AND p.IntentosFallidos < 5
                              AND (
                                p.FechaVencimiento IS NULL OR
                                p.FechaVencimiento > SYSUTCDATETIME()
                              )
                            """, db);

                        buscar.Parameters.Add("@Uso", SqlDbType.Int).Value = usoId;

                        await using var lector =
                            await buscar.ExecuteReaderAsync(http.RequestAborted);

                        if (!await lector.ReadAsync(http.RequestAborted))
                            continue;

                        sal = (byte[])lector.GetValue(0);
                        guardado = (byte[])lector.GetValue(1);

                        if (sal.Length != 32 || guardado.Length != 32)
                            continue;

                        calculado = CodigoPinRetiroPersonal.CalcularHash(
                            solicitud.Pin, usoId, sal, config);

                        if (CryptographicOperations.FixedTimeEquals(
                            calculado, guardado))
                            coincidenciasPin.Add(usoId);
                    }
                    finally
                    {
                        if (sal is not null)
                            CryptographicOperations.ZeroMemory(sal);

                        if (guardado is not null)
                            CryptographicOperations.ZeroMemory(guardado);

                        if (calculado is not null)
                            CryptographicOperations.ZeroMemory(calculado);
                    }
                }

                // No adivinar a que operacion corresponde el PIN.
                if (coincidenciasPin.Count > 1)
                    return Results.BadRequest(new { mensaje = MensajeRechazo });

                int usoSeleccionado = coincidenciasPin.Count == 1
                    ? coincidenciasPin[0]
                    : candidatos.Length == 1 ? candidatos[0] : 0;

                if (usoSeleccionado <= 0)
                    return Results.BadRequest(new { mensaje = MensajeRechazo });

                byte[]? salFinal = null;
                byte[]? hashFinal = null;

                try
                {
                    await using (var buscarSal = new SqlCommand("""
                        SELECT PinSalt
                        FROM dbo.CodigosRetiroPersonal
                        WHERE UsoId = @Uso
                        """, db))
                    {
                        buscarSal.Parameters.Add(
                            "@Uso", SqlDbType.Int).Value = usoSeleccionado;

                        salFinal = await buscarSal.ExecuteScalarAsync(
                            http.RequestAborted) as byte[];
                    }

                    if (salFinal is not { Length: 32 })
                        return Results.BadRequest(new { mensaje = MensajeRechazo });

                    hashFinal = CodigoPinRetiroPersonal.CalcularHash(
                        solicitud.Pin, usoSeleccionado, salFinal, config);

                    await using var validar = new SqlCommand(
                        "dbo.PrevalidarRetiroPersonalSynchro", db)
                    {
                        CommandType = CommandType.StoredProcedure
                    };

                    validar.Parameters.Add(
                        "@UsoId", SqlDbType.Int).Value = usoSeleccionado;

                    validar.Parameters.Add(
                        "@PinHashCandidato", SqlDbType.VarBinary, 32)
                        .Value = hashFinal;

                    validar.Parameters.Add(
                        "@PinSaltLeido", SqlDbType.VarBinary, 32)
                        .Value = salFinal;

                    string codigoCasillero;
                    int minutos;

                    await using (var resultado =
                        await validar.ExecuteReaderAsync(http.RequestAborted))
                    {
                        if (!await resultado.ReadAsync(http.RequestAborted) ||
                            !resultado.GetBoolean(
                                resultado.GetOrdinal("PinValido")))
                            return Results.BadRequest(new { mensaje = MensajeRechazo });

                        codigoCasillero = resultado.GetString(
                            resultado.GetOrdinal("CodigoCasillero"));

                        minutos = resultado.GetInt32(
                            resultado.GetOrdinal("MinutosUtilizados"));
                    }

                    // Token protegido de corta duracion.
                    // NO constituye comprobante de pago.
                    string autorizacionPago = proveedor
                        .CreateProtector(
                            "SYNCHRO.RostroManual.PagoPendiente.v1")
                        .ToTimeLimitedDataProtector()
                        .Protect(
                            usoSeleccionado.ToString(
                                CultureInfo.InvariantCulture),
                            TimeSpan.FromMinutes(5));

                    // No permitir reutilizar esta captura facial.
                    if (Interlocked.CompareExchange(
                            ref control.Consumido, 1, 0) != 0)
                        return Results.BadRequest(new { mensaje = MensajeRechazo });

                    return Results.Ok(new
                    {
                        pinValidado = true,
                        pagoPendiente = true,
                        codigoCasillero,
                        minutosUtilizados = minutos,
                        estadoCasillero = "Ocupado",
                        autorizacionPago,
                        pagoConfirmado = false,
                        retiroConfirmado = false
                    });
                }
                finally
                {
                    if (salFinal is not null)
                        CryptographicOperations.ZeroMemory(salFinal);

                    if (hashFinal is not null)
                        CryptographicOperations.ZeroMemory(hashFinal);
                }
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudo verificar la operacion.");
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "Verificacion del PIN no disponible.");
            }
            catch (FormatException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "Configuracion del PIN no disponible.");
            }
        });

        grupo.MapPost("/pin", async (RetiroManualPinSolicitud solicitud, HttpContext http,
            IAntiforgery csrf, IDataProtectionProvider proveedor, IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!http.Request.IsHttps || !await csrf.IsRequestValidAsync(http)) return Results.StatusCode(403);
            // SYNCHRO-BLOQUEO-RETIRO-DIRECTO-V1
            if (config.GetValue<bool>("CobroRetiroPersonal:Obligatorio"))
                return Results.Conflict(new {
                    mensaje = "Se requiere pago antes del retiro.",
                    pagoObligatorio = true,
                    retiroConfirmado = false
                });
            if (solicitud.Pin is not { Length: 6 } || !solicitud.Pin.All(char.IsAsciiDigit) ||
                solicitud.TokenFacial is null || solicitud.TokenFacial.Length is < 32 or > 4096)
                return Results.BadRequest(new { mensaje = MensajeRechazo });

            int[] candidatos;
            try
            {
                string texto = proveedor.CreateProtector(ContextoAutorizacion)
                    .ToTimeLimitedDataProtector().Unprotect(solicitud.TokenFacial);
                candidatos = texto.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                if (candidatos.Length is < 1 or > MaxCoincidencias ||
                    candidatos.Any(x => x <= 0) || candidatos.Distinct().Count() != candidatos.Length)
                    return Results.BadRequest(new { mensaje = MensajeRechazo });
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or OverflowException)
            { return Results.BadRequest(new { mensaje = "Verificacion facial caducada. Repite el escaneo." }); }

            if (!ControlesRetiro.TryGetValue(
                    ClaveDeToken(solicitud.TokenFacial), out var control) ||
                control.VenceUtc <= DateTimeOffset.UtcNow ||
                Volatile.Read(ref control.Consumido) != 0)
            {
                return Results.BadRequest(new { mensaje = MensajeRechazo });
            }

            if (Interlocked.Increment(ref control.Intentos) > 5)
                return Results.StatusCode(429);

            byte[]? salt = null;
            byte[]? calculado = null;
            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                int? usoElegido = null;
                foreach (int usoId in candidatos)
                {
                    await using var buscar = new SqlCommand("""
                        SELECT p.PinSalt, p.PinHash
                        FROM dbo.CodigosRetiroPersonal AS p
                        INNER JOIN dbo.UsosCasillero AS u ON u.Id = p.UsoId
                        INNER JOIN dbo.AutorizacionesWhatsAppManuales AS a
                            ON a.UsoId = u.Id AND a.Estado = N'Confirmado'
                        INNER JOIN dbo.RostrosRetiroPersonalManual AS r ON r.UsoId = u.Id
                        WHERE u.Id = @Uso AND u.Estado = N'Ocupado'
                            AND u.FechaRetiro IS NULL AND u.TipoServicio = N'ResguardoPersonal'
                            AND u.OrigenReserva = N'Terminal' AND p.FechaUso IS NULL
                            AND (p.FechaVencimiento IS NULL OR p.FechaVencimiento > SYSUTCDATETIME())
                            AND p.IntentosFallidos < 5
                        """, db);
                    buscar.Parameters.Add("@Uso", SqlDbType.Int).Value = usoId;
                    await using var lector = await buscar.ExecuteReaderAsync(http.RequestAborted);
                    if (!await lector.ReadAsync(http.RequestAborted)) continue;
                    salt = (byte[])lector.GetValue(0);
                    byte[] hashGuardado = (byte[])lector.GetValue(1);
                    try
                    {
                        if (salt.Length != 32 || hashGuardado.Length != 32) continue;
                        calculado = CodigoPinRetiroPersonal.CalcularHash(solicitud.Pin, usoId, salt, config);
                        if (CryptographicOperations.FixedTimeEquals(calculado, hashGuardado))
                        {
                            if (usoElegido.HasValue) return Results.BadRequest(new { mensaje = MensajeRechazo });
                            usoElegido = usoId;
                        }
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(hashGuardado);
                        CryptographicOperations.ZeroMemory(salt);
                        if (calculado is not null) CryptographicOperations.ZeroMemory(calculado);
                        salt = null; calculado = null;
                    }
                }
                if (!usoElegido.HasValue)
                {
                    // No bloquear operaciones ajenas cuando varias
                    // referencias visuales coinciden en el prototipo.
                    if (candidatos.Length == 1)
                    {
                        int id = candidatos[0];

                        await using var leerSal = new SqlCommand(
                            "SELECT PinSalt FROM dbo.CodigosRetiroPersonal WHERE UsoId=@Uso", db);

                        leerSal.Parameters.Add("@Uso", SqlDbType.Int).Value = id;

                        byte[]? salActual = await leerSal.ExecuteScalarAsync(
                            http.RequestAborted) as byte[];

                        if (salActual is { Length: 32 })
                        {
                            try
                            {
                                byte[] hashIntento =
                                    CodigoPinRetiroPersonal.CalcularHash(
                                        solicitud.Pin, id, salActual, config);

                                try
                                {
                                    await using var contar = new SqlCommand("""
                                        IF DB_NAME() <> N'SynchroDB_Pruebas'
                                            THROW 57400, 'Base de pruebas incorrecta.', 1;

                                        UPDATE p
                                        SET IntentosFallidos = IntentosFallidos + 1
                                        FROM dbo.CodigosRetiroPersonal AS p
                                        INNER JOIN dbo.UsosCasillero AS u
                                            ON u.Id = p.UsoId
                                        INNER JOIN dbo.AutorizacionesWhatsAppManuales AS a
                                            ON a.UsoId = u.Id AND a.Estado = N'Confirmado'
                                        INNER JOIN dbo.RostrosRetiroPersonalManual AS r
                                            ON r.UsoId = u.Id
                                        WHERE p.UsoId = @Uso
                                          AND u.Estado = N'Ocupado'
                                          AND u.FechaRetiro IS NULL
                                          AND u.TipoServicio = N'ResguardoPersonal'
                                          AND u.OrigenReserva = N'Terminal'
                                          AND p.FechaUso IS NULL
                                          AND (p.FechaVencimiento IS NULL
                                               OR p.FechaVencimiento > SYSUTCDATETIME())
                                          AND p.IntentosFallidos < 5
                                          AND p.PinSalt = @Sal
                                          AND p.PinHash <> @Hash;
                                        """, db);

                                    contar.Parameters.Add("@Uso", SqlDbType.Int).Value = id;
                                    contar.Parameters.Add("@Sal", SqlDbType.VarBinary, 32).Value = salActual;
                                    contar.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = hashIntento;

                                    await contar.ExecuteNonQueryAsync(http.RequestAborted);
                                }
                                finally
                                {
                                    CryptographicOperations.ZeroMemory(hashIntento);
                                }
                            }
                            finally
                            {
                                CryptographicOperations.ZeroMemory(salActual);
                            }
                        }
                    }

                    return Results.BadRequest(new { mensaje = MensajeRechazo });
                }

                // La operacion SQL controla estado, atomicidad, consumo unico del PIN y liberacion.
                // Tras encontrar el PIN se lee de nuevo el salt: el SP verifica que no haya cambiado.
                await using var buscarFinal = new SqlCommand("""
                    SELECT PinSalt FROM dbo.CodigosRetiroPersonal WHERE UsoId=@Uso
                    """, db);
                buscarFinal.Parameters.Add("@Uso", SqlDbType.Int).Value = usoElegido.Value;
                salt = await buscarFinal.ExecuteScalarAsync(http.RequestAborted) as byte[];
                if (salt is not { Length: 32 }) return Results.BadRequest(new { mensaje = MensajeRechazo });
                calculado = CodigoPinRetiroPersonal.CalcularHash(solicitud.Pin, usoElegido.Value, salt, config);
                await using var retirar = new SqlCommand("dbo.RegistrarRetiroPersonalSynchro", db)
                { CommandType = CommandType.StoredProcedure };
                retirar.Parameters.Add("@UsoId", SqlDbType.Int).Value = usoElegido.Value;
                retirar.Parameters.Add("@PinHashCandidato", SqlDbType.VarBinary, 32).Value = calculado;
                retirar.Parameters.Add("@PinSaltLeido", SqlDbType.VarBinary, 32).Value = salt;
                await using var resultado = await retirar.ExecuteReaderAsync(http.RequestAborted);
                if (!await resultado.ReadAsync(http.RequestAborted) ||
                    !resultado.GetBoolean(resultado.GetOrdinal("RetiroConfirmado")))
                    return Results.BadRequest(new { mensaje = MensajeRechazo });
                string casillero = resultado.GetString(resultado.GetOrdinal("CodigoCasillero"));
                await resultado.DisposeAsync();
                await using var limpiar = new SqlCommand("DELETE dbo.RostrosRetiroPersonalManual WHERE UsoId=@Uso", db);
                limpiar.Parameters.Add("@Uso", SqlDbType.Int).Value = usoElegido.Value;
                await limpiar.ExecuteNonQueryAsync(http.RequestAborted);
                Interlocked.Exchange(ref control.Consumido, 1);

                return Results.Ok(new { retiroConfirmado = true, codigoCasillero = casillero, estadoCasillero = "Disponible", hardwareSimulado = true });
            }
            catch (SqlException) { return Results.Problem(statusCode: 503, title: "No se pudo confirmar el retiro."); }
            catch (InvalidOperationException) { return Results.Problem(statusCode: 503, title: "PIN no disponible."); }
            catch (FormatException) { return Results.Problem(statusCode: 503, title: "PIN no configurado."); }
            finally
            {
                if (salt is not null) CryptographicOperations.ZeroMemory(salt);
                if (calculado is not null) CryptographicOperations.ZeroMemory(calculado);
            }
        });
    }
}
