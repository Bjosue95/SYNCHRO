
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public static class WhatsAppWebhookEndpoints
{
    private const int MaximoBytes = 256 * 1024;

    public static void MapWhatsAppWebhookEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/webhooks/whatsapp",
            (HttpContext http, IConfiguration config) =>
            {
                if (!http.Request.IsHttps)
                    return Results.StatusCode(403);

                string? esperado =
                    config["WhatsApp:WebhookVerifyToken"];

                if (string.IsNullOrWhiteSpace(esperado))
                    return Results.StatusCode(503);

                string modo =
                    http.Request.Query["hub.mode"].ToString();

                string recibido =
                    http.Request.Query["hub.verify_token"].ToString();

                string desafio =
                    http.Request.Query["hub.challenge"].ToString();

                if (modo != "subscribe" ||
                    !CompararSeguro(recibido, esperado) ||
                    desafio.Length is < 1 or > 128 ||
                    !desafio.All(char.IsAsciiDigit))
                {
                    return Results.StatusCode(403);
                }

                return Results.Text(desafio, "text/plain");
            })
            .AllowAnonymous();

        app.MapPost("/api/webhooks/whatsapp",
            async (HttpContext http, IConfiguration config) =>
            {
                if (!http.Request.IsHttps)
                    return Results.StatusCode(403);

                string? secreto =
                    config["WhatsApp:AppSecret"];

                string? numeroMeta =
                    config["WhatsApp:PhoneNumberId"];

                string? cadena =
                    config.GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(secreto) ||
                    string.IsNullOrWhiteSpace(numeroMeta) ||
                    string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.StatusCode(503);
                }

                if (http.Request.ContentLength > MaximoBytes)
                    return Results.StatusCode(413);

                byte[] cuerpo;

                using (var memoria = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int leidos;

                    while ((leidos =
                        await http.Request.Body.ReadAsync(
                            buffer.AsMemory(),
                            http.RequestAborted)) > 0)
                    {
                        if (memoria.Length + leidos > MaximoBytes)
                            return Results.StatusCode(413);

                        memoria.Write(buffer, 0, leidos);
                    }

                    cuerpo = memoria.ToArray();
                }

                string firma = http.Request.Headers[
                    "X-Hub-Signature-256"].ToString();

                if (!FirmaValida(cuerpo, firma, secreto))
                    return Results.Unauthorized();

                List<EventoWebhook> eventos;

                try
                {
                    using var documento =
                        JsonDocument.Parse(cuerpo);

                    var raiz = documento.RootElement;

                    if (LeerTexto(raiz, "object") !=
                        "whatsapp_business_account")
                    {
                        return Results.BadRequest();
                    }

                    eventos = ExtraerEventos(
                        raiz, numeroMeta);
                }
                catch (JsonException)
                {
                    return Results.BadRequest();
                }

                if (eventos.Count == 0)
                    return Results.Ok();

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync(
                        http.RequestAborted);

                    await using var transaccion =
                        (SqlTransaction)
                        await conexion.BeginTransactionAsync(
                            http.RequestAborted);


                    const string sql = """
    DECLARE @Nuevo BIT = 0;

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.WhatsAppWebhookEventos
             WITH (UPDLOCK, HOLDLOCK)
        WHERE ClaveEvento = @Clave
    )
    BEGIN
        INSERT INTO dbo.WhatsAppWebhookEventos
            (ClaveEvento, TipoEvento, MensajeId,
             WhatsappId, EstadoMensaje,
             RespuestaClasificada)
        VALUES
            (@Clave, @Tipo, @Id, @WaId,
             @Estado, @Respuesta);

        SET @Nuevo = 1;
    END;

    SELECT @Nuevo;
    """;



                    foreach (var evento in eventos)
                    {
                        await using var comando =
                            new SqlCommand(
                                sql, conexion, transaccion);

                        comando.Parameters.Add(
                            "@Clave",
                            SqlDbType.VarBinary, 32
                        ).Value = SHA256.HashData(
                            Encoding.UTF8.GetBytes(evento.Clave));

                        comando.Parameters.Add(
                            "@Tipo",
                            SqlDbType.NVarChar, 16
                        ).Value = evento.Tipo;

                        comando.Parameters.Add(
                            "@Id",
                            SqlDbType.NVarChar, 255
                        ).Value = evento.Id;

                        comando.Parameters.Add(
                            "@WaId",
                            SqlDbType.NVarChar, 32
                        ).Value = evento.WaId;

                        comando.Parameters.Add(
                            "@Estado",
                            SqlDbType.NVarChar, 24
                        ).Value =
                            (object?)evento.Estado ?? DBNull.Value;

                        comando.Parameters.Add(
                            "@Respuesta",
                            SqlDbType.NVarChar, 20
                        ).Value =
                            (object?)evento.Respuesta ?? DBNull.Value;


                        bool nuevo = Convert.ToBoolean(
                            await comando.ExecuteScalarAsync(
                                http.RequestAborted));

                        if (nuevo && evento.Tipo == "Mensaje")
                        {
                            await ConsentimientosPreReservaWhatsApp
                                .ProcesarMensajeAsync(
                                    conexion,
                                    transaccion,
                                    evento.WaId,
                                    evento.Texto,
                                    evento.Id,
                                    config,
                                    http.RequestAborted);
                        }

                    }


                    await transaccion.CommitAsync(
                        http.RequestAborted);

                    return Results.Ok();
                }
                catch (SqlException)
                {
                    // No confirmar recepción si SQL falló.
                    return Results.StatusCode(503);
                }
            })
            .AllowAnonymous()
            .DisableAntiforgery();
    }

    private static List<EventoWebhook> ExtraerEventos(
        JsonElement raiz, string numeroMeta)
    {
        var resultado = new List<EventoWebhook>();

        if (!raiz.TryGetProperty("entry", out var entradas) ||
            entradas.ValueKind != JsonValueKind.Array)
            return resultado;

        foreach (var entrada in entradas.EnumerateArray())
        {
            if (entrada.ValueKind != JsonValueKind.Object ||
                !entrada.TryGetProperty(
                    "changes", out var cambios) ||
                cambios.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var cambio in cambios.EnumerateArray())
            {
                if (cambio.ValueKind != JsonValueKind.Object ||
                    LeerTexto(cambio, "field") != "messages" ||
                    !cambio.TryGetProperty("value", out var valor) ||
                    valor.ValueKind != JsonValueKind.Object ||
                    !valor.TryGetProperty(
                        "metadata", out var metadata) ||
                    LeerTexto(metadata, "phone_number_id") !=
                        numeroMeta)
                    continue;

                if (valor.TryGetProperty(
                    "messages", out var mensajes) &&
                    mensajes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var mensaje in mensajes.EnumerateArray())
                    {
                        string? id = LeerTexto(mensaje, "id");
                        string? de = LeerTexto(mensaje, "from");

                        if (!Valido(id, 255) ||
                            !TelefonoValido(de))
                            continue;

                        string? texto = null;

                        if (mensaje.TryGetProperty(
                            "text", out var campoTexto))
                        {
                            texto = LeerTexto(campoTexto, "body");
                        }

                        string? respuesta =
                            texto?.Trim().ToUpperInvariant() switch
                            {
                                "ACEPTO" => "ACEPTO",
                                "NO ACEPTO" => "RECHAZO",
                                _ => null
                            };

                        resultado.Add(new(
                            $"M:{id}", "Mensaje",
                            id!, de!, null, respuesta, texto));
                    }
                }

                if (valor.TryGetProperty(
                    "statuses", out var estados) &&
                    estados.ValueKind == JsonValueKind.Array)
                {
                    foreach (var estado in estados.EnumerateArray())
                    {
                        string? id = LeerTexto(estado, "id");
                        string? a = LeerTexto(
                            estado, "recipient_id");
                        string? valorEstado =
                            LeerTexto(estado, "status");

                        if (!Valido(id, 255) ||
                            !TelefonoValido(a) ||
                            valorEstado is not (
                                "sent" or "delivered" or
                                "read" or "failed"))
                            continue;

                        resultado.Add(new(
                            $"S:{id}:{valorEstado}",
                            "Estado", id!, a!,
                            valorEstado, null));
                    }
                }
            }
        }

        return resultado;
    }

    private static bool Valido(string? valor, int maximo) =>
        !string.IsNullOrWhiteSpace(valor) &&
        valor.Length <= maximo;

    private static bool TelefonoValido(string? valor) =>
        Valido(valor, 15) &&
        valor!.Length >= 8 &&
        valor.All(char.IsAsciiDigit);

    private static string? LeerTexto(
        JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object &&
        elemento.TryGetProperty(propiedad, out var dato) &&
        dato.ValueKind == JsonValueKind.String
            ? dato.GetString()
            : null;

    private static bool CompararSeguro(
        string recibido, string esperado)
    {
        byte[] a = SHA256.HashData(
            Encoding.UTF8.GetBytes(recibido));

        byte[] b = SHA256.HashData(
            Encoding.UTF8.GetBytes(esperado));

        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static bool FirmaValida(
        byte[] cuerpo, string firma, string secreto)
    {
        if (!firma.StartsWith(
                "sha256=", StringComparison.Ordinal) ||
            firma.Length != 71)
            return false;

        try
        {
            byte[] recibida =
                Convert.FromHexString(firma[7..]);

            byte[] calculada = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secreto),
                cuerpo);

            return CryptographicOperations.FixedTimeEquals(
                recibida, calculada);
        }
        catch (FormatException)
        {
            return false;
        }
    }


    private sealed record EventoWebhook(
        string Clave,
        string Tipo,
        string Id,
        string WaId,
        string? Estado,
        string? Respuesta,
        string? Texto = null);

}
