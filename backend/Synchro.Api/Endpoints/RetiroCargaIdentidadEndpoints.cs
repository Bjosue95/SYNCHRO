using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Endpoints;

public sealed record RostroRetiroCargaPeticion(
    string? CodigoEspacio,
    double[]? Descriptor
)
{
    public override string ToString() =>
        "Captura facial protegida de retiro";
}

public sealed record PinRetiroCargaPeticion(
    string? Pin,
    string? TokenFacial
)
{
    public override string ToString() =>
        "Verificacion protegida de PIN";
}

public static class RetiroCargaIdentidadEndpoints
{
    private const string Modelo =
        "SYNCHRO-HUMANJS-EMBEDDING-1024-v3";

    private const string Consentimiento =
        "SYNCHRO-FACIAL-V3-2026-11";

    private const string PropositoFacial =
        "SYNCHRO.Carga.RetiroFacial.v1";

    private const string PropositoPin =
        "SYNCHRO.Carga.RetiroPinValidado.v1";

    private const string Rechazo =
        "No se pudo verificar la identidad para el retiro.";

    private static bool EntornoValido(
        WebApplication app,
        IConfiguration config)
    {
        if (!app.Environment.IsDevelopment() ||
            !config.GetValue<bool>(
                "CargaDispositivos:RegistroHabilitado"))
            return false;

        string? cadena =
            config.GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
            return false;

        try
        {
            return ConexionSqlAislada.EsWhatsAppPermitido(
                new SqlConnectionStringBuilder(cadena)
            );
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static void MapRetiroCargaIdentidadEndpoints(
        this WebApplication app)
    {
        var grupo =
            app.MapGroup("/api/terminales/carga/retiro")
               .AllowAnonymous()
               .RequireRateLimiting("RetiroPersonal");

        // PASO 1: comparar el rostro de retiro V3.
        // No valida PIN, no cobra y no libera espacios.
        grupo.MapPost("/rostro-v3", async (
            RostroRetiroCargaPeticion datos,
            HttpContext http,
            IAntiforgery csrf,
            IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!EntornoValido(app, config))
                return Results.NotFound();

            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);

            if (
                http.Request.Headers[
                    "X-SYNCHRO-CONSENTIMIENTO"].ToString()
                != "ACEPTO"
            )
                return Results.BadRequest(new {
                    mensaje = "Se requiere consentimiento facial."
                });

            string? codigo = datos.CodigoEspacio;

            if (codigo is not (
                "W1" or "W2" or
                "C1" or "C2" or "C3" or "C4" or
                "L1" or "L2"))
                return Results.BadRequest(new {
                    mensaje = "Espacio de carga invalido."
                });

            double[]? muestra =
                DescriptorFacialV3.Normalizar(datos.Descriptor);

            if (muestra is null)
                return Results.BadRequest(new {
                    mensaje = "Captura facial V3 invalida."
                });

            byte[]? cifrado = null;
            byte[]? plano = null;
            double[]? referencia = null;

            try
            {
                double umbral = config.GetValue(
                    "RostroV3:UmbralDistanciaCoseno",
                    0.22d);

                if (!double.IsFinite(umbral) ||
                    umbral < 0d || umbral > 0.5d)
                    return Results.Problem(
                        statusCode: 503,
                        title: "Configuracion facial invalida."
                    );

                var cadena =
                    config.GetConnectionString("SynchroDB");

                await using var db =
                    new SqlConnection(cadena);

                await db.OpenAsync(http.RequestAborted);

                Guid sesionId = Guid.Empty;

                await using (var buscar = new SqlCommand("""
                    SELECT
                        s.Id,
                        c.RostroCifrado
                    FROM dbo.SesionesCargaSynchro AS s
                    INNER JOIN dbo.CredencialesCargaSynchro AS c
                        ON c.SesionId = s.Id
                    INNER JOIN dbo.EspaciosCarga AS e
                        ON e.Codigo = s.CodigoEspacio
                    WHERE s.CodigoEspacio = @codigo
                      AND s.Estado = N'Activa'
                      AND s.InicioUtc IS NOT NULL
                      AND s.RetiroUtc IS NULL
                      AND e.Estado = N'Ocupado'
                      AND c.ModeloFacial = @modelo
                      AND c.VersionConsentimiento = @consentimiento;
                    """, db))
                {
                    buscar.Parameters.Add(
                        "@codigo", SqlDbType.NVarChar, 10
                    ).Value = codigo;

                    buscar.Parameters.Add(
                        "@modelo", SqlDbType.NVarChar, 80
                    ).Value = Modelo;

                    buscar.Parameters.Add(
                        "@consentimiento", SqlDbType.NVarChar, 80
                    ).Value = Consentimiento;

                    await using var lector =
                        await buscar.ExecuteReaderAsync(
                            http.RequestAborted);

                    if (!await lector.ReadAsync(
                        http.RequestAborted))
                        return Results.Conflict(new {
                            mensaje =
                                "No existe una sesion activa para ese espacio."
                        });

                    sesionId = lector.GetGuid(0);
                    cifrado = (byte[])lector.GetValue(1);
                }

                plano = proteccion.CreateProtector(
                    $"SYNCHRO.Carga.RostroV3.v1.{sesionId:N}"
                ).Unprotect(cifrado);

                referencia =
                    JsonSerializer.Deserialize<double[]>(plano);

                double? distancia =
                    DescriptorFacialV3.DistanciaCoseno(
                        referencia, muestra);

                if (!distancia.HasValue ||
                    distancia.Value > umbral)
                    return Results.BadRequest(new {
                        mensaje = Rechazo
                    });

                string token = proteccion.CreateProtector(
                    PropositoFacial
                ).ToTimeLimitedDataProtector().Protect(
                    sesionId.ToString("N"),
                    TimeSpan.FromMinutes(2)
                );

                return Results.Ok(new {
                    rostroValidado = true,
                    tokenFacial = token,
                    pinRequerido = true,
                    retiroConfirmado = false
                });
            }
            catch (CryptographicException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "Referencia facial no disponible."
                );
            }
            catch (JsonException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "Referencia facial invalida."
                );
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudo consultar la sesion."
                );
            }
            finally
            {
                Array.Clear(muestra);

                if (datos.Descriptor is not null)
                    Array.Clear(datos.Descriptor);

                if (referencia is not null)
                    Array.Clear(referencia);

                if (cifrado is not null)
                    CryptographicOperations.ZeroMemory(cifrado);

                if (plano is not null)
                    CryptographicOperations.ZeroMemory(plano);
            }
        });

        // PASO 2: validar el PIN de la misma sesion.
        // Maximo cinco intentos fallidos por sesion.
        // No libera el espacio ni marca ningun pago.
        grupo.MapPost("/pin", async (
            PinRetiroCargaPeticion datos,
            HttpContext http,
            IAntiforgery csrf,
            IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!EntornoValido(app, config))
                return Results.NotFound();

            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);

            if (datos.Pin is null ||
                datos.Pin.Length != 6 ||
                datos.Pin.Any(c => c < '0' || c > '9'))
                return Results.BadRequest(new {
                    mensaje = "El PIN debe tener seis digitos."
                });

            if (datos.TokenFacial is null ||
                datos.TokenFacial.Length is < 32 or > 4096)
                return Results.BadRequest(new {
                    mensaje = Rechazo
                });

            Guid sesionId;

            try
            {
                string valor = proteccion.CreateProtector(
                    PropositoFacial
                ).ToTimeLimitedDataProtector().Unprotect(
                    datos.TokenFacial);

                if (!Guid.TryParseExact(
                    valor, "N", out sesionId))
                    return Results.BadRequest(new {
                        mensaje = Rechazo
                    });
            }
            catch (CryptographicException)
            {
                return Results.BadRequest(new {
                    mensaje = "La autorizacion facial vencio."
                });
            }
            catch (FormatException)
            {
                return Results.BadRequest(new {
                    mensaje = Rechazo
                });
            }

            byte[]? clave = null;
            byte[]? salt = null;
            byte[]? hashEsperado = null;
            byte[]? mensajePin = null;
            byte[]? calculado = null;

            try
            {
                string? textoClave =
                    config["CargaDispositivos:ClaveHmac"];

                if (string.IsNullOrWhiteSpace(textoClave))
                    return Results.Problem(
                        statusCode: 503,
                        title: "Clave HMAC no disponible."
                    );

                clave = Convert.FromBase64String(textoClave);

                if (clave.Length != 32)
                    return Results.Problem(
                        statusCode: 503,
                        title: "Clave HMAC invalida."
                    );

                var cadena =
                    config.GetConnectionString("SynchroDB");

                await using var db =
                    new SqlConnection(cadena);

                await db.OpenAsync(http.RequestAborted);

                using var transaccion =
                    db.BeginTransaction(IsolationLevel.Serializable);

                int intentos = 0;
                string? codigoIdentificado = null;
                bool activa = false;

                await using (var consultar = new SqlCommand("""
                    SELECT
                        c.PinHash,
                        c.PinSalt,
                        c.IntentosFallidos,
                        s.Estado,
                        e.Estado,
                        s.CodigoEspacio
                    FROM dbo.SesionesCargaSynchro AS s
                        WITH (UPDLOCK, HOLDLOCK)
                    INNER JOIN dbo.CredencialesCargaSynchro AS c
                        WITH (UPDLOCK, HOLDLOCK)
                        ON c.SesionId = s.Id
                    INNER JOIN dbo.EspaciosCarga AS e
                        ON e.Codigo = s.CodigoEspacio
                    WHERE s.Id = @id
                      AND s.InicioUtc IS NOT NULL
                      AND s.RetiroUtc IS NULL;
                    """, db, transaccion))
                {
                    consultar.Parameters.Add(
                        "@id", SqlDbType.UniqueIdentifier
                    ).Value = sesionId;

                    await using var lector =
                        await consultar.ExecuteReaderAsync(
                            http.RequestAborted);

                    if (await lector.ReadAsync(
                        http.RequestAborted))
                    {
                        hashEsperado =
                            (byte[])lector.GetValue(0);

                        salt =
                            (byte[])lector.GetValue(1);

                        intentos = lector.GetInt32(2);

                        activa =
                            lector.GetString(3) == "Activa" &&
                            lector.GetString(4) == "Ocupado";
                        codigoIdentificado = lector.GetString(5);
                    }
                }

                if (!activa ||
                    hashEsperado is null ||
                    salt is null)
                    return Results.Conflict(new {
                        mensaje =
                            "La sesion ya no permite verificar el PIN."
                    });

                if (intentos >= 5)
                    return Results.Conflict(new {
                        mensaje =
                            "PIN bloqueado. Solicita asistencia al operador."
                    });

                mensajePin = Encoding.UTF8.GetBytes(
                    $"{sesionId:N}:{Convert.ToHexString(salt)}:{datos.Pin}"
                );

                calculado =
                    HMACSHA256.HashData(clave, mensajePin);

                bool coincide =
                    hashEsperado.Length == calculado.Length &&
                    CryptographicOperations.FixedTimeEquals(
                        hashEsperado, calculado);

                if (!coincide)
                {
                    await using var fallido = new SqlCommand("""
                        UPDATE dbo.CredencialesCargaSynchro
                        SET IntentosFallidos = IntentosFallidos + 1
                        WHERE SesionId = @id
                          AND IntentosFallidos < 5;
                        """, db, transaccion);

                    fallido.Parameters.Add(
                        "@id", SqlDbType.UniqueIdentifier
                    ).Value = sesionId;

                    await fallido.ExecuteNonQueryAsync(
                        http.RequestAborted);

                    transaccion.Commit();

                    return Results.BadRequest(new {
                        mensaje =
                            "PIN incorrecto. Verifica los digitos."
                    });
                }

                transaccion.Commit();

                string token = proteccion.CreateProtector(
                    PropositoPin
                ).ToTimeLimitedDataProtector().Protect(
                    sesionId.ToString("N"),
                    TimeSpan.FromMinutes(5)
                );

                return Results.Ok(new {
                    pinValidado = true,
                    rostroValidado = true,
                    tokenCobro = token,
                    codigoEspacio = codigoIdentificado,
                    pagoPendiente = true,
                    retiroConfirmado = false
                });
            }
            catch (FormatException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "Configuracion HMAC invalida."
                );
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudo verificar el PIN."
                );
            }
            finally
            {
                if (clave is not null)
                    CryptographicOperations.ZeroMemory(clave);

                if (salt is not null)
                    CryptographicOperations.ZeroMemory(salt);

                if (hashEsperado is not null)
                    CryptographicOperations.ZeroMemory(hashEsperado);

                if (mensajePin is not null)
                    CryptographicOperations.ZeroMemory(mensajePin);

                if (calculado is not null)
                    CryptographicOperations.ZeroMemory(calculado);
            }
        });
    }
}