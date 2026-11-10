
internal readonly record struct EvidenciaFacialRemitente(
    Guid EvidenciaId,
    int ReservaId,
    int RemitenteId,
    int TerminalId,
    DateTimeOffset FechaEmisionUtc,
    DateTimeOffset FechaExpiracionUtc,
    bool Consumida,
    ResultadoFacial ResultadoFacial
);

internal static class ValidacionEvidenciaFacialRemitente
{
    // Vigencia maxima provisional de una evidencia.
    private static readonly TimeSpan VigenciaMaxima =
        TimeSpan.FromMinutes(2);

    public static bool EsValidaParaContinuar(
        EvidenciaFacialRemitente? evidencia,
        int reservaId,
        int remitenteId,
        int terminalId,
        DateTimeOffset ahoraUtc)
    {
        if (evidencia is not { } e)
            return false;

        // Identificadores validos y vinculados.
        if (e.EvidenciaId == Guid.Empty ||
            reservaId <= 0 ||
            remitenteId <= 0 ||
            terminalId <= 0)
            return false;

        if (e.ReservaId != reservaId ||
            e.RemitenteId != remitenteId ||
            e.TerminalId != terminalId)
            return false;

        // Nunca aceptar una evidencia consumida.
        if (e.Consumida)
            return false;

        // Comprobar fechas y tiempo maximo.
        if (e.FechaEmisionUtc > ahoraUtc ||
            e.FechaExpiracionUtc <= ahoraUtc ||
            e.FechaExpiracionUtc <= e.FechaEmisionUtc)
            return false;

        if (e.FechaExpiracionUtc -
            e.FechaEmisionUtc > VigenciaMaxima)
            return false;

        // Rostro y prueba de vida deben aprobarse.
        return e.ResultadoFacial.Aprobado;
    }
}
