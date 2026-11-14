using System.Data;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.WhatsApp;

internal sealed class SqlOutboxVerificacionTelefono(IConfiguration configuracion) : IOutboxVerificacionTelefono
{
    private async Task<SqlConnection> AbrirAsync(CancellationToken token)
    {
        var c = new SqlConnection(Services.ConexionSqlAislada.Exigir(
            configuracion.GetConnectionString("SynchroDB"), "SynchroDB_Pruebas"));
        try
        {
            await c.OpenAsync(token);
            await using var q = new SqlCommand("""
                IF DB_NAME() <> N'SynchroDB_Pruebas' OR NOT EXISTS
                    (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
                    THROW 57200, 'Base aislada no autorizada.', 1;
                """, c);
            await q.ExecuteNonQueryAsync(token);
            return c;
        }
        catch { await c.DisposeAsync(); throw; }
    }

    // El vinculo procede de una solicitud firmada ya preparada; no de datos HTTP.
    private const string Autorizacion = """
        EXISTS (SELECT 1 FROM dbo.SolicitudesInicioSesionWhatsApp s
            JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=s.ReservaId
            JOIN dbo.ConsentimientosPreReservaWhatsApp c ON c.ReservaId=r.Id
            JOIN dbo.VerificacionesTelefono v ON v.Id=s.VerificacionId
            WHERE s.VerificacionId=o.VerificacionId AND s.Estado=N'Preparada'
              AND v.Finalidad=N'InicioSesionDestinatario' AND v.Estado=N'Pendiente'
              AND v.FechaSesionEmitida IS NULL AND v.FechaExpiracion>SYSUTCDATETIME()
              AND c.Estado=N'Confirmado' AND NULLIF(c.MensajeIdConfirmacion,N'') IS NOT NULL
              AND c.FechaConfirmacionUtc BETWEEN c.FechaCreacionUtc AND c.FechaExpiracionUtc
              AND c.FechaConfirmacionUtc<=SYSUTCDATETIME()
              AND c.TelefonoWhatsApp=v.Telefono
              AND c.TelefonoWhatsApp=CASE WHEN LEFT(r.TelefonoDestinatario,1)=N'+'
                  THEN r.TelefonoDestinatario ELSE N'+'+r.TelefonoDestinatario END
              AND (r.FechaExpiracion>SYSUTCDATETIME() OR EXISTS
                  (SELECT 1 FROM dbo.DepositosPreReservaEncomienda d WHERE d.ReservaId=r.Id)))
        """;

    public async Task<EnvioVerificacionTelefono?> TomarAsync(CancellationToken token)
    {
        await using var c = await AbrirAsync(token);
        await using var t = (SqlTransaction)await c.BeginTransactionAsync(token);
        await using var q = new SqlCommand($"""
            SET XACT_ABORT ON;
            UPDATE dbo.OutboxVerificacionTelefono SET Estado=N'Indeterminado',
                CodigoError=N'ArrendamientoVencido',ContenidoProtegido=NULL,
                Arrendamiento=NULL,ArrendamientoHastaUtc=NULL
            WHERE Estado=N'Enviando' AND ArrendamientoHastaUtc<=SYSUTCDATETIME();
            UPDATE o SET Estado=CASE WHEN ExpiraUtc<=SYSUTCDATETIME() THEN N'Caducado'
                    WHEN Intentos>=5 THEN N'Fallido' ELSE N'Cancelado' END,
                ContenidoProtegido=NULL,Arrendamiento=NULL,ArrendamientoHastaUtc=NULL
            FROM dbo.OutboxVerificacionTelefono o
            WHERE Estado IN(N'Pendiente',N'Reintento')
              AND (ExpiraUtc<=SYSUTCDATETIME() OR Intentos>=5 OR NOT {Autorizacion});
            ;WITH siguiente AS (
                SELECT TOP(1) o.* FROM dbo.OutboxVerificacionTelefono o WITH(UPDLOCK,READPAST,ROWLOCK)
                WHERE Estado IN(N'Pendiente',N'Reintento') AND DisponibleUtc<=SYSUTCDATETIME()
                  AND ExpiraUtc>SYSUTCDATETIME() AND Intentos<5 AND {Autorizacion}
                ORDER BY CreadoUtc,Id)
            UPDATE siguiente SET Estado=N'Enviando',Intentos=Intentos+1,
                Arrendamiento=NEWID(),ArrendamientoHastaUtc=DATEADD(second,60,SYSUTCDATETIME())
            OUTPUT inserted.Id,inserted.Arrendamiento,inserted.VerificacionId,
                inserted.ContenidoProtegido,inserted.Intentos,inserted.ExpiraUtc;
            """, c, t);
        EnvioVerificacionTelefono? envio = null;
        await using (var r = await q.ExecuteReaderAsync(token))
            if (await r.ReadAsync(token)) envio = new(r.GetGuid(0), r.GetGuid(1), r.GetInt32(2),
                (byte[])r[3], r.GetInt32(4), new(DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc)));
        await t.CommitAsync(token);
        return envio;
    }

    public async Task<bool> AutorizarAsync(EnvioVerificacionTelefono envio, CancellationToken token)
    {
        await using var c = await AbrirAsync(token);
        await using var q = new SqlCommand($"""
            SELECT COUNT(*) FROM dbo.OutboxVerificacionTelefono o
            WHERE Id=@id AND Arrendamiento=@lease AND Estado=N'Enviando'
              AND ArrendamientoHastaUtc>SYSUTCDATETIME() AND ExpiraUtc>SYSUTCDATETIME()
              AND {Autorizacion};
            """, c);
        Identificadores(q, envio);
        return Convert.ToInt32(await q.ExecuteScalarAsync(token)) == 1;
    }

    public async Task CerrarAsync(EnvioVerificacionTelefono envio, CierreEnvioWhatsApp cierre, CancellationToken token)
    {
        await using var c = await AbrirAsync(token);
        await using var q = new SqlCommand("""
            UPDATE dbo.OutboxVerificacionTelefono SET Estado=@estado,MensajeMetaId=@meta,
                CodigoError=@error,DisponibleUtc=COALESCE(@proxima,DisponibleUtc),
                ContenidoProtegido=CASE WHEN @estado=N'Reintento' THEN ContenidoProtegido ELSE NULL END,
                Arrendamiento=NULL,ArrendamientoHastaUtc=NULL
            WHERE Id=@id AND Arrendamiento=@lease AND Estado=N'Enviando'
              AND ArrendamientoHastaUtc>SYSUTCDATETIME();
            IF @@ROWCOUNT<>1 THROW 57201,'Arrendamiento no vigente.',1;
            """, c);
        Identificadores(q, envio);
        q.Parameters.Add("@estado", SqlDbType.NVarChar,20).Value=cierre.Estado.ToString();
        q.Parameters.Add("@meta", SqlDbType.NVarChar,255).Value=(object?)cierre.IdMensaje??DBNull.Value;
        q.Parameters.Add("@error", SqlDbType.NVarChar,64).Value=(object?)cierre.CodigoError??DBNull.Value;
        q.Parameters.Add("@proxima", SqlDbType.DateTime2).Value=(object?)cierre.ReintentarUtc?.UtcDateTime??DBNull.Value;
        await q.ExecuteNonQueryAsync(token);
        // Tambien recoge estados firmados recibidos antes de confirmar el ID de Meta.
        await ConciliarPendientesAsync(c, null, token);
    }

    internal static async Task ConciliarPendientesAsync(SqlConnection c, SqlTransaction? t, CancellationToken token)
    {
        if (c.Database != "SynchroDB_Pruebas") throw new InvalidOperationException("Base no autorizada.");
        await using var q = new SqlCommand("""
            UPDATE o SET Estado=CASE WHEN e.Leido=1 THEN N'Leido'
                WHEN e.Entregado=1 AND o.Estado<>N'Leido' THEN N'Entregado'
                WHEN e.Fallido=1 AND o.Estado NOT IN(N'Leido',N'Entregado') THEN N'Fallido'
                ELSE o.Estado END
            FROM dbo.OutboxVerificacionTelefono o
            CROSS APPLY (SELECT
                MAX(CASE WHEN EstadoMensaje=N'read' THEN 1 ELSE 0 END) Leido,
                MAX(CASE WHEN EstadoMensaje=N'delivered' THEN 1 ELSE 0 END) Entregado,
                MAX(CASE WHEN EstadoMensaje=N'failed' THEN 1 ELSE 0 END) Fallido
                FROM dbo.WhatsAppWebhookEventos e WHERE e.TipoEvento=N'Estado'
                  AND e.MensajeId=o.MensajeMetaId
                  AND HASHBYTES('SHA2_256',CONVERT(nvarchar(33),N'+'+e.WhatsappId))=o.TelefonoHash) e
            WHERE o.MensajeMetaId IS NOT NULL;
            """, c, t);
        await q.ExecuteNonQueryAsync(token);
    }

    private static void Identificadores(SqlCommand q, EnvioVerificacionTelefono e)
    {
        q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=e.Id;
        q.Parameters.Add("@lease",SqlDbType.UniqueIdentifier).Value=e.Arrendamiento;
    }
}
