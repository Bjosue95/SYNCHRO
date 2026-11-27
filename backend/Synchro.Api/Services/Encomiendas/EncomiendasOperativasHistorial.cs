using System.Data;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

internal sealed record ConsultaHistorialEncomienda(int Pagina=1,int TamanoPagina=20,string? Estado=null,
    DateOnly? Desde=null,DateOnly? Hasta=null,int? Numero=null,string? Categoria=null);
internal sealed record CancelarEncomiendaSolicitud(string Motivo,string? Detalle,bool Confirmada,long Version);
internal sealed record CancelacionEncomienda(int ReservaId,string Motivo,string? Detalle,DateTimeOffset FechaUtc,int ResponsableId,bool RevisionPago);
internal sealed record EventoSeguimientoEncomienda(string Tipo,string Descripcion,DateTimeOffset FechaUtc);
internal sealed record FilaHistorialEncomienda(int Id,string Tamano,string Estado,DateTimeOffset CreadaUtc,DateTimeOffset VenceUtc,
    string TelefonoDestinatario,string PagoEstado,decimal? ImporteHnl,int? UsoId,string? Casillero,
    DateTimeOffset? DepositoUtc,DateTimeOffset? EntregaUtc,bool Historica,CancelacionEncomienda? Cancelacion,ReservaPrototipo? Reserva=null)
{
    public bool RetiradaConfirmada => Estado=="Retirada" && EntregaUtc.HasValue;
    public bool PagoRequiereRevision => Cancelacion?.RevisionPago==true;
}

internal sealed partial class EncomiendasOperativasSql
{
    private const string AccionCancelacion="EncomiendaCancelada";
    private const string BaseHistorial="""
        WITH Historial AS (
          SELECT r.Id,r.TamanoSolicitado,
            CASE WHEN COALESCE(o.Estado,r.Estado) IN(N'Preparacion',N'ListaParaDeposito',N'PendienteConsentimiento',N'Pendiente')
                AND r.FechaExpiracion<=@ahora AND d.UsoId IS NULL THEN N'Vencida'
              WHEN o.ReservaId IS NULL AND u.FechaRetiro IS NOT NULL THEN N'Retirada'
              WHEN o.ReservaId IS NULL AND d.UsoId IS NOT NULL THEN N'Depositada'
              ELSE COALESCE(o.Estado,r.Estado) END Estado,
            r.FechaCreacion,r.FechaExpiracion,r.TelefonoDestinatario,COALESCE(o.PagoEstado,N'NoRegistrado') PagoEstado,o.ImporteHnl,
            COALESCE(o.UsoId,d.UsoId) UsoId,u.CodigoCasillero,COALESCE(o.FechaDepositoUtc,d.FechaDepositoUtc) DepositoUtc,
            COALESCE(o.FechaEntregaUtc,u.FechaRetiro) EntregaUtc,CAST(CASE WHEN o.ReservaId IS NULL THEN 1 ELSE 0 END AS bit) Historica,
            cancelacion.Detalle CancelacionJson,seleccion.Detalle ReservaJson
          FROM dbo.PreReservasEncomiendasWeb r
          LEFT JOIN dbo.EncomiendasOperativas o ON o.ReservaId=r.Id
          LEFT JOIN dbo.DepositosPreReservaEncomienda d ON d.ReservaId=r.Id AND o.ReservaId IS NULL
          LEFT JOIN dbo.UsosCasillero u ON u.Id=COALESCE(o.UsoId,d.UsoId)
          OUTER APPLY(SELECT TOP(1) a.Detalle FROM dbo.AuditoriaEncomiendaOperativa a WHERE a.ReservaId=r.Id AND a.Accion=N'ReservaPrototipoCreada' ORDER BY a.Id) seleccion
          OUTER APPLY(SELECT TOP(1) a.Detalle FROM dbo.AuditoriaEncomiendaOperativa a
            WHERE a.Accion=N'EncomiendaCancelada' AND (a.ReservaId=r.Id OR
              (a.ReservaId IS NULL AND a.PersonaId=r.ClientePersonaId AND
                TRY_CONVERT(int,JSON_VALUE(CASE WHEN ISJSON(a.Detalle)=1 THEN a.Detalle ELSE N'{}' END,'$.ReservaId'))=r.Id))
            ORDER BY a.Id) cancelacion
          WHERE (@persona IS NULL OR r.ClientePersonaId=@persona) AND (@persona IS NOT NULL OR o.ReservaId IS NOT NULL)
        )
        """;
    private const string FiltroHistorial="""
        WHERE (@estado IS NULL OR Estado=@estado) AND (@desde IS NULL OR FechaCreacion>=@desde)
          AND (@hasta IS NULL OR FechaCreacion<@hasta) AND (@numero IS NULL OR Id=@numero)
          AND (@categoria IS NULL OR
            (@categoria=N'Activas' AND Estado NOT IN(N'Cancelada',N'Vencida',N'Rechazada')
              AND NOT (Estado=N'Retirada' AND EntregaUtc IS NOT NULL)) OR
            (@categoria=N'Historial' AND (Estado IN(N'Cancelada',N'Vencida',N'Rechazada')
              OR (Estado=N'Retirada' AND EntregaUtc IS NOT NULL))))
        """;

    private static FilaHistorialEncomienda FilaHistorial(SqlDataReader r)=>new(r.GetInt32(0),r.GetString(1),r.GetString(2),Fecha(r,3),Fecha(r,4),
        r.GetString(5),r.GetString(6),r.IsDBNull(7)?null:r.GetDecimal(7),r.IsDBNull(8)?null:r.GetInt32(8),r.IsDBNull(9)?null:r.GetString(9),
        r.IsDBNull(10)?null:Fecha(r,10),r.IsDBNull(11)?null:Fecha(r,11),r.GetBoolean(12),r.IsDBNull(13)?null:JsonSerializer.Deserialize<CancelacionEncomienda>(r.GetString(13)),r.IsDBNull(14)?null:JsonSerializer.Deserialize<ReservaPrototipo>(r.GetString(14)));

    public Task<object> Listar(int? persona,CancellationToken ct)=>Listar(persona,new(),ct);
    public async Task<object> Listar(int? persona,ConsultaHistorialEncomienda filtro,CancellationToken ct)
    {
        Exigir(filtro.Pagina>0&&filtro.Pagina<=1000000&&filtro.TamanoPagina is >=1 and <=100,"PaginacionInvalida","Revisa la página y su tamaño (1 a 100).",400);
        var estado=string.IsNullOrWhiteSpace(filtro.Estado)?null:Texto(filtro.Estado,30,"el estado");
        var categoria=string.IsNullOrWhiteSpace(filtro.Categoria)?null:Texto(filtro.Categoria,20,"la categoría");
        Exigir(categoria is null or "Activas" or "Historial","CategoriaInvalida","Elige reservas activas o historial.",400);
        Exigir(filtro.Numero is null or >0,"NumeroInvalido","El número de encomienda debe ser positivo.",400);
        Exigir(filtro.Desde is null||filtro.Hasta is null||filtro.Desde<=filtro.Hasta,"FechasInvalidas","La fecha inicial debe ser anterior a la final.",400);
        Exigir(filtro.Hasta!=DateOnly.MaxValue,"FechasInvalidas","Revisa la fecha final.",400);
        // Fechas de calendario de Honduras; extremo final exclusivo, sin depender de la zona del servidor.
        DateTime? desde=filtro.Desde?.ToDateTime(TimeOnly.MinValue).AddHours(6);
        DateTime? hasta=filtro.Hasta?.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(6);
        await using var c=await Abrir(ct);
        await using var q=Comando(c,null,BaseHistorial+" SELECT COUNT(*) FROM Historial "+FiltroHistorial+";"+
            BaseHistorial+" SELECT * FROM Historial "+FiltroHistorial+" ORDER BY FechaCreacion DESC,Id DESC OFFSET @salto ROWS FETCH NEXT @tamano ROWS ONLY;"+
            BaseHistorial+" SELECT COUNT(CASE WHEN Estado IN(N'Preparacion',N'ListaParaDeposito') THEN 1 END),COUNT(CASE WHEN Estado=N'Depositada' THEN 1 END),COUNT(CASE WHEN Estado=N'Retirada' AND EntregaUtc IS NOT NULL THEN 1 END) FROM Historial;",
            ("@persona",persona),("@ahora",Ahora.UtcDateTime),("@estado",estado),("@desde",desde),("@hasta",hasta),("@numero",filtro.Numero),
            ("@categoria",categoria),("@salto",(filtro.Pagina-1)*filtro.TamanoPagina),("@tamano",filtro.TamanoPagina));
        await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);var total=r.GetInt32(0);
        await r.NextResultAsync(ct);var items=new List<FilaHistorialEncomienda>();while(await r.ReadAsync(ct))items.Add(FilaHistorial(r));
        await r.NextResultAsync(ct);await r.ReadAsync(ct);var resumen=new{preparacion=r.GetInt32(0),depositadas=r.GetInt32(1),entregadas=r.GetInt32(2)};
        return new{encomiendas=items,pagina=filtro.Pagina,tamanoPagina=filtro.TamanoPagina,total,totalPaginas=(int)Math.Ceiling((double)total/filtro.TamanoPagina),resumen};
    }

    private async Task<FilaHistorialEncomienda> LeerHistorial(SqlConnection c,int id,int? persona,CancellationToken ct)
    {
        await using var q=Comando(c,null,BaseHistorial+" SELECT * FROM Historial WHERE Id=@id;",("@id",id),("@persona",persona),("@ahora",Ahora.UtcDateTime));
        await using var r=await q.ExecuteReaderAsync(ct);
        Exigir(await r.ReadAsync(ct),"NoEncontrada","Encomienda no encontrada.",404);return FilaHistorial(r);
    }

    private async Task<List<EventoSeguimientoEncomienda>> Seguimiento(SqlConnection c,FilaHistorialEncomienda fila,CancellationToken ct)
    {
        var eventos=new List<EventoSeguimientoEncomienda>{new("Creada",fila.Reserva?.Servicio=="Personal"?"Reserva de pertenencias personales creada":"Encomienda creada",fila.CreadaUtc)};
        if(fila.DepositoUtc.HasValue)eventos.Add(new("Depositada","Depósito confirmado; pendiente de retiro",fila.DepositoUtc.Value));
        if(fila.RetiradaConfirmada)eventos.Add(new("Retirada",fila.Reserva?.Servicio=="Personal"?"Retiro de pertenencias confirmado":"Retirada por el destinatario",fila.EntregaUtc!.Value));
        if(fila.Cancelacion is { } cancelacion)eventos.Add(new("Cancelada","Cancelada por el titular de la reserva: "+cancelacion.Motivo,cancelacion.FechaUtc));
        if(!fila.Historica) {
            await using var q=Comando(c,null,"""
                SELECT N'AceptacionDestinatario',CASE Estado WHEN N'Confirmado' THEN N'Aceptación del destinatario comprobada manualmente' ELSE N'Rechazo del destinatario registrado manualmente' END,FechaUtc
                FROM dbo.AuditoriaEncomiendaOperativa WHERE ReservaId=@id AND Accion=N'ConsentimientoDestinatario' AND Estado IN(N'Confirmado',N'Rechazado')
                UNION ALL SELECT N'PagoConfirmado',N'Pago del remitente confirmado manualmente',PagoConfirmadoUtc
                FROM dbo.EncomiendasOperativas WHERE ReservaId=@id AND PagoConfirmadoUtc IS NOT NULL
                UNION ALL SELECT N'PinEntregado',CASE Finalidad WHEN N'Deposito' THEN N'Envío del PIN de depósito declarado por el operador' ELSE N'Envío del PIN de retiro declarado por el operador' END,EntregadoUtc
                FROM dbo.PinesEncomiendaOperativa WHERE ReservaId=@id AND EntregadoUtc IS NOT NULL
                UNION ALL SELECT N'PinDepositoSolicitado',N'PIN de depósito solicitado por el titular',FechaUtc
                FROM dbo.AuditoriaEncomiendaOperativa WHERE ReservaId=@id AND Accion=N'PinDepositoSolicitado';
                """,("@id",fila.Id));
            await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))eventos.Add(new(r.GetString(0),r.GetString(1),Fecha(r,2)));
        }
        return eventos.OrderBy(e=>e.FechaUtc).ToList();
    }

    public async Task<object> Cancelar(int id,int persona,CancelarEncomiendaSolicitud datos,CancellationToken ct)
    {
        Exigir(datos.Confirmada,"ConfirmacionRequerida","Confirma expresamente la cancelación.",400);
        Exigir(datos.Motivo is "Número de destinatario incorrecto" or "Tamaño de casillero incorrecto" or "Ya no desea enviar la encomienda" or "Ya no desea utilizar el casillero" or "Otro motivo","MotivoInvalido","Selecciona un motivo de cancelación.",400);
        var explicacion=datos.Motivo=="Otro motivo"?Texto(datos.Detalle,160,"el motivo de cancelación"):null;
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        // Mismo orden que el depósito: operación canónica -> pre-reserva -> solicitudes.
        bool operativa;await using(var q=Comando(c,tx,"SELECT ReservaId FROM dbo.EncomiendasOperativas WITH(UPDLOCK,HOLDLOCK) WHERE ReservaId=@id",("@id",id)))operativa=await q.ExecuteScalarAsync(ct) is int;
        string estado,pago;long version;
        await using(var q=Comando(c,tx,"""
            SELECT COALESCE(o.Estado,r.Estado),COALESCE(o.PagoEstado,N'NoRegistrado'),COALESCE(o.Version,1),
              COALESCE(o.UsoId,d.UsoId)
            FROM dbo.PreReservasEncomiendasWeb r WITH(UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.EncomiendasOperativas o ON o.ReservaId=r.Id
            LEFT JOIN dbo.DepositosPreReservaEncomienda d ON d.ReservaId=r.Id
            WHERE r.Id=@id AND r.ClientePersonaId=@persona;
            """,("@id",id),("@persona",persona))) {
            await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"NoEncontrada","Encomienda no encontrada.",404);
            estado=r.GetString(0);pago=r.GetString(1);version=Convert.ToInt64(r[2]);
            Exigir(r.IsDBNull(3),"CancelacionNoPermitida","La encomienda ya tiene un depósito confirmado.");
        }
        if(estado=="Cancelada") {await tx.CommitAsync(ct);return new{cancelada=true,idempotente=true};}
        Exigir(estado is "Preparacion" or "ListaParaDeposito" or "PendienteConsentimiento" or "Pendiente" or "Vencida","CancelacionNoPermitida","Solo puedes cancelar encomiendas antes del depósito.");
        Exigir(datos.Version==version,"VersionNoCoincide","La encomienda cambió. Actualiza el detalle antes de cancelar.");
        var cancelacion=new CancelacionEncomienda(id,datos.Motivo,explicacion,Ahora,persona,pago=="ConfirmadoManual");
        var json=JsonSerializer.Serialize(cancelacion,new JsonSerializerOptions{Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
        Exigir(json.Length<=500,"MotivoInvalido","Escribe una explicación más breve.",400);
        await using(var q=Comando(c,tx,"UPDATE dbo.PreReservasEncomiendasWeb SET Estado=N'Cancelada' WHERE Id=@id;",("@id",id)))await q.ExecuteNonQueryAsync(ct);
        if(operativa) {
            await using var q=Comando(c,tx,"""
                UPDATE dbo.EncomiendasOperativas SET Estado=N'Cancelada',Version=Version+1 WHERE ReservaId=@id;
                UPDATE dbo.PinesEncomiendaOperativa SET Activo=0,RevocadoUtc=COALESCE(RevocadoUtc,@ahora) WHERE ReservaId=@id AND Activo=1;
                UPDATE dbo.SolicitudesDepositoEncomiendaOperativa SET Estado=N'Cancelada' WHERE ReservaId=@id AND Estado IN(N'Pendiente',N'Autorizada');
                UPDATE dbo.ParticipantesEncomiendaOperativa SET Consentimiento=N'Revocado',AceptaEncomienda=0 WHERE ReservaId=@id AND Rol=N'Destinatario';
                UPDATE dbo.EvidenciasEncomiendaOperativa SET VenceUtc=CASE WHEN VenceUtc>@ahora THEN @ahora ELSE VenceUtc END WHERE ReservaId=@id AND ConsumidaUtc IS NULL;
                """,("@id",id),("@ahora",Ahora.UtcDateTime));await q.ExecuteNonQueryAsync(ct);
        }
        // Cierra también cualquier consentimiento histórico específico; nunca toca el permiso general de cuenta.
        await LiberarCasilleroReserva(c,tx,id,await LeerReserva(c,tx,id,ct),Ahora,ct);
        await using(var q=Comando(c,tx,"UPDATE dbo.ConsentimientosPreReservaWhatsApp SET Estado=N'Revocado' WHERE ReservaId=@id;",("@id",id)))await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,operativa?id:null,persona,"Cliente",persona,AccionCancelacion,"Cancelada",json,ct);
        if(cancelacion.RevisionPago)await Auditar(c,tx,id,persona,"Cliente",persona,"PagoPendienteRevision","PendienteRevision","Pago confirmado antes de cancelar. Requiere revisión administrativa; no se ha realizado un reembolso.",ct);
        await tx.CommitAsync(ct);return new{cancelada=true,idempotente=false,pagoRequiereRevision=cancelacion.RevisionPago};
    }
}
