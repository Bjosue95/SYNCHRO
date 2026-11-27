using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Services.Encomiendas;

internal sealed partial class EncomiendasOperativasSql
{
    internal const string VersionFacialPersonalWeb = "SYNCHRO-FACIAL-V3-2026-11";

    private static void ValidarCapturaPersonalWeb(CapturaEncomiendaSolicitud captura)
    {
        Exigir(captura.Modelo==DescriptorFacialV3.Modelo&&captura.VersionConsentimiento==VersionFacialPersonalWeb,
            "ConsentimientoV3Requerido","Autoriza y registra el rostro V3 para guardar tus pertenencias.",400);
        var normalizado=DescriptorFacialV3.Normalizar(captura.Descriptor);
        Exigir(normalizado is not null,"DescriptorInvalido","La referencia facial V3 no es válida.",400);
        Array.Clear(normalizado!);
    }

    private async Task RegistrarRostroPersonalWeb(SqlConnection c,SqlTransaction tx,Operacion o,int uso,double[] descriptor,int terminal,CancellationToken ct)
    {
        var normalizado=DescriptorFacialV3.Normalizar(descriptor);
        Exigir(normalizado is not null,"DescriptorInvalido","La referencia facial V3 no es válida.",400);
        byte[]? plano=null, cifrado=null;
        try
        {
            plano=JsonSerializer.SerializeToUtf8Bytes(normalizado);
            cifrado=proteccion.CreateProtector("SYNCHRO.RostroManual.V3.v1",uso.ToString(CultureInfo.InvariantCulture)).Protect(plano);
            await using var q=Comando(c,tx,"""
                INSERT dbo.RostrosRetiroPersonalV3(UsoId,PatronProtegido,Modelo,RegistradoUtc)
                    VALUES(@uso,@patron,@modelo,@ahora);
                INSERT dbo.ConsentimientosRostroPersonalV3(UsoId,VersionTexto,DeclaradoUtc,Canal)
                    VALUES(@uso,@version,@ahora,N'ReservaWeb');
                """,("@uso",uso),("@patron",cifrado),("@modelo",DescriptorFacialV3.Modelo),("@ahora",Ahora.UtcDateTime),("@version",VersionFacialPersonalWeb));
            await q.ExecuteNonQueryAsync(ct);
            await Auditar(c,tx,o.Id,o.RemitenteId,"Terminal",terminal,"RostroPersonalV3Registrado","Depositada",
                "Referencia V3 cifrada por uso; el PIN original se conserva hasta el retiro.",ct);
        }
        finally
        {
            Array.Clear(normalizado!);
            if(plano is not null)CryptographicOperations.ZeroMemory(plano);
            if(cifrado is not null)CryptographicOperations.ZeroMemory(cifrado);
        }
    }

    private const string ContextoRetiroWeb = "SYNCHRO.ReservaPersonal.ConfirmarRetiro.v1";
    private sealed record PermisoRetiroWeb(int ReservaId,int UsoId,Guid PinId,string Casillero);

    // Solo se invoca con los candidatos del token facial validado por el servidor.
    internal async Task<object?> AutorizarRetiroPersonalWeb(int[] usos,string pin,bool coincidePresencial,CancellationToken ct)
    {
        if(!config.GetValue<bool>("EncomiendasOperacion:Habilitado")||!config.GetValue<bool>("RostroV3:RetiroHabilitado"))return null;
        Exigir(pin is {Length:6}&&pin.All(char.IsAsciiDigit),"PinInvalido","No se pudo autorizar el retiro.",400);
        await using var c=await Abrir(ct);
        var candidatos=new List<int>();var coincidencias=new List<int>();
        foreach(int uso in usos)
        {
            await using var q=Comando(c,null,"""
                SELECT o.ReservaId,r.ClientePersonaId,p.PinHash,p.PinSalt
                FROM dbo.EncomiendasOperativas o
                JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=o.ReservaId
                JOIN dbo.UsosCasillero u ON u.Id=o.UsoId
                JOIN dbo.Terminales t ON t.Id=o.TerminalId AND t.Activa=1
                JOIN dbo.RostrosRetiroPersonalV3 v ON v.UsoId=u.Id
                JOIN dbo.ConsentimientosRostroPersonalV3 cv ON cv.UsoId=u.Id AND cv.VersionTexto=@version
                JOIN dbo.PinesEncomiendaOperativa p ON p.ReservaId=o.ReservaId
                WHERE u.Id=@uso AND u.OrigenReserva=N'Web' AND u.TipoServicio=N'ResguardoPersonal'
                    AND u.Estado=N'Ocupado' AND u.FechaRetiro IS NULL
                    AND o.Estado=N'Depositada' AND o.PagoEstado=N'ConfirmadoManual'
                    AND o.DestinatarioId=r.ClientePersonaId AND v.Modelo=@modelo
                    AND p.Finalidad=N'Deposito' AND p.Activo=1 AND p.ConsumidoUtc IS NULL
                    AND p.EntregadoUtc IS NOT NULL AND p.IntentosFallidos<5;
                """,("@uso",uso),("@version",VersionFacialPersonalWeb),("@modelo",DescriptorFacialV3.Modelo));
            await using var r=await q.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct))continue;
            int id=r.GetInt32(0),persona=r.GetInt32(1);candidatos.Add(id);
            byte[] hash=(byte[])r[2],salt=(byte[])r[3];byte[]? calculado=null;
            try
            {
                calculado=CodigoPinRetiro.CalcularHashEncomienda(pin,id,persona,salt,config,FinalidadPinEncomienda.Deposito);
                if(CodigoPinRetiro.VerificarHash(calculado,hash))coincidencias.Add(id);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);CryptographicOperations.ZeroMemory(salt);
                if(calculado is not null)CryptographicOperations.ZeroMemory(calculado);
            }
        }
        Exigir(coincidencias.Count<=1&&!(coincidePresencial&&coincidencias.Count>0),
            "PinAmbiguo","No se pudo autorizar el retiro con los datos proporcionados.",403);
        if(coincidencias.Count==0)
        {
            if(!coincidePresencial&&usos.Length==1&&candidatos.Count==1)
            {
                await using var fallo=(SqlTransaction)await c.BeginTransactionAsync(ct);
                var o=await Leer(c,fallo,candidatos[0],null,ct);
                Exigir(o.Estado=="Depositada"&&o.TerminalId.HasValue,"RetiroNoDisponible","No se pudo autorizar el retiro.",403);
                await VerificarPin(c,fallo,o,FinalidadPinEncomienda.Deposito,pin,o.TerminalId!.Value,ct,true);
                throw new OperacionEncomiendaException("RetiroNoDisponible","Repite la verificación facial.",403);
            }
            return null;
        }
        int idReserva=coincidencias[0];
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        var op=await Leer(c,tx,idReserva,null,ct);
        Exigir(op.Estado=="Depositada"&&op.Pago=="ConfirmadoManual"&&op.UsoId.HasValue&&op.TerminalId.HasValue&&
            usos.Contains(op.UsoId.Value)&&op.RemitenteId==op.DestinatarioId&&
            (await LeerReserva(c,tx,idReserva,ct))?.Servicio=="Personal",
            "RetiroNoDisponible","La reserva no permite este retiro.",403);
        var pinId=await VerificarPin(c,tx,op,FinalidadPinEncomienda.Deposito,pin,op.TerminalId!.Value,ct,true);
        string codigo;
        await using(var q=Comando(c,tx,"""
            SELECT u.CodigoCasillero FROM dbo.UsosCasillero u WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.RostrosRetiroPersonalV3 v ON v.UsoId=u.Id
            JOIN dbo.ConsentimientosRostroPersonalV3 cv ON cv.UsoId=u.Id AND cv.VersionTexto=@version
            JOIN dbo.Terminales t ON t.Id=@terminal AND t.Activa=1
            WHERE u.Id=@uso AND u.OrigenReserva=N'Web' AND u.TipoServicio=N'ResguardoPersonal'
              AND u.Estado=N'Ocupado' AND u.FechaRetiro IS NULL AND v.Modelo=@modelo;
            """,("@uso",op.UsoId),("@terminal",op.TerminalId),("@version",VersionFacialPersonalWeb),("@modelo",DescriptorFacialV3.Modelo)))
            codigo=(string?)await q.ExecuteScalarAsync(ct)??throw new OperacionEncomiendaException("RostroV3Requerido","No existe un registro facial V3 vigente para esta reserva.",403);
        var permiso=new PermisoRetiroWeb(idReserva,op.UsoId!.Value,pinId,codigo);
        string autorizacion=proteccion.CreateProtector(ContextoRetiroWeb).ToTimeLimitedDataProtector()
            .Protect(JsonSerializer.Serialize(permiso),TimeSpan.FromMinutes(10));
        await Auditar(c,tx,idReserva,op.RemitenteId,"Terminal",op.TerminalId!.Value,"RetiroPersonalAutorizado","Depositada",
            "Rostro V3 y PIN comprobados. Pendiente confirmación de retiro.",ct,pinId);
        await tx.CommitAsync(ct);
        return new { retiroAutorizado=true,retiroConfirmado=false,pagoPendiente=false,pagoConfirmado=true,pinValidado=true,
            codigoCasillero=codigo,estadoCasillero="Ocupado",origenReserva="Web",autorizacionRetiro=autorizacion,
            vigenciaSegundos=600,hardwareSimulado=true };
    }

    internal async Task<object> ConfirmarRetiroPersonalWeb(string? autorizacion,bool retiroComprobado,CancellationToken ct)
    {
        Exigir(config.GetValue<bool>("EncomiendasOperacion:Habilitado")&&config.GetValue<bool>("RostroV3:RetiroHabilitado"),
            "RetiroNoDisponible","Retiro no disponible.",403);
        Exigir(retiroComprobado,"RetiroNoComprobado","Confirma que retiraste tus pertenencias.",400);
        Exigir(autorizacion is {Length:>=32 and <=4096},"AutorizacionInvalida","Repite la verificación facial y el PIN.",403);
        PermisoRetiroWeb permiso;
        try {
            var texto=proteccion.CreateProtector(ContextoRetiroWeb).ToTimeLimitedDataProtector().Unprotect(autorizacion!);
            permiso=JsonSerializer.Deserialize<PermisoRetiroWeb>(texto)??throw new JsonException();
        } catch(Exception ex) when(ex is CryptographicException or JsonException or FormatException) {
            throw new OperacionEncomiendaException("AutorizacionVencida","La autorización venció o no es válida. Repite la verificación facial y el PIN.",403);
        }
        int idReserva=permiso.ReservaId;
        Guid pinId=permiso.PinId;
        string codigo=permiso.Casillero;
        await using var c=await Abrir(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        var op=await Leer(c,tx,idReserva,null,ct);
        Exigir(op.UsoId==permiso.UsoId&&op.TerminalId.HasValue&&op.Pago=="ConfirmadoManual"&&op.RemitenteId==op.DestinatarioId&&
            (await LeerReserva(c,tx,idReserva,ct))?.Servicio=="Personal","RetiroNoDisponible","La reserva no permite este retiro.",403);
        if(op.Estado=="Retirada") {
            await using var anterior=Comando(c,tx,"SELECT COUNT(*) FROM dbo.AuditoriaEncomiendaOperativa WHERE ReservaId=@id AND Accion=N'EntregaConfirmada' AND PinId=@pin",("@id",idReserva),("@pin",pinId));
            Exigir(Convert.ToInt32(await anterior.ExecuteScalarAsync(ct))==1,"RetiroNoDisponible","La operación ya fue cerrada.",409);
            await tx.CommitAsync(ct);
            return new { retiroConfirmado=true,codigoCasillero=codigo,estadoCasillero="Disponible",origenReserva="Web",idempotente=true };
        }
        Exigir(op.Estado=="Depositada","RetiroNoDisponible","La reserva no está depositada.",409);
        await using(var q=Comando(c,tx,"""
            SELECT COUNT(*) FROM dbo.PinesEncomiendaOperativa p WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.UsosCasillero u WITH(UPDLOCK,HOLDLOCK) ON u.Id=@uso
            JOIN dbo.RostrosRetiroPersonalV3 v ON v.UsoId=u.Id AND v.Modelo=@modelo
            JOIN dbo.ConsentimientosRostroPersonalV3 cv ON cv.UsoId=u.Id AND cv.VersionTexto=@version
            JOIN dbo.Terminales t ON t.Id=@terminal AND t.Activa=1
            WHERE p.Id=@pin AND p.ReservaId=@id AND p.Finalidad=N'Deposito'
              AND p.Activo=1 AND p.ConsumidoUtc IS NULL AND p.RevocadoUtc IS NULL
              AND p.EntregadoUtc IS NOT NULL AND p.IntentosFallidos<5
              AND u.Estado=N'Ocupado' AND u.FechaRetiro IS NULL AND u.CodigoCasillero=@codigo
              AND u.OrigenReserva=N'Web' AND u.TipoServicio=N'ResguardoPersonal';
            """,("@pin",pinId),("@id",idReserva),("@uso",permiso.UsoId),("@codigo",codigo),("@terminal",op.TerminalId),
              ("@modelo",DescriptorFacialV3.Modelo),("@version",VersionFacialPersonalWeb)))
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"RetiroNoDisponible","La autorización ya no corresponde a una reserva activa.",403);
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.Casilleros SET Estado=N'Disponible' WHERE Codigo=@codigo AND Estado=N'Ocupado'
              AND NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK)
                WHERE CodigoCasillero=@codigo AND Id<>@uso AND FechaRetiro IS NULL AND Estado IN(N'Reservado',N'Ocupado'));
            """,("@codigo",codigo),("@uso",op.UsoId)))
            Exigir(await q.ExecuteNonQueryAsync(ct)==1,"CasilleroNoDisponible","No se pudo liberar el casillero.",409);
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.UsosCasillero SET Estado=N'Finalizado',FechaRetiro=@ahora WHERE Id=@uso;
            UPDATE dbo.EncomiendasOperativas SET Estado=N'Retirada',FechaEntregaUtc=@ahora,Version=Version+1 WHERE ReservaId=@id;
            UPDATE dbo.PreReservasEncomiendasWeb SET Estado=N'Retirada' WHERE Id=@id;
            UPDATE dbo.PinesEncomiendaOperativa SET Activo=0,ConsumidoUtc=@ahora WHERE Id=@pin;
            DELETE dbo.RostrosRetiroPersonalV3 WHERE UsoId=@uso;
            """,("@ahora",Ahora.UtcDateTime),("@uso",op.UsoId),("@id",idReserva),("@pin",pinId)))
            await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,idReserva,op.RemitenteId,"Terminal",op.TerminalId!.Value,"EntregaConfirmada","Retirada",
            "Retiro web con coincidencia V3 y PIN original; pago previo. Referencia V3 eliminada.",ct,pinId);
        await tx.CommitAsync(ct);
        return new { retiroConfirmado=true,codigoCasillero=codigo,estadoCasillero="Disponible",origenReserva="Web",hardwareSimulado=true };
    }
}
