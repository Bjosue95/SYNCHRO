
using System.Globalization;
using System.Text.RegularExpressions;

namespace Synchro.Api.Services.WhatsApp;

internal static class ComandoInicioSesionWhatsApp
{
    private static readonly Regex Patron = new(
        @"\ASYNCHRO INICIAR ([1-9][0-9]{0,9})\z",
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static bool IntentarLeer(
        string? texto,
        out int reservaId)
    {
        reservaId = 0;

        if (string.IsNullOrWhiteSpace(texto) ||
            texto.Length > 100)
        {
            return false;
        }

        Match coincidencia = Patron.Match(texto.Trim());

        return coincidencia.Success &&
            int.TryParse(
                coincidencia.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out reservaId) &&
            reservaId > 0;
    }
}
