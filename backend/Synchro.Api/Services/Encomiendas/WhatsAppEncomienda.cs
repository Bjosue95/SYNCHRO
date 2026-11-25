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

    public static string? Invitacion(int id, string? oficial)
    {
        var enlace = Enlace(oficial, Aceptacion(id));
        return enlace is null ? null : $"Tienes una encomienda SYNCHRO #{id}. Para aceptar recibirla y autorizar los avisos y el PIN de retiro por WhatsApp, abre este enlace y envía el mensaje prellenado desde tu propio WhatsApp al número oficial de SYNCHRO: {enlace}\nNo necesitas cuenta ni iniciar sesión. La referencia no permite retirar la encomienda. Después del depósito recibirás un PIN de retiro de un solo uso. No hay cobro adicional al retirar.";
    }
}
