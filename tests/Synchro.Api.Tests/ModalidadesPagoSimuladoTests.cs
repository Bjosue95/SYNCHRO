using Synchro.Api.Services.Pagos;

namespace Synchro.Api.Tests;

public sealed class ModalidadesPagoSimuladoTests
{
    [Theory]
    [InlineData(1, null, "NfcSimulado")]
    [InlineData(1, "NFC", "NfcSimulado")]
    [InlineData(1, "Tarjeta", "TarjetaTerminalSimulada")]
    [InlineData(1, "Efectivo", "EfectivoSimulado")]
    [InlineData(null, "TarjetaWeb", "WebSimulado")]
    [InlineData(null, null, "WebSimulado")]
    public void ConservaPagosExistentes(
        int? terminal, string? metodo, string esperado)
    {
        Assert.Equal(
            esperado,
            ModalidadesPagoSimulado.ParaServicioTemporal(
                terminal, metodo));
    }

    [Theory]
    [InlineData("Efectivo", "EfectivoSimulado")]
    [InlineData("Tarjeta", "TarjetaSimulada")]
    [InlineData("NFC", "NfcSimulado")]
    public void RetiroPersonalUtilizaPoliticaCompartida(
        string metodo, string esperado)
    {
        Assert.Equal(
            esperado,
            ModalidadesPagoSimulado.ParaRetiroPersonal(metodo));
    }

    [Theory]
    [InlineData("EfectivoSimulado")]
    [InlineData("")]
    [InlineData(null)]
    public void RetiroRechazaMetodosNoAutorizados(
        string? metodo)
    {
        Assert.Throws<ArgumentException>(() =>
            ModalidadesPagoSimulado.ParaRetiroPersonal(metodo));
    }
}