using System.Security.Cryptography;

namespace Synchro.Api.Services;

internal sealed class PinEncomiendaPreparado(string pin, byte[] hash, byte[] salt, DateTimeOffset venceUtc) : IDisposable
{
    public string Pin { get; } = pin;
    public byte[] Hash { get; } = hash;
    public byte[] Salt { get; } = salt;
    public DateTimeOffset VenceUtc { get; } = venceUtc;
    public void Dispose() { CryptographicOperations.ZeroMemory(Hash);CryptographicOperations.ZeroMemory(Salt); }
    public override string ToString() => "PIN de retiro protegido";
}

internal sealed class PreparadorPinEncomienda(IConfiguration configuracion,TimeProvider reloj)
{
    public PinEncomiendaPreparado CrearDistinto(int usoId,int destinatarioId,byte[] hashAnterior,byte[] saltAnterior)
    {
        for(int intento=0;intento<100;intento++)
        {
            var nuevo=Crear(usoId,destinatarioId);
            byte[] comparacion=CodigoPinRetiro.CalcularHash(nuevo.Pin,usoId,destinatarioId,saltAnterior,configuracion);
            bool distinto;
            try{distinto=!CryptographicOperations.FixedTimeEquals(comparacion,hashAnterior);}
            finally{CryptographicOperations.ZeroMemory(comparacion);}
            if(distinto)return nuevo;
            nuevo.Dispose();
        }
        throw new InvalidOperationException("No fue posible rotar el PIN.");
    }
    public PinEncomiendaPreparado Crear(int usoId,int destinatarioId)
    {
        int minutos=configuracion.GetValue("RetiroPin:VigenciaMinutos",30);
        if (minutos is < 1 or > 1440)throw new InvalidOperationException("Vigencia de PIN invalida.");
        byte[] salt=CodigoPinRetiro.GenerarSalt();
        try
        {
            string pin=CodigoPinRetiro.Generar();
            byte[] hash=CodigoPinRetiro.CalcularHash(pin,usoId,destinatarioId,salt,configuracion);
            return new(pin,hash,salt,reloj.GetUtcNow().AddMinutes(minutos));
        }
        catch { CryptographicOperations.ZeroMemory(salt);throw; }
    }
}
