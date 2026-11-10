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

        // Se usa fuera de la demostracion o cuando el modo simulado se
        // deshabilita expresamente. No representa una dependencia externa.
        return Task.FromResult(
            new ResultadoFacial(false, false));
    }
}
