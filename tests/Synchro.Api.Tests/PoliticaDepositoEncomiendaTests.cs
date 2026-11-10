
using Xunit;

namespace Synchro.Api.Tests;

public class PoliticaDepositoEncomiendaTests
{
    [Fact]
    public void SinVerificacionFacial_DebeRechazar()
    {
        var condiciones = new CondicionesDepositoEncomienda(
            true, true, true, true, true, null);

        Assert.False(
            PoliticaDepositoEncomienda.PuedeAutorizar(condiciones));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void VerificacionFacialIncompleta_DebeRechazar(
        bool rostroCoincide,
        bool pruebaVida)
    {
        var facial = new ResultadoFacial(
            rostroCoincide, pruebaVida);

        var condiciones = new CondicionesDepositoEncomienda(
            true, true, true, true, true, facial);

        Assert.False(
            PoliticaDepositoEncomienda.PuedeAutorizar(condiciones));
    }

    [Theory]
    [InlineData(false, true, true, true, true)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, true, true, false)]
    public void FaltaUnaCondicion_DebeRechazar(
        bool reserva,
        bool consentimiento,
        bool terminal,
        bool remitente,
        bool casillero)
    {
        var condiciones = new CondicionesDepositoEncomienda(
            reserva,
            consentimiento,
            terminal,
            remitente,
            casillero,
            new ResultadoFacial(true, true));

        Assert.False(
            PoliticaDepositoEncomienda.PuedeAutorizar(condiciones));
    }

    [Fact]
    public void TodasLasCondicionesValidas_ApruebaLaRegla()
    {
        var condiciones = new CondicionesDepositoEncomienda(
            true, true, true, true, true,
            new ResultadoFacial(true, true));

        Assert.True(
            PoliticaDepositoEncomienda.PuedeAutorizar(condiciones));
    }
}
