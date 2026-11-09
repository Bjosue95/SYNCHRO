
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public record ConsultarReservaWebTerminalSolicitud(
    int Referencia,
    string? TokenGestion
);

public static class EncomiendasTerminalEndpoints
{
    private static readonly Regex TokenRegex = new(
        @"\A[0-9a-fA-F]{64}\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );

    public static void MapEncomiendasTerminalEndpoints(
        this WebApplication app)
    {
        app.MapPost(
            "/api/terminales/encomiendas/reservas/consultar",
            async (
                ConsultarReservaWebTerminalSolicitud solicitud,
                HttpContext contexto,
                IAntiforgery antiforgery) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "HTTPS obligatorio."
                    });
                }

                if (!await antiforgery.IsRequestValidAsync(contexto))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido o ausente."
                    });
                }

                string? token = solicitud.TokenGestion?.Trim();

                if (solicitud.Referencia <= 0 ||
                    token is null ||
                    !TokenRegex.IsMatch(token))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Referencia o credencial invalidas."
                    });
                }

                byte[] tokenBytes = Convert.FromHexString(token);
                byte[] tokenHash = SHA256.HashData(tokenBytes);

                CryptographicOperations.ZeroMemory(tokenBytes);

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    CryptographicOperations.ZeroMemory(tokenHash);

                    return Results.Problem(
                        "Conexion SQL no configurada.");
                }

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync(
                        contexto.RequestAborted);

                    const string sql = """
                        SELECT
                            TamanoSolicitado,
                            Estado,
                            FechaExpiracion
                        FROM dbo.PreReservasEncomiendasWeb
                        WHERE Id = @Referencia
                          AND TokenGestionHash = @TokenHash;
                        """;

                    await using var comando =
                        new SqlCommand(sql, conexion);

                    comando.Parameters.Add(
                        "@Referencia",
                        SqlDbType.Int
                    ).Value = solicitud.Referencia;

                    comando.Parameters.Add(
                        "@TokenHash",
                        SqlDbType.VarBinary,
                        32
                    ).Value = tokenHash;

                    await using var lector =
                        await comando.ExecuteReaderAsync(
                            contexto.RequestAborted);

                    if (!await lector.ReadAsync(
                        contexto.RequestAborted))
                    {
                        return Results.NotFound(new
                        {
                            mensaje =
                                "Reserva no encontrada o credencial invalida."
                        });
                    }

                    string tamano = lector.GetString(0);
                    string estado = lector.GetString(1);

                    DateTime vencimiento = DateTime.SpecifyKind(
                        lector.GetDateTime(2),
                        DateTimeKind.Utc
                    );

                    if (vencimiento <= DateTime.UtcNow)
                    {
                        estado = "Vencida";
                    }

                    contexto.Response.Headers.CacheControl = "no-store";

                    return Results.Ok(new
                    {
                        referencia = solicitud.Referencia,
                        tamanoSolicitado = tamano,
                        estado,
                        fechaExpiracion = vencimiento,
                        depositoHabilitado = false,
                        mensaje =
                            "Reserva identificada. El deposito requiere " +
                            "verificacion del remitente y consentimiento " +
                            "confirmado por WhatsApp."
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No fue posible consultar la reserva.");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(tokenHash);
                }
            }
        )
        .RequireAuthorization("SoloTerminal")
        .RequireRateLimiting("LoginLimitado");

        // No existe endpoint para crear encomiendas
        // sin reserva previa desde la tablet.
    }
}
