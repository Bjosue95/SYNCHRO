
internal sealed class PreparadorEvidenciaFacialRemitente
{
    private readonly IVerificadorFacial _verificador;
    private readonly TimeProvider _reloj;

    public PreparadorEvidenciaFacialRemitente(
        IVerificadorFacial verificador,
        TimeProvider reloj)
    {
        _verificador = verificador
            ?? throw new ArgumentNullException(nameof(verificador));

        _reloj = reloj
            ?? throw new ArgumentNullException(nameof(reloj));
    }

    public async Task<EvidenciaFacialRemitente?> PrepararAsync(
        int reservaId,
        int remitenteId,
        int terminalId,
        Stream capturaTemporal,
        CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();

        // Nunca preparar evidencia con identificadores invalidos.
        if (reservaId <= 0 ||
            remitenteId <= 0 ||
            terminalId <= 0)
            return null;

        // La captura debe ser legible y no estar vacia.
        if (capturaTemporal is null ||
            !capturaTemporal.CanRead)
            return null;

        if (capturaTemporal.CanSeek &&
            capturaTemporal.Position >= capturaTemporal.Length)
            return null;

        // El resultado procede del verificador del servidor.
        // No se recibe un booleano de aprobacion del navegador.
        ResultadoFacial resultado =
            await _verificador.VerificarAsync(
                remitenteId,
                capturaTemporal,
                cancelacion);

        if (!resultado.Aprobado)
            return null;

        // La hora se obtiene del servidor.
        DateTimeOffset ahora = _reloj.GetUtcNow();

        return new EvidenciaFacialRemitente(
            Guid.NewGuid(),
            reservaId,
            remitenteId,
            terminalId,
            ahora,
            ahora.AddMinutes(2),
            false,
            resultado);
    }
}
