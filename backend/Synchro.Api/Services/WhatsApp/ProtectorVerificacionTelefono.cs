
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Synchro.Api.Services.WhatsApp;

internal sealed class ProtectorVerificacionTelefono(
    IDataProtectionProvider proveedor)
{
    private IDataProtector CrearProtector(
        Guid mensajeId,
        int verificacionId)
    {
        if (mensajeId == Guid.Empty || verificacionId <= 0)
        {
            throw new ArgumentException(
                "Identificadores de verificacion invalidos.");
        }

        return proveedor.CreateProtector(
            "SYNCHRO.WhatsApp.VerificacionTelefono.v1",
            verificacionId.ToString(
                CultureInfo.InvariantCulture),
            mensajeId.ToString("N"));
    }

    public byte[] Proteger(
        Guid mensajeId,
        int verificacionId,
        MensajeWhatsApp mensaje)
    {
        if (mensaje is null ||
            !MensajeWhatsApp.EsValido(
                mensaje.Telefono,
                mensaje.Plantilla,
                mensaje.Variables))
        {
            throw new ArgumentException(
                "Mensaje de verificacion invalido.");
        }

        IDataProtector protector = CrearProtector(
            mensajeId,
            verificacionId);

        byte[] contenido = JsonSerializer
            .SerializeToUtf8Bytes(mensaje);

        try
        {
            return protector.Protect(contenido);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contenido);
        }
    }

    public MensajeWhatsApp Abrir(
        Guid mensajeId,
        int verificacionId,
        byte[] contenidoProtegido)
    {
        IDataProtector protector = CrearProtector(
            mensajeId,
            verificacionId);

        if (contenidoProtegido is null ||
            contenidoProtegido.Length == 0 ||
            contenidoProtegido.Length > 16384)
        {
            throw new CryptographicException(
                "Contenido protegido invalido.");
        }

        byte[] contenido = protector.Unprotect(
            contenidoProtegido);

        try
        {
            MensajeWhatsApp? mensaje = JsonSerializer
                .Deserialize<MensajeWhatsApp>(contenido);

            if (mensaje is null ||
                !MensajeWhatsApp.EsValido(
                    mensaje.Telefono,
                    mensaje.Plantilla,
                    mensaje.Variables))
            {
                throw new CryptographicException(
                    "Mensaje descifrado invalido.");
            }

            return mensaje;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(contenido);
        }
    }
}
