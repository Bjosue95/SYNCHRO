
using Xunit;

namespace Synchro.Api.Tests;

public class ComparadorDescriptoresFacialesTests
{
    private static double[] PatronValido()
    {
        return Enumerable.Repeat(
            0.25,
            ComparadorDescriptoresFaciales.DimensionEsperada
        ).ToArray();
    }

    [Fact]
    public void PatronesIdenticos_DistanciaCero()
    {
        var a = PatronValido();
        var b = PatronValido();

        double? distancia =
            ComparadorDescriptoresFaciales
                .CalcularDistancia(a, b);

        Assert.True(distancia.HasValue);
        Assert.Equal(0.0, distancia.Value, 8);
    }

    [Fact]
    public void PatronesDiferentes_DistanciaPositiva()
    {
        var a = PatronValido();
        var b = PatronValido();

        b[100] += 0.5;

        double? distancia =
            ComparadorDescriptoresFaciales
                .CalcularDistancia(a, b);

        Assert.True(distancia.HasValue);
        Assert.Equal(0.5, distancia.Value, 8);
    }

    [Fact]
    public void Distancia_DebeSerSimetrica()
    {
        var a = PatronValido();
        var b = PatronValido();

        b[10] += 0.3;
        b[20] -= 0.1;

        var primera =
            ComparadorDescriptoresFaciales
                .CalcularDistancia(a, b);

        var segunda =
            ComparadorDescriptoresFaciales
                .CalcularDistancia(b, a);

        Assert.Equal(primera, segunda);
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(1023, 1024)]
    [InlineData(1024, 1025)]
    public void DimensionesIncorrectas_SeRechazan(
        int longitudA,
        int longitudB)
    {
        var a = Enumerable.Repeat(
            0.25, longitudA).ToArray();

        var b = Enumerable.Repeat(
            0.25, longitudB).ToArray();

        Assert.Null(
            ComparadorDescriptoresFaciales
                .CalcularDistancia(a, b));
    }

    [Fact]
    public void ReferenciaNula_SeRechaza()
    {
        Assert.Null(
            ComparadorDescriptoresFaciales
                .CalcularDistancia(null, PatronValido()));

        Assert.Null(
            ComparadorDescriptoresFaciales
                .CalcularDistancia(PatronValido(), null));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinito")]
    public void ValoresInvalidos_SeRechazan(
        string tipo)
    {
        var a = PatronValido();
        var b = PatronValido();

        b[100] = tipo == "NaN"
            ? double.NaN
            : double.PositiveInfinity;

        Assert.Null(
            ComparadorDescriptoresFaciales
                .CalcularDistancia(a, b));
    }

    [Fact]
    public void PatronTodoCeros_SeRechaza()
    {
        var vacio = new double[
            ComparadorDescriptoresFaciales.DimensionEsperada];

        Assert.Null(
            ComparadorDescriptoresFaciales
                .CalcularDistancia(vacio, PatronValido()));
    }
}
