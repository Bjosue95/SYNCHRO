using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

internal sealed partial class EncomiendasOperativasSql
{
    public async Task<object> ConsultarParaRetiro(int id,int terminal,CancellationToken ct)
    {
        await using var c=await Abrir(ct);var o=await Leer(c,null,id,null,ct);
        Exigir(o.TerminalId==terminal&&o.Estado is "Depositada" or "Retirada","NoEncontrada","Encomienda no disponible en esta terminal.",404);
        Exigir(o.Pago=="ConfirmadoManual","PagoPendiente","La encomienda no tiene un pago confirmado.");
        await using var q=Comando(c,null,"SELECT CodigoCasillero FROM dbo.UsosCasillero WHERE Id=@uso",("@uso",o.UsoId));
        return new{encomiendaId=id,estado=o.Estado,tamano=o.Tamano,casillero=(string?)await q.ExecuteScalarAsync(ct),
            pagoConfirmado=true,pagoAdicionalRequerido=false,capturaComoEvidencia=true,
            versionConsentimientoRetiro=VersionConsentimientoRetiro,conservacionFotografiaDias=30,hardwareSimulado=true};
    }

    public async Task ValidarTerminal(int id,CancellationToken ct)
    {
        await using var c=await Abrir(ct);await using var q=Comando(c,null,"SELECT COUNT(*) FROM dbo.Terminales WHERE Id=@id AND Activa=1",("@id",id));
        Exigir(id>0&&Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"TerminalNoAutorizada","La terminal no está activa.",403);
    }
    public async Task<object> CrearSolicitudDeposito(int terminal,string codigo,CancellationToken ct)
    {
        codigo=Texto(codigo,10,"el casillero").ToUpperInvariant();
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.Terminales WHERE Id=@id AND Activa=1",("@id",terminal)))
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"TerminalNoAutorizada","Terminal no disponible.",403);
        await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.Casilleros WHERE Codigo=@codigo AND Tipo=N'Normal' AND Estado=N'Disponible'",("@codigo",codigo)))
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"CasilleroNoDisponible","Casillero no disponible.");
        var id=Guid.NewGuid();var vence=Ahora.AddMinutes(2);
        await using(var q=Comando(c,tx,"INSERT dbo.SolicitudesDepositoEncomiendaOperativa(Id,TerminalId,CodigoCasillero,CreadaUtc,VenceUtc) VALUES(@id,@terminal,@codigo,@ahora,@vence)",("@id",id),("@terminal",terminal),("@codigo",codigo),("@ahora",Ahora.UtcDateTime),("@vence",vence.UtcDateTime)))await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,null,null,"Terminal",terminal,"SolicitudDepositoCreada","Pendiente","Solicitud temporal para autorización desde la cuenta del remitente.",ct);
        await tx.CommitAsync(ct);return new{solicitudId=id,venceUtc=vence,casillero=codigo,aperturaRealizada=false};
    }

    public async Task<object> ConsultarSolicitudDeposito(Guid id,int terminal,CancellationToken ct)
    {
        await using var c=await Abrir(ct);await using var q=Comando(c,null,"""
            SELECT s.Estado,s.VenceUtc,s.CodigoCasillero,s.ReservaId FROM dbo.SolicitudesDepositoEncomiendaOperativa s
            JOIN dbo.Terminales t ON t.Id=s.TerminalId AND t.Activa=1 WHERE s.Id=@id AND s.TerminalId=@terminal
            """,("@id",id),("@terminal",terminal));
        await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"SolicitudNoEncontrada","Solicitud no encontrada.",404);
        return new{solicitudId=id,estado=Fecha(r,1)<=Ahora&&r.GetString(0)!="Depositada"?"Vencida":r.GetString(0),venceUtc=Fecha(r,1),casillero=r.GetString(2),encomiendaId=r.IsDBNull(3)?(int?)null:r.GetInt32(3),hardwareSimulado=true};
    }

    private async Task<CondicionesEncomiendaOperativa> Condiciones(SqlConnection c,SqlTransaction tx,Operacion o,CancellationToken ct)
    {
        bool activa;await using(var q=Comando(c,tx,"SELECT Activa FROM dbo.ClientesWebPrototipo WITH(UPDLOCK,HOLDLOCK) WHERE PersonaId=@id",("@id",o.RemitenteId)))activa=await q.ExecuteScalarAsync(ct) is true;
        var remitente=EstadoConsentimientoEncomienda.Pendiente;var destinatario=EstadoConsentimientoEncomienda.Pendiente;bool acepta=false;
        await using(var q=Comando(c,tx,"SELECT Rol,Consentimiento,AceptaEncomienda FROM dbo.ParticipantesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE ReservaId=@id",("@id",o.Id))) {
            await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)) {
                var estado=Enum.Parse<EstadoConsentimientoEncomienda>(r.GetString(1));
                if(r.GetString(0)=="Remitente")remitente=estado;else{destinatario=estado;acepta=r.GetBoolean(2);}
            }
        }
        return new(o.Id,o.RemitenteId,o.DestinatarioId,Enum.Parse<EstadoEncomiendaOperativa>(o.Estado),activa,remitente,destinatario,acepta,
            o.Pago=="ConfirmadoManual"?EstadoPagoEncomienda.ConfirmadoManual:EstadoPagoEncomienda.Pendiente,o.Vence);
    }
    private async Task<CondicionesPinEncomiendaOperativa> CondicionesPin(SqlConnection c,SqlTransaction tx,int reserva,Guid id,FinalidadPinEncomienda finalidad,CancellationToken ct)
    {
        await using var q=Comando(c,tx,"SELECT Finalidad,Activo,IntentosFallidos,VenceUtc,ConsumidoUtc,RevocadoUtc,EntregadoUtc FROM dbo.PinesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND ReservaId=@reserva",("@id",id),("@reserva",reserva));
        await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"PinNoDisponible","PIN no disponible.");
        return new(Enum.Parse<FinalidadPinEncomienda>(r.GetString(0)),r.GetBoolean(1)&&r.GetString(0)==finalidad.ToString(),!r.IsDBNull(6),!r.IsDBNull(4),!r.IsDBNull(5),r.GetInt32(2),5,Fecha(r,3));
    }
    private async Task<EvidenciaEncomiendaOperativa> Evidencia(SqlConnection c,SqlTransaction tx,Guid id,CancellationToken ct)
    {
        await using var q=Comando(c,tx,"SELECT Id,ReservaId,TerminalId,PersonaId,Tipo,EmitidaUtc,VenceUtc,ConsumidaUtc,VersionConsentimiento,ComparacionRemitenteAprobada FROM dbo.EvidenciasEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",("@id",id));
        await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"EvidenciaNoDisponible","Evidencia no disponible.");var tipo=Enum.Parse<TipoEvidenciaEncomienda>(r.GetString(4));
        return new(r.GetGuid(0),r.GetInt32(1),r.GetInt32(2),r.GetInt32(3),tipo,Fecha(r,5),Fecha(r,6),!r.IsDBNull(7),r.GetString(8)==(tipo==TipoEvidenciaEncomienda.VerificacionRemitente?VersionConsentimiento:VersionConsentimientoRetiro),r.GetBoolean(9));
    }

    public async Task<object> ConfirmarDeposito(Guid solicitud,int terminal,bool comprobado,CancellationToken ct)
    {
        Exigir(comprobado,"DepositoNoComprobado","La terminal debe confirmar expresamente el depósito.",400);
        await using var c=await Abrir(ct);
        int id;
        // Descubrir el vínculo antes de la transacción; después bloquear operación -> solicitud,
        // igual que AutorizarDeposito, y comprobar nuevamente el vínculo bajo bloqueo.
        await using(var q=Comando(c,null,"SELECT ReservaId FROM dbo.SolicitudesDepositoEncomiendaOperativa WHERE Id=@id AND TerminalId=@terminal AND ReservaId IS NOT NULL",("@id",solicitud),("@terminal",terminal)))
            id=await q.ExecuteScalarAsync(ct) is int reserva?reserva:throw new OperacionEncomiendaException("SolicitudNoDisponible","Solicitud no autorizada para esta terminal.");
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        var o=await Leer(c,tx,id,null,ct);
        Guid evidenciaId,pinId;string codigo,estado;DateTimeOffset vence;
        await using(var q=Comando(c,tx,"SELECT ReservaId,EvidenciaId,PinId,CodigoCasillero,Estado,VenceUtc FROM dbo.SolicitudesDepositoEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND TerminalId=@terminal AND ReservaId=@reserva",("@id",solicitud),("@terminal",terminal),("@reserva",id))) {
            await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"SolicitudNoDisponible","Solicitud no autorizada para esta terminal.");
            id=r.GetInt32(0);evidenciaId=r.GetGuid(1);pinId=r.GetGuid(2);codigo=r.GetString(3);estado=r.GetString(4);vence=Fecha(r,5);
        }
        if(estado=="Depositada"&&o.UsoId.HasValue){await tx.CommitAsync(ct);return new{depositoConfirmado=true,usoId=o.UsoId,casillero=codigo,hardwareSimulado=true};}
        Exigir(estado=="Autorizada"&&vence>Ahora,"SolicitudVencida","La autorización de depósito no está vigente.");
        bool activa,disponible;
        await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.Terminales WHERE Id=@id AND Activa=1",("@id",terminal)))activa=Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1;
        await using(var q=Comando(c,tx,"""
            SELECT COUNT(*) FROM dbo.Casilleros ca WITH(UPDLOCK,HOLDLOCK) WHERE ca.Codigo=@codigo AND ca.Estado=N'Disponible'
             AND ca.Tipo=N'Normal' AND ca.Tamano=@tamano AND NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero u WITH(UPDLOCK,HOLDLOCK)
                WHERE u.CodigoCasillero=ca.Codigo AND u.FechaRetiro IS NULL AND u.Estado IN(N'Reservado',N'Ocupado'))
            """,("@codigo",codigo),("@tamano",o.Tamano)))disponible=Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1;
        await using(var q=Comando(c,tx,"""
            SELECT COUNT(*) FROM dbo.EvidenciasEncomiendaOperativa e JOIN dbo.ReferenciasEncomiendaV3 v
             ON v.ReferenciaId=e.ReferenciaId AND v.PersonaId=e.PersonaId AND v.RevalidarUtc>@ahora
            WHERE e.Id=@id AND e.PinValidadoId=@pin
            """,("@ahora",Ahora.UtcDateTime),("@id",evidenciaId),("@pin",pinId)))
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"ReferenciaNoVigente","La referencia o PIN vinculados ya no son válidos.");
        var decision=PoliticaEncomiendaOperativa.Depositar(await Condiciones(c,tx,o,ct),await CondicionesPin(c,tx,id,pinId,FinalidadPinEncomienda.Deposito,ct),await Evidencia(c,tx,evidenciaId,ct),terminal,activa,true,disponible,Ahora);
        Exigir(decision.Permitida,decision.Codigo,"No se cumplen las condiciones para depositar.");
        int uso;
        await using(var q=Comando(c,tx,"""
            INSERT dbo.UsosCasillero(CodigoCasillero,TipoServicio,FechaReserva,FechaIngreso,Estado,OrigenReserva)
            OUTPUT INSERTED.Id VALUES(@codigo,N'Encomienda',@ahora,@ahora,N'Ocupado',N'Web')
            """,("@codigo",codigo),("@ahora",Ahora.UtcDateTime)))uso=Convert.ToInt32(await q.ExecuteScalarAsync(ct));
        await using(var q=Comando(c,tx,"""
            INSERT dbo.ParticipantesUso(UsoId,PersonaId,Rol) VALUES(@uso,@remitente,N'Remitente'),(@uso,@destinatario,N'Destinatario');
            UPDATE dbo.Casilleros SET Estado=N'Ocupado' WHERE Codigo=@codigo;
            UPDATE dbo.EncomiendasOperativas SET UsoId=@uso,TerminalId=@terminal,Estado=N'Depositada',FechaDepositoUtc=@ahora,Version=Version+1 WHERE ReservaId=@id;
            UPDATE dbo.PreReservasEncomiendasWeb SET Estado=N'Convertida' WHERE Id=@id;
            UPDATE dbo.PinesEncomiendaOperativa SET Activo=0,ConsumidoUtc=@ahora WHERE Id=@pin;
            UPDATE dbo.EvidenciasEncomiendaOperativa SET ConsumidaUtc=@ahora WHERE Id=@ev;
            UPDATE dbo.SolicitudesDepositoEncomiendaOperativa SET Estado=N'Depositada' WHERE Id=@solicitud;
            """,("@uso",uso),("@remitente",o.RemitenteId),("@destinatario",o.DestinatarioId),("@codigo",codigo),("@terminal",terminal),("@ahora",Ahora.UtcDateTime),("@id",id),("@pin",pinId),("@ev",evidenciaId),("@solicitud",solicitud)))await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,id,null,"Terminal",terminal,"DepositoConfirmado","Depositada","Depósito confirmado en terminal; apertura física simulada.",ct,pinId,evidenciaId);
        await tx.CommitAsync(ct);return new{depositoConfirmado=true,usoId=uso,casillero=codigo,hardwareSimulado=true};
    }

    public async Task<object> Retirar(int id,int terminal,RetiroOperativoSolicitud datos,CancellationToken ct)
    {
        Exigir(datos.EntregaComprobada,"EntregaNoComprobada","La terminal debe confirmar expresamente la entrega.",400);
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,null,ct);
        Exigir(o.Estado=="Depositada"&&o.TerminalId==terminal&&o.UsoId.HasValue,"RetiroNoDisponible","Retiro no disponible para esta terminal.");
        var pin=await VerificarPin(c,tx,o,FinalidadPinEncomienda.Retiro,datos.Pin,terminal,ct);
        bool activa;await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.Terminales WHERE Id=@id AND Activa=1",("@id",terminal)))activa=Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1;
        var decision=PoliticaEncomiendaOperativa.Retirar(await Condiciones(c,tx,o,ct),await CondicionesPin(c,tx,id,pin,FinalidadPinEncomienda.Retiro,ct),await Evidencia(c,tx,datos.EvidenciaId,ct),terminal,activa,true,Ahora);
        Exigir(decision.Permitida,decision.Codigo,"No se cumplen las condiciones para retirar.");
        string codigo;await using(var q=Comando(c,tx,"SELECT CodigoCasillero FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND TipoServicio=N'Encomienda' AND Estado=N'Ocupado' AND FechaRetiro IS NULL",("@id",o.UsoId)))
            codigo=(string?)await q.ExecuteScalarAsync(ct)??throw new OperacionEncomiendaException("UsoNoDisponible","El casillero no tiene una entrega pendiente.");
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.FotografiasEncomiendaOperativa SET EntregaUtc=@ahora,EliminarUtc=@eliminar
             WHERE EvidenciaId=@ev AND EliminarUtc>@ahora;
            """,("@ahora",Ahora.UtcDateTime),("@eliminar",Ahora.AddDays(30).UtcDateTime),("@ev",datos.EvidenciaId)))
            Exigir(await q.ExecuteNonQueryAsync(ct)==1,"FotografiaNoDisponible","La evidencia de entrega no está disponible.");
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.Casilleros SET Estado=N'Disponible' WHERE Codigo=@codigo AND Estado=N'Ocupado'
             AND NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK) WHERE CodigoCasillero=@codigo AND Id<>@uso AND FechaRetiro IS NULL AND Estado IN(N'Reservado',N'Ocupado'));
            """,("@codigo",codigo),("@uso",o.UsoId)))Exigir(await q.ExecuteNonQueryAsync(ct)==1,"CasilleroNoDisponible","No fue posible liberar el casillero de forma segura.");
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.UsosCasillero SET Estado=N'Finalizado',FechaRetiro=@ahora WHERE Id=@uso;
            UPDATE dbo.EncomiendasOperativas SET Estado=N'Retirada',FechaEntregaUtc=@ahora,Version=Version+1 WHERE ReservaId=@id;
            UPDATE dbo.PreReservasEncomiendasWeb SET Estado=N'Retirada' WHERE Id=@id;
            UPDATE dbo.PinesEncomiendaOperativa SET Activo=0,ConsumidoUtc=@ahora WHERE Id=@pin;
            UPDATE dbo.EvidenciasEncomiendaOperativa SET ConsumidaUtc=@ahora,PinValidadoId=@pin WHERE Id=@ev;
            """,("@ahora",Ahora.UtcDateTime),("@uso",o.UsoId),("@id",id),("@pin",pin),("@ev",datos.EvidenciaId)))await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,id,o.DestinatarioId,"Terminal",terminal,"EntregaConfirmada","Retirada","PIN de retiro consumido; evidencia ligada a casillero y entrega. Sin cobro adicional; apertura física simulada.",ct,pin,datos.EvidenciaId);
        await tx.CommitAsync(ct);return new{retiroConfirmado=true,pagoAdicionalRequerido=false,casillero=codigo,hardwareSimulado=true};
    }
}
