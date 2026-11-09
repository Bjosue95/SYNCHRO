
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Synchro.Api.Services.WhatsApp;

public sealed class WhatsAppCloudService : IWhatsAppCloudService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuracion;

    private static readonly Regex TelefonoValido = new(
        @"^\+[1-9][0-9]{7,14}$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    private static readonly Regex PlantillaValida = new(
        @"^[a-z][a-z0-9_]{0,511}$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    private static readonly Regex VersionValida = new(
        @"^v[0-9]{1,2}\.0$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100)
    );

    public WhatsAppCloudService(
        HttpClient http,
        IConfiguration configuracion)
    {
        _http = http;
        _configuracion = configuracion;
    }

    public async Task<WhatsAppResultado> EnviarPlantillaAsync(
        string telefonoE164,
        string nombrePlantilla,
        IReadOnlyList<string> variables,
        CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(telefonoE164) ||
            !TelefonoValido.IsMatch(telefonoE164) ||
            string.IsNullOrWhiteSpace(nombrePlantilla) ||
            !PlantillaValida.IsMatch(nombrePlantilla) ||
            variables is null ||
            variables.Count > 10 ||
            variables.Any(x =>
                string.IsNullOrEmpty(x) || x.Length > 1024))
        {
            return new(
                false,
                null,
                "Los datos del mensaje no son validos."
            );
        }

        string version = _configuracion[
            "WhatsApp:ApiVersion"] ?? "v26.0";

        string? numeroId = _configuracion[
            "WhatsApp:PhoneNumberId"];

        string? token = _configuracion[
            "WhatsApp:AccessToken"];

        string idioma = _configuracion[
            "WhatsApp:Idioma"] ?? "es";

        bool numeroIdValido =
            !string.IsNullOrWhiteSpace(numeroId) &&
            numeroId.All(char.IsAsciiDigit);

        if (!VersionValida.IsMatch(version) ||
            !numeroIdValido ||
            string.IsNullOrWhiteSpace(token) ||
            !Regex.IsMatch(
                idioma,
                @"^[a-z]{2}(_[A-Z]{2})?$"))
        {
            return new(
                false,
                null,
                "WhatsApp Cloud API no esta configurado."
            );
        }

        var plantilla = new Dictionary<string, object>
        {
            ["name"] = nombrePlantilla,
            ["language"] = new { code = idioma }
        };

        if (variables.Count > 0)
        {
            plantilla["components"] = new[]
            {
                new
                {
                    type = "body",
                    parameters = variables.Select(
                        valor => new
                        {
                            type = "text",
                            text = valor
                        }
                    ).ToArray()
                }
            };
        }

        var contenido = new
        {
            messaging_product = "whatsapp",
            to = telefonoE164[1..],
            type = "template",
            template = plantilla
        };

        var url =
            $"https://graph.facebook.com/" +
            $"{version}/{numeroId}/messages";

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Post,
            url
        );

        solicitud.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token
            );

        solicitud.Content = JsonContent.Create(contenido);

        try
        {
            using var respuesta = await _http.SendAsync(
                solicitud,
                HttpCompletionOption.ResponseHeadersRead,
                cancelacion
            );

            if (!respuesta.IsSuccessStatusCode)
            {
                return new(
                    false,
                    null,
                    $"Meta rechazo el envio (HTTP {(int)respuesta.StatusCode})."
                );
            }

            await using var flujo =
                await respuesta.Content.ReadAsStreamAsync(
                    cancelacion
                );

            using var documento =
                await JsonDocument.ParseAsync(
                    flujo,
                    cancellationToken: cancelacion
                );

            if (documento.RootElement.TryGetProperty(
                    "messages",
                    out var mensajes) &&
                mensajes.ValueKind == JsonValueKind.Array &&
                mensajes.GetArrayLength() > 0 &&
                mensajes[0].TryGetProperty(
                    "id",
                    out var identificador) &&
                identificador.ValueKind == JsonValueKind.String)
            {
                string? id = identificador.GetString();

                if (!string.IsNullOrWhiteSpace(id))
                {
                    return new(
                        true,
                        id,
                        "Mensaje aceptado por Meta. Entrega pendiente de confirmar."
                    );
                }
            }

            return new(
                false,
                null,
                "Meta respondio, pero no confirmo el identificador del mensaje."
            );
        }
        catch (HttpRequestException)
        {
            return new(
                false,
                null,
                "No fue posible comunicarse con Meta."
            );
        }
        catch (TaskCanceledException)
            when (!cancelacion.IsCancellationRequested)
        {
            return new(
                false,
                null,
                "Se agoto el tiempo de respuesta de Meta."
            );
        }
        catch (JsonException)
        {
            return new(
                false,
                null,
                "Meta devolvio una respuesta no reconocida."
            );
        }
    }
}
