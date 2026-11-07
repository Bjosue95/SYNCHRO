internal sealed class VerificadorFacialNoDisponible
    : IVerificadorFacial
{
    public Task<ResultadoFacial> VerificarAsync(
        int destinatarioId,
        Stream capturaTemporal,
        CancellationToken cancelacion = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            destinatarioId);

        ArgumentNullException.ThrowIfNull(capturaTemporal);

        cancelacion.ThrowIfCancellationRequested();

        // Sin un proveedor biometrico confiable,
        // ninguna verificacion puede aprobarse.
        return Task.FromResult(
            new ResultadoFacial(false, false));
    }
}
