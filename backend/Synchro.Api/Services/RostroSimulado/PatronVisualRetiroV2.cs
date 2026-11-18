using System.Security.Cryptography;

namespace Synchro.Api.Services.RostroSimulado;

// Descriptor EXCLUSIVO del retiro personal simulado en la base aislada.
// Suaviza y centra la luminancia y permite alinear variaciones pequeñas de
// encuadre (hasta dos pixeles de la malla 32x32). NO identifica personas;
// no sustituye PIN, consentimiento, cobro ni un sistema biometrico real.
internal static class PatronVisualRetiroV2
{
    public const string Modelo = "SYNCHRO-SIM-LUMINANCIA-32-v2";
    public const int Dimension = 32 * 32;

    // Distancia sobre correlacion centrada: <= 0.20 implica correlacion >= .98.
    // Umbral conservador del prototipo; NO relajarlo ante un rechazo.
    public const double UmbralDistancia = 0.20d;

    public static double[]? Extraer(byte[] png)
    {
        double[]? baseV1 = PatronVisualSimulado.Extraer(png);
        if (baseV1 is null) return null;
        try { return Transformar(baseV1); }
        finally { Array.Clear(baseV1); }
    }

    // Transforma solamente para usos nuevos de v2. Nunca alterar ni
    // reinterpretar referencias v1 almacenadas en SQL Server.
    internal static double[]? Transformar(double[]? baseV1)
    {
        if (baseV1 is not { Length: Dimension } ||
            baseV1.Any(v => !double.IsFinite(v))) return null;

        var filtrado = new double[Dimension];
        double suma = 0;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                double acumulado = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = Math.Clamp(y + dy, 0, 31);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = Math.Clamp(x + dx, 0, 31);
                        int peso = (dy == 0 ? 2 : 1) * (dx == 0 ? 2 : 1);
                        acumulado += peso * baseV1[yy * 32 + xx];
                    }
                }
                double valor = acumulado / 16d;
                filtrado[y * 32 + x] = valor;
                suma += valor;
            }
        }

        double media = suma / Dimension;
        double normaCuadrada = 0;
        for (int i = 0; i < Dimension; i++)
        {
            filtrado[i] -= media;
            normaCuadrada += filtrado[i] * filtrado[i];
        }

        if (!double.IsFinite(normaCuadrada) || normaCuadrada < 1e-12d)
        {
            Array.Clear(filtrado);
            return null;
        }

        double norma = Math.Sqrt(normaCuadrada);
        for (int i = 0; i < Dimension; i++)
            filtrado[i] /= norma;

        return filtrado;
    }

    // Compara solo descriptores del MISMO modelo v2. La alineacion no altera
    // datos persistidos y sigue exigiendo una coincidencia estricta.
    public static double? CalcularDistancia(double[]? referencia, double[]? captura)
    {
        if (referencia is not { Length: Dimension } ||
            captura is not { Length: Dimension }) return null;
        if (referencia.Any(v => !double.IsFinite(v)) ||
            captura.Any(v => !double.IsFinite(v))) return null;

        double mejor = double.PositiveInfinity;
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int inicioY = Math.Max(0, dy);
                int finY = Math.Min(32, 32 + dy);
                int inicioX = Math.Max(0, dx);
                int finX = Math.Min(32, 32 + dx);

                int n = 0;
                double sumaA = 0, sumaB = 0, sumaAA = 0, sumaBB = 0, sumaAB = 0;
                for (int y = inicioY; y < finY; y++)
                {
                    for (int x = inicioX; x < finX; x++)
                    {
                        double a = referencia[y * 32 + x];
                        double b = captura[(y - dy) * 32 + x - dx];
                        n++;
                        sumaA += a;
                        sumaB += b;
                        sumaAA += a * a;
                        sumaBB += b * b;
                        sumaAB += a * b;
                    }
                }

                double varA = sumaAA - sumaA * sumaA / n;
                double varB = sumaBB - sumaB * sumaB / n;
                if (varA <= 1e-12d || varB <= 1e-12d) continue;

                double correlacion = (sumaAB - sumaA * sumaB / n) /
                    Math.Sqrt(varA * varB);
                if (!double.IsFinite(correlacion)) continue;
                correlacion = Math.Clamp(correlacion, -1d, 1d);
                double distancia = Math.Sqrt(Math.Max(0d, 2d - 2d * correlacion));
                mejor = Math.Min(mejor, distancia);
            }
        }
        return double.IsFinite(mejor) ? mejor : null;
    }
}
