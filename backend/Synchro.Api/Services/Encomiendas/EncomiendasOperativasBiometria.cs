using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Services.Encomiendas;

internal sealed partial class EncomiendasOperativasSql
{
    public const string VersionConsentimientoRetiro="SYNCHRO-ENCOMIENDAS-EVIDENCIA-2026-11";
    private IDataProtector ProtectorReferencia(int persona)=>proteccion.CreateProtector("SYNCHRO.Encomiendas.ReferenciaV3.v1",persona.ToString(CultureInfo.InvariantCulture));
    private IDataProtector ProtectorFoto(Guid evidencia)=>proteccion.CreateProtector("SYNCHRO.Encomiendas.FotografiaRetiro.v1",evidencia.ToString("N"));

    private static byte[] LeerPng(CapturaEncomiendaSolicitud datos,bool referencia)
    {
        ArgumentNullException.ThrowIfNull(datos);
        Exigir(datos.VersionConsentimiento==(referencia?VersionConsentimiento:VersionConsentimientoRetiro)||!referencia&&datos.VersionConsentimiento==VersionCapturaDemostrativa,"ConsentimientoCaptura","Falta el consentimiento de captura vigente.",400);
        Exigir(!string.IsNullOrEmpty(datos.ImagenPng)&&datos.ImagenPng.Length<=1400000,"CapturaInvalida","La captura PNG no es válida o excede el límite.",400);
        Exigir(referencia?datos.Modelo==DescriptorFacialV3.Modelo:datos.Descriptor is null,"ModeloInvalido","Modelo facial no compatible con la finalidad.",400);
        byte[] imagen;
        try{imagen=Convert.FromBase64String(datos.ImagenPng);}catch(FormatException){throw new OperacionEncomiendaException("CapturaInvalida","La captura PNG no es válida.",400);}
        var patron=PatronVisualSimulado.Extraer(imagen);
        if(patron is null){CryptographicOperations.ZeroMemory(imagen);throw new OperacionEncomiendaException("CapturaInvalida","La captura PNG no tiene calidad o formato suficientes.",400);}
        Array.Clear(patron);return imagen;
    }

    private async Task<object> EstadoReferencia(SqlConnection c,int persona,CancellationToken ct)
    {
        await using var q=Comando(c,null,"SELECT ReferenciaId,Modelo,ConsentidoUtc,RevalidarUtc FROM dbo.ReferenciasEncomiendaV3 WHERE PersonaId=@persona",("@persona",persona));
        await using var r=await q.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return new{registrada=false,vigente=false,modelo=DescriptorFacialV3.Modelo};
        return new{registrada=true,vigente=Fecha(r,3)>Ahora,referenciaId=r.GetGuid(0),modelo=r.GetString(1),consentidoUtc=Fecha(r,2),revalidarUtc=Fecha(r,3)};
    }
    public async Task<object> ConsultarReferencia(int persona,CancellationToken ct)
    { await using var c=await Abrir(ct);return await EstadoReferencia(c,persona,ct); }

    public async Task<object> RegistrarReferencia(int persona,CapturaEncomiendaSolicitud datos,CancellationToken ct)
    {
        byte[] png=LeerPng(datos,true);double[]? descriptor=null;byte[]? plano=null,cifrado=null;
        try {
            descriptor=DescriptorFacialV3.Normalizar(datos.Descriptor);
            Exigir(descriptor is not null,"DescriptorInvalido","La referencia debe contener los 1024 valores válidos del modelo V3.",400);
            plano=JsonSerializer.SerializeToUtf8Bytes(descriptor);cifrado=ProtectorReferencia(persona).Protect(plano);
            await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
            await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.ClientesWebPrototipo WITH(UPDLOCK,HOLDLOCK) WHERE PersonaId=@persona AND Activa=1",("@persona",persona)))
                Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"CuentaNoDisponible","La cuenta no está disponible.",403);
            var referenciaId=Guid.NewGuid();var vence=Ahora.AddYears(1);
            await using(var q=Comando(c,tx,"""
                UPDATE dbo.ReferenciasEncomiendaV3 WITH(UPDLOCK,HOLDLOCK) SET ReferenciaId=@ref,DescriptorProtegido=@descriptor,
                  Modelo=@modelo,VersionConsentimiento=@consentimiento,ConsentidoUtc=@ahora,RevalidarUtc=@vence WHERE PersonaId=@persona;
                IF @@ROWCOUNT=0 INSERT dbo.ReferenciasEncomiendaV3(PersonaId,ReferenciaId,DescriptorProtegido,Modelo,VersionConsentimiento,ConsentidoUtc,RevalidarUtc)
                 VALUES(@persona,@ref,@descriptor,@modelo,@consentimiento,@ahora,@vence);
                """,("@ref",referenciaId),("@descriptor",cifrado),("@modelo",DescriptorFacialV3.Modelo),("@consentimiento",VersionConsentimiento),("@ahora",Ahora.UtcDateTime),("@vence",vence.UtcDateTime),("@persona",persona)))await q.ExecuteNonQueryAsync(ct);
            await Auditar(c,tx,null,persona,"Cliente",persona,"ReferenciaV3Registrada","Vigente","Referencia cifrada y consentimiento con revalidación anual. No se conserva fotografía del remitente.",ct);
            await tx.CommitAsync(ct);return new{registrada=true,vigente=true,referenciaId,revalidarUtc=vence};
        }finally{CryptographicOperations.ZeroMemory(png);if(descriptor is not null)Array.Clear(descriptor);if(datos.Descriptor is not null)Array.Clear(datos.Descriptor);if(plano is not null)CryptographicOperations.ZeroMemory(plano);if(cifrado is not null)CryptographicOperations.ZeroMemory(cifrado);}
    }

    public async Task<object> RevocarReferencia(int persona,CancellationToken ct)
    {
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        await using(var q=Comando(c,tx,"""
            DELETE FROM dbo.ReferenciasEncomiendaV3 WHERE PersonaId=@persona;
            UPDATE dbo.EvidenciasEncomiendaOperativa SET ConsumidaUtc=@ahora
              WHERE PersonaId=@persona AND Tipo=N'VerificacionRemitente' AND ConsumidaUtc IS NULL;
            """,("@persona",persona),("@ahora",Ahora.UtcDateTime)))await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,null,persona,"Cliente",persona,"ConsentimientoBiometricoRevocado","Eliminada","Plantilla activa eliminada; metadatos de auditoría conservados sin descriptor.",ct);
        await tx.CommitAsync(ct);return new{referenciaEliminada=true};
    }

    public async Task<object> AutorizarDeposito(int id,int persona,AutorizarDepositoEncomiendaSolicitud datos,CancellationToken ct)
    {
        byte[] png=LeerPng(datos.Captura,true);double[]? muestra=null,referencia=null;byte[]? plano=null;
        try {
            muestra=DescriptorFacialV3.Normalizar(datos.Captura.Descriptor);Exigir(muestra is not null,"DescriptorInvalido","Descriptor V3 no válido.",400);
            await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,persona,ct);Preparacion(o,Ahora);
            Exigir(await LeerReserva(c,tx,id,ct) is null,"DepositoEnTablet","Esta reserva se deposita en la tablet con PIN y captura demostrativa; no requiere verificar el rostro en la web.");
            Exigir(o.Pago=="ConfirmadoManual"&&await Consentimientos(c,tx,id,ct),"CondicionesPendientes","Faltan pago o consentimientos comprobados.");
            int terminal;string casillero;DateTimeOffset solicitudVence;
            await using(var q=Comando(c,tx,"""
                SELECT s.TerminalId,s.CodigoCasillero,s.VenceUtc FROM dbo.SolicitudesDepositoEncomiendaOperativa s WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.Terminales t ON t.Id=s.TerminalId AND t.Activa=1
                JOIN dbo.Casilleros ca ON ca.Codigo=s.CodigoCasillero AND ca.Estado=N'Disponible' AND ca.Tipo=N'Normal' AND ca.Tamano=@tamano
                WHERE s.Id=@solicitud AND s.Estado=N'Pendiente' AND s.VenceUtc>@ahora;
                """,("@solicitud",datos.SolicitudId),("@tamano",o.Tamano),("@ahora",Ahora.UtcDateTime))) {
                await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"SolicitudNoDisponible","La solicitud de estación venció o no tiene disponibilidad.");
                terminal=r.GetInt32(0);casillero=r.GetString(1);solicitudVence=Fecha(r,2);
            }
            var pinId=await VerificarPin(c,tx,o,FinalidadPinEncomienda.Deposito,datos.Pin,0,ct);Guid referenciaId;
            await using(var q=Comando(c,tx,"""
                SELECT v.ReferenciaId,v.DescriptorProtegido FROM dbo.ReferenciasEncomiendaV3 v WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.ClientesWebPrototipo cli ON cli.PersonaId=v.PersonaId AND cli.Activa=1
                WHERE v.PersonaId=@persona AND v.Modelo=@modelo AND v.RevalidarUtc>@ahora;
                """,("@persona",persona),("@modelo",DescriptorFacialV3.Modelo),("@ahora",Ahora.UtcDateTime))) {
                await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"ReferenciaNoVigente","Registra o revalida tu referencia facial.");
                referenciaId=r.GetGuid(0);var cifrado=(byte[])r[1];try{plano=ProtectorReferencia(persona).Unprotect(cifrado);}finally{CryptographicOperations.ZeroMemory(cifrado);}
            }
            referencia=JsonSerializer.Deserialize<double[]>(plano!);
            double umbral=config.GetValue("RostroV3:UmbralDistanciaCoseno",.22d);
            Exigir(double.IsFinite(umbral)&&umbral>0&&umbral<=.5,"ConfiguracionFacial","Umbral V3 no configurado correctamente.",503);
            var distancia=DescriptorFacialV3.DistanciaCoseno(referencia,muestra);
            if(!distancia.HasValue||distancia.Value>umbral) {
                await Auditar(c,tx,id,persona,"Cliente",persona,"VerificacionRemitenteRechazada",o.Estado,"La comparación V3 del servidor no autorizó el depósito.",ct);
                await tx.CommitAsync(ct);throw new OperacionEncomiendaException("IdentidadNoVerificada","No se pudo verificar el rostro del remitente.",403);
            }
            var evidencia=Guid.NewGuid();var vence=Ahora.AddMinutes(2);if(solicitudVence<vence)vence=solicitudVence;
            await using(var q=Comando(c,tx,"""
                INSERT dbo.EvidenciasEncomiendaOperativa(Id,ReservaId,TerminalId,PersonaId,Tipo,VersionConsentimiento,EmitidaUtc,VenceUtc,ReferenciaId,ComparacionRemitenteAprobada,PinValidadoId)
                VALUES(@ev,@id,@terminal,@persona,N'VerificacionRemitente',@version,@ahora,@vence,@ref,1,@pin);
                UPDATE dbo.SolicitudesDepositoEncomiendaOperativa SET ReservaId=@id,EvidenciaId=@ev,PinId=@pin,Estado=N'Autorizada',VenceUtc=@vence WHERE Id=@solicitud;
                """,("@ev",evidencia),("@id",id),("@terminal",terminal),("@persona",persona),("@version",VersionConsentimiento),("@ahora",Ahora.UtcDateTime),("@vence",vence.UtcDateTime),("@ref",referenciaId),("@pin",pinId),("@solicitud",datos.SolicitudId)))await q.ExecuteNonQueryAsync(ct);
            await Auditar(c,tx,id,persona,"Cliente",persona,"DepositoAutorizadoWeb",o.Estado,"Identidad y PIN verificados; la web no abre el casillero.",ct,pinId,evidencia);
            await Versionar(c,tx,id,ct);await tx.CommitAsync(ct);return new{autorizada=true,solicitudId=datos.SolicitudId,venceUtc=vence,casillero,aperturaRealizada=false,hardwareSimulado=true};
        }finally{CryptographicOperations.ZeroMemory(png);if(muestra is not null)Array.Clear(muestra);if(referencia is not null)Array.Clear(referencia);if(datos.Captura.Descriptor is not null)Array.Clear(datos.Captura.Descriptor);if(plano is not null)CryptographicOperations.ZeroMemory(plano);}
    }

    public async Task<object> CapturarDestinatario(int id,int terminal,CapturaEncomiendaSolicitud datos,CancellationToken ct)
    {
        byte[] png=LeerPng(datos,false);byte[]? cifrado=null;
        try {
            await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,null,ct);
            Exigir(o.Estado=="Depositada"&&o.TerminalId==terminal,"RetiroNoDisponible","La reserva no está depositada en esta terminal.");
            var idEv=Guid.NewGuid();var vence=Ahora.AddMinutes(2);cifrado=ProtectorFoto(idEv).Protect(png);
            await using(var q=Comando(c,tx,"""
                INSERT dbo.EvidenciasEncomiendaOperativa(Id,ReservaId,TerminalId,PersonaId,Tipo,VersionConsentimiento,EmitidaUtc,VenceUtc,ComparacionRemitenteAprobada)
                VALUES(@ev,@id,@terminal,@persona,N'CapturaDestinatario',@version,@ahora,@vence,0);
                INSERT dbo.FotografiasEncomiendaOperativa(EvidenciaId,FotografiaProtegida,EliminarUtc) VALUES(@ev,@foto,@vence);
                """,("@ev",idEv),("@id",id),("@terminal",terminal),("@persona",o.DestinatarioId),("@version",datos.VersionConsentimiento),("@ahora",Ahora.UtcDateTime),("@vence",vence.UtcDateTime),("@foto",cifrado)))await q.ExecuteNonQueryAsync(ct);
            await Auditar(c,tx,id,o.DestinatarioId,"Terminal",terminal,"EvidenciaRetiroCapturada",o.Estado,"Captura consentida como evidencia; no se comprobó coincidencia biométrica.",ct,evidencia:idEv);
            await tx.CommitAsync(ct);return new{evidenciaId=idEv,venceUtc=vence,coincidenciaBiometrica=(bool?)null,evidenciaDeRetiro=true};
        }finally{CryptographicOperations.ZeroMemory(png);if(cifrado is not null)CryptographicOperations.ZeroMemory(cifrado);}
    }

    public async Task<byte[]> ConsultarFotografia(int id,Guid evidencia,int admin,string motivo,CancellationToken ct)
    {
        motivo=Texto(motivo,500,"el motivo del acceso a la evidencia");
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        byte[] cifrado;
        await using(var q=Comando(c,tx,"""
            SELECT f.FotografiaProtegida FROM dbo.FotografiasEncomiendaOperativa f JOIN dbo.EvidenciasEncomiendaOperativa e ON e.Id=f.EvidenciaId
            WHERE e.Id=@ev AND e.ReservaId=@id AND e.ConsumidaUtc IS NOT NULL AND (f.EliminarUtc>@ahora OR f.ExtensionHastaUtc>@ahora);
            """,("@ev",evidencia),("@id",id),("@ahora",Ahora.UtcDateTime)))
            cifrado=(byte[]?)await q.ExecuteScalarAsync(ct)??throw new OperacionEncomiendaException("FotoNoDisponible","La fotografía no está disponible o terminó su conservación.",404);
        try {
            var foto=ProtectorFoto(evidencia).Unprotect(cifrado);
            try {await Auditar(c,tx,id,null,"Administrador",admin,"EvidenciaConsultada","Consulta","Acceso restringido a fotografía. Motivo: "+motivo,ct,evidencia:evidencia);await tx.CommitAsync(ct);return foto;}
            catch{CryptographicOperations.ZeroMemory(foto);throw;}
        }finally{CryptographicOperations.ZeroMemory(cifrado);}
    }

    public async Task<object> ExtenderConservacion(int id,Guid evidencia,int admin,string incidencia,string motivo,DateTimeOffset hasta,CancellationToken ct)
    {
        incidencia=Texto(incidencia,160,"la incidencia formal");motivo=Texto(motivo,500,"la justificación de conservación");
        Exigir(hasta>Ahora&&hasta<=Ahora.AddDays(30),"PlazoInvalido","Cada prórroga debe tener fecha límite dentro de los próximos 30 días.",400);
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        await using(var q=Comando(c,tx,"""
            UPDATE f SET IncidenciaReferencia=@incidencia,MotivoExtension=@motivo,ExtensionHastaUtc=@hasta,ExtensionAdminId=@admin
            FROM dbo.FotografiasEncomiendaOperativa f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.EvidenciasEncomiendaOperativa e ON e.Id=f.EvidenciaId
            WHERE e.ReservaId=@id AND e.Id=@ev AND f.EntregaUtc IS NOT NULL AND (f.EliminarUtc>@ahora OR f.ExtensionHastaUtc>@ahora)
              AND @hasta>f.EliminarUtc AND (f.ExtensionHastaUtc IS NULL OR @hasta>f.ExtensionHastaUtc);
            """,("@incidencia",incidencia),("@motivo",motivo),("@hasta",hasta.UtcDateTime),("@admin",admin),("@id",id),("@ev",evidencia),("@ahora",Ahora.UtcDateTime)))
            Exigir(await q.ExecuteNonQueryAsync(ct)==1,"ExtensionNoDisponible","La evidencia no está disponible o el plazo no amplía su conservación.");
        await Auditar(c,tx,id,null,"Administrador",admin,"ConservacionExtendida","Incidencia",$"Incidencia {incidencia}. Hasta {hasta:O}. Motivo: {motivo}".Substring(0,Math.Min(500,$"Incidencia {incidencia}. Hasta {hasta:O}. Motivo: {motivo}".Length)),ct,evidencia:evidencia);
        await tx.CommitAsync(ct);return new{conservacionHastaUtc=hasta};
    }

    public async Task LimpiarBiometria(CancellationToken ct)
    {
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        // Solo material biométrico del módulo nuevo; conservar todos los metadatos operativos.
        await using(var q=Comando(c,tx,"""
            INSERT dbo.AuditoriaEncomiendaOperativa(ReservaId,PersonaId,ActorTipo,ActorId,Accion,Estado,Detalle,EvidenciaId)
             SELECT e.ReservaId,e.PersonaId,N'Sistema',0,N'FotografiaEliminada',N'Eliminada',N'Plazo de conservación cumplido.',e.Id
             FROM dbo.FotografiasEncomiendaOperativa f WITH(UPDLOCK,HOLDLOCK) JOIN dbo.EvidenciasEncomiendaOperativa e ON e.Id=f.EvidenciaId
             WHERE f.EliminarUtc<=@ahora AND (f.ExtensionHastaUtc IS NULL OR f.ExtensionHastaUtc<=@ahora);
            DELETE FROM dbo.FotografiasEncomiendaOperativa WHERE EliminarUtc<=@ahora AND (ExtensionHastaUtc IS NULL OR ExtensionHastaUtc<=@ahora);
            INSERT dbo.AuditoriaEncomiendaOperativa(PersonaId,ActorTipo,ActorId,Accion,Estado,Detalle)
             SELECT v.PersonaId,N'Sistema',0,N'ReferenciaV3Eliminada',N'Eliminada',N'Revalidación anual vencida o cuenta inactiva.'
             FROM dbo.ReferenciasEncomiendaV3 v WITH(UPDLOCK,HOLDLOCK) JOIN dbo.ClientesWebPrototipo cli ON cli.PersonaId=v.PersonaId
             WHERE v.RevalidarUtc<=@ahora OR cli.Activa=0;
            DELETE v FROM dbo.ReferenciasEncomiendaV3 v JOIN dbo.ClientesWebPrototipo cli ON cli.PersonaId=v.PersonaId WHERE v.RevalidarUtc<=@ahora OR cli.Activa=0;
            """,("@ahora",Ahora.UtcDateTime)))await q.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
