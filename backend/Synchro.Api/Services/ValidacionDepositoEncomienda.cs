namespace Synchro.Api.Services;

public static class ValidacionDepositoEncomienda
{
    public static bool ReservaVigente(
        string estado,
        DateTime fechaExpiracionUtc,
        DateTime ahoraUtc)
    {
        return estado == "PendienteConsentimiento"
            && fechaExpiracionUtc > ahoraUtc;
    }

    public static bool ConsentimientoConfirmado(
        string? estado,
        DateTime? fechaConfirmacionUtc,
        DateTime fechaCreacionUtc,
        DateTime fechaExpiracionUtc)
    {
        return estado == "Confirmado"
            && fechaConfirmacionUtc.HasValue
            && fechaConfirmacionUtc.Value >= fechaCreacionUtc
            && fechaConfirmacionUtc.Value <= fechaExpiracionUtc;
    }

    public static bool PuedeContinuarVerificacion(
        bool reservaVigente,
        bool consentimientoConfirmado)
    {
        return reservaVigente && consentimientoConfirmado;
    }
}
