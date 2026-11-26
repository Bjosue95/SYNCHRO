using System.Globalization;
using System.Text.RegularExpressions;

namespace Synchro.Api.Services.Encomiendas;

internal static class WhatsAppEncomienda
{
    public static string? Telefono(string? valor)
    {
        var numero = valor?.Trim();
        return numero is not null && Regex.IsMatch(numero, @"\A\+?[1-9][0-9]{7,14}\z") ? "+" + numero.TrimStart('+') : null;
    }

    public static string Aceptacion(int? encomienda = null) => encomienda.HasValue
        ? "SYNCHRO ACEPTO " + encomienda.Value.ToString(CultureInfo.InvariantCulture)
        : "SYNCHRO ACEPTO";

    public static string? Enlace(string? telefono, string texto)
    {
        var numero = Telefono(telefono);
        return numero is null ? null : $"https://wa.me/{numero[1..]}?text={Uri.EscapeDataString(texto)}";
    }

    // La referencia de consentimiento es aleatoria; no revela número de reserva.
    public static string AceptacionDestinatario(Guid codigo) =>
        "SYNCHRO ACEPTO ENCOMIENDA " + codigo.ToString("N");

    public static string? Invitacion(Guid codigo, string? oficial)
    {
        var enlace = Enlace(oficial, AceptacionDestinatario(codigo));
        return enlace is null ? null : $"Tienes una encomienda pendiente de retiro en SYNCHRO. Para aceptar recibirla y autorizar el PIN por WhatsApp, abre este enlace y envía el mensaje prellenado desde tu propio número al WhatsApp oficial de SYNCHRO: {enlace}\nLa aceptación no permite retirar por sí sola. Recibirás un PIN exclusivo después de que el operador compruebe tu mensaje. No hay un segundo cobro al retirar.";
    }
}
