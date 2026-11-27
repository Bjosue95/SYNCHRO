
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Antiforgery;

namespace Synchro.Api.Endpoints;

public record ConfirmarVinculacionTerminalSolicitud(
    string? Token
);

public static class ConfirmacionTerminalEndpoints
{
    public static void MapConfirmacionTerminalEndpoints(
        this WebApplication app)
    {
        app.MapPost(
            "/api/terminales/vinculacion/confirmar",
            async (
                ConfirmarVinculacionTerminalSolicitud solicitud,
                HttpContext contexto) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                if (string.IsNullOrWhiteSpace(solicitud.Token)
                    || solicitud.Token.Length > 64)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Token invalido."
                    });
                }

                byte[] token;

                try
                {
                    token = Convert.FromBase64String(solicitud.Token);
                }
                catch (FormatException)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Formato de token invalido."
                    });
                }

                if (token.Length != 32)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Longitud de token invalida."
                    });
                }

                var tokenHash = SHA256.HashData(token);

                var cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.Problem(
                        "Conexion de base de datos no configurada.");
                }

                int terminalId;
                string vinculoVersion;

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync();

                    // Consume una sola vez una solicitud aprobada, tanto en la
                    // primera vinculación como al renovar una terminal activa.
                    // Sin la aprobación del administrador NO se emite cookie.
                    await using var comando = new SqlCommand(
                        """
                        SET XACT_ABORT ON;
                        BEGIN TRY
                          BEGIN TRANSACTION;
                          DECLARE @terminalId INT;
                          SELECT @terminalId=s.TerminalId
                          FROM dbo.SolicitudesVinculacionTerminal s WITH(UPDLOCK,HOLDLOCK)
                          INNER JOIN dbo.Terminales t WITH(UPDLOCK,HOLDLOCK)
                            ON t.Id=s.TerminalId
                          WHERE s.TokenHash=@TokenHash
                            AND s.Estado=N'Aprobada'
                            AND s.FechaExpiracion>SYSUTCDATETIME();
                          IF @terminalId IS NULL
                            THROW 51021, 'Solicitud no aprobada o vencida.', 1;
                          UPDATE dbo.Terminales
                          SET Activa=1, FechaAutorizacion=SYSUTCDATETIME()
                          WHERE Id=@terminalId;
                          UPDATE dbo.SolicitudesVinculacionTerminal
                          SET Estado=N'Consumida',FechaConsumo=SYSUTCDATETIME()
                          WHERE TokenHash=@TokenHash
                            AND Estado=N'Aprobada'
                            AND FechaExpiracion>SYSUTCDATETIME();
                          IF @@ROWCOUNT<>1
                            THROW 51023, 'No se pudo consumir la solicitud.', 1;
                          COMMIT TRANSACTION;
                          SELECT Id,FechaAutorizacion FROM dbo.Terminales WHERE Id=@terminalId;
                        END TRY
                        BEGIN CATCH
                          IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
                          THROW;
                        END CATCH
                        """,
                        conexion);

                    comando.Parameters.Add(
                        "@TokenHash",
                        SqlDbType.VarBinary,
                        32).Value = tokenHash;

                    await using var resultado = await comando.ExecuteReaderAsync(contexto.RequestAborted);
                    if (!await resultado.ReadAsync(contexto.RequestAborted) ||
                        resultado.IsDBNull(1))
                        return Results.Problem("No se pudo confirmar la terminal.");

                    terminalId = resultado.GetInt32(0);
                    vinculoVersion = resultado.GetDateTime(1).Ticks
                        .ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (SqlException ex)
                    when (ex.Number == 51020 || ex.Number == 51021)
                {
                    return Results.Unauthorized();
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "Error al confirmar la vinculacion.");
                }

                var claims = new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        terminalId.ToString()),

                    new Claim(
                        ClaimTypes.Role,
                        "Terminal")
                };

                var identidad = new ClaimsIdentity(
                    claims,
                    "Terminal");

                var principal = new ClaimsPrincipal(identidad);

                // Sesión operativa renovable y credencial de dispositivo independiente.
                // Ambas son cookies HttpOnly y Secure, nunca accesibles por JavaScript.
                await contexto.SignInAsync(
                    "Terminal", principal,
                    new AuthenticationProperties { IsPersistent = true });

                var vinculo = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, terminalId.ToString()),
                    new Claim(ClaimTypes.Role, "TerminalDispositivo"),
                    new Claim("VinculoVersion", vinculoVersion)
                }, "TerminalDispositivo"));

                await contexto.SignInAsync("TerminalDispositivo", vinculo,
                    new AuthenticationProperties { IsPersistent = true });

                contexto.Response.Headers.CacheControl = "no-store";

                return Results.Ok(new
                {
                    mensaje = "Terminal vinculada correctamente.",
                    terminalId,
                    estado = "Activa"
                });
            })
            .RequireRateLimiting("LoginLimitado");

        app.MapGet(
            "/api/terminales/mi-sesion",
            (ClaimsPrincipal usuario) =>
            {
                return Results.Ok(new
                {
                    mensaje = "Sesion de terminal autorizada",
                    terminalId = usuario.FindFirstValue(
                        ClaimTypes.NameIdentifier),
                    rol = "Terminal"
                });

            })
            .RequireAuthorization("SoloTerminal");

        app.MapGet(
            "/api/terminales/csrf-token",
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
            .RequireAuthorization("SoloTerminal");

    }

}

