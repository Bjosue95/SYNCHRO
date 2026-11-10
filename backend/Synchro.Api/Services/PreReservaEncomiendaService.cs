using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services;

internal sealed record PreReservaEncomiendaCreada(int Id,string TokenGestion,DateTimeOffset FechaExpiracion)
{public override string ToString()=>"Reserva web protegida";}
internal interface IPreReservasEncomienda
{
    Task<PreReservaEncomiendaCreada> CrearAsync(int clientePersonaId,string? destinatario,string? tamano,CancellationToken token);
}
internal sealed class PreReservaEncomiendaService(IConfiguration config) : IPreReservasEncomienda
{
    internal static (string Destinatario,string Tamano) Validar(int clientePersonaId,string? destinatario,string? tamano)
    {
        string? telefono=destinatario?.Trim();string? t=tamano?.Trim();
        if(clientePersonaId<=0||telefono is null||telefono.Length>20||!Regex.IsMatch(telefono,@"\A\+?[1-9][0-9]{7,14}\z")||
            t is not("Pequeno" or "Mediano" or "Grande"))throw new ArgumentException("Datos de reserva invalidos.");
        return(telefono.StartsWith('+')?telefono:"+"+telefono,t);
    }
    public async Task<PreReservaEncomiendaCreada> CrearAsync(int clientePersonaId,string? destinatario,string? tamano,CancellationToken token)
    {
        var datos=Validar(clientePersonaId,destinatario,tamano);token.ThrowIfCancellationRequested();
        string cadena=ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");
        byte[] bytes=RandomNumberGenerator.GetBytes(32);string credencial=Convert.ToHexString(bytes);
        byte[] hash=SHA256.HashData(bytes);CryptographicOperations.ZeroMemory(bytes);
        try
        {
            await using var c=new SqlConnection(cadena);await c.OpenAsync(token);
            await using var q=new SqlCommand("""
                INSERT dbo.PreReservasEncomiendasWeb(TokenGestionHash,TelefonoRemitente,TelefonoDestinatario,TamanoSolicitado,
                    FechaExpiracion,OrigenPreReserva,TerminalCreacionId,ClientePersonaId)
                OUTPUT INSERTED.Id,INSERTED.FechaExpiracion
                SELECT @hash,c.Telefono,@destinatario,@tamano,DATEADD(HOUR,24,SYSUTCDATETIME()),N'Web',NULL,c.PersonaId
                FROM dbo.ClientesWebPrototipo c WITH(HOLDLOCK) WHERE c.PersonaId=@cliente AND c.Activa=1;
                """,c);
            q.Parameters.Add("@cliente",SqlDbType.Int).Value=clientePersonaId;q.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=hash;
            q.Parameters.Add("@destinatario",SqlDbType.NVarChar,20).Value=datos.Destinatario;q.Parameters.Add("@tamano",SqlDbType.NVarChar,12).Value=datos.Tamano;
            await using var r=await q.ExecuteReaderAsync(token);
            if(!await r.ReadAsync(token))throw new EncomiendaNoDisponibleException();
            return new(r.GetInt32(0),credencial,new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(1),DateTimeKind.Utc)));
        }
        finally{CryptographicOperations.ZeroMemory(hash);}
    }
}
