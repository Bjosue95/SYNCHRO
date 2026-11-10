using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Services;

internal sealed record SolicitudReemisionPin(int UsoId,int DestinatarioId,int TerminalId,
    Guid OperacionId,string TelefonoVerificado,DateTimeOffset VerificadoUtc)
{
    public override string ToString()=>"Reemision protegida de PIN";
}
internal enum ResultadoReemisionPin { Encolado,YaEncolado,NoDisponible,RequiereVerificacionReciente,LimiteAlcanzado }
internal interface IReemisionPinEncomienda
{
    Task<ResultadoReemisionPin> ReemitirAsync(SolicitudReemisionPin solicitud,CancellationToken token);
}
internal static class PoliticaReemisionPin
{
    public static ResultadoReemisionPin? Evaluar(DateTimeOffset ahora,DateTimeOffset creado,DateTimeOffset verificado,int reemisiones)
    {
        if(reemisiones>=3)return ResultadoReemisionPin.LimiteAlcanzado;
        // Una sesion previa al PIN no permite reiniciar sus cinco intentos.
        if(verificado<=creado||verificado>ahora||ahora-verificado>TimeSpan.FromMinutes(2))
            return ResultadoReemisionPin.RequiereVerificacionReciente;
        if(ahora-creado<TimeSpan.FromMinutes(2))return ResultadoReemisionPin.LimiteAlcanzado;
        return null;
    }
}
internal sealed class ReemisionPinEncomiendaSql(IConfiguration config,PreparadorPinEncomienda pines,SqlOutboxWhatsApp outbox) : IReemisionPinEncomienda
{
    public async Task<ResultadoReemisionPin> ReemitirAsync(SolicitudReemisionPin s,CancellationToken token)
    {
        if(s.UsoId<=0||s.DestinatarioId<=0||s.TerminalId<=0||s.OperacionId==Guid.Empty)throw new ArgumentException("Solicitud invalida.");
        string? plantilla=config["WhatsApp:PlantillaPinRetiro"];
        if(!MensajeWhatsApp.PlantillaEsValida(plantilla))throw new InvalidOperationException("Plantilla no configurada.");
        await using var c=new SqlConnection(ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas"));
        await c.OpenAsync(token);await using var t=(SqlTransaction)await c.BeginTransactionAsync(token);
        // Mismo orden de retiro: terminal, casillero, uso, participante, PIN.
        string codigo;int reserva;DateTimeOffset creado,ahora;int reemisiones;
        await using(var q=new SqlCommand("""
            IF NOT EXISTS(SELECT 1 FROM dbo.Terminales WITH(UPDLOCK,HOLDLOCK)WHERE Id=@terminal AND Activa=1)RETURN;
            DECLARE @codigo NVARCHAR(10)=(SELECT CodigoCasillero FROM dbo.UsosCasillero WHERE Id=@uso);
            IF NOT EXISTS(SELECT 1 FROM dbo.Casilleros WITH(UPDLOCK,HOLDLOCK)WHERE Codigo=@codigo AND Estado=N'Ocupado')RETURN;
            IF NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK)
                WHERE Id=@uso AND TipoServicio=N'Encomienda' AND Estado=N'Ocupado' AND FechaRetiro IS NULL)RETURN;
            IF NOT EXISTS(SELECT 1 FROM dbo.ParticipantesUso WITH(UPDLOCK,HOLDLOCK)
                WHERE UsoId=@uso AND PersonaId=@persona AND Rol=N'Destinatario')RETURN;
            SELECT @codigo,d.ReservaId,p.CreadoUtc
            FROM dbo.CodigosRetiroEncomiendaPrototipo p WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.DepositosPreReservaEncomienda d ON d.UsoId=p.UsoId
            WHERE p.UsoId=@uso AND p.DestinatarioId=@persona AND p.Consumido=0;
            """,c,t))
        {
            Parametros(q,s);await using var r=await q.ExecuteReaderAsync(token);if(!await r.ReadAsync(token))return ResultadoReemisionPin.NoDisponible;
            codigo=r.GetString(0);reserva=r.GetInt32(1);creado=Utc(r.GetDateTime(2));
        }
        // Contar despues de adquirir el bloqueo del PIN: otro reintento puede
        // haber esperado. No usar una subconsulta evaluable antes de esa espera.
        await using(var q=new SqlCommand("SELECT SYSUTCDATETIME(),COUNT(*)FROM dbo.ReemisionesPinEncomienda WHERE UsoId=@uso",c,t))
        {
            q.Parameters.Add("@uso",SqlDbType.Int).Value=s.UsoId;await using var r=await q.ExecuteReaderAsync(token);await r.ReadAsync(token);
            ahora=Utc(r.GetDateTime(0));reemisiones=r.GetInt32(1);
        }
        // Reintento de la MISMA solicitud no rota de nuevo el PIN ni repite el envio.
        await using(var q=new SqlCommand("SELECT UsoId,DestinatarioId FROM dbo.ReemisionesPinEncomienda WITH(UPDLOCK,HOLDLOCK)WHERE OperacionId=@operacion",c,t))
        {
            q.Parameters.Add("@operacion",SqlDbType.UniqueIdentifier).Value=s.OperacionId;
            await using var r=await q.ExecuteReaderAsync(token);
            if(await r.ReadAsync(token))return r.GetInt32(0)==s.UsoId&&r.GetInt32(1)==s.DestinatarioId?
                ResultadoReemisionPin.YaEncolado:ResultadoReemisionPin.NoDisponible;
        }
        var rechazo=PoliticaReemisionPin.Evaluar(ahora,creado,s.VerificadoUtc,reemisiones);if(rechazo.HasValue)return rechazo.Value;
        await using(var q=new SqlCommand("""
            SELECT COUNT(*) FROM dbo.PreReservasEncomiendasWeb r WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.ConsentimientosPreReservaWhatsApp w WITH(UPDLOCK,HOLDLOCK)ON w.ReservaId=r.Id
            WHERE r.Id=@reserva AND w.Estado=N'Confirmado' AND NULLIF(w.MensajeIdConfirmacion,N'')IS NOT NULL
                AND w.FechaConfirmacionUtc BETWEEN w.FechaCreacionUtc AND w.FechaExpiracionUtc
                AND w.FechaConfirmacionUtc<=SYSUTCDATETIME() AND w.TelefonoWhatsApp=@telefono
                AND w.TelefonoWhatsApp=CASE WHEN LEFT(r.TelefonoDestinatario,1)=N'+'THEN r.TelefonoDestinatario ELSE N'+'+r.TelefonoDestinatario END;
            """,c,t))
        {
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=reserva;q.Parameters.Add("@telefono",SqlDbType.NVarChar,20).Value=s.TelefonoVerificado;
            if(Convert.ToInt32(await q.ExecuteScalarAsync(token))!=1)return ResultadoReemisionPin.NoDisponible;
        }
        await using(var q=new SqlCommand("SELECT COUNT(*)FROM dbo.OutboxWhatsApp WITH(UPDLOCK,HOLDLOCK)WHERE (ReservaId=@reserva AND Estado=N'Enviando')OR Id=@operacion",c,t))
        {
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=reserva;
            q.Parameters.Add("@operacion",SqlDbType.UniqueIdentifier).Value=s.OperacionId;
            // No invalidar un envio en curso ni reutilizar una clave de otro evento.
            if(Convert.ToInt32(await q.ExecuteScalarAsync(token))!=0)return ResultadoReemisionPin.NoDisponible;
        }
        PinEncomiendaPreparado preparado;
        await using(var q=new SqlCommand("SELECT PinHash,PinSalt FROM dbo.CodigosRetiroEncomiendaPrototipo WHERE UsoId=@uso",c,t))
        {
            q.Parameters.Add("@uso",SqlDbType.Int).Value=s.UsoId;
            await using var r=await q.ExecuteReaderAsync(token);if(!await r.ReadAsync(token))return ResultadoReemisionPin.NoDisponible;
            byte[] hash=(byte[])r[0],salt=(byte[])r[1];
            try{preparado=pines.CrearDistinto(s.UsoId,s.DestinatarioId,hash,salt);}
            finally{CryptographicOperations.ZeroMemory(hash);CryptographicOperations.ZeroMemory(salt);}
        }
        using var pin=preparado;
        await using(var q=new SqlCommand("""
            IF @verificado>SYSUTCDATETIME() OR @verificado<=DATEADD(MINUTE,-2,SYSUTCDATETIME())
                THROW 56503,'Requiere nueva verificacion.',1;
            UPDATE dbo.CodigosRetiroEncomiendaPrototipo SET PinHash=@hash,PinSalt=@salt,CreadoUtc=SYSUTCDATETIME(),
                VenceUtc=@vence,IntentosFallidos=0 WHERE UsoId=@uso AND Consumido=0;
            IF @@ROWCOUNT<>1 THROW 56501,'PIN no disponible.',1;
            UPDATE dbo.OutboxWhatsApp SET Estado=N'Cancelado',ContenidoProtegido=NULL,CodigoError=N'PinReemitido'
                WHERE ReservaId=@reserva AND Estado IN(N'Pendiente',N'Reintento');
            INSERT dbo.ReemisionesPinEncomienda(OperacionId,UsoId,DestinatarioId,TerminalId)VALUES(@operacion,@uso,@persona,@terminal);
            INSERT dbo.AuditoriaEncomiendasPrototipo(UsoId,TerminalId,Accion)VALUES(@uso,@terminal,N'PIN reemitido');
            """,c,t))
        {
            Parametros(q,s);q.Parameters.Add("@verificado",SqlDbType.DateTime2).Value=s.VerificadoUtc.UtcDateTime;
            q.Parameters.Add("@operacion",SqlDbType.UniqueIdentifier).Value=s.OperacionId;
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=reserva;q.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=pin.Hash;
            q.Parameters.Add("@salt",SqlDbType.VarBinary,32).Value=pin.Salt;q.Parameters.Add("@vence",SqlDbType.DateTime2).Value=pin.VenceUtc.UtcDateTime;
            await q.ExecuteNonQueryAsync(token);
        }
        await outbox.EncolarAsync(c,t,s.OperacionId,reserva,new(s.TelefonoVerificado,plantilla!,[pin.Pin,codigo]),pin.VenceUtc,token);
        await t.CommitAsync(token);return ResultadoReemisionPin.Encolado;
    }
    private static DateTimeOffset Utc(DateTime d)=>new(DateTime.SpecifyKind(d,DateTimeKind.Utc));
    private static void Parametros(SqlCommand q,SolicitudReemisionPin s)
    {
        q.Parameters.Add("@uso",SqlDbType.Int).Value=s.UsoId;q.Parameters.Add("@persona",SqlDbType.Int).Value=s.DestinatarioId;
        q.Parameters.Add("@terminal",SqlDbType.Int).Value=s.TerminalId;
    }
}
