using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

internal sealed record ConsultaBandejaWhatsApp(int Pagina=1,int TamanoPagina=20,string? Servicio=null,string? Telefono=null,string? Referencia=null,bool SoloPendientes=false);

internal sealed partial class EncomiendasOperativasSql
{
    public async Task<object> BandejaWhatsApp(ConsultaBandejaWhatsApp filtro,CancellationToken ct)
    {
        Exigir(filtro.Pagina is >0 and <=1000000&&filtro.TamanoPagina is >=1 and <=100,"PaginacionInvalida","Revisa la paginación.",400);
        Exigir(string.IsNullOrEmpty(filtro.Servicio)||filtro.Servicio is "Encomiendas" or "ResguardoPersonal","ServicioInvalido","Selecciona un servicio válido.",400);
        var telefono=string.IsNullOrWhiteSpace(filtro.Telefono)?null:WhatsAppEncomienda.Telefono(filtro.Telefono);
        Exigir(string.IsNullOrWhiteSpace(filtro.Telefono)||telefono is not null,"TelefonoInvalido","Introduce el teléfono completo con código de país.",400);
        var referencia=string.IsNullOrWhiteSpace(filtro.Referencia)?null:Texto(filtro.Referencia,40,"la referencia de operación");
        await using var c=await Abrir(ct,true);
        bool auditable;await using(var esquema=Comando(c,null,"SELECT CASE WHEN OBJECT_ID(N'dbo.AuditoriaEncomiendaOperativa',N'U') IS NULL THEN 0 ELSE 1 END"))auditable=Convert.ToInt32(await esquema.ExecuteScalarAsync(ct))==1;
        var fuentes=new List<string>();
        if(config.GetValue<bool>("EncomiendasOperacion:Habilitado")&&filtro.Servicio!="ResguardoPersonal")fuentes.Add("""
            SELECT N'Encomiendas' Servicio,CONVERT(nvarchar(40),o.ReservaId) OperacionId,o.ReservaId EncomiendaId,
              CAST(NULL AS uniqueidentifier) IntentoId,o.UsoId,CASE WHEN JSON_VALUE(seleccion.Detalle,'$.Servicio')=N'Personal' THEN N'Titular' ELSE p.Rol END Rol,
              CASE p.Rol WHEN N'Remitente' THEN cuenta.NombreUsuario ELSE N'Destinatario sin cuenta requerida' END Persona,
              CASE p.Rol WHEN N'Remitente' THEN cuenta.Telefono ELSE p.Telefono END Telefono,
              CASE WHEN p.Rol=N'Remitente' AND general.Id IS NOT NULL THEN
                  CASE WHEN cuenta.Activa=1 AND cuenta.Telefono=JSON_VALUE(general.Detalle,'$.Telefono') THEN general.Estado ELSE N'Pendiente' END
                WHEN p.Rol=N'Remitente' AND (cuenta.Activa=0 OR cuenta.Telefono<>p.Telefono) THEN N'Pendiente' ELSE p.Consentimiento END Consentimiento,
              CASE WHEN JSON_VALUE(seleccion.Detalle,'$.Servicio')=N'Personal' THEN N'DepositoRetiro' WHEN p.Rol=N'Remitente' THEN N'Deposito' ELSE N'Retiro' END Finalidad,
              CASE WHEN pin.Id IS NULL THEN N'NoEmitido' WHEN pin.ConsumidoUtc IS NOT NULL THEN N'Consumido'
                WHEN pin.Activo=0 THEN N'Revocado' WHEN pin.IntentosFallidos>=5 THEN N'Bloqueado' WHEN pin.VenceUtc<=@ahora THEN N'Vencido' ELSE N'Emitido' END Emision,
              CASE WHEN pin.EntregadoUtc IS NULL THEN N'Pendiente' ELSE N'EnviadoManual' END Entrega,
              CASE WHEN o.Estado IN(N'Preparacion',N'ListaParaDeposito') AND r.FechaExpiracion<=@ahora THEN N'Vencida' ELSE o.Estado END Estado,
              p.SolicitadoUtc CreadoUtc,CASE WHEN o.Estado=N'Cancelada' AND o.PagoEstado=N'ConfirmadoManual' THEN N'PendienteRevision' ELSE o.PagoEstado END Pago,
              CASE WHEN p.Rol=N'Remitente' AND general.Id IS NOT NULL AND general.Estado=N'Confirmado' THEN general.FechaUtc ELSE p.VerificadoUtc END VerificadoUtc,
              CASE WHEN p.Rol=N'Remitente' AND general.Id IS NOT NULL THEN ag.NombreUsuario ELSE ap.NombreUsuario END Responsable,
              CASE p.Rol WHEN N'Remitente' THEN N'SYNCHRO ACEPTO' ELSE N'SYNCHRO ACEPTO ENCOMIENDA '+REPLACE(CONVERT(nvarchar(36),p.CodigoSolicitud),N'-',N'') END Referencia,
              pin.EntregadoUtc,COALESCE(JSON_VALUE(seleccion.Detalle,'$.Servicio'),N'Encomienda') ClaseReserva,
              COALESCE(JSON_VALUE(seleccion.Detalle,'$.TipoCasillero'),N'Normal') TipoCasillero,JSON_VALUE(seleccion.Detalle,'$.Minutos') Minutos,
              CAST(CASE WHEN solicitudPin.Id IS NOT NULL AND p.Rol=N'Remitente' THEN 1 ELSE 0 END AS bit) SolicitudPinDeposito
            FROM dbo.EncomiendasOperativas o JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=o.ReservaId
            JOIN dbo.ParticipantesEncomiendaOperativa p ON p.ReservaId=o.ReservaId
            JOIN dbo.ClientesWebPrototipo cuenta ON cuenta.PersonaId=r.ClientePersonaId
            OUTER APPLY(SELECT TOP(1) a.Detalle FROM dbo.AuditoriaEncomiendaOperativa a WHERE a.ReservaId=o.ReservaId AND a.Accion=N'ReservaPrototipoCreada' ORDER BY a.Id) seleccion
            OUTER APPLY(SELECT TOP(1) a.Id,a.Estado,a.Detalle,a.FechaUtc,a.ActorId,a.ActorTipo
              FROM dbo.AuditoriaEncomiendaOperativa a WHERE a.ReservaId IS NULL AND a.PersonaId=r.ClientePersonaId AND a.Accion=N'ConsentimientoCuentaRemitente' ORDER BY a.Id DESC) general
            LEFT JOIN dbo.Administradores ag ON general.ActorTipo=N'Administrador' AND ag.Id=general.ActorId
            LEFT JOIN dbo.Administradores ap ON ap.Id=p.VerificadoAdminId
            OUTER APPLY(SELECT TOP(1) a.Id FROM dbo.AuditoriaEncomiendaOperativa a
              WHERE a.ReservaId=o.ReservaId AND a.Accion=N'PinDepositoSolicitado' ORDER BY a.Id DESC) solicitudPin
            OUTER APPLY(SELECT TOP(1) * FROM dbo.PinesEncomiendaOperativa pin WHERE pin.ReservaId=o.ReservaId
              AND (JSON_VALUE(seleccion.Detalle,'$.Servicio')=N'Personal' OR pin.Finalidad=CASE p.Rol WHEN N'Remitente' THEN N'Deposito' ELSE N'Retiro' END) ORDER BY pin.CreadoUtc DESC) pin
            WHERE (JSON_VALUE(seleccion.Detalle,'$.Servicio') IS NULL OR JSON_VALUE(seleccion.Detalle,'$.Servicio')<>N'Personal' OR p.Rol=N'Remitente')
              AND (p.Rol=N'Remitente' OR (o.Estado=N'Depositada' AND o.UsoId IS NOT NULL))
            """);
        if(config.GetValue<bool>("WhatsAppManual:Habilitado")&&filtro.Servicio!="Encomiendas")fuentes.Add($"""
            SELECT N'ResguardoPersonal',CONVERT(nvarchar(40),a.Id),CAST(NULL AS int),a.Id,a.UsoId,N'Titular',N'Cliente de tablet',
              CASE WHEN a.TelefonoCliente IS NULL THEN NULL ELSE N'+'+a.TelefonoCliente END,
              CASE WHEN a.OperadorId IS NULL OR a.TelefonoCliente IS NULL THEN N'Pendiente' ELSE N'Confirmado' END,N'RetiroPersonal',
              CASE WHEN pin.UsoId IS NULL THEN N'NoEmitido' WHEN pin.FechaUso IS NOT NULL THEN N'Consumido'
                WHEN pin.FechaVencimiento<=@ahora THEN N'Vencido' ELSE N'Emitido' END,
              CASE WHEN a.FechaEnvioDeclaradaUtc IS NULL THEN N'Pendiente' ELSE N'EnviadoManual' END,
              CASE WHEN u.FechaRetiro IS NOT NULL THEN N'Retirada' WHEN u.FechaIngreso IS NOT NULL THEN N'Depositada'
                WHEN a.Estado NOT IN(N'Confirmado',N'Cancelado') AND (a.VenceUtc<=@ahora OR u.FechaVencimiento<=@ahora) THEN N'Vencido' ELSE a.Estado END,
              a.CreadoUtc,N'NoAplicaEncomienda',{(auditable?"verificado.FechaUtc":"CAST(NULL AS datetime2)")},operador.NombreUsuario,
              CONVERT(nvarchar(40),a.CodigoAutorizacion),a.FechaEnvioDeclaradaUtc,N'PersonalTablet',N'Normal',CAST(NULL AS nvarchar(10)),CAST(0 AS bit)
            FROM dbo.AutorizacionesWhatsAppManuales a LEFT JOIN dbo.UsosCasillero u ON u.Id=a.UsoId
            LEFT JOIN dbo.CodigosRetiroPersonal pin ON pin.UsoId=a.UsoId
            LEFT JOIN dbo.Administradores operador ON operador.Id=a.OperadorId
            """+(auditable?"""
             OUTER APPLY(SELECT TOP(1) h.FechaUtc FROM dbo.AuditoriaEncomiendaOperativa h
               WHERE h.Accion=N'WhatsAppPersonalConfirmado' AND
                 JSON_VALUE(CASE WHEN ISJSON(h.Detalle)=1 THEN h.Detalle ELSE N'{}' END,'$.IntentoId')=CONVERT(nvarchar(36),a.Id)
               ORDER BY h.Id DESC) verificado
            """:""));
        Exigir(fuentes.Count>0,"NoHabilitado","La gestión manual no está habilitada.",503);
        var cte="WITH Bandeja(Servicio,OperacionId,EncomiendaId,IntentoId,UsoId,Rol,Persona,Telefono,Consentimiento,Finalidad,Emision,Entrega,Estado,CreadoUtc,Pago,VerificadoUtc,Responsable,Referencia,EntregadoUtc,ClaseReserva,TipoCasillero,Minutos,SolicitudPinDeposito) AS ("+string.Join(" UNION ALL ",fuentes)+") ";
        var where=" WHERE (@servicio IS NULL OR Servicio=@servicio) AND (@telefono IS NULL OR Telefono=@telefono) AND (@referencia IS NULL OR OperacionId=@referencia OR CONVERT(nvarchar(20),UsoId)=@referencia OR Referencia=@referencia) ";
        if(filtro.SoloPendientes)
            where+=" AND Estado NOT IN(N'Cancelada',N'Cancelado',N'Retirada',N'Rechazada',N'Vencida',N'Vencido',N'Enviado') AND Consentimiento<>N'Rechazado' AND Entrega<>N'EnviadoManual' AND (Consentimiento=N'Pendiente' OR Emision=N'NoEmitido' OR (Emision=N'Emitido' AND Entrega=N'Pendiente')) ";
        // Cada búsqueda debe planificarse con sus filtros reales; evitar recorrer ambos servicios y auditorías cuando se busca una sola operación.
        await using var q=Comando(c,null,cte+"SELECT COUNT(*),COUNT(CASE WHEN Estado NOT IN(N'Cancelada',N'Cancelado',N'Retirada',N'Rechazada',N'Vencida',N'Vencido') AND Consentimiento=N'Pendiente' THEN 1 END),COUNT(CASE WHEN Estado NOT IN(N'Cancelada',N'Cancelado',N'Retirada',N'Rechazada',N'Vencida',N'Vencido') AND Emision=N'Emitido' AND Entrega=N'Pendiente' THEN 1 END) FROM Bandeja "+where+" OPTION(RECOMPILE);"+cte+"SELECT * FROM Bandeja "+where+" ORDER BY CreadoUtc DESC,Servicio,OperacionId,Rol OFFSET @salto ROWS FETCH NEXT @tamano ROWS ONLY OPTION(RECOMPILE);",
            ("@servicio",string.IsNullOrEmpty(filtro.Servicio)?null:filtro.Servicio),("@telefono",telefono),("@referencia",referencia),("@ahora",Ahora.UtcDateTime),
            ("@salto",(filtro.Pagina-1)*filtro.TamanoPagina),("@tamano",filtro.TamanoPagina));
        await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);var total=r.GetInt32(0);var porVerificar=r.GetInt32(1);var pinPorEnviar=r.GetInt32(2);await r.NextResultAsync(ct);
        var items=new List<object>();while(await r.ReadAsync(ct))items.Add(new{
            servicio=r.GetString(0),operacionId=r.GetString(1),encomiendaId=r.IsDBNull(2)?(int?)null:r.GetInt32(2),intentoId=r.IsDBNull(3)?(Guid?)null:r.GetGuid(3),
            usoId=r.IsDBNull(4)?(int?)null:r.GetInt32(4),rol=r.GetString(5),persona=r.GetString(6),telefono=r.IsDBNull(7)?null:r.GetString(7),
            consentimiento=r.GetString(8),finalidad=r.GetString(9),emision=r.GetString(10),entrega=r.GetString(11),estado=r.GetString(12),creadoUtc=Fecha(r,13),
            pagoEstado=r.GetString(14),verificadoUtc=r.IsDBNull(15)?(DateTimeOffset?)null:Fecha(r,15),responsable=r.IsDBNull(16)?null:r.GetString(16),referencia=r.GetString(17),
            entregadoUtc=r.IsDBNull(18)?(DateTimeOffset?)null:Fecha(r,18),claseReserva=r.GetString(19),tipoCasillero=r.GetString(20),minutos=r.IsDBNull(21)?null:r.GetString(21),
            solicitudPinDeposito=r.GetBoolean(22)});
        return new{solicitudes=items,total,porVerificar,pinPorEnviar,pagina=filtro.Pagina,tamanoPagina=filtro.TamanoPagina,totalPaginas=(int)Math.Ceiling((double)total/filtro.TamanoPagina)};
    }
}

