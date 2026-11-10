using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.WhatsApp;

internal sealed class SqlOutboxWhatsApp(IConfiguration configuracion,ProtectorMensajeWhatsApp protector) : IAlmacenOutboxWhatsApp
{
    private string Cadena => ConexionSqlAislada.Exigir(configuracion.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");

    // Debe llamarse desde la MISMA transaccion del evento de negocio. Este metodo
    // no confirma ni crea otra transaccion; el llamador decide commit/rollback.
    public async Task EncolarAsync(SqlConnection conexion,SqlTransaction transaccion,Guid operacionId,
        int reservaId,MensajeWhatsApp mensaje,DateTimeOffset expiraUtc,CancellationToken cancelacion=default)
    {
        if (conexion.Database != "SynchroDB_Pruebas" || transaccion.Connection != conexion)
            throw new InvalidOperationException("Se requiere una transaccion SQL de pruebas activa.");
        byte[] cifrado=protector.Proteger(operacionId,reservaId,mensaje);
        byte[] telefono=Encoding.Unicode.GetBytes(mensaje.Telefono);
        try
        {
            await using var q=new SqlCommand("""
                IF DB_NAME() <> N'SynchroDB_Pruebas' THROW 55800,'Base no autorizada.',1;
                IF @expira <= SYSUTCDATETIME() THROW 55801,'Mensaje vencido.',1;
                IF NOT EXISTS (SELECT 1 FROM dbo.OutboxWhatsApp WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id)
                    INSERT dbo.OutboxWhatsApp(Id,ReservaId,TelefonoHash,ContenidoProtegido,ExpiraUtc)
                    VALUES (@id,@reserva,@telefono,@cifrado,@expira);
                ELSE IF NOT EXISTS (SELECT 1 FROM dbo.OutboxWhatsApp WHERE Id=@id AND ReservaId=@reserva AND TelefonoHash=@telefono)
                    THROW 55802,'Clave de operacion en conflicto.',1;
                """,conexion,transaccion);
            q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=operacionId;
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=reservaId;
            q.Parameters.Add("@telefono",SqlDbType.VarBinary,32).Value=SHA256.HashData(telefono);
            q.Parameters.Add("@cifrado",SqlDbType.VarBinary,-1).Value=cifrado;
            q.Parameters.Add("@expira",SqlDbType.DateTime2).Value=expiraUtc.UtcDateTime;
            await q.ExecuteNonQueryAsync(cancelacion);
        }
        finally { CryptographicOperations.ZeroMemory(telefono);CryptographicOperations.ZeroMemory(cifrado); }
    }

    public async Task<EnvioPendienteWhatsApp?> TomarAsync(CancellationToken cancelacion)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(cancelacion);
        await using var q=new SqlCommand("dbo.TomarOutboxWhatsAppSynchro",c){CommandType=CommandType.StoredProcedure};
        await using var r=await q.ExecuteReaderAsync(cancelacion);
        if (!await r.ReadAsync(cancelacion))return null;
        return new(r.GetGuid(0),r.GetGuid(1),r.GetInt32(2),(byte[])r[3],r.GetInt32(4),
            new DateTimeOffset(DateTime.SpecifyKind(r.GetDateTime(5),DateTimeKind.Utc)));
    }

    public async Task CerrarAsync(EnvioPendienteWhatsApp envio,CierreEnvioWhatsApp cierre,CancellationToken cancelacion)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(cancelacion);
        await using var q=new SqlCommand("""
            IF DB_NAME() <> N'SynchroDB_Pruebas' THROW 55800,'Base no autorizada.',1;
            UPDATE dbo.OutboxWhatsApp
            SET Estado=@estado,MensajeMetaId=@meta,DisponibleUtc=COALESCE(@proxima,DisponibleUtc),
                CodigoError=@error,Arrendamiento=NULL,ArrendamientoHastaUtc=NULL,
                ContenidoProtegido=CASE WHEN @estado=N'Reintento' THEN ContenidoProtegido ELSE NULL END
            WHERE Id=@id AND Arrendamiento=@arrendamiento AND Estado=N'Enviando'
              AND ArrendamientoHastaUtc > SYSUTCDATETIME();
            IF @@ROWCOUNT <> 1 THROW 55803,'Arrendamiento no vigente.',1;
            """,c);
        q.Parameters.Add("@estado",SqlDbType.NVarChar,20).Value=cierre.Estado.ToString();
        q.Parameters.Add("@meta",SqlDbType.NVarChar,255).Value=(object?)cierre.IdMensaje??DBNull.Value;
        q.Parameters.Add("@proxima",SqlDbType.DateTime2).Value=(object?)cierre.ReintentarUtc?.UtcDateTime??DBNull.Value;
        q.Parameters.Add("@error",SqlDbType.NVarChar,64).Value=(object?)cierre.CodigoError??DBNull.Value;
        q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=envio.Id;
        q.Parameters.Add("@arrendamiento",SqlDbType.UniqueIdentifier).Value=envio.Arrendamiento;
        await q.ExecuteNonQueryAsync(cancelacion);
    }

    public static async Task ConciliarAsync(SqlConnection c,SqlTransaction t,string idMensaje,string telefono,
        string estado,CancellationToken cancelacion)
    {
        if (c.Database != "SynchroDB_Pruebas" || t.Connection != c)
            throw new InvalidOperationException("Se requiere una transaccion de pruebas activa.");
        if (estado is not ("sent" or "delivered" or "read" or "failed"))return;
        await using var q=new SqlCommand("""
            UPDATE dbo.OutboxWhatsApp
            SET Estado=CASE
                WHEN @estado=N'read' THEN N'Leido'
                WHEN @estado=N'delivered' AND Estado<>N'Leido' THEN N'Entregado'
                WHEN @estado=N'failed' AND Estado NOT IN (N'Entregado',N'Leido') THEN N'Fallido'
                ELSE Estado END
            WHERE MensajeMetaId=@id AND TelefonoHash=@telefono;
            """,c,t);
        q.Parameters.Add("@estado",SqlDbType.NVarChar,16).Value=estado;
        q.Parameters.Add("@id",SqlDbType.NVarChar,255).Value=idMensaje;
        // Meta envia el numero sin '+'. El formato ya fue validado por el webhook firmado.
        q.Parameters.Add("@telefono",SqlDbType.VarBinary,32).Value=SHA256.HashData(Encoding.Unicode.GetBytes("+"+telefono));
        await q.ExecuteNonQueryAsync(cancelacion);
    }
}
