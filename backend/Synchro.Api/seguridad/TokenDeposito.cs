
using System;
using System.Security.Cryptography;

namespace Synchro.Api.Seguridad;

public static class TokenDeposito
{
    public static (string Token, byte[] Hash) Generar()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);

        string token = Convert.ToHexString(bytes);

        byte[] hash = SHA256.HashData(bytes);

        return (token, hash);
    }
}
