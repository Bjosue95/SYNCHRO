using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Endpoints;

public static class RetiroCargaAutoIdentificacionEndpoints
{
    public static void MapRetiroCargaAutoIdentificacionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/terminales/carga/retiro/identificar-v3", async (
            RostroRetiroCargaPeticion solicitud, HttpContext http,
            IAntiforgery csrf, IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!app.Environment.IsDevelopment() ||
                !config.GetValue<bool>("CargaDispositivos:RegistroHabilitado"))
                return Results.NotFound();
            if (!http.Request.IsHttps || !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);
            if (http.Request.Headers["X-SYNCHRO-CONSENTIMIENTO"].ToString() != "ACEPTO")
                return Results.BadRequest(new { mensaje = "Se requiere consentimiento facial." });
            var cadena = config.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena)) return Results.NotFound();
            SqlConnectionStringBuilder datosConexion;
            try { datosConexion = new SqlConnectionStringBuilder(cadena); }
            catch (ArgumentException) { return Results.NotFound(); }
            if (!string.Equals(datosConexion.InitialCatalog, "SynchroDB_Pruebas", StringComparison.OrdinalIgnoreCase) ||
                !ConexionSqlAislada.EsWhatsAppPermitido(datosConexion)) return Results.NotFound();

            double[]? muestra = null;
            var candidatas = new List<(Guid Id, byte[] Cifrado)>();
            try
            {
                muestra = DescriptorFacialV3.Normalizar(solicitud.Descriptor);
                if (muestra is null) return Results.BadRequest(new { mensaje = "Captura facial V3 invalida." });
                double umbral = config.GetValue("RostroV3:UmbralDistanciaCoseno", 0.22d);
                if (!double.IsFinite(umbral) || umbral < 0 || umbral > 0.5)
                    return Results.Problem(statusCode: 503, title: "Configuracion facial invalida.");

                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using (var consulta = new SqlCommand("""
                    SELECT TOP (9) s.Id, c.RostroCifrado
                    FROM dbo.SesionesCargaSynchro AS s
                    INNER JOIN dbo.CredencialesCargaSynchro AS c ON c.SesionId = s.Id
                    INNER JOIN dbo.EspaciosCarga AS e ON e.Codigo = s.CodigoEspacio
                    WHERE s.Estado = N'Activa' AND s.InicioUtc IS NOT NULL
                      AND s.RetiroUtc IS NULL AND e.Estado = N'Ocupado'
                      AND c.IntentosFallidos < 5
                      AND c.ModeloFacial = N'SYNCHRO-HUMANJS-EMBEDDING-1024-v3'
                      AND c.VersionConsentimiento = N'SYNCHRO-FACIAL-V3-2026-11';
                    """, db))
                {
                    await using var lector = await consulta.ExecuteReaderAsync(http.RequestAborted);
                    while (await lector.ReadAsync(http.RequestAborted))
                        candidatas.Add((lector.GetGuid(0), (byte[])lector.GetValue(1)));
                }
                if (candidatas.Count > 8)
                    return Results.Problem(statusCode: 503, title: "Inventario inconsistente.");

                int coincidencias = 0;
                Guid sesion = Guid.Empty;
                foreach (var candidata in candidatas)
                {
                    byte[]? plano = null;
                    double[]? referencia = null;
                    try
                    {
                        plano = proteccion.CreateProtector(
                            $"SYNCHRO.Carga.RostroV3.v1.{candidata.Id:N}"
                        ).Unprotect(candidata.Cifrado);
                        referencia = JsonSerializer.Deserialize<double[]>(plano);
                        double? distancia = DescriptorFacialV3.DistanciaCoseno(referencia, muestra);

                        // SYNCHRO-DIAG-V3-TEMP
                        // Diagnostico exclusivamente local.
                        // No imprimir vectores, PIN ni claves.
                        app.Logger.LogWarning(
                            "SYNCHRO_DIAG_V3 | Dimension={Dimension} | Distancia={Distancia} | Umbral={Umbral} | Resultado={Resultado}",
                            referencia?.Length ?? 0,
                            distancia.HasValue
                                ? distancia.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)
                                : "NO_CALCULABLE",
                            umbral.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                            !distancia.HasValue
                                ? "REFERENCIA_INVALIDA"
                                : distancia.Value <= umbral
                                    ? "COINCIDE"
                                    : "NO_COINCIDE"
                        );
                        if (distancia.HasValue && distancia.Value <= umbral)
                        {
                            coincidencias++;
                            sesion = candidata.Id;
                        }
                    }
                    finally
                    {
                        if (referencia is not null) Array.Clear(referencia);
                        if (plano is not null) CryptographicOperations.ZeroMemory(plano);
                    }
                }
                if (coincidencias == 0)
                    return Results.BadRequest(new { mensaje = "No se pudo verificar el rostro para una carga activa." });
                if (coincidencias > 1)
                    return Results.Conflict(new { mensaje = "Varias cargas coinciden con este rostro. Solicita asistencia." });

                // Nunca devolver codigo o sesion antes del PIN.
                string token = proteccion.CreateProtector("SYNCHRO.Carga.RetiroFacial.v1")
                    .ToTimeLimitedDataProtector()
                    .Protect(sesion.ToString("N"), TimeSpan.FromMinutes(2));
                return Results.Ok(new
                {
                    rostroValidado = true, tokenFacial = token,
                    pinRequerido = true, retiroConfirmado = false
                });
            }
            catch (CryptographicException)
            {
                return Results.Problem(statusCode: 503, title: "Referencia facial no disponible.");
            }
            catch (JsonException)
            {
                return Results.Problem(statusCode: 503, title: "Referencia facial invalida.");
            }
            catch (SqlException)
            {
                return Results.Problem(statusCode: 503, title: "No se pudieron consultar las cargas activas.");
            }
            finally
            {
                if (muestra is not null) Array.Clear(muestra);
                if (solicitud.Descriptor is not null) Array.Clear(solicitud.Descriptor);
                foreach (var c in candidatas) CryptographicOperations.ZeroMemory(c.Cifrado);
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("RetiroPersonal");
    }
}