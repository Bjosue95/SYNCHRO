using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

public class CodigoPinRetiroTests
{
    private static IConfiguration CrearConfiguracion()
    {
        // Clave ficticia, generada exclusivamente
        // para esta ejecucion de pruebas.
        byte[] clave = RandomNumberGenerator.GetBytes(32);

        try
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["RetiroPin:ClaveHmac"] =
                            Convert.ToBase64String(clave)
                    })
                .Build();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
        }
    }

    [Fact]
    public void PinGeneradoTieneFormatoCorrecto()
    {
        string pin = CodigoPinRetiro.Generar();
        byte[] salt = CodigoPinRetiro.GenerarSalt();

        Assert.Matches(@"^[0-9]{6}$", pin);
        Assert.Equal(32, salt.Length);
    }

    [Fact]
    public void PinSoloCoincideConSuEncomiendaYDestinatario()
    {
        var configuracion = CrearConfiguracion();
        byte[] salt = CodigoPinRetiro.GenerarSalt();

        byte[] original = CodigoPinRetiro.CalcularHash(
            "123456", 10, 20, salt, configuracion);

        byte[] correcto = CodigoPinRetiro.CalcularHash(
            "123456", 10, 20, salt, configuracion);

        byte[] otroPin = CodigoPinRetiro.CalcularHash(
            "654321", 10, 20, salt, configuracion);

        byte[] otraEncomienda = CodigoPinRetiro.CalcularHash(
            "123456", 11, 20, salt, configuracion);

        byte[] otroDestinatario = CodigoPinRetiro.CalcularHash(
            "123456", 10, 21, salt, configuracion);

        byte[] otroSalt = CodigoPinRetiro.CalcularHash(
            "123456", 10, 20,
            CodigoPinRetiro.GenerarSalt(),
            configuracion);

        Assert.True(
            CodigoPinRetiro.VerificarHash(original, correcto));

        Assert.False(
            CodigoPinRetiro.VerificarHash(original, otroPin));

        Assert.False(
            CodigoPinRetiro.VerificarHash(original, otraEncomienda));

        Assert.False(
            CodigoPinRetiro.VerificarHash(original, otroDestinatario));

        Assert.False(
            CodigoPinRetiro.VerificarHash(original, otroSalt));
    }
}
