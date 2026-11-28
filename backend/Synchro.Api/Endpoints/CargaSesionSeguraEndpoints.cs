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

public sealed record PrepararCargaSeguraPeticion(
    string? CodigoEspacio,
    string? Dispositivo,
    string? Modalidad,
    string? Pin,
    double[]? Descriptor
);

public sealed record ConfirmarColocacionSeguraPeticion(
    string? TokenColocacion
);

public static class CargaSesionSeguraEndpoints
{
    private const string PropositoToken =
        "SYNCHRO.Carga.Colocacion.v1";

    private const string Modelo =
        "SYNCHRO-HUMANJS-EMBEDDING-1024-v3";

    private const string Consentimiento =
        "SYNCHRO-FACIAL-V3-2026-11";

    private static bool CombinacionValida(
        string? codigo,
        string? dispositivo,
        string? modalidad)
    {
        return
            (
                dispositivo == "Celular" &&
                modalidad == "Inalambrica" &&
                codigo is "W1" or "W2"
            ) ||
            (
                dispositivo == "Celular" &&
                modalidad == "Cable" &&
                codigo is "C1" or "C2" or "C3" or "C4"
            ) ||
            (
                dispositivo == "Laptop" &&
                modalidad == "Laptop" &&
                codigo is "L1" or "L2"
            );
    }

    private static byte[]? ObtenerClave(IConfiguration config)
    {
        string? texto =
            config["CargaDispositivos:ClaveHmac"] ??
            config["ServiciosTemporales:ClaveHmac"];

        if (string.IsNullOrWhiteSpace(texto))
            return null;

        try
        {
            byte[] clave = Convert.FromBase64String(texto);

            if (clave.Length == 32)
                return clave;

            CryptographicOperations.ZeroMemory(clave);
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static void MapCargaSesionSeguraEndpoints(
        this WebApplication app, bool definitiva = false)
    {
        var grupo = app.MapGroup("/api/terminales/carga")
            .RequireRateLimiting("OperacionesTerminal")
            .AddEndpointFilter<OperacionTerminalSeguraFilter>();
        if(definitiva)grupo.RequireAuthorization("SoloTerminal");else grupo.AllowAnonymous();
        grupo.MapGet("/espacios",async (HttpContext http,IConfiguration config)=>{
            if(!definitiva){if(!app.Environment.IsDevelopment())return Results.NotFound();ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");}
            await using var db=new SqlConnection(definitiva?Synchro.Api.Services.Administracion.DestinoAdministrativoDefinitivo.Exigir(config.GetConnectionString("SynchroDB")):config.GetConnectionString("SynchroDB"));
            await db.OpenAsync(http.RequestAborted);
            if(definitiva && !await VerificarBaseCargaDefinitiva.ValidarAsync(db,http.RequestAborted))return Results.Problem(statusCode:503,title:"Base de carga no autorizada.");
            await using var q=new SqlCommand("SELECT Codigo,Estado FROM dbo.EspaciosCarga WHERE Codigo IN(N'C1',N'C2',N'C3',N'C4',N'W1',N'W2',N'L1',N'L2') ORDER BY Codigo",db);
            var lista=new List<object>();await using var r=await q.ExecuteReaderAsync(http.RequestAborted);
            while(await r.ReadAsync(http.RequestAborted))lista.Add(new{codigo=r.GetString(0),estado=r.GetString(1)});
            return lista.Count==8?Results.Ok(lista):Results.Problem(statusCode:503,title:"Inventario de carga incompleto.");
        });

        grupo.MapPost("/preparar", async (
            PrepararCargaSeguraPeticion datos,
            HttpContext http,
            IAntiforgery csrf,
            IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if ((!definitiva && !app.Environment.IsDevelopment()) ||
                !config.GetValue<bool>(
                    "CargaDispositivos:RegistroHabilitado"))
                return Results.NotFound();

            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);

            if (http.Request.Headers[
                "X-SYNCHRO-CONSENTIMIENTO"].ToString() != "ACEPTO")
                return Results.BadRequest(new {
                    mensaje = "Falta consentimiento facial."
                });

            if (!CombinacionValida(
                datos.CodigoEspacio,
                datos.Dispositivo,
                datos.Modalidad))
                return Results.BadRequest(new {
                    mensaje = "Espacio o dispositivo invalido."
                });

            if (datos.Pin is null ||
                datos.Pin.Length != 6 ||
                datos.Pin.Any(c => c < '0' || c > '9'))
                return Results.BadRequest(new {
                    mensaje = "El PIN debe contener 6 digitos."
                });

            double[]? normalizado =
                DescriptorFacialV3.Normalizar(datos.Descriptor);

            if (normalizado is null)
                return Results.BadRequest(new {
                    mensaje = "Captura facial V3 invalida."
                });

            byte[]? clave = ObtenerClave(config);

            if (clave is null)
            {
                Array.Clear(normalizado);
                return Results.Problem(
                    statusCode: 503,
                    title: "La clave segura de carga no esta configurada.");
            }

            byte[]? serializado = null;
            byte[]? cifrado = null;
            byte[]? mensajePin = null;
            byte[]? hash = null;
            byte[]? salt = null;

            try
            {
                var cadena =
                    config.GetConnectionString("SynchroDB");

                if(definitiva)Synchro.Api.Services.Administracion.DestinoAdministrativoDefinitivo.Exigir(cadena);
                else ConexionSqlAislada.Exigir(cadena,"SynchroDB_Pruebas");

                Guid id = Guid.NewGuid();
                DateTime ahora = DateTime.UtcNow;
                DateTime vence = ahora.AddMinutes(10);

                serializado =
                    JsonSerializer.SerializeToUtf8Bytes(normalizado);

                cifrado = proteccion.CreateProtector(
                    $"SYNCHRO.Carga.RostroV3.v1.{id:N}"
                ).Protect(serializado);

                salt = RandomNumberGenerator.GetBytes(32);

                mensajePin = Encoding.UTF8.GetBytes(
                    $"{id:N}:{Convert.ToHexString(salt)}:{datos.Pin}"
                );

                hash = HMACSHA256.HashData(
                    clave, mensajePin);

                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                if(definitiva && !await VerificarBaseCargaDefinitiva.ValidarAsync(db,http.RequestAborted))
                    return Results.Problem(statusCode:503,title:"Base definitiva de carga no autorizada.");

                using var tx = db.BeginTransaction(
                    IsolationLevel.Serializable);

                // Cancelar preparaciones vencidas.
                using (var limpiar = new SqlCommand("""
                    UPDATE dbo.SesionesCargaSynchro
                    SET Estado = N'Cancelada'
                    WHERE Estado = N'Preparada'
                      AND VencePreparacionUtc <= SYSUTCDATETIME();
                    """, db, tx))
                {
                    await limpiar.ExecuteNonQueryAsync(
                        http.RequestAborted);
                }

                // Bloqueo de lectura para evitar selecciones
                // concurrentes del mismo espacio.
                using (var comprobar = new SqlCommand("""
                    SELECT Estado
                    FROM dbo.EspaciosCarga WITH (UPDLOCK,HOLDLOCK)
                    WHERE Codigo = @codigo;
                    """, db, tx))
                {
                    comprobar.Parameters.Add(
                        "@codigo", SqlDbType.NVarChar, 10
                    ).Value = datos.CodigoEspacio!;

                    var estado = await comprobar.ExecuteScalarAsync(
                        http.RequestAborted);

                    if (estado is not string valor ||
                        valor != "Disponible")
                    {
                        tx.Rollback();

                        return Results.Conflict(new {
                            mensaje = "El espacio no esta disponible."
                        });
                    }
                }

                using (var insertar = new SqlCommand("""
                    INSERT INTO dbo.SesionesCargaSynchro
                    (
                        Id,
                        CodigoEspacio,
                        Dispositivo,
                        Modalidad,
                        Estado,
                        FechaCreacionUtc,
                        VencePreparacionUtc
                    )
                    VALUES
                    (
                        @id, @codigo, @dispositivo,
                        @modalidad, N'Preparada',
                        @ahora, @vence
                    );
                    """, db, tx))
                {
                    insertar.Parameters.Add(
                        "@id", SqlDbType.UniqueIdentifier
                    ).Value = id;

                    insertar.Parameters.Add(
                        "@codigo", SqlDbType.NVarChar, 10
                    ).Value = datos.CodigoEspacio!;

                    insertar.Parameters.Add(
                        "@dispositivo", SqlDbType.NVarChar, 12
                    ).Value = datos.Dispositivo!;

                    insertar.Parameters.Add(
                        "@modalidad", SqlDbType.NVarChar, 20
                    ).Value = datos.Modalidad!;

                    insertar.Parameters.Add(
                        "@ahora", SqlDbType.DateTime2
                    ).Value = ahora;

                    insertar.Parameters.Add(
                        "@vence", SqlDbType.DateTime2
                    ).Value = vence;

                    await insertar.ExecuteNonQueryAsync(
                        http.RequestAborted);
                }

                using (var credenciales = new SqlCommand("""
                    INSERT INTO dbo.CredencialesCargaSynchro
                    (
                        SesionId,
                        RostroCifrado,
                        ModeloFacial,
                        VersionConsentimiento,
                        PinHash,
                        PinSalt
                    )
                    VALUES
                    (
                        @id, @rostro, @modelo, @version,
                        @hash, @salt
                    );
                    """, db, tx))
                {
                    credenciales.Parameters.Add(
                        "@id", SqlDbType.UniqueIdentifier
                    ).Value = id;

                    credenciales.Parameters.Add(
                        "@rostro", SqlDbType.VarBinary, -1
                    ).Value = cifrado;

                    credenciales.Parameters.Add(
                        "@modelo", SqlDbType.NVarChar, 80
                    ).Value = Modelo;

                    credenciales.Parameters.Add(
                        "@version", SqlDbType.NVarChar, 80
                    ).Value = Consentimiento;

                    credenciales.Parameters.Add(
                        "@hash", SqlDbType.VarBinary, 32
                    ).Value = hash;

                    credenciales.Parameters.Add(
                        "@salt", SqlDbType.VarBinary, 32
                    ).Value = salt;

                    await credenciales.ExecuteNonQueryAsync(
                        http.RequestAborted);
                }

                tx.Commit();

                string token = proteccion.CreateProtector(
                    PropositoToken
                ).ToTimeLimitedDataProtector().Protect(
                    id.ToString("D"),
                    TimeSpan.FromMinutes(10)
                );

                return Results.Ok(new {
                    sesionPreparada = true,
                    codigoEspacio = datos.CodigoEspacio,
                    tokenColocacion = token,
                    venceUtc = vence,
                    rostroV3Registrado = true,
                    pinProtegido = true,
                    cargaIniciada = false
                });
            }
            catch (SqlException ex) when (
                ex.Number is 2601 or 2627)
            {
                return Results.Conflict(new {
                    mensaje = "El espacio tiene otra sesion activa."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudo preparar la sesion.");
            }
            catch (CryptographicException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudieron proteger las credenciales.");
            }
            finally
            {
                Array.Clear(normalizado);
                if (datos.Descriptor is not null)
                    Array.Clear(datos.Descriptor);

                CryptographicOperations.ZeroMemory(clave);

                if (serializado is not null)
                    CryptographicOperations.ZeroMemory(serializado);

                if (cifrado is not null)
                    CryptographicOperations.ZeroMemory(cifrado);

                if (mensajePin is not null)
                    CryptographicOperations.ZeroMemory(mensajePin);

                if (hash is not null)
                    CryptographicOperations.ZeroMemory(hash);

                if (salt is not null)
                    CryptographicOperations.ZeroMemory(salt);
            }
        });

        grupo.MapPost("/colocacion/confirmar", async (
            ConfirmarColocacionSeguraPeticion datos,
            HttpContext http,
            IAntiforgery csrf,
            IDataProtectionProvider proteccion,
            IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if ((!definitiva && !app.Environment.IsDevelopment()) ||
                !config.GetValue<bool>(
                    "CargaDispositivos:RegistroHabilitado"))
                return Results.NotFound();

            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);

            if (datos.TokenColocacion is null ||
                datos.TokenColocacion.Length is < 32 or > 4096)
                return Results.BadRequest(new {
                    mensaje = "Autorizacion de colocacion invalida."
                });

            Guid id;

            try
            {
                string texto = proteccion.CreateProtector(
                    PropositoToken
                ).ToTimeLimitedDataProtector().Unprotect(
                    datos.TokenColocacion);

                if (!Guid.TryParse(texto, out id))
                    return Results.BadRequest(new {
                        mensaje = "Autorizacion invalida."
                    });
            }
            catch (CryptographicException)
            {
                return Results.BadRequest(new {
                    mensaje = "Autorizacion vencida o invalida."
                });
            }
            catch (FormatException)
            {
                return Results.BadRequest(new {
                    mensaje = "Autorizacion invalida."
                });
            }

            try
            {
                var cadena =
                    config.GetConnectionString("SynchroDB");

                if(definitiva)Synchro.Api.Services.Administracion.DestinoAdministrativoDefinitivo.Exigir(cadena);
                else ConexionSqlAislada.Exigir(cadena,"SynchroDB_Pruebas");

                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                if(definitiva && !await VerificarBaseCargaDefinitiva.ValidarAsync(db,http.RequestAborted))
                    return Results.Problem(statusCode:503,title:"Base definitiva de carga no autorizada.");

                using var tx = db.BeginTransaction(
                    IsolationLevel.Serializable);

                string? codigo = null;
                string? estado = null;
                DateTime vence = DateTime.MinValue;

                using (var buscar = new SqlCommand("""
                    SELECT
                        CodigoEspacio,
                        Estado,
                        VencePreparacionUtc
                    FROM dbo.SesionesCargaSynchro
                        WITH (UPDLOCK,HOLDLOCK)
                    WHERE Id = @id;
                    """, db, tx))
                {
                    buscar.Parameters.Add(
                        "@id", SqlDbType.UniqueIdentifier
                    ).Value = id;

                    using var lector = await buscar.ExecuteReaderAsync(
                        http.RequestAborted);

                    if (await lector.ReadAsync(
                        http.RequestAborted))
                    {
                        codigo = lector.GetString(0);
                        estado = lector.GetString(1);
                        vence = lector.GetDateTime(2);
                    }
                }

                if (codigo is null ||
                    estado != "Preparada" ||
                    vence <= DateTime.UtcNow)
                {
                    tx.Rollback();

                    return Results.Conflict(new {
                        mensaje = "La preparacion ya no esta vigente."
                    });
                }

                // El espacio solo se ocupa cuando se confirma
                // la colocacion, nunca antes.
                using (var ocupar = new SqlCommand("""
                    UPDATE dbo.EspaciosCarga
                    SET Estado = N'Ocupado'
                    WHERE Codigo = @codigo
                      AND Estado = N'Disponible';
                    """, db, tx))
                {
                    ocupar.Parameters.Add(
                        "@codigo", SqlDbType.NVarChar, 10
                    ).Value = codigo;

                    int filas = await ocupar.ExecuteNonQueryAsync(
                        http.RequestAborted);

                    if (filas != 1)
                    {
                        tx.Rollback();

                        return Results.Conflict(new {
                            mensaje = "El espacio ya no esta disponible."
                        });
                    }
                }

                using (var iniciar = new SqlCommand("""
                    UPDATE dbo.SesionesCargaSynchro
                    SET
                        Estado = N'Activa',
                        InicioUtc = SYSUTCDATETIME()
                    WHERE Id = @id
                      AND Estado = N'Preparada'
                      AND VencePreparacionUtc > SYSUTCDATETIME()
                      AND InicioUtc IS NULL;
                    """, db, tx))
                {
                    iniciar.Parameters.Add(
                        "@id", SqlDbType.UniqueIdentifier
                    ).Value = id;

                    int filas = await iniciar.ExecuteNonQueryAsync(
                        http.RequestAborted);

                    if (filas != 1)
                    {
                        tx.Rollback();

                        return Results.Conflict(new {
                            mensaje = "No pudo iniciarse la carga."
                        });
                    }
                }

                tx.Commit();

                return Results.Ok(new {
                    colocacionConfirmada = true,
                    sesionIniciada = true,
                    codigoEspacio = codigo,
                    estadoEspacio = "Ocupado",
                    hardwareSimulado = true
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudo iniciar la carga.");
            }
        });
    }
}