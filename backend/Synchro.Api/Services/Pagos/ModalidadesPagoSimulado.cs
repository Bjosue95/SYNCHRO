namespace Synchro.Api.Services.Pagos;

// Politica compartida del prototipo.
// Identifica el metodo seleccionado; NO procesa dinero real.
internal static class ModalidadesPagoSimulado
{
    internal static string ParaServicioTemporal(
        int? terminal,
        string? metodoPago)
    {
        if (terminal is not null)
        {
            return metodoPago switch
            {
                null or "NFC" => "NfcSimulado",
                "Tarjeta" => "TarjetaTerminalSimulada",
                "Efectivo" => "EfectivoSimulado",
                _ => throw new ArgumentException(
                    "Metodo de pago simulado invalido.")
            };
        }

        return metodoPago switch
        {
            null or "TarjetaWeb" => "WebSimulado",
            _ => throw new ArgumentException(
                "El pago web no admite esta modalidad.")
        };
    }

    internal static string ParaRetiroPersonal(string? metodoPago)
    {
        return metodoPago switch
        {
            "Efectivo" => "EfectivoSimulado",
            "Tarjeta" => "TarjetaSimulada",
            _ => throw new ArgumentException(
                "Metodo de retiro simulado invalido.")
        };
    }
}