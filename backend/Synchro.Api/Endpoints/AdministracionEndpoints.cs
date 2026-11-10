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
using Synchro.Api.Services.Administracion;

namespace Synchro.Api.Endpoints;

public static class AdministracionEndpoints
{
    public static void MapAdministracionEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/login", async (
            LoginAdministradorSolicitud solicitud,
            HttpContext contexto,
            IPasswordHasher<Administrador> hasher) =>
        {
            if (!contexto.Request.IsHttps)
            {
                return Results.BadRequest(new
                {
                    mensaje = "El inicio de sesion requiere HTTPS."
                });
            }

            if (string.IsNullOrWhiteSpace(solicitud.NombreUsuario) ||
                string.IsNullOrWhiteSpace(solicitud.Contrasena) ||
                solicitud.NombreUsuario.Length > 80)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Datos de inicio de sesion invalidos."
                });
            }

            var cadena = app.Configuration
                .GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            Administrador? administrador = null;

            using (var comando = new SqlCommand(@"
                SELECT Id, NombreUsuario, PasswordHash, Activo
                FROM dbo.Administradores
                WHERE NombreUsuario = @usuario",
                conexion))
            {
                comando.Parameters.Add(
                    "@usuario",
                    System.Data.SqlDbType.NVarChar,
                    80).Value = solicitud.NombreUsuario;

                using var lector = await comando.ExecuteReaderAsync();

                if (await lector.ReadAsync())
                {
                    administrador = new Administrador
                    {
                        Id = lector.GetInt32(0),
                        NombreUsuario = lector.GetString(1),
                        PasswordHash = lector.GetString(2),
                        Activo = lector.GetBoolean(3)
                    };
                }
            }

            if (administrador == null || !administrador.Activo)
            {
                return Results.Unauthorized();
            }

            var resultado = hasher.VerifyHashedPassword(
                administrador,
                administrador.PasswordHash,
                solicitud.Contrasena);

            if (resultado == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            var acceso=await AccesoAdministrativoSql.ObtenerAsync(conexion,administrador.Id,contexto.RequestAborted);
            if(acceso.Roles.Length==0)return Results.Unauthorized();
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    administrador.Id.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    administrador.NombreUsuario),

                new Claim(AccesoAdministrativoSql.ClaimVersion,acceso.VersionSesion.ToString())
            };

            claims.AddRange(acceso.Roles.Select(rol=>new Claim(ClaimTypes.Role,rol)));
            var identidad = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identidad);

            await contexto.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal);

            return Results.Ok(new
            {
                mensaje = "Inicio de sesion correcto",
                usuario = administrador.NombreUsuario,
                rol = acceso.Roles[0],roles=acceso.Roles
            });
        }).RequireRateLimiting("LoginLimitado");


        app.MapGet("/api/admin/mi-sesion", (ClaimsPrincipal usuario) =>
        {
            return Results.Ok(new
            {
                mensaje = "Acceso administrativo autorizado",
                usuario = usuario.Identity?.Name,
                rol = usuario.FindFirstValue(ClaimTypes.Role),
                roles=usuario.FindAll(ClaimTypes.Role).Select(c=>c.Value).ToArray()
            });
        })
        .RequireAuthorization("SesionAdministrativa");


        app.MapPost("/api/admin/logout", async (
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
                    mensaje = "Token CSRF invalido o ausente."
                });
            }

            await contexto.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return Results.Ok(new
            {
                mensaje = "Sesion administrativa cerrada correctamente"
            });
        })
        .RequireAuthorization("SesionAdministrativa");



        app.MapGet("/api/admin/csrf-token",
            (HttpContext contexto, IAntiforgery antiforgery) =>
        {
            if (!contexto.Request.IsHttps)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Se requiere HTTPS."
                });
            }

            contexto.Response.Headers.CacheControl = "no-store";

            var tokens = antiforgery.GetAndStoreTokens(contexto);

            return Results.Ok(new
            {
                csrfToken = tokens.RequestToken
            });
        })
        .RequireAuthorization("SesionAdministrativa");
    }
}
