using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

// Preferencia de pago vinculada a la reserva. Nunca incluye PAN, vencimiento ni CVV.
internal sealed record ConfirmacionPagoReservaDemoSolicitud(decimal ImporteHnl, string? JustificacionImporte, bool Comprobado);
internal sealed record SolicitudPagoReservaTablet(string Pin, string Metodo)
{
    public override string ToString() => "Solicitud de pago de reserva con PIN protegido";
}

internal sealed partial class EncomiendasOperativasSql
{
    private static bool PagoDiferidoDestinatario(ReservaPrototipo? reserva) =>
        reserva is { Servicio: "Encomienda", Pagador: "Destinatario", ModalidadPago: "Tablet" };

    private static bool PagoWebDemo(ReservaPrototipo? reserva) =>
        reserva is { Pagador: "Remitente", ModalidadPago: "WebTarjeta" };

    public async Task<object> CobrosReservaPendientes(CancellationToken ct)
    {
        await using var c = await Abrir(ct);
        await using var q = Comando(c, null, """
            SELECT TOP(80) o.ReservaId, r.TamanoSolicitado, o.Estado, o.PagoEstado,
                   r.FechaCreacion, a.Detalle,
                   (SELECT TOP(1) h.Detalle
                    FROM dbo.AuditoriaEncomiendaOperativa h
                    WHERE h.ReservaId=o.ReservaId AND h.Accion=N'PagoTabletPendiente'
                    ORDER BY h.Id DESC) AS MetodoTablet
            FROM dbo.EncomiendasOperativas o
            JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=o.ReservaId
            CROSS APPLY (
                SELECT TOP(1) h.Detalle
                FROM dbo.AuditoriaEncomiendaOperativa h
                WHERE h.ReservaId=o.ReservaId AND h.Accion=N'ReservaPrototipoCreada'
                ORDER BY h.Id DESC
            ) a
            WHERE o.PagoEstado<>N'ConfirmadoManual'
              AND (o.Estado IN(N'Preparacion',N'ListaParaDeposito')
                   AND r.FechaExpiracion>SYSUTCDATETIME()
                   OR o.Estado=N'Depositada')
            ORDER BY r.FechaCreacion DESC;
            """);
        var lista=new List<object>();
        await using var r=await q.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))
        {
            ReservaPrototipo? reserva;
            try {reserva=JsonSerializer.Deserialize<ReservaPrototipo>(r.GetString(5));}
            catch(JsonException) {continue;}
            if(reserva is null) continue;
            var web=PagoWebDemo(reserva);
            var terminal=PagoDiferidoDestinatario(reserva) && !r.IsDBNull(6);
            if(!web && !terminal)continue;
            var metodo=web ? "Tarjeta" : r.GetString(6);
            var importe=reserva.TarifaHnl ?? Tarifa(r.GetString(1));
            lista.Add(new {
                reservaId=r.GetInt32(0),
                codigoCasillero=reserva.CodigoCasillero,
                servicio=reserva.Servicio,
                tipoCasillero=reserva.TipoCasillero,
                pagador=reserva.Pagador,
                origen=web?"Web":"Tablet",
                metodoPago=metodo,
                importeHnl=importe,
                requiereImporteManual=!importe.HasValue,
                estado=r.GetString(2),
                creadoUtc=DateTime.SpecifyKind(r.GetDateTime(4),DateTimeKind.Utc),
                pagoSimulado=true
            });
        }
        return new { cobros=lista };
    }

    public async Task<object> ConfirmarPagoReservaDemo(int id, ConfirmacionPagoReservaDemoSolicitud datos, int admin, CancellationToken ct)
    {
        Exigir(datos.Comprobado,"PagoNoComprobado",
            "Confirma expresamente la operación de prueba antes de aprobarla.",400);
        await using var c=await Abrir(ct);
        var reserva=await LeerReserva(c,null,id,ct);
        Exigir(PagoWebDemo(reserva)||PagoDiferidoDestinatario(reserva),
            "CobroNoDisponible","La reserva no tiene un cobro de prueba pendiente.",400);
        string metodo;
        if(PagoWebDemo(reserva))metodo="Tarjeta";
        else {
            await using var q=Comando(c,null,"""
                SELECT TOP(1) Detalle FROM dbo.AuditoriaEncomiendaOperativa
                WHERE ReservaId=@id AND Accion=N'PagoTabletPendiente' ORDER BY Id DESC;
                """,("@id",id));
            metodo=(string?)await q.ExecuteScalarAsync(ct)??throw new OperacionEncomiendaException(
                "PagoTabletPendiente","El destinatario aún no ha solicitado el pago en la Tablet.");
            Exigir(metodo is "Efectivo" or "Tarjeta" or "NFC",
                "MetodoInvalido","La modalidad de la Tablet no es válida.",400);
            // La modalidad NFC es equivalente a Tarjeta para la confirmación manual.
            if(metodo=="NFC")metodo="Tarjeta";
        }
        var referencia=(PagoWebDemo(reserva)?"WEB-PRUEBA-":"TABLET-PRUEBA-")+id;
        return await ConfirmarPago(id,new(metodo,datos.ImporteHnl,referencia,true,datos.JustificacionImporte),admin,ct);
    }

    public async Task<object> EstadoPagoReservaTablet(int id,int terminal,CancellationToken ct)
    {
        await using var c=await Abrir(ct);
        var o=await Leer(c,null,id,null,ct);
        Exigir(o.Estado=="Depositada" && o.TerminalId==terminal && o.UsoId.HasValue,
            "ReservaNoDisponible","La reserva no corresponde a esta terminal.",404);
        var reserva=await LeerReserva(c,null,id,ct);
        Exigir(reserva is not null,"ReservaNoDisponible","Reserva no disponible.",404);
        return new { reservaId=id,casillero=reserva!.CodigoCasillero,
            pagoConfirmado=o.Pago=="ConfirmadoManual",
            pagoAdicionalRequerido=PagoDiferidoDestinatario(reserva)&&o.Pago!="ConfirmadoManual",
            importeHnl=reserva.TarifaHnl??Tarifa(o.Tamano) };
    }

    public async Task<object> SolicitarPagoReservaTablet(int id,int terminal,SolicitudPagoReservaTablet datos,CancellationToken ct)
    {
        Exigir(datos.Metodo is "Efectivo" or "Tarjeta" or "NFC",
            "MetodoInvalido","Selecciona una modalidad de pago de la Tablet.",400);
        Exigir(datos.Pin is {Length:6} && datos.Pin.All(char.IsAsciiDigit),
            "PinNoValido","El PIN no es válido.",403);
        await using var c=await Abrir(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        var o=await Leer(c,tx,id,null,ct);
        var reserva=await LeerReserva(c,tx,id,ct);
        Exigir(PagoDiferidoDestinatario(reserva) && o.Estado=="Depositada" && o.UsoId.HasValue &&
               o.TerminalId==terminal && o.Pago!="ConfirmadoManual",
            "CobroNoDisponible","Esta reserva no tiene un pago pendiente en esta terminal.");
        var pin=await VerificarPin(c,tx,o,FinalidadPinEncomienda.Retiro,datos.Pin,terminal,ct);
        var condiciones=await CondicionesPin(c,tx,id,pin,FinalidadPinEncomienda.Retiro,ct);
        Exigir(condiciones.EntregaManualConfirmada && !condiciones.Consumido &&
               !condiciones.Revocado && condiciones.VenceUtc>Ahora,
            "PinNoDisponible","El PIN de retiro ya no es válido.",403);
        await using(var q=Comando(c,tx,"""
            SELECT COUNT(*) FROM dbo.ParticipantesEncomiendaOperativa
            WHERE ReservaId=@id AND Rol=N'Destinatario' AND Consentimiento=N'Confirmado'
              AND AceptaEncomienda=1;
            """,("@id",id)))
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,
                "ConsentimientoPendiente","Falta la aceptación del destinatario.");
        await Auditar(c,tx,id,o.DestinatarioId,"Terminal",terminal,"PagoTabletPendiente",
            "Pendiente",datos.Metodo,ct);
        await Versionar(c,tx,id,ct);
        await tx.CommitAsync(ct);
        return new { pendiente=true, pagoConfirmado=false, metodoPago=datos.Metodo,
                     pagoRealProcesado=false, pagoSimulado=true };
    }
}
