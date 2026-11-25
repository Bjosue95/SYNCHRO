using Microsoft.Extensions.Configuration;
using Synchro.Api.Services.Encomiendas;

namespace Synchro.Api.Tests;

public sealed class PinEncomiendaOperativaTests
{
    [Fact]
    public void PinPersonalNoEsIntercambiableConDepositoNiRetiroDeEncomienda()
    {
        var configuracion=new ConfigurationBuilder().AddConfiguration(Configuracion).AddInMemoryCollection(new Dictionary<string,string?>{
            ["RetiroPersonal:ClaveHmac"]=Configuracion["RetiroPin:ClaveHmac"]}).Build();
        var personal=CodigoPinRetiroPersonal.CalcularHash("123456",17,Salt,configuracion);
        foreach(var finalidad in new[]{FinalidadPinEncomienda.Deposito,FinalidadPinEncomienda.Retiro})
            Assert.False(CodigoPinRetiro.VerificarHash(personal,CodigoPinRetiro.CalcularHashEncomienda("123456",17,101,Salt,configuracion,finalidad)));
    }
    // Vector público de prueba. No es una clave de configuración ni se usa fuera de estos tests.
    private static IConfiguration Configuracion => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> {
            ["RetiroPin:ClaveHmac"] = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray())
        }).Build();
    private static byte[] Salt => Enumerable.Range(32, 32).Select(i => (byte)i).ToArray();

    [Fact]
    public void FormatoDelHashHistoricoNoCambia()
    {
        var hash = CodigoPinRetiro.CalcularHash("123456", 17, 101, Salt, Configuracion);
        Assert.Equal("B082F0952B68890154F80030591E7EC9E616AEA6AC4DD8E5F971A5B32977CD67", Convert.ToHexString(hash));
    }

    [Fact]
    public void FinalidadesDeDepositoRetiroEHistoricaNoSonIntercambiables()
    {
        var deposito = CodigoPinRetiro.CalcularHashEncomienda("123456", 17, 101, Salt, Configuracion, FinalidadPinEncomienda.Deposito);
        var retiro = CodigoPinRetiro.CalcularHashEncomienda("123456", 17, 101, Salt, Configuracion, FinalidadPinEncomienda.Retiro);
        var historico = CodigoPinRetiro.CalcularHash("123456", 17, 101, Salt, Configuracion);
        Assert.False(CodigoPinRetiro.VerificarHash(deposito, retiro));
        Assert.False(CodigoPinRetiro.VerificarHash(deposito, historico));
        Assert.False(CodigoPinRetiro.VerificarHash(retiro, historico));
        Assert.True(CodigoPinRetiro.VerificarHash(deposito,
            CodigoPinRetiro.CalcularHashEncomienda("123456", 17, 101, Salt, Configuracion, FinalidadPinEncomienda.Deposito)));
    }

    [Fact]
    public void HashEstaLigadoAEncomiendaYParticipante()
    {
        var hash = CodigoPinRetiro.CalcularHashEncomienda("123456", 17, 101, Salt, Configuracion, FinalidadPinEncomienda.Deposito);
        Assert.False(CodigoPinRetiro.VerificarHash(hash,
            CodigoPinRetiro.CalcularHashEncomienda("123456", 18, 101, Salt, Configuracion, FinalidadPinEncomienda.Deposito)));
        Assert.False(CodigoPinRetiro.VerificarHash(hash,
            CodigoPinRetiro.CalcularHashEncomienda("123456", 17, 202, Salt, Configuracion, FinalidadPinEncomienda.Deposito)));
        Assert.False(CodigoPinRetiro.VerificarHash(hash,
            CodigoPinRetiro.CalcularHashEncomienda("654321", 17, 101, Salt, Configuracion, FinalidadPinEncomienda.Deposito)));
    }

    [Fact]
    public void FinalidadNoReconocidaNoSeConvierteEnUnPinValido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CodigoPinRetiro.CalcularHashEncomienda(
            "123456", 17, 101, Salt, Configuracion, (FinalidadPinEncomienda)99));
    }
}
