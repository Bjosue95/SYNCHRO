
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

public sealed class ComandoInicioSesionWhatsAppTests
{
    [Theory]
    [InlineData("SYNCHRO INICIAR 123", 123)]
    [InlineData("synchro iniciar 456", 456)]
    [InlineData("SYNCHRO INICIAR 1", 1)]
    [InlineData("  SYNCHRO INICIAR 789  ", 789)]
    public void ReconoceComandosValidos(
        string mensaje,
        int reservaEsperada)
    {
        bool valido =
            ComandoInicioSesionWhatsApp.IntentarLeer(
                mensaje,
                out int reservaId);

        Assert.True(valido);
        Assert.Equal(reservaEsperada, reservaId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("HOLA")]
    [InlineData("INICIAR 123")]
    [InlineData("SYNCHRO INICIAR")]
    [InlineData("SYNCHRO INICIAR 0")]
    [InlineData("SYNCHRO INICIAR -1")]
    [InlineData("SYNCHRO INICIAR 00123")]
    [InlineData("SYNCHRO INICIAR ABC")]
    [InlineData("SYNCHRO INICIAR 123 ABC")]
    [InlineData("SYNCHRO INICIAR 99999999999")]
    [InlineData("SYNCHRO  INICIAR 123")]
    [InlineData("SYNCHRO INICIAR 123\nOTRO MENSAJE")]
    public void RechazaComandosInvalidos(
        string mensaje)
    {
        bool valido =
            ComandoInicioSesionWhatsApp.IntentarLeer(
                mensaje,
                out int reservaId);

        Assert.False(valido);
        Assert.Equal(0, reservaId);
    }

    [Fact]
    public void RechazaMensajeNulo()
    {
        bool valido =
            ComandoInicioSesionWhatsApp.IntentarLeer(
                null,
                out int reservaId);

        Assert.False(valido);
        Assert.Equal(0, reservaId);
    }

    [Fact]
    public void RechazaMensajesDemasiadoLargos()
    {
        string mensaje =
            "SYNCHRO INICIAR 123" +
            new string('A', 200);

        bool valido =
            ComandoInicioSesionWhatsApp.IntentarLeer(
                mensaje,
                out int reservaId);

        Assert.False(valido);
        Assert.Equal(0, reservaId);
    }

    [Fact]
    public void RechazaReservaFueraDeRangoInt()
    {
        bool valido =
            ComandoInicioSesionWhatsApp.IntentarLeer(
                "SYNCHRO INICIAR 9999999999",
                out int reservaId);

        Assert.False(valido);
        Assert.Equal(0, reservaId);
    }
}
