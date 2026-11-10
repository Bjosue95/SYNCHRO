using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Synchro.Api.Services.WhatsApp;

public sealed class WhatsAppCloudService(HttpClient http, IConfiguration configuracion) : IWhatsAppCloudService
{
    public async Task<WhatsAppResultado> EnviarPlantillaAsync(
        string telefonoE164, string nombrePlantilla, IReadOnlyList<string> variables,
        CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();
        if (!MensajeWhatsApp.EsValido(telefonoE164, nombrePlantilla, variables))
            return new(false, null, "Los datos del mensaje no son validos.", FalloWhatsApp.DatosInvalidos);

        string version = configuracion["WhatsApp:ApiVersion"] ?? "v26.0";
        string? numeroId = configuracion["WhatsApp:PhoneNumberId"];
        string? token = configuracion["WhatsApp:AccessToken"];
        string idioma = configuracion["WhatsApp:Idioma"] ?? "es";
        if (!Regex.IsMatch(version, @"\Av[0-9]{1,2}\.0\z") ||
            string.IsNullOrWhiteSpace(numeroId) || numeroId.Length > 32 || !numeroId.All(char.IsAsciiDigit) ||
            string.IsNullOrWhiteSpace(token) || token.Length > 4096 || token.Any(char.IsControl) || !Regex.IsMatch(idioma, @"\A[a-z]{2}(_[A-Z]{2})?\z"))
            return new(false, null, "WhatsApp Cloud API no esta configurado.", FalloWhatsApp.Configuracion);

        var plantilla = new Dictionary<string, object>
        {
            ["name"] = nombrePlantilla,
            ["language"] = new { code = idioma }
        };
        if (variables.Count > 0)
            plantilla["components"] = new[] { new { type = "body", parameters = variables.Select(
                valor => new { type = "text", text = valor }).ToArray() } };

        using var solicitud = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.facebook.com/{version}/{numeroId}/messages");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        solicitud.Content = JsonContent.Create(new
        {
            messaging_product = "whatsapp", to = telefonoE164[1..], type = "template", template = plantilla
        });
        try
        {
            using var respuesta = await http.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, cancelacion);
            if (!respuesta.IsSuccessStatusCode)
            {
                // Reintentar solo un rechazo explicito por limite. Un 5xx o un
                // fallo de transporte puede ocurrir DESPUES de aceptar el envio.
                FalloWhatsApp fallo = respuesta.StatusCode == HttpStatusCode.TooManyRequests
                    ? FalloWhatsApp.Reintentable
                    : (int)respuesta.StatusCode >= 500 ? FalloWhatsApp.Indeterminado : FalloWhatsApp.Rechazado;
                TimeSpan? espera = respuesta.Headers.RetryAfter?.Delta;
                if (espera is null && respuesta.Headers.RetryAfter?.Date is { } fecha)
                    espera = fecha - DateTimeOffset.UtcNow;
                if (espera < TimeSpan.Zero) espera = TimeSpan.Zero;
                return new(false, null, $"Meta rechazo el envio (HTTP {(int)respuesta.StatusCode}).", fallo, espera);
            }

            await using var flujo = await respuesta.Content.ReadAsStreamAsync(cancelacion);
            using var memoria = new MemoryStream();
            byte[] buffer = new byte[8192];
            int leidos;
            while ((leidos = await flujo.ReadAsync(buffer, cancelacion)) > 0)
            {
                if (memoria.Length + leidos > 64 * 1024)
                    return new(false, null, "Respuesta de Meta demasiado grande.", FalloWhatsApp.Indeterminado);
                memoria.Write(buffer, 0, leidos);
            }
            using var documento = JsonDocument.Parse(memoria.ToArray());
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("messages", out var mensajes) &&
                mensajes.ValueKind == JsonValueKind.Array && mensajes.GetArrayLength() == 1 &&
                mensajes[0].ValueKind == JsonValueKind.Object && mensajes[0].TryGetProperty("id", out var datoId) &&
                datoId.ValueKind == JsonValueKind.String && datoId.GetString() is { Length: > 0 and <= 255 } id &&
                !id.Any(char.IsWhiteSpace) && !id.Any(char.IsControl))
                return new(true, id, "Mensaje aceptado por Meta. Entrega pendiente de confirmar.");
            return new(false, null, "Meta no confirmo el identificador del mensaje.", FalloWhatsApp.Indeterminado);
        }
        catch (HttpRequestException)
        {
            return new(false, null, "No fue posible confirmar el envio a Meta.", FalloWhatsApp.Indeterminado);
        }
        catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            return new(false, null, "Se agoto el tiempo de respuesta de Meta.", FalloWhatsApp.Indeterminado);
        }
        catch (JsonException)
        {
            return new(false, null, "Meta devolvio una respuesta no reconocida.", FalloWhatsApp.Indeterminado);
        }
    }
}
