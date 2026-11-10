internal readonly record struct ResultadoFacial(
    bool RostroCoincide,
    // Nombre legado. En el prototipo simulado indica captura procesada;
    // no constituye ni afirma una prueba de vida fisica.
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
