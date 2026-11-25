using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

internal sealed record DepositoDemostrativoSolicitud(string Pin,CapturaEncomiendaSolicitud Captura,bool DepositoComprobado,string? CodigoCasillero=null)
{ public override string ToString()=>"Depósito con PIN y captura demostrativa protegidos"; }

internal sealed partial class EncomiendasOperativasSql
{
    public const string VersionCapturaDemostrativa="SYNCHRO-CAPTURA-DEMOSTRATIVA-2026-11";
    public async Task<object> DepositarDemostrativo(int id,int terminal,DepositoDemostrativoSolicitud datos,CancellationToken ct)
    {
        Exigir(datos.DepositoComprobado,"DepositoNoComprobado","Confirma el depósito desde la tablet.",400);
        var captura=datos.Captura??throw new OperacionEncomiendaException("CapturaInvalida","Se requiere la captura demostrativa de depósito.",400);
        Exigir(captura.Descriptor is null&&captura.VersionConsentimiento==VersionCapturaDemostrativa,"CapturaInvalida","Solo se admite una captura demostrativa, sin descriptor ni comparación facial.",400);
        byte[] png=LeerPng(captura,false);Guid solicitud;
        try {
            await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
            var o=await Leer(c,tx,id,null,ct);Preparacion(o,Ahora);
            Exigir(o.Pago=="ConfirmadoManual"&&await Consentimientos(c,tx,id,ct),"CondicionesPendientes","Faltan pago o consentimientos comprobados.");
            var reserva=await LeerReserva(c,tx,id,ct);var codigo=reserva?.CodigoCasillero??Texto(datos.CodigoCasillero,10,"el casillero");
            await ExigirCompartimento(c,tx,reserva,codigo,o.Tamano,ct);
            var pin=await VerificarPin(c,tx,o,FinalidadPinEncomienda.Deposito,datos.Pin,terminal,ct);
            var evidencia=Guid.NewGuid();solicitud=Guid.NewGuid();var vence=Ahora.AddMinutes(2);
            var protegida=ProtectorFoto(evidencia).Protect(png);
            try {
                await using var q=Comando(c,tx,"""
                    INSERT dbo.EvidenciasEncomiendaOperativa(Id,ReservaId,TerminalId,PersonaId,Tipo,VersionConsentimiento,EmitidaUtc,VenceUtc,ComparacionRemitenteAprobada,PinValidadoId)
                    VALUES(@ev,@id,@terminal,@persona,N'VerificacionRemitente',@version,@ahora,@vence,0,@pin);
                    INSERT dbo.FotografiasEncomiendaOperativa(EvidenciaId,FotografiaProtegida,EliminarUtc) VALUES(@ev,@foto,@eliminar);
                    INSERT dbo.SolicitudesDepositoEncomiendaOperativa(Id,TerminalId,ReservaId,EvidenciaId,PinId,CodigoCasillero,Estado,CreadaUtc,VenceUtc)
                    VALUES(@solicitud,@terminal,@id,@ev,@pin,@codigo,N'Autorizada',@ahora,@vence);
                    """,("@ev",evidencia),("@id",id),("@terminal",terminal),("@persona",o.RemitenteId),("@version",VersionCapturaDemostrativa),
                    ("@ahora",Ahora.UtcDateTime),("@vence",vence.UtcDateTime),("@pin",pin),("@foto",protegida),("@eliminar",Ahora.AddDays(30).UtcDateTime),("@solicitud",solicitud),("@codigo",codigo));
                await q.ExecuteNonQueryAsync(ct);
            } finally {CryptographicOperations.ZeroMemory(protegida);}
            await Auditar(c,tx,id,o.RemitenteId,"Terminal",terminal,"CapturaDepositoDemostrativa","PendienteDeposito","Captura demostrativa vinculada a un PIN válido. No se compararon rostros.",ct,pin,evidencia);
            await tx.CommitAsync(ct);
        } finally {CryptographicOperations.ZeroMemory(png);}
        return await ConfirmarDeposito(solicitud,terminal,true,ct);
    }
}
