using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services;

internal sealed class EncomiendaNoDisponibleException : Exception;

internal sealed record ContextoReservaEncomienda(int ReservaId,int RemitenteId);
internal sealed record EvidenciaRetiroEncomienda(Guid Id,int UsoId,int DestinatarioId,int TerminalId,
    DateTimeOffset EmitidaUtc,DateTimeOffset VenceUtc);
internal sealed record RetirarEncomiendaComando(int UsoId,int DestinatarioId,int TerminalId,Guid EvidenciaId,string Pin)
{
    public override string ToString()=>"Solicitud de retiro protegida";
}
internal enum ResultadoRetiroEncomienda { Retirado,NoDisponible,PinIncorrecto,PinBloqueado,EvidenciaNoValida }

internal interface IFlujoEncomiendasTerminal
{
    Task<ContextoReservaEncomienda?> ObtenerReservaAsync(int reservaId,int terminalId,byte[] tokenHash,CancellationToken token);
    Task GuardarEvidenciaRemitenteAsync(EvidenciaFacialRemitente evidencia,byte[] tokenHash,CancellationToken token);
    Task<DepositoEncomiendaResultado> DepositarAsync(DepositoEncomiendaComando comando,CancellationToken token);
    Task<bool> PuedeRetirarAsync(int usoId,int destinatarioId,int terminalId,CancellationToken token);
    Task GuardarEvidenciaRetiroAsync(EvidenciaRetiroEncomienda evidencia,CancellationToken token);
    Task<ResultadoRetiroEncomienda> RetirarAsync(RetirarEncomiendaComando comando,CancellationToken token);
}

internal sealed class FlujoEncomiendasTerminalSql(IConfiguration config,DepositoEncomiendaService deposito,TimeProvider reloj) : IFlujoEncomiendasTerminal
{
    private string Cadena=>ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");

    public async Task<ContextoReservaEncomienda?> ObtenerReservaAsync(int reservaId,int terminalId,byte[] tokenHash,CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var q=new SqlCommand("""
            SELECT p.RemitenteId FROM dbo.PreReservasEncomiendasWeb r
            JOIN dbo.ParticipantesPreReservaVerificados p ON p.ReservaId=r.Id
            JOIN dbo.ConsentimientosPreReservaWhatsApp w ON w.ReservaId=r.Id
            WHERE r.Id=@reserva AND r.TokenGestionHash=@hash AND r.Estado=N'PendienteConsentimiento'
              AND r.FechaExpiracion>SYSUTCDATETIME()
              AND p.FechaVerificacionUtc<=SYSUTCDATETIME() AND p.FechaExpiracionUtc>SYSUTCDATETIME()
              AND w.Estado=N'Confirmado' AND w.MensajeIdConfirmacion IS NOT NULL
              AND w.FechaConfirmacionUtc BETWEEN w.FechaCreacionUtc AND w.FechaExpiracionUtc
              AND w.FechaConfirmacionUtc<=SYSUTCDATETIME()
              AND w.TelefonoWhatsApp=CASE WHEN LEFT(r.TelefonoDestinatario,1)=N'+'
                  THEN r.TelefonoDestinatario ELSE N'+'+r.TelefonoDestinatario END
              AND EXISTS(SELECT 1 FROM dbo.Terminales WHERE Id=@terminal AND Activa=1)
              AND NOT EXISTS(SELECT 1 FROM dbo.DepositosPreReservaEncomienda WHERE ReservaId=r.Id);
            """,c);
        q.Parameters.Add("@reserva",SqlDbType.Int).Value=reservaId;
        q.Parameters.Add("@terminal",SqlDbType.Int).Value=terminalId;
        q.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=tokenHash;
        object? id=await q.ExecuteScalarAsync(token);
        return id is int remitente?new(reservaId,remitente):null;
    }

    public async Task GuardarEvidenciaRemitenteAsync(EvidenciaFacialRemitente e,byte[] tokenHash,CancellationToken token)
    {
        // La evidencia solo puede proceder del preparador con verificacion del servidor.
        if (!ValidacionEvidenciaFacialRemitente.EsValidaParaContinuar(e,e.ReservaId,e.RemitenteId,e.TerminalId,reloj.GetUtcNow()))
            throw new InvalidOperationException("Evidencia no valida.");
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var q=new SqlCommand("""
            INSERT dbo.EvidenciasFacialesRemitente
                (EvidenciaId,ReservaId,RemitenteId,TerminalId,FechaEmisionUtc,FechaExpiracionUtc,RostroCoincide,PruebaDeVidaSuperada)
            SELECT @id,r.Id,@persona,@terminal,@emision,@vence,1,1
            FROM dbo.PreReservasEncomiendasWeb r
            JOIN dbo.ParticipantesPreReservaVerificados p ON p.ReservaId=r.Id AND p.RemitenteId=@persona
            WHERE r.Id=@reserva AND r.TokenGestionHash=@hash AND r.Estado=N'PendienteConsentimiento'
              AND r.FechaExpiracion>SYSUTCDATETIME() AND p.FechaExpiracionUtc>SYSUTCDATETIME()
              AND EXISTS(SELECT 1 FROM dbo.Terminales WHERE Id=@terminal AND Activa=1);
            IF @@ROWCOUNT<>1 THROW 56001,'Reserva no disponible.',1;
            """,c);
        q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=e.EvidenciaId;
        q.Parameters.Add("@reserva",SqlDbType.Int).Value=e.ReservaId;
        q.Parameters.Add("@persona",SqlDbType.Int).Value=e.RemitenteId;
        q.Parameters.Add("@terminal",SqlDbType.Int).Value=e.TerminalId;
        q.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=tokenHash;
        q.Parameters.Add("@emision",SqlDbType.DateTime2).Value=e.FechaEmisionUtc.UtcDateTime;
        q.Parameters.Add("@vence",SqlDbType.DateTime2).Value=e.FechaExpiracionUtc.UtcDateTime;
        await q.ExecuteNonQueryAsync(token);
    }

    public Task<DepositoEncomiendaResultado> DepositarAsync(DepositoEncomiendaComando comando,CancellationToken token)
        =>deposito.DepositarAsync(comando,token);

    public async Task<bool> PuedeRetirarAsync(int usoId,int destinatarioId,int terminalId,CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var q=new SqlCommand("""
            SELECT COUNT(*) FROM dbo.UsosCasillero u
            JOIN dbo.ParticipantesUso p ON p.UsoId=u.Id AND p.Rol=N'Destinatario' AND p.PersonaId=@persona
            WHERE u.Id=@uso AND u.TipoServicio=N'Encomienda' AND u.Estado=N'Ocupado' AND u.FechaRetiro IS NULL
              AND EXISTS(SELECT 1 FROM dbo.Terminales WHERE Id=@terminal AND Activa=1);
            """,c);
        q.Parameters.Add("@uso",SqlDbType.Int).Value=usoId;
        q.Parameters.Add("@persona",SqlDbType.Int).Value=destinatarioId;
        q.Parameters.Add("@terminal",SqlDbType.Int).Value=terminalId;
        return Convert.ToInt32(await q.ExecuteScalarAsync(token))==1;
    }

    public async Task GuardarEvidenciaRetiroAsync(EvidenciaRetiroEncomienda e,CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var q=new SqlCommand("""
            INSERT dbo.EvidenciasFacialesRetiroEncomienda(EvidenciaId,UsoId,DestinatarioId,TerminalId,EmitidaUtc,VenceUtc)
            SELECT @id,u.Id,@persona,@terminal,@emision,@vence
            FROM dbo.UsosCasillero u JOIN dbo.ParticipantesUso p ON p.UsoId=u.Id AND p.Rol=N'Destinatario'
            WHERE u.Id=@uso AND p.PersonaId=@persona AND u.TipoServicio=N'Encomienda' AND u.Estado=N'Ocupado'
              AND u.FechaRetiro IS NULL AND EXISTS(SELECT 1 FROM dbo.Terminales WHERE Id=@terminal AND Activa=1);
            IF @@ROWCOUNT<>1 THROW 56001,'Operacion no disponible.',1;
            """,c);
        q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=e.Id;
        q.Parameters.Add("@uso",SqlDbType.Int).Value=e.UsoId;
        q.Parameters.Add("@persona",SqlDbType.Int).Value=e.DestinatarioId;
        q.Parameters.Add("@terminal",SqlDbType.Int).Value=e.TerminalId;
        q.Parameters.Add("@emision",SqlDbType.DateTime2).Value=e.EmitidaUtc.UtcDateTime;
        q.Parameters.Add("@vence",SqlDbType.DateTime2).Value=e.VenceUtc.UtcDateTime;
        await q.ExecuteNonQueryAsync(token);
    }

    public async Task<ResultadoRetiroEncomienda> RetirarAsync(RetirarEncomiendaComando solicitud,CancellationToken token)
    {
        if (solicitud.UsoId<=0 || solicitud.DestinatarioId<=0 || solicitud.TerminalId<=0 || solicitud.EvidenciaId==Guid.Empty ||
            solicitud.Pin is not {Length:6} || !solicitud.Pin.All(char.IsAsciiDigit))throw new ArgumentException("Solicitud invalida.");
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        byte[] salt;
        await using(var q=new SqlCommand("SELECT PinSalt FROM dbo.CodigosRetiroEncomiendaPrototipo WHERE UsoId=@uso AND DestinatarioId=@persona;",c))
        {
            q.Parameters.Add("@uso",SqlDbType.Int).Value=solicitud.UsoId;
            q.Parameters.Add("@persona",SqlDbType.Int).Value=solicitud.DestinatarioId;
            if (await q.ExecuteScalarAsync(token) is not byte[] encontrado)return ResultadoRetiroEncomienda.NoDisponible;
            salt=encontrado;
        }
        byte[]? hash=null;
        try
        {
            hash=CodigoPinRetiro.CalcularHash(solicitud.Pin,solicitud.UsoId,solicitud.DestinatarioId,salt,config);
            await using var q=new SqlCommand("dbo.RetirarEncomiendaTerminalSynchro",c){CommandType=CommandType.StoredProcedure};
            q.Parameters.Add("@UsoId",SqlDbType.Int).Value=solicitud.UsoId;
            q.Parameters.Add("@DestinatarioId",SqlDbType.Int).Value=solicitud.DestinatarioId;
            q.Parameters.Add("@TerminalId",SqlDbType.Int).Value=solicitud.TerminalId;
            q.Parameters.Add("@EvidenciaId",SqlDbType.UniqueIdentifier).Value=solicitud.EvidenciaId;
            q.Parameters.Add("@PinHash",SqlDbType.VarBinary,32).Value=hash;
            q.Parameters.Add("@PinSaltEsperado",SqlDbType.VarBinary,32).Value=salt;
            object? resultado=await q.ExecuteScalarAsync(token);
            return Enum.TryParse<ResultadoRetiroEncomienda>(resultado?.ToString(),out var estado)?estado:
                throw new InvalidOperationException("SQL no confirmo el retiro.");
        }
        finally {CryptographicOperations.ZeroMemory(salt);if(hash is not null)CryptographicOperations.ZeroMemory(hash);}
    }
}
