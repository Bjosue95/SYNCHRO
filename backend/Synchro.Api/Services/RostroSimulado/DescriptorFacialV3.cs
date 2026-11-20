namespace Synchro.Api.Services.RostroSimulado;

// Descriptor experimental Human.js 3.3.6.
// No autentica personas por si solo.
internal static class DescriptorFacialV3
{
    public const string Modelo = "SYNCHRO-HUMANJS-EMBEDDING-1024-v3";
    public const int Dimension = 1024;

    public static double[]? Normalizar(double[]? datos)
    {
        if (datos is not { Length: Dimension })
            return null;

        double suma = 0;

        foreach (double valor in datos)
        {
            if (!double.IsFinite(valor) || Math.Abs(valor) > 1000d)
                return null;

            suma += valor * valor;
        }

        if (!double.IsFinite(suma) || suma < 1e-8d)
            return null;

        double norma = Math.Sqrt(suma);
        double[] resultado = new double[Dimension];

        for (int i = 0; i < Dimension; i++)
            resultado[i] = datos[i] / norma;

        return resultado;
    }

    public static double? DistanciaCoseno(
        double[]? referencia, double[]? captura)
    {
        double[]? a = Normalizar(referencia);
        double[]? b = Normalizar(captura);

        try
        {
            if (a is null || b is null)
                return null;

            double producto = 0;

            for (int i = 0; i < Dimension; i++)
                producto += a[i] * b[i];

            return 1d - Math.Clamp(producto, -1d, 1d);
        }
        finally
        {
            if (a is not null) Array.Clear(a);
            if (b is not null) Array.Clear(b);
        }
    }
}