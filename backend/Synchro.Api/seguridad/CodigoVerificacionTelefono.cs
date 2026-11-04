
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

internal static class CodigoVerificacionTelefono
{
    public static string Generar()
    {
        int numero = RandomNumberGenerator.GetInt32(1_000_000);

        return numero.ToString("D6", CultureInfo.InvariantCulture);
    }

    public static byte[] CalcularHash(
        string codigo,
        int personaId,
        string telefono,
        IConfiguration configuracion)
    {
        if (string.IsNullOrEmpty(codigo) ||
            codigo.Length != 6 ||
            codigo.Any(c => c < '0' || c > '9'))
        {
            throw new ArgumentException(
                "El codigo debe contener seis digitos.");
        }

        if (personaId <= 0 || string.IsNullOrWhiteSpace(telefono))
        {
            throw new ArgumentException(
                "Persona o telefono invalido.");
        }

        string? claveBase64 =
            configuracion["VerificacionTelefono:ClaveHmac"];

        if (string.IsNullOrWhiteSpace(claveBase64))
        {
            throw new InvalidOperationException(
                "La clave HMAC no esta configurada.");
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
                $"SYNCHRO|telefono|{personaId}|{telefono}|{codigo}";

            byte[] mensaje = Encoding.UTF8.GetBytes(contenido);

            return HMACSHA256.HashData(clave, mensaje);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
        }
    }
}
