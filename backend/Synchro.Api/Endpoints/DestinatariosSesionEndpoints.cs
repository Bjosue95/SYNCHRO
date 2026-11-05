using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Synchro.Api.Models;
using Synchro.Api.Endpoints;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Synchro.Api.Endpoints;

public static class DestinatariosSesionEndpoints
{
    public static void MapDestinatariosSesionEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/api/destinatarios/mi-sesion",
            (HttpContext contexto) =>
            {
                return Results.Ok(new
                {
                    autenticado = true,
                    tipoUsuario = contexto.User
                        .FindFirst("tipo_usuario")?.Value
                });
            }
        )
        .RequireAuthorization("SoloDestinatarios");

        app.MapGet(
            "/api/destinatarios/csrf-token",
            async (HttpContext contexto, IAntiforgery antiforgery) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                // Reconocer expresamente la sesion del destinatario.
                var autenticacion =
                    await contexto.AuthenticateAsync("Destinatarios");

                if (autenticacion.Succeeded &&
                    autenticacion.Principal is not null)
                {
                    contexto.User = autenticacion.Principal;
                }
                else
                {
                    // Antes del login, el token es anonimo.
                    contexto.User =
                        new System.Security.Claims.ClaimsPrincipal(
                            new System.Security.Claims.ClaimsIdentity());
                }

                contexto.Response.Headers.CacheControl = "no-store";

                var tokens =
                    antiforgery.GetAndStoreTokens(contexto);

                return Results.Ok(new
                {
                    csrfToken = tokens.RequestToken
                });
            }
        )
        .AllowAnonymous();

        app.MapPost("/api/destinatarios/login",
            async (
                InicioSesionDestinatarioPeticion datos,
                HttpContext contexto,
                IAntiforgery antiforgery) =>
        {
            // Mantener deshabilitado el acceso en produccion
            // hasta implementar la entrega real de codigos.
            if (!app.Environment.IsDevelopment())
                return Results.StatusCode(503);

            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new
                {
                    mensaje = "Se requiere HTTPS."
                });

            contexto.Response.Headers.CacheControl = "no-store";

            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest(new
                {
                    mensaje = "Token CSRF invalido."
                });

            // Comprobar que el codigo contiene seis digitos.
            if (datos.VerificacionId <= 0 ||
                datos.Codigo is null ||
                datos.Codigo.Length != 6 ||
                datos.Codigo.Any(c => c < '0' || c > '9'))
                return Results.Unauthorized();

            string? cadena = app.Configuration
                .GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem(
                    "Conexion SQL no configurada.");

            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync();

                int personaId;
                string telefono;

                // La identidad procede de SQL Server, no del usuario.
                await using (var buscar = new SqlCommand(
                    @"SELECT PersonaId, Telefono
                      FROM dbo.VerificacionesTelefono
                      WHERE Id = @Id
                        AND Finalidad = N'InicioSesionDestinatario'",
                    conexion))
                {
                    buscar.Parameters.Add(
                        "@Id", System.Data.SqlDbType.Int
                    ).Value = datos.VerificacionId;

                    await using var lector =
                        await buscar.ExecuteReaderAsync();

                    if (!await lector.ReadAsync())
                        return Results.Unauthorized();

                    personaId = lector.GetInt32(0);
                    telefono = lector.GetString(1);
                }

                byte[] hash =
                    CodigoVerificacionTelefono.CalcularHash(
                        datos.Codigo,
                        personaId,
                        telefono,
                        app.Configuration);

                try
                {
                    string resultado;

                    // Validar codigo y finalidad.
                    await using (var validar = new SqlCommand(
                        "dbo.ValidarCodigoTelefonoConFinalidadSynchro",
                        conexion))
                    {
                        validar.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        validar.Parameters.Add(
                            "@VerificacionId",
                            System.Data.SqlDbType.Int
                        ).Value = datos.VerificacionId;

                        validar.Parameters.Add(
                            "@PersonaId",
                            System.Data.SqlDbType.Int
                        ).Value = personaId;

                        validar.Parameters.Add(
                            "@CodigoHash",
                            System.Data.SqlDbType.VarBinary, 32
                        ).Value = hash;

                        validar.Parameters.Add(
                            "@FinalidadEsperada",
                            System.Data.SqlDbType.NVarChar, 40
                        ).Value = "InicioSesionDestinatario";

                        await using var lector =
                            await validar.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                            return Results.Unauthorized();

                        resultado = lector["Resultado"].ToString() ?? "";
                    }

                    if (resultado != "Verificado")
                        return Results.Unauthorized();
                }
                finally
                {
                    System.Security.Cryptography
                        .CryptographicOperations.ZeroMemory(hash);
                }

                // Impedir emitir dos sesiones con la misma verificacion.
                await using (var consumir = new SqlCommand(
                    "dbo.ConsumirVerificacionInicioSesionSynchro",
                    conexion))
                {
                    consumir.CommandType =
                        System.Data.CommandType.StoredProcedure;

                    consumir.Parameters.Add(
                        "@VerificacionId",
                        System.Data.SqlDbType.Int
                    ).Value = datos.VerificacionId;

                    consumir.Parameters.Add(
                        "@PersonaId",
                        System.Data.SqlDbType.Int
                    ).Value = personaId;

                    await using var lector =
                        await consumir.ExecuteReaderAsync();

                    if (!await lector.ReadAsync())
                        return Results.Unauthorized();
                }

                // Solo despues de validar y consumir el codigo
                // creamos la identidad del destinatario.
                var identidad = new System.Security.Claims.ClaimsIdentity(
                    new[]
                    {
                        new System.Security.Claims.Claim(
                            System.Security.Claims.ClaimTypes.NameIdentifier,
                            personaId.ToString()),

                        new System.Security.Claims.Claim(
                            "tipo_usuario",
                            "Destinatario")
                    },
                    "Destinatarios");

                var usuario =
                    new System.Security.Claims.ClaimsPrincipal(identidad);

                await contexto.SignInAsync(
                    "Destinatarios",
                    usuario,
                    new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                    {
                        IsPersistent = false,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10)
                    });

                return Results.Ok(new
                {
                    mensaje = "Sesion de destinatario iniciada."
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50222 ||
                ex.Number == 50274 ||
                ex.Number == 50275 ||
                ex.Number == 50122)
            {
                return Results.Unauthorized();
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "No se pudo completar el inicio de sesion.");
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(
                    "Configuracion de seguridad no disponible.");
            }
            catch (FormatException)
            {
                return Results.Problem(
                    "Configuracion de seguridad invalida.");
            }
        })
        .AllowAnonymous();

        app.MapPost(
            "/api/destinatarios/logout",
            async (
                HttpContext contexto,
                IAntiforgery antiforgery) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                if (!await antiforgery.IsRequestValidAsync(contexto))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido."
                    });
                }

                await contexto.SignOutAsync("Destinatarios");

                contexto.Response.Headers.CacheControl = "no-store";

                return Results.Ok(new
                {
                    mensaje = "Sesion de destinatario cerrada."
                });
            }
        )
        .RequireAuthorization("SoloDestinatarios");
    }
}
