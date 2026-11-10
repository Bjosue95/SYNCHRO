using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

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
                contexto.Response.Headers.CacheControl = "no-store";

                if (!contexto.Request.IsHttps)
                    return Results.BadRequest(new
                    {
                        mensaje = "HTTPS obligatorio."
                    });

                if (!await antiforgery.IsRequestValidAsync(contexto))
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido o ausente."
                    });

                string? token = solicitud.TokenGestion?.Trim();

                if (solicitud.Referencia <= 0 ||
                    token is null ||
                    !TokenRegex.IsMatch(token))
                    return Results.BadRequest(new
                    {
                        mensaje = "Referencia o credencial invalidas."
                    });

                byte[] tokenBytes = Convert.FromHexString(token);
                byte[] tokenHash = SHA256.HashData(tokenBytes);

                CryptographicOperations.ZeroMemory(tokenBytes);

                try
                {
                    string? cadena = app.Configuration
                        .GetConnectionString("SynchroDB");

                    if (string.IsNullOrWhiteSpace(cadena))
                        return Results.Problem(
                            "Conexion SQL no configurada.");

                    // Este modulo funciona solamente
                    // con nuestra base de pruebas.
                    var datos = new SqlConnectionStringBuilder(cadena);

                    if (!string.Equals(
                        datos.InitialCatalog,
                        "SynchroDB_Pruebas",
                        StringComparison.OrdinalIgnoreCase))
                        return Results.Problem(
                            "Base de pruebas no configurada.");

                    await using var conexion = new SqlConnection(cadena);

                    await conexion.OpenAsync(contexto.RequestAborted);

                    const string sql = """
                        SELECT
                            r.TamanoSolicitado,
                            r.Estado,
                            r.FechaExpiracion,
                            r.TelefonoDestinatario,
                            c.Estado AS EstadoConsentimiento,
                            c.TelefonoWhatsApp,
                            c.FechaCreacionUtc,
                            c.FechaExpiracionUtc,
                            c.FechaConfirmacionUtc,
                            c.MensajeIdConfirmacion
                        FROM dbo.PreReservasEncomiendasWeb r
                        LEFT JOIN dbo.ConsentimientosPreReservaWhatsApp c
                            ON c.ReservaId = r.Id
                        WHERE r.Id = @Referencia
                          AND r.TokenGestionHash = @TokenHash;
                        """;

                    await using var comando = new SqlCommand(
                        sql, conexion);

                    comando.Parameters.Add(
                        "@Referencia", SqlDbType.Int
                    ).Value = solicitud.Referencia;

                    comando.Parameters.Add(
                        "@TokenHash", SqlDbType.VarBinary, 32
                    ).Value = tokenHash;

                    await using var lector =
                        await comando.ExecuteReaderAsync(
                            contexto.RequestAborted);

                    if (!await lector.ReadAsync(
                        contexto.RequestAborted))
                        return Results.NotFound(new
                        {
                            mensaje =
                                "Reserva no encontrada o credencial invalida."
                        });

                    string tamano = lector.GetString(0);
                    string estadoReserva = lector.GetString(1);

                    DateTime venceReserva = DateTime.SpecifyKind(
                        lector.GetDateTime(2), DateTimeKind.Utc);

                    string telefonoDestinatario = lector.GetString(3);

                    string? estadoConsentimiento =
                        lector.IsDBNull(4) ? null : lector.GetString(4);

                    string? telefonoWhatsApp =
                        lector.IsDBNull(5) ? null : lector.GetString(5);

                    DateTime? creacionConsentimiento =
                        lector.IsDBNull(6)
                            ? null
                            : DateTime.SpecifyKind(
                                lector.GetDateTime(6), DateTimeKind.Utc);

                    DateTime? vencimientoConsentimiento =
                        lector.IsDBNull(7)
                            ? null
                            : DateTime.SpecifyKind(
                                lector.GetDateTime(7), DateTimeKind.Utc);

                    DateTime? confirmacionConsentimiento =
                        lector.IsDBNull(8)
                            ? null
                            : DateTime.SpecifyKind(
                                lector.GetDateTime(8), DateTimeKind.Utc);

                    string? mensajeConfirmacion =
                        lector.IsDBNull(9) ? null : lector.GetString(9);

                    DateTime ahora = DateTime.UtcNow;

                    bool reservaVigente =
                        ValidacionDepositoEncomienda.ReservaVigente(
                            estadoReserva,
                            venceReserva,
                            ahora);

                    // Normalizar telefono del destinatario.
                    string telefonoEsperado =
                        telefonoDestinatario.StartsWith('+')
                            ? telefonoDestinatario
                            : "+" + telefonoDestinatario;

                    bool telefonoCoincide =
                        string.Equals(
                            telefonoWhatsApp,
                            telefonoEsperado,
                            StringComparison.Ordinal);

                    bool consentimientoConfirmado =
                        telefonoCoincide
                        && !string.IsNullOrWhiteSpace(mensajeConfirmacion)
                        && creacionConsentimiento.HasValue
                        && vencimientoConsentimiento.HasValue
                        && ValidacionDepositoEncomienda
                            .ConsentimientoConfirmado(
                                estadoConsentimiento,
                                confirmacionConsentimiento,
                                creacionConsentimiento.Value,
                                vencimientoConsentimiento.Value);

                    bool puedeContinuarVerificacion =
                        ValidacionDepositoEncomienda
                            .PuedeContinuarVerificacion(
                                reservaVigente,
                                consentimientoConfirmado);

                    string estadoVisible =
                        venceReserva <= ahora
                            ? "Vencida"
                            : estadoReserva;

                    string mensaje =
                        !reservaVigente
                            ? "La pre-reserva esta vencida o no disponible."
                            : !consentimientoConfirmado
                                ? "Pendiente de consentimiento valido por WhatsApp."
                                : "Consentimiento comprobado. Falta verificar al remitente y completar las condiciones del deposito.";

                    return Results.Ok(new
                    {
                        referencia = solicitud.Referencia,
                        tamanoSolicitado = tamano,
                        estado = estadoVisible,
                        fechaExpiracion = venceReserva,
                        reservaVigente,
                        estadoConsentimiento =
                            estadoConsentimiento ?? "Sin solicitud",
                        consentimientoConfirmado,
                        puedeContinuarVerificacion,

                        // Mantener bloqueado el deposito.
                        depositoHabilitado = false,
                        mensaje
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
    }
}
