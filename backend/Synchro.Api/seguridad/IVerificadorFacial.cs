internal readonly record struct ResultadoFacial(
    bool RostroCoincide,
    bool PruebaDeVidaSuperada)
{
    public bool Aprobado =>
        RostroCoincide && PruebaDeVidaSuperada;
}

internal interface IVerificadorFacial
{
    Task<ResultadoFacial> VerificarAsync(
        int destinatarioId,
        Stream capturaTemporal,
        CancellationToken cancelacion = default);
}
