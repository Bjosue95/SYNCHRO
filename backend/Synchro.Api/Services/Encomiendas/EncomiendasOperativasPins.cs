using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

internal sealed record PinEncomiendaEmitido(Guid Id,string Pin,string Finalidad,string Telefono,DateTimeOffset VenceUtc)
{ public override string ToString()=>"PIN de encomienda de visualización temporal"; }

internal sealed partial class EncomiendasOperativasSql
{
    public async Task<object> EmitirPin(int id,string finalidadEntrada,bool reemplazar,int admin,CancellationToken ct)
    {
        var finalidad=Finalidad(finalidadEntrada);var rol=finalidad==FinalidadPinEncomienda.Deposito?"Remitente":"Destinatario";
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,null,ct);
        if(finalidad==FinalidadPinEncomienda.Deposito)Preparacion(o,Ahora);
        else Exigir(o.Estado=="Depositada","EstadoNoDisponible","El PIN de retiro se emite después del depósito.");
        Exigir(o.Pago=="ConfirmadoManual","PagoPendiente","Primero debe confirmarse el pago manual del remitente.");
        if(finalidad==FinalidadPinEncomienda.Deposito) {
            Exigir(await Consentimientos(c,tx,id,ct),"ConsentimientosPendientes","Faltan consentimientos de los participantes.");
            await using var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.ReferenciasEncomiendaV3 r JOIN dbo.ClientesWebPrototipo u ON u.PersonaId=r.PersonaId WHERE r.PersonaId=@persona AND r.RevalidarUtc>@ahora AND u.Activa=1",("@persona",o.RemitenteId),("@ahora",Ahora.UtcDateTime));
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"ReferenciaPendiente","El remitente debe registrar o revalidar su referencia facial.");
        }
        string telefono;
        await using(var q=Comando(c,tx,"SELECT Telefono FROM dbo.ParticipantesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE ReservaId=@id AND Rol=@rol AND Consentimiento=N'Confirmado' AND AceptaEncomienda=1",("@id",id),("@rol",rol)))
            telefono=(string?)await q.ExecuteScalarAsync(ct)??throw new OperacionEncomiendaException("NumeroNoAutorizado","No se puede entregar un PIN a un número sin autorización comprobada.");
        await using(var q=Comando(c,tx,"SELECT COUNT(*),MAX(CreadoUtc),SUM(CASE WHEN Activo=1 THEN 1 ELSE 0 END) FROM dbo.PinesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE ReservaId=@id AND Finalidad=@fin",("@id",id),("@fin",finalidad.ToString()))) {
            await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);var cantidad=r.GetInt32(0);
            Exigir(cantidad<4,"LimiteReemisiones","Se alcanzó el límite de reemisiones.",429);
            Exigir(cantidad==0||reemplazar,"PinExistente","Ya existe un PIN; la reemisión debe ser explícita.");
            Exigir(cantidad==0||Fecha(r,1).AddMinutes(2)<=Ahora,"ReemisionReciente","Espera antes de volver a emitir el PIN.",429);
        }
        var pin=CodigoPinRetiro.Generar();byte[] salt=CodigoPinRetiro.GenerarSalt();
        byte[]? otroHash=null,otroSalt=null;
        try {
            var otra=finalidad==FinalidadPinEncomienda.Deposito?FinalidadPinEncomienda.Retiro:FinalidadPinEncomienda.Deposito;
            await using(var q=Comando(c,tx,"SELECT TOP(1) PinHash,PinSalt FROM dbo.PinesEncomiendaOperativa WHERE ReservaId=@id AND Finalidad=@fin ORDER BY CreadoUtc DESC",("@id",id),("@fin",otra.ToString()))) {
                await using var r=await q.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)){otroHash=(byte[])r[0];otroSalt=(byte[])r[1];}
            }
            if(otroHash is not null&&otroSalt is not null) {
                for(var intento=0;intento<100;intento++) {
                    byte[] comparacion=CodigoPinRetiro.CalcularHashEncomienda(pin,id,otra==FinalidadPinEncomienda.Deposito?o.RemitenteId:o.DestinatarioId,otroSalt,config,otra);
                    bool igual;try{igual=CodigoPinRetiro.VerificarHash(comparacion,otroHash);}finally{CryptographicOperations.ZeroMemory(comparacion);}
                    if(!igual)break;
                    Exigir(intento<99,"PinNoDisponible","No fue posible generar PIN independientes.",503);pin=CodigoPinRetiro.Generar();
                }
            }
            byte[] hash=CodigoPinRetiro.CalcularHashEncomienda(pin,id,finalidad==FinalidadPinEncomienda.Deposito?o.RemitenteId:o.DestinatarioId,salt,config,finalidad);
            try {
                int minutos=config.GetValue("EncomiendasOperacion:PinVigenciaMinutos",30);Exigir(minutos is >=1 and <=1440,"ConfiguracionPin","Vigencia de PIN no válida.",503);
                var vence=Ahora.AddMinutes(minutos);var pinId=Guid.NewGuid();
                await using(var q=Comando(c,tx,"""
                    UPDATE dbo.PinesEncomiendaOperativa SET Activo=0,RevocadoUtc=@ahora WHERE ReservaId=@id AND Finalidad=@fin AND Activo=1;
                    INSERT dbo.PinesEncomiendaOperativa(Id,ReservaId,Finalidad,PinHash,PinSalt,CreadoUtc,VenceUtc,AdminEmisionId)
                    VALUES(@pinId,@id,@fin,@hash,@salt,@ahora,@vence,@admin);
                    """,("@ahora",Ahora.UtcDateTime),("@id",id),("@fin",finalidad.ToString()),("@pinId",pinId),("@hash",hash),("@salt",salt),("@vence",vence.UtcDateTime),("@admin",admin)))await q.ExecuteNonQueryAsync(ct);
                await Auditar(c,tx,id,null,"Administrador",admin,"Pin"+finalidad+"Emitido",o.Estado,"PIN generado para entrega manual al número autorizado; valor no incluido en auditoría.",ct,pinId);
                await Versionar(c,tx,id,ct);await tx.CommitAsync(ct);
                return new PinEncomiendaEmitido(pinId,pin,finalidad.ToString(),telefono,vence);
            } finally{CryptographicOperations.ZeroMemory(hash);}
        } finally{CryptographicOperations.ZeroMemory(salt);if(otroHash is not null)CryptographicOperations.ZeroMemory(otroHash);if(otroSalt is not null)CryptographicOperations.ZeroMemory(otroSalt);}
    }

    public async Task<object> ConfirmarEntregaPin(int id,EntregaPinEncomiendaSolicitud datos,int admin,CancellationToken ct)
    {
        Exigir(datos.EnvioComprobado,"EnvioNoComprobado","Confirma explícitamente la entrega manual del mensaje.",400);
        var referencia=Texto(datos.MensajeReferencia,160,"la referencia del envío");
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,null,ct);
        Exigir(o.Pago=="ConfirmadoManual","PagoPendiente","El pago no está confirmado.");
        string finalidadEntrega;
        await using(var q=Comando(c,tx,"SELECT Finalidad FROM dbo.PinesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE Id=@pin AND ReservaId=@id",("@pin",datos.PinId),("@id",id)))
            finalidadEntrega=(string?)await q.ExecuteScalarAsync(ct)??throw new OperacionEncomiendaException("PinNoDisponible","PIN no disponible.");
        if(finalidadEntrega=="Deposito") {
            Preparacion(o,Ahora);
            Exigir(await Consentimientos(c,tx,id,ct),"ConsentimientosPendientes","Faltan consentimientos de los participantes.");
            await using var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.ReferenciasEncomiendaV3 r JOIN dbo.ClientesWebPrototipo u ON u.PersonaId=r.PersonaId WHERE r.PersonaId=@persona AND r.RevalidarUtc>@ahora AND u.Activa=1",("@persona",o.RemitenteId),("@ahora",Ahora.UtcDateTime));
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"ReferenciaPendiente","La cuenta y la referencia facial deben estar vigentes.");
        } else Exigir(o.Estado=="Depositada","EstadoNoDisponible","La encomienda no permite entregar el PIN de retiro.");
        await using(var q=Comando(c,tx,"""
            UPDATE pin SET EntregadoUtc=@ahora,AdminEntregaId=@admin,MensajeReferencia=@referencia
            FROM dbo.PinesEncomiendaOperativa pin WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.ParticipantesEncomiendaOperativa p ON p.ReservaId=pin.ReservaId
              AND p.Rol=CASE pin.Finalidad WHEN N'Deposito' THEN N'Remitente' ELSE N'Destinatario' END
            WHERE pin.Id=@pin AND pin.ReservaId=@id AND pin.Activo=1 AND pin.ConsumidoUtc IS NULL
              AND pin.VenceUtc>@ahora AND pin.IntentosFallidos<5 AND pin.EntregadoUtc IS NULL
              AND p.Telefono=@telefono AND p.Consentimiento=N'Confirmado' AND p.AceptaEncomienda=1;
            """,("@ahora",Ahora.UtcDateTime),("@admin",admin),("@referencia",referencia),("@pin",datos.PinId),("@id",id),("@telefono",datos.Telefono.Trim())))
            Exigir(await q.ExecuteNonQueryAsync(ct)==1,"EntregaNoDisponible","El PIN no está vigente o el número no tiene autorización comprobada.");
        await Auditar(c,tx,id,null,"Administrador",admin,"PinEntregadoManual",o.Estado,"Entrega manual comprobada. Referencia: "+referencia,ct,datos.PinId);
        await Versionar(c,tx,id,ct);await tx.CommitAsync(ct);return new{entregaManualConfirmada=true};
    }

    private async Task<Guid> VerificarPin(SqlConnection c,SqlTransaction tx,Operacion o,FinalidadPinEncomienda finalidad,string valor,int terminal,CancellationToken ct)
    {
        Exigir(valor is {Length:6}&&valor.All(char.IsAsciiDigit),"PinInvalido","El PIN debe contener seis dígitos.",400);
        Guid id;byte[] hash,salt;int intentos;DateTimeOffset vence;bool entregado;
        await using(var q=Comando(c,tx,"SELECT Id,PinHash,PinSalt,IntentosFallidos,VenceUtc,EntregadoUtc FROM dbo.PinesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE ReservaId=@id AND Finalidad=@fin AND Activo=1 AND ConsumidoUtc IS NULL",("@id",o.Id),("@fin",finalidad.ToString()))) {
            await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"PinNoDisponible","PIN no disponible.");
            id=r.GetGuid(0);hash=(byte[])r[1];salt=(byte[])r[2];intentos=r.GetInt32(3);vence=Fecha(r,4);entregado=!r.IsDBNull(5);
        }
        try {
            Exigir(intentos<5&&vence>Ahora&&entregado,"PinNoDisponible","El PIN está vencido, bloqueado o no consta como entregado.");
            byte[] calculado=CodigoPinRetiro.CalcularHashEncomienda(valor,o.Id,finalidad==FinalidadPinEncomienda.Deposito?o.RemitenteId:o.DestinatarioId,salt,config,finalidad);
            bool correcto;try{correcto=CodigoPinRetiro.VerificarHash(calculado,hash);}finally{CryptographicOperations.ZeroMemory(calculado);}
            if(!correcto) {
                await using(var q=Comando(c,tx,"UPDATE dbo.PinesEncomiendaOperativa SET IntentosFallidos=IntentosFallidos+1 WHERE Id=@id AND IntentosFallidos<5",("@id",id)))await q.ExecuteNonQueryAsync(ct);
                await Auditar(c,tx,o.Id,null,terminal>0?"Terminal":"Cliente",terminal>0?terminal:o.RemitenteId,"PinRechazado",o.Estado,"Intento de PIN rechazado; valor omitido.",ct,id);
                // Conservar el contador de fallo incluso cuando se rechaza la operación HTTP.
                await tx.CommitAsync(ct);throw new OperacionEncomiendaException("PinNoValido","No se pudo validar el PIN.",403);
            }
            return id;
        }finally{CryptographicOperations.ZeroMemory(hash);CryptographicOperations.ZeroMemory(salt);}
    }
}
