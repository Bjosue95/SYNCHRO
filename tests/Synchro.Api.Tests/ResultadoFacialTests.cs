using Xunit;

namespace Synchro.Api.Tests;

public sealed class ResultadoFacialTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    public void SoloApruebaCuandoAmbasCondicionesSeCumplen(
        bool rostroCoincide,
        bool pruebaDeVidaSuperada,
        bool resultadoEsperado)
    {
        var resultado = new ResultadoFacial(
            rostroCoincide,
            pruebaDeVidaSuperada);

        Assert.Equal(
            resultadoEsperado,
            resultado.Aprobado);
    }
}
