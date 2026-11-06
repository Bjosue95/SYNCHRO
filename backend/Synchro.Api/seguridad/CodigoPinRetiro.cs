using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

internal static class CodigoPinRetiro
{
    private const int LongitudSalt = 32;

    // Generar un PIN aleatorio de seis digitos.
    public static string Generar()
    {
        return CodigoVerificacionTelefono.Generar();
    }

    // Crear un salt diferente para cada PIN.
    public static byte[] GenerarSalt()
    {
        return RandomNumberGenerator.GetBytes(LongitudSalt);
    }

    // Calcular el hash protegido del PIN.
    public static byte[] CalcularHash(
        string pin,
        int usoId,
        int destinatarioId,
        byte[] salt,
        IConfiguration configuracion)
    {
        if (pin is null ||
            pin.Length != 6 ||
            pin.Any(c => c < '0' || c > '9'))
        {
            throw new ArgumentException(
                "El PIN debe contener seis digitos.");
        }

        if (usoId <= 0 || destinatarioId <= 0)
        {
            throw new ArgumentException(
                "Encomienda o destinatario invalido.");
        }

        if (salt is null || salt.Length != LongitudSalt)
        {
            throw new ArgumentException(
                "El salt debe tener 32 bytes.");
        }

        string? claveBase64 =
            configuracion["RetiroPin:ClaveHmac"];

        if (string.IsNullOrWhiteSpace(claveBase64))
        {
            throw new InvalidOperationException(
                "La clave HMAC del retiro no esta configurada.");
        }

        byte[] clave = Convert.FromBase64String(claveBase64);

        try
        {
            if (clave.Length != 32)
            {
                throw new InvalidOperationException(
                    "La clave HMAC debe tener 256 bits.");
            }

            string contenido =
                $"SYNCHRO|pin-retiro|v1|{usoId}|" +
                $"{destinatarioId}|{Convert.ToHexString(salt)}|{pin}";

            byte[] mensaje = Encoding.UTF8.GetBytes(contenido);

            try
            {
                return HMACSHA256.HashData(clave, mensaje);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(mensaje);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
        }
    }

    // Comparar hashes sin depender de una
    // comparacion convencional de bytes.
    public static bool VerificarHash(
        byte[] hashCalculado,
        byte[] hashGuardado)
    {
        if (hashCalculado is null ||
            hashGuardado is null ||
            hashCalculado.Length != 32 ||
            hashGuardado.Length != 32)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            hashCalculado,
            hashGuardado);
    }
}
