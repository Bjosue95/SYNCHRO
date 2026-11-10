using System.Text.RegularExpressions;

namespace Synchro.Api.Services.WhatsApp;

internal sealed record MensajeWhatsApp(string Telefono, string Plantilla, string[] Variables)
{
    public override string ToString() => "Mensaje WhatsApp protegido";
    public static bool PlantillaEsValida(string? plantilla)
        => plantilla is { Length: >= 1 and <= 512 } && Regex.IsMatch(plantilla, @"\A[a-z][a-z0-9_]*\z");

    public static bool EsValido(string? telefono, string? plantilla, IReadOnlyList<string>? variables)
        => telefono is { Length: >= 9 and <= 16 } && Regex.IsMatch(telefono, @"\A\+[1-9][0-9]{7,14}\z") &&
           PlantillaEsValida(plantilla) &&
           variables is { Count: <= 10 } && variables.All(x=>x is { Length: > 0 and <= 1024 });
}
