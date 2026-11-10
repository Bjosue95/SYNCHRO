
internal static class ComparadorDescriptoresFaciales
{
    // Dimension prevista para los descriptores
    // del modelo facial que evaluaremos.
    public const int DimensionEsperada = 1024;

    // Calcula distancia euclidiana.
    // Menor distancia significa mayor semejanza
    // matematica entre los descriptores.
    //
    // NO autentica personas ni autoriza depositos.
    public static double? CalcularDistancia(
        double[]? perfilRegistrado,
        double[]? capturaActual)
    {
        if (perfilRegistrado is not
            { Length: DimensionEsperada })
            return null;

        if (capturaActual is not
            { Length: DimensionEsperada })
            return null;

        double sumaDiferencias = 0;
        double normaPerfil = 0;
        double normaCaptura = 0;

        for (int i = 0; i < DimensionEsperada; i++)
        {
            double a = perfilRegistrado[i];
            double b = capturaActual[i];

            // Rechazar valores matematicos invalidos.
            if (!double.IsFinite(a) ||
                !double.IsFinite(b))
                return null;

            double diferencia = a - b;

            sumaDiferencias += diferencia * diferencia;
            normaPerfil += a * a;
            normaCaptura += b * b;

            if (!double.IsFinite(sumaDiferencias) ||
                !double.IsFinite(normaPerfil) ||
                !double.IsFinite(normaCaptura))
                return null;
        }

        // Rechazar representaciones sin informacion.
        if (normaPerfil <= 0 ||
            normaCaptura <= 0)
            return null;

        return Math.Sqrt(sumaDiferencias);
    }
}
