using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Synchro.Api.Services.Temporales;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaseServicioTemporal { CargaCelular,CargaLaptop,Frio,Calor,Personal }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoServicioTemporal { Reservada,Pagada,Activa,Agotada,Finalizada,Cancelada }

public sealed record ConfiguracionServicioTemporal(string Codigo,ClaseServicioTemporal Clase,string? Conector,
    bool Habilitado,Dictionary<int,decimal> Tarifas,decimal? TemperaturaMinima=null,
    decimal? TemperaturaMaxima=null,decimal? TemperaturaSimulada=null)
{
    public string Tamano {get;init;}="Mediano";
    public void Validar()
    {
        if(string.IsNullOrWhiteSpace(Codigo)||Codigo.Length>10||Codigo.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-')||
            !Enum.IsDefined(Clase)||Tamano is not("Pequeno" or "Mediano" or "Grande")||Tarifas is null||Tarifas.Count is <1 or >20||
            Tarifas.Any(p=>p.Key is <1 or >1440||p.Value<0||p.Value>1_000_000||decimal.Round(p.Value,2)!=p.Value))
            throw new ArgumentException("Configuracion de compartimento invalida.");
        if(Clase is ClaseServicioTemporal.CargaCelular or ClaseServicioTemporal.CargaLaptop)
        {
            if(Tarifas.Count!=3||!new[]{30,60,90}.All(Tarifas.ContainsKey))throw new ArgumentException("La carga requiere tarifas de 30, 60 y 90 minutos.");
            if(Clase==ClaseServicioTemporal.CargaCelular&&Conector is not("USB-C" or "Lightning" or "Inalambrico")||
               Clase==ClaseServicioTemporal.CargaLaptop&&Conector is not("USB-C PD" or "CargadorPropio"))
                throw new ArgumentException("Conector incompatible.");
        }
        else if(Conector is not null)throw new ArgumentException("El servicio no admite conector de carga.");
        if(Clase is ClaseServicioTemporal.Frio or ClaseServicioTemporal.Calor)
        {
            if(TemperaturaMinima is null||TemperaturaMaxima is null||TemperaturaSimulada is null||
                TemperaturaMinima>=TemperaturaMaxima||TemperaturaMinima< -273.15m||TemperaturaMaxima>200)
                throw new ArgumentException("Rango de temperatura invalido.");
        }
        else if(TemperaturaMinima is not null||TemperaturaMaxima is not null||TemperaturaSimulada is not null)
            throw new ArgumentException("Temperatura no aplicable al servicio.");
    }
    public bool TemperaturaApta=>Clase is not(ClaseServicioTemporal.Frio or ClaseServicioTemporal.Calor)||
        TemperaturaSimulada>=TemperaturaMinima&&TemperaturaSimulada<=TemperaturaMaxima;
}

internal sealed record OperacionServicioTemporal
{
    public Guid Id {get;init;}
    public string Codigo {get;init;}="";
    public ClaseServicioTemporal Clase {get;init;}
    public int? TerminalId {get;set;}
    public int? ClientePersonaId {get;init;}
    public string? OrigenReserva {get;init;}
    public int Minutos {get;init;}
    public decimal Monto {get;init;}
    public string TokenHash {get;init;}="";
    public EstadoServicioTemporal Estado {get;set;}=EstadoServicioTemporal.Reservada;
    public DateTimeOffset CreadaUtc {get;init;}
    public DateTimeOffset VenceReservaUtc {get;init;}
    public Guid? PagoId {get;set;}
    public DateTimeOffset? PagoUtc {get;set;}
    public string? CanalPago {get;set;}
    public bool PagoAnulado {get;set;}
    public string? PinHash {get;set;}
    public string? PinSalt {get;set;}
    public int IntentosPin {get;set;}
    public DateTimeOffset? InicioUtc {get;set;}
    public DateTimeOffset? FinContratadoUtc {get;set;}
    public DateTimeOffset? RetiroUtc {get;set;}
    public bool Ocupa=>Estado is EstadoServicioTemporal.Reservada or EstadoServicioTemporal.Pagada or EstadoServicioTemporal.Activa or EstadoServicioTemporal.Agotada;
    public override string ToString()=>"Operacion temporal protegida";
}
internal sealed record EventoServicioTemporal(Guid? OperacionId,string Accion,string Actor,DateTimeOffset FechaUtc);
internal sealed class DatosServicioTemporal
{
    public ConfiguracionServicioTemporal Configuracion {get;set;}=null!;
    public string EstadoInventario {get;set;}="Disponible";
    public Dictionary<Guid,OperacionServicioTemporal> Operaciones {get;}=[];
    public List<EventoServicioTemporal> Eventos {get;}=[];
    public List<string> Incidencias {get;}=[];
}
public sealed record ResumenServicioTemporal(Guid Id,string Codigo,ClaseServicioTemporal Clase,
    EstadoServicioTemporal Estado,int Minutos,decimal Monto,string Moneda,DateTimeOffset VenceReservaUtc,
    DateTimeOffset? InicioUtc,DateTimeOffset? FinContratadoUtc,bool PagoAnulado,bool HardwareSimulado=true);
public sealed record ReservaServicioTemporal(ResumenServicioTemporal Operacion,string Credencial)
{
    public override string ToString()=>"Reserva temporal protegida";
}
public sealed record PagoServicioTemporal(ResumenServicioTemporal Operacion,string? PinRetiro,bool YaConfirmado)
{
    public override string ToString()=>"Pago simulado protegido";
}
public sealed record RetiroServicioTemporal(bool Autorizado,bool PinBloqueado,ResumenServicioTemporal Operacion);
public sealed record DisponibilidadServicioTemporal(ConfiguracionServicioTemporal Configuracion,string Estado,bool HardwareSimulado=true);
public sealed record ReporteServiciosTemporales(int Reservas,int EnUso,int Finalizadas,decimal IngresosSimuladosHnl,int Incidencias,long MinutosOcupadosSimulados);

internal interface IRepositorioServicioTemporal
{
    Task<T> TransaccionAsync<T>(string codigo,Guid? operacionId,int? terminalId,
        Func<DatosServicioTemporal,T> cambio,CancellationToken token,int? clientePersonaId=null);
    Task ConfigurarAsync(ConfiguracionServicioTemporal config,int administradorId,CancellationToken token);
    Task<IReadOnlyList<string>> CodigosAsync(CancellationToken token);
    Task<ReporteServiciosTemporales> ReportarAsync(DateTimeOffset desde,DateTimeOffset hasta,CancellationToken token);
}
internal sealed class ServicioTemporalNoDisponibleException(bool conflicto=false) : Exception
{
    public bool Conflicto {get;}=conflicto;
}

internal static class CredencialesServicioTemporal
{
    public static string HashToken(string credencial)
    {
        if(credencial is not{Length:64}||!credencial.All(char.IsAsciiHexDigit))throw new ServicioTemporalNoDisponibleException();
        byte[] bytes=Convert.FromHexString(credencial);
        try{return Convert.ToBase64String(SHA256.HashData(bytes));}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    public static string HashPin(string pin,Guid id,string salt,IConfiguration config)
    {
        if(pin is not{Length:6}||!pin.All(char.IsAsciiDigit))throw new ArgumentException("PIN invalido.");
        byte[] clave=Convert.FromBase64String(config["ServiciosTemporales:ClaveHmac"]??throw new InvalidOperationException("Clave HMAC no configurada."));
        try
        {
            if(clave.Length!=32)throw new InvalidOperationException("Clave HMAC invalida.");
            byte[] mensaje=Encoding.UTF8.GetBytes($"SYNCHRO|servicio-temporal|v1|{id:N}|{salt}|{pin}");
            try{return Convert.ToBase64String(HMACSHA256.HashData(clave,mensaje));}
            finally{CryptographicOperations.ZeroMemory(mensaje);}
        }
        finally{CryptographicOperations.ZeroMemory(clave);}
    }
    public static bool Iguales(string a,string b)
    {
        byte[] x=Convert.FromBase64String(a),y=Convert.FromBase64String(b);
        try{return x.Length==32&&y.Length==32&&CryptographicOperations.FixedTimeEquals(x,y);}
        finally{CryptographicOperations.ZeroMemory(x);CryptographicOperations.ZeroMemory(y);}
    }
}

internal sealed class ClienteWebRequeridoException : Exception;
internal sealed class ReservaSoloWebException : Exception;
