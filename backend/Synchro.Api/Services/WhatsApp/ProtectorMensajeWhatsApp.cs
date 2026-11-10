using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Synchro.Api.Services.WhatsApp;

internal sealed class ProtectorMensajeWhatsApp(IDataProtectionProvider proveedor)
{
    private IDataProtector Protector(Guid operacionId, int reservaId)
    {
        if (operacionId == Guid.Empty || reservaId <= 0) throw new ArgumentException("Contexto de mensaje invalido.");
        return proveedor.CreateProtector("SYNCHRO.WhatsApp.Outbox.v1", operacionId.ToString("N"),
            reservaId.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public byte[] Proteger(Guid operacionId, int reservaId, MensajeWhatsApp mensaje)
    {
        if (!MensajeWhatsApp.EsValido(mensaje.Telefono, mensaje.Plantilla, mensaje.Variables))
            throw new ArgumentException("Mensaje WhatsApp invalido.");
        byte[] texto = JsonSerializer.SerializeToUtf8Bytes(mensaje);
        try { return Protector(operacionId,reservaId).Protect(texto); }
        finally { CryptographicOperations.ZeroMemory(texto); }
    }

    public MensajeWhatsApp Abrir(Guid operacionId, int reservaId, byte[] cifrado)
    {
        byte[] texto = Protector(operacionId,reservaId).Unprotect(cifrado);
        try
        {
            var mensaje = JsonSerializer.Deserialize<MensajeWhatsApp>(texto);
            if (mensaje is null || !MensajeWhatsApp.EsValido(mensaje.Telefono,mensaje.Plantilla,mensaje.Variables))
                throw new CryptographicException("Contenido del mensaje no valido.");
            return mensaje;
        }
        finally { CryptographicOperations.ZeroMemory(texto); }
    }
}
