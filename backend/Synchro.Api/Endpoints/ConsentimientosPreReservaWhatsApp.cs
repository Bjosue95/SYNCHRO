
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public static class ConsentimientosPreReservaWhatsApp
{
    private static readonly Regex Token = new(
        @"\A[0-9A-Fa-f]{64}\z",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex Mensaje = new(
        @"\ASYNCHRO ACEPTO ([1-9][0-9]{0,9}) ([0-9A-F]{16}) RECIBIR AVISOS Y PIN DE RETIRO POR WHATSAPP\z",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static void MapConsentimientosPreReservaWhatsApp(
        this WebApplication app)
    {
        app.MapPost(
            "/api/web/encomiendas/pre-reservas/{id:int}/consentimiento/iniciar",
            async (
                int id,
                HttpContext http,
                IAntiforgery antiforgery) =>
            {
                if (!http.Request.IsHttps)
                    return Results.BadRequest(new
                    {
                        mensaje = "HTTPS obligatorio."
                    });

                if (!await antiforgery.IsRequestValidAsync(http))
                    return Results.BadRequest(new
                    {
                        mensaje = "CSRF invalido."
                    });

                if (id <= 0)
                    return Results.BadRequest(new
                    {
                        mensaje = "Reserva invalida."
                    });

                string? clave = app.Configuration[
                    "WhatsApp:ConsentimientoHmacKey"];

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(clave) ||
                    clave.Length < 32 ||
                    string.IsNullOrWhiteSpace(cadena))
                    return Results.StatusCode(503);

                string token = http.Request.Headers[
                    "X-SYNCHRO-RESERVA"].ToString();

                if (!Token.IsMatch(token))
                    return Results.NotFound(new
                    {
                        mensaje = "Reserva no encontrada."
                    });

                byte[] tokenBytes =
                    Convert.FromHexString(token);

                byte[] hashToken =
                    SHA256.HashData(tokenBytes);

                CryptographicOperations.ZeroMemory(tokenBytes);

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync(
                        http.RequestAborted);

                    await using var tx = (SqlTransaction)
                        await conexion.BeginTransactionAsync(
                            http.RequestAborted);

                    string telefono;
                    string estado;
                    DateTime expiraReserva;

                    await using (var cmd = new SqlCommand("""
                        SELECT TelefonoDestinatario,
                               Estado,
                               FechaExpiracion
                        FROM dbo.PreReservasEncomiendasWeb
                             WITH (UPDLOCK, HOLDLOCK)
                        WHERE Id = @Id
                          AND TokenGestionHash = @Hash AND ClientePersonaId=@cliente AND OrigenPreReserva=N'Web';
                        """, conexion, tx))
                    {
                        cmd.Parameters.Add("@cliente",SqlDbType.Int).Value=int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                        cmd.Parameters.Add(
                            "@Id", SqlDbType.Int).Value = id;

                        cmd.Parameters.Add(
                            "@Hash", SqlDbType.VarBinary, 32
                        ).Value = hashToken;

                        await using var lector =
                            await cmd.ExecuteReaderAsync(
                                http.RequestAborted);

                        if (!await lector.ReadAsync(
                            http.RequestAborted))
                            return Results.NotFound(new
                            {
                                mensaje = "Reserva no encontrada."
                            });

                        telefono = lector.GetString(0);
                        estado = lector.GetString(1);
                        expiraReserva = lector.GetDateTime(2);
                    }

                    if (estado != "PendienteConsentimiento" ||
                        expiraReserva <= DateTime.UtcNow)
                        return Results.Conflict(new
                        {
                            mensaje = "Reserva vencida o no disponible."
                        });

                    string telefonoNormalizado =
                        "+" + telefono.TrimStart('+');

                    if (!Regex.IsMatch(
                        telefonoNormalizado,
                        @"\A\+[1-9][0-9]{7,14}\z"))
                        return Results.Conflict(new
                        {
                            mensaje = "Telefono de reserva no valido."
                        });

                    string? estadoConsentimiento = null;
                    DateTime vencimientoActual =
                        DateTime.MinValue;

                    await using (var cmd = new SqlCommand("""
                        SELECT Estado, FechaExpiracionUtc
                        FROM dbo.ConsentimientosPreReservaWhatsApp
                             WITH (UPDLOCK, HOLDLOCK)
                        WHERE ReservaId = @Id;
                        """, conexion, tx))
                    {
                        cmd.Parameters.Add(
                            "@Id", SqlDbType.Int).Value = id;

                        await using var lector =
                            await cmd.ExecuteReaderAsync(
                                http.RequestAborted);

                        if (await lector.ReadAsync(
                            http.RequestAborted))
                        {
                            estadoConsentimiento =
                                lector.GetString(0);

                            vencimientoActual =
                                lector.GetDateTime(1);
                        }
                    }

                    bool cerrado = estadoConsentimiento is
                        "Confirmado" or "Revocado" or "Rechazado";

                    bool pendienteVigente =
                        estadoConsentimiento == "Pendiente" &&
                        vencimientoActual > DateTime.UtcNow;

                    if (cerrado || pendienteVigente)
                        return Results.Conflict(new
                        {
                            mensaje =
                                "Ya existe una solicitud vigente o cerrada."
                        });

                    string codigo = Convert.ToHexString(
                        RandomNumberGenerator.GetBytes(8));

                    byte[] hashCodigo = CalcularHash(
                        clave, id, telefonoNormalizado, codigo);

                    DateTime vence =
                        DateTime.UtcNow.AddMinutes(15);

                    try
                    {
                        await using var cmd = new SqlCommand("""
                            IF EXISTS (
                                SELECT 1
                                FROM dbo.ConsentimientosPreReservaWhatsApp
                                WHERE ReservaId = @Id
                            )
                            BEGIN
                                UPDATE dbo.ConsentimientosPreReservaWhatsApp
                                SET TelefonoWhatsApp = @Telefono,
                                    CodigoHash = @CodigoHash,
                                    Estado = N'Pendiente',
                                    IntentosFallidos = 0,
                                    FechaCreacionUtc = SYSUTCDATETIME(),
                                    FechaExpiracionUtc = @Vence,
                                    FechaConfirmacionUtc = NULL,
                                    MensajeIdConfirmacion = NULL
                                WHERE ReservaId = @Id;
                            END
                            ELSE
                            BEGIN
                                INSERT INTO dbo.ConsentimientosPreReservaWhatsApp
                                    (ReservaId, TelefonoWhatsApp,
                                     CodigoHash, FechaExpiracionUtc)
                                VALUES
                                    (@Id, @Telefono,
                                     @CodigoHash, @Vence);
                            END;
                            """, conexion, tx);

                        cmd.Parameters.Add(
                            "@Id", SqlDbType.Int).Value = id;

                        cmd.Parameters.Add(
                            "@Telefono", SqlDbType.NVarChar, 20
                        ).Value = telefonoNormalizado;

                        cmd.Parameters.Add(
                            "@CodigoHash", SqlDbType.VarBinary, 32
                        ).Value = hashCodigo;

                        cmd.Parameters.Add(
                            "@Vence", SqlDbType.DateTime2
                        ).Value = vence;

                        await cmd.ExecuteNonQueryAsync(
                            http.RequestAborted);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(
                            hashCodigo);
                    }

                    await tx.CommitAsync(http.RequestAborted);

                    string texto =
                        $"SYNCHRO ACEPTO {id} {codigo} " +
                        "RECIBIR AVISOS Y PIN DE RETIRO POR WHATSAPP";

                    string? numeroEmpresa =
                        app.Configuration[
                            "WhatsApp:BusinessPhoneE164"
                        ]?.TrimStart('+');

                    string? enlace =
                        numeroEmpresa is not null &&
                        Regex.IsMatch(
                            numeroEmpresa,
                            @"\A[1-9][0-9]{7,14}\z")
                            ? $"https://wa.me/{numeroEmpresa}" +
                              $"?text={Uri.EscapeDataString(texto)}"
                            : null;

                    http.Response.Headers.CacheControl =
                        "no-store";

                    return Results.Ok(new
                    {
                        reservaId = id,
                        destinatarioUltimos4 =
                            telefonoNormalizado[^4..],
                        mensajeParaCompartirConDestinatario = texto,
                        enlaceParaDestinatario = enlace,
                        fechaExpiracionUtc = vence,
                        consentimientoConfirmado = false,
                        depositoHabilitado = false
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No fue posible preparar el consentimiento.");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(hashToken);
                }
            })
            .RequireAuthorization("SoloClientes").RequireRateLimiting("LoginLimitado");
    }

    // Llamar solamente despues de verificar la firma de Meta.
    // Se ejecuta dentro de la transaccion del webhook.
    public static async Task ProcesarMensajeAsync(
        SqlConnection conexion,
        SqlTransaction tx,
        string numeroOrigen,
        string? texto,
        string mensajeId,
        IConfiguration configuracion,
        CancellationToken cancelar)
    {
        if (string.IsNullOrWhiteSpace(texto) ||
            texto.Length > 200 ||
            string.IsNullOrWhiteSpace(numeroOrigen) ||
            numeroOrigen.Length > 15 ||
            !numeroOrigen.All(char.IsAsciiDigit))
            return;

        string telefono = "+" + numeroOrigen;
        string limpio = texto.Trim();

        if (limpio.Equals(
                "BAJA",
                StringComparison.OrdinalIgnoreCase) ||
            limpio.Equals(
                "STOP",
                StringComparison.OrdinalIgnoreCase))
        {
            await using var revocar = new SqlCommand("""
                UPDATE dbo.ConsentimientosPreReservaWhatsApp
                SET Estado = N'Revocado'
                WHERE TelefonoWhatsApp = @Telefono
                  AND Estado IN (N'Pendiente', N'Confirmado');
                """, conexion, tx);

            revocar.Parameters.Add(
                "@Telefono", SqlDbType.NVarChar, 20
            ).Value = telefono;

            await revocar.ExecuteNonQueryAsync(cancelar);
            return;
        }

        Match coincidir = Mensaje.Match(limpio);

        if (!coincidir.Success ||
            !int.TryParse(
                coincidir.Groups[1].Value,
                out int reservaId))
            return;

        string? clave = configuracion[
            "WhatsApp:ConsentimientoHmacKey"];

        if (string.IsNullOrWhiteSpace(clave) ||
            clave.Length < 32)
            throw new InvalidOperationException(
                "Clave de consentimiento no configurada.");

        byte[] hashEsperado = CalcularHash(
            clave,
            reservaId,
            telefono,
            coincidir.Groups[2].Value.ToUpperInvariant());

        try
        {
            byte[]? guardado;

            await using (var consultar = new SqlCommand("""
                SELECT c.CodigoHash
                FROM dbo.ConsentimientosPreReservaWhatsApp c
                     WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.PreReservasEncomiendasWeb r
                    ON r.Id = c.ReservaId
                WHERE c.ReservaId = @Id
                  AND c.TelefonoWhatsApp = @Telefono
                  AND (
                      r.TelefonoDestinatario = @Telefono
                      OR r.TelefonoDestinatario = @SinMas
                  )
                  AND c.Estado = N'Pendiente'
                  AND c.IntentosFallidos < 5
                  AND c.FechaExpiracionUtc > SYSUTCDATETIME()
                  AND r.Estado = N'PendienteConsentimiento'
                  AND r.FechaExpiracion > SYSUTCDATETIME();
                """, conexion, tx))
            {
                consultar.Parameters.Add(
                    "@Id", SqlDbType.Int).Value = reservaId;

                consultar.Parameters.Add(
                    "@Telefono", SqlDbType.NVarChar, 20
                ).Value = telefono;

                consultar.Parameters.Add(
                    "@SinMas", SqlDbType.NVarChar, 20
                ).Value = numeroOrigen;

                object? valor =
                    await consultar.ExecuteScalarAsync(cancelar);

                guardado = valor as byte[];
            }

            if (guardado is null)
                return;

            bool valido =
                CryptographicOperations.FixedTimeEquals(
                    guardado, hashEsperado);

            CryptographicOperations.ZeroMemory(guardado);

            string sql = valido ? """
                UPDATE dbo.ConsentimientosPreReservaWhatsApp
                SET Estado = N'Confirmado',
                    FechaConfirmacionUtc = SYSUTCDATETIME(),
                    MensajeIdConfirmacion = @MensajeId
                WHERE ReservaId = @Id
                  AND Estado = N'Pendiente';
                """ : """
                UPDATE dbo.ConsentimientosPreReservaWhatsApp
                SET IntentosFallidos = IntentosFallidos + 1
                WHERE ReservaId = @Id
                  AND Estado = N'Pendiente'
                  AND IntentosFallidos < 5;
                """;

            await using var actualizar =
                new SqlCommand(sql, conexion, tx);

            actualizar.Parameters.Add(
                "@Id", SqlDbType.Int).Value = reservaId;

            if (valido)
                actualizar.Parameters.Add(
                    "@MensajeId", SqlDbType.NVarChar, 255
                ).Value = mensajeId;

            await actualizar.ExecuteNonQueryAsync(cancelar);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hashEsperado);
        }
    }

    private static byte[] CalcularHash(
        string clave,
        int id,
        string telefono,
        string codigo)
    {
        return HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(clave),
            Encoding.UTF8.GetBytes(
                $"{id}|{telefono}|{codigo}"));
    }
}
