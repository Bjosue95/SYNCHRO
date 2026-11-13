
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

public sealed class ProtectorVerificacionTelefonoTests
{
    private static ProtectorVerificacionTelefono CrearProtector()
    {
        var proveedor = new EphemeralDataProtectionProvider();

        return new ProtectorVerificacionTelefono(proveedor);
    }

    private static MensajeWhatsApp CrearMensaje()
    {
        return new MensajeWhatsApp(
            "+50499990001",
            "codigo_inicio_sesion",
            ["123456"]
        );
    }

    [Fact]
    public void CifraYDescifraMensajeCorrectamente()
    {
        var protector = CrearProtector();

        Guid mensajeId = Guid.NewGuid();
        int verificacionId = 100;

        var mensaje = CrearMensaje();

        byte[] cifrado = protector.Proteger(
            mensajeId,
            verificacionId,
            mensaje
        );

        try
        {
            Assert.NotEmpty(cifrado);

            var recuperado = protector.Abrir(
                mensajeId,
                verificacionId,
                cifrado
            );

            Assert.Equal(
                mensaje.Telefono,
                recuperado.Telefono
            );

            Assert.Equal(
                mensaje.Plantilla,
                recuperado.Plantilla
            );

            Assert.Equal(
                mensaje.Variables,
                recuperado.Variables
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cifrado);
        }
    }

    [Fact]
    public void RechazaIdentificadorDeMensajeDiferente()
    {
        var protector = CrearProtector();

        Guid mensajeId = Guid.NewGuid();
        int verificacionId = 101;

        byte[] cifrado = protector.Proteger(
            mensajeId,
            verificacionId,
            CrearMensaje()
        );

        try
        {
            Assert.Throws<CryptographicException>(() =>
                protector.Abrir(
                    Guid.NewGuid(),
                    verificacionId,
                    cifrado
                )
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cifrado);
        }
    }

    [Fact]
    public void RechazaIdentificadorDeVerificacionDiferente()
    {
        var protector = CrearProtector();

        Guid mensajeId = Guid.NewGuid();
        int verificacionId = 102;

        byte[] cifrado = protector.Proteger(
            mensajeId,
            verificacionId,
            CrearMensaje()
        );

        try
        {
            Assert.Throws<CryptographicException>(() =>
                protector.Abrir(
                    mensajeId,
                    verificacionId + 1,
                    cifrado
                )
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cifrado);
        }
    }

    [Fact]
    public void RechazaMensajeCifradoManipulado()
    {
        var protector = CrearProtector();

        Guid mensajeId = Guid.NewGuid();
        int verificacionId = 103;

        byte[] cifrado = protector.Proteger(
            mensajeId,
            verificacionId,
            CrearMensaje()
        );

        try
        {
            cifrado[^1] ^= 0xFF;

            Assert.Throws<CryptographicException>(() =>
                protector.Abrir(
                    mensajeId,
                    verificacionId,
                    cifrado
                )
            );
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cifrado);
        }
    }

    [Fact]
    public void RechazaTelefonosInvalidos()
    {
        var protector = CrearProtector();

        var mensaje = new MensajeWhatsApp(
            "telefono_invalido",
            "codigo_inicio_sesion",
            ["123456"]
        );

        Assert.Throws<ArgumentException>(() =>
            protector.Proteger(
                Guid.NewGuid(),
                104,
                mensaje
            )
        );
    }
}
