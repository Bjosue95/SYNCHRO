using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services;

internal sealed record PreReservaEncomiendaCreada(int Id,string TokenGestion,DateTimeOffset FechaExpiracion)
{
    public override string ToString()=>"Pre-reserva protegida";
}
internal interface IPreReservasEncomienda
{
    Task<PreReservaEncomiendaCreada> CrearAsync(string? remitente,string? destinatario,string? tamano,CancellationToken token,int? terminalId=null);
}
internal sealed class PreReservaEncomiendaService(IConfiguration config) : IPreReservasEncomienda
{
    internal static (string Remitente,string Destinatario,string Tamano) Validar(string? remitente,string? destinatario,string? tamano)
    {
        static string Telefono(string? valor)
        {
            string? s=valor?.Trim();
            if(s is null||s.Length>20||!Regex.IsMatch(s,@"\A\+?[1-9][0-9]{7,14}\z"))
                throw new ArgumentException("Telefonos o tamano invalidos.");
            return s.StartsWith('+')?s:"+"+s;
        }
        string? t=tamano?.Trim();
        if(t is not("Pequeno" or "Mediano" or "Grande"))throw new ArgumentException("Telefonos o tamano invalidos.");
        return(Telefono(remitente),Telefono(destinatario),t);
    }
    public async Task<PreReservaEncomiendaCreada> CrearAsync(string? remitente,string? destinatario,string? tamano,CancellationToken token,int? terminalId=null)
    {
        var datos=Validar(remitente,destinatario,tamano);token.ThrowIfCancellationRequested();
        if(terminalId is <=0)throw new ArgumentException("Terminal invalida.");
        string cadena=ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");
        byte[] bytes=RandomNumberGenerator.GetBytes(32);string credencial=Convert.ToHexString(bytes);
        byte[] hash=SHA256.HashData(bytes);CryptographicOperations.ZeroMemory(bytes);
        try
        {
            await using var c=new SqlConnection(cadena);await c.OpenAsync(token);
            await using var q=new SqlCommand("""
                INSERT dbo.PreReservasEncomiendasWeb(TokenGestionHash,TelefonoRemitente,TelefonoDestinatario,TamanoSolicitado,FechaExpiracion,OrigenPreReserva,TerminalCreacionId)
                OUTPUT INSERTED.Id,INSERTED.FechaExpiracion
                SELECT @hash,@remitente,@destinatario,@tamano,DATEADD(HOUR,24,SYSUTCDATETIME()),
                    CASE WHEN @terminal IS NULL THEN N'Web' ELSE N'Terminal' END,@terminal
                WHERE @terminal IS NULL OR EXISTS(SELECT 1 FROM dbo.Terminales WHERE Id=@terminal AND Activa=1);
                """,c);
            q.Parameters.Add("@terminal",SqlDbType.Int).Value=(object?)terminalId??DBNull.Value;
            q.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=hash;
            q.Parameters.Add("@remitente",SqlDbType.NVarChar,20).Value=datos.Remitente;
            q.Parameters.Add("@destinatario",SqlDbType.NVarChar,20).Value=datos.Destinatario;
            q.Parameters.Add("@tamano",SqlDbType.NVarChar,12).Value=datos.Tamano;
            await using var r=await q.ExecuteReaderAsync(token);
            if(!await r.ReadAsync(token))throw new InvalidOperationException("Pre-reserva no confirmada.");
            return new(r.GetInt32(0),credencial,new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(1),DateTimeKind.Utc)));
        }
        finally{CryptographicOperations.ZeroMemory(hash);}
    }
}
