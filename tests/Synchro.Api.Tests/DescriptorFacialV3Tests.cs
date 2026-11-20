using Synchro.Api.Services.RostroSimulado;
using Xunit;

namespace Synchro.Api.Tests;

public sealed class DescriptorFacialV3Tests
{
    private static double[] Crear(int indice, double valor = 1d)
    {
        var datos = new double[1024];
        datos[indice] = valor;
        return datos;
    }

    [Fact]
    public void MismaReferencia_DistanciaCero()
    {
        double? resultado = DescriptorFacialV3.DistanciaCoseno(
            Crear(0, 5), Crear(0, 2));

        Assert.NotNull(resultado);
        Assert.InRange(resultado.Value, 0d, 1e-10d);
    }

    [Fact]
    public void VectoresOrtogonales_TienenDistanciaUno()
    {
        double? resultado = DescriptorFacialV3.DistanciaCoseno(
            Crear(0), Crear(1));

        Assert.NotNull(resultado);
        Assert.Equal(1d, resultado.Value, 10);
    }

    [Fact]
    public void DimensionesYValoresInvalidos_SeRechazan()
    {
        Assert.Null(DescriptorFacialV3.Normalizar(new double[1023]));
        Assert.Null(DescriptorFacialV3.Normalizar(new double[1024]));

        double[] invalidos = Crear(0);
        invalidos[1] = double.NaN;

        Assert.Null(DescriptorFacialV3.Normalizar(invalidos));
        Assert.Null(DescriptorFacialV3.DistanciaCoseno(
            invalidos, Crear(0)));
    }

    [Fact]
    public void ModeloV3_NoEsModeloV2()
    {
        Assert.NotEqual(
            PatronVisualRetiroV2.Modelo,
            DescriptorFacialV3.Modelo);

        Assert.Equal(1024, DescriptorFacialV3.Dimension);
    }
}