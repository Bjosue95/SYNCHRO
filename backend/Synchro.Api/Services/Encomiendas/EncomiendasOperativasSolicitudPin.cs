using System.Data;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

// El cliente solo registra una solicitud. La emision y entrega del PIN
// continúan siendo operaciones exclusivamente administrativas.
internal sealed partial class EncomiendasOperativasSql
{
    public async Task<object> SolicitarPinDeposito(int id,int persona,CancellationToken ct)
    {
        await using var c=await Abrir(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        var o=await Leer(c,tx,id,persona,ct);
        Preparacion(o,Ahora);
        Exigir(o.Pago=="ConfirmadoManual"||PagoDiferidoDestinatario(await LeerReserva(c,tx,id,ct)),
            "PagoPendiente","El pago debe estar confirmado antes de solicitar el PIN, salvo pago diferido del destinatario.");
        var cuenta=await LeerConsentimientoCuenta(c,tx,persona,ct);
        Exigir(cuenta.Activa&&(cuenta.Estado=="Confirmado"||await Consentimientos(c,tx,id,ct)),
            "WhatsAppPendiente","Debes tener autorizado tu WhatsApp antes de solicitar el PIN.");
        await using(var pin=Comando(c,tx,"SELECT TOP(1) Id FROM dbo.PinesEncomiendaOperativa WHERE ReservaId=@id AND Finalidad=N'Deposito' AND Activo=1 AND ConsumidoUtc IS NULL",("@id",id)))
            Exigir(await pin.ExecuteScalarAsync(ct) is null,"PinYaEmitido","Ya se emitió un PIN de depósito; consulta el estado de tu reserva.");

        bool existente;
        await using(var consulta=Comando(c,tx,"SELECT TOP(1) Id FROM dbo.AuditoriaEncomiendaOperativa WHERE ReservaId=@id AND Accion=N'PinDepositoSolicitado' ORDER BY Id DESC",("@id",id)))
            existente=await consulta.ExecuteScalarAsync(ct) is not null;
        if(!existente)
            await Auditar(c,tx,id,persona,"Cliente",persona,"PinDepositoSolicitado","Pendiente",
                "El titular solicitó la emisión del PIN de depósito; pendiente de gestión administrativa.",ct);
        await tx.CommitAsync(ct);
        return new {reservaId=id,solicitudRegistrada=true,yaExistia=existente};
    }
}
