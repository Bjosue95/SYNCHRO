using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Synchro.Api.Services.Clientes;
using Synchro.Api.Services.Encomiendas;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Tests;

// Escribe únicamente fixtures propios con identificadores únicos en la instancia expresamente autorizada.
// No aplica migraciones, no borra registros existentes y conserva el historial de las pruebas.
[Collection("SQL Deposito Aislado")]
public sealed partial class EncomiendasOperativasSqlTests
{
    private const string Cadena=@"Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False;Connect Timeout=5";
    // Credencial efímera de fixture; no guardar una contraseña reutilizable en Git.
    private static readonly string PasswordPrueba=$"SYNCHRO-Fixture-{Guid.NewGuid():N}!";
    private static IConfiguration Configuracion => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["ConnectionStrings:SynchroDB"]=Cadena,["EncomiendasOperacion:Habilitado"]="true",
        ["WhatsAppManual:NumeroE164"]="50433182642",
        ["RetiroPin:ClaveHmac"]=Convert.ToBase64String(Enumerable.Range(1,32).Select(i=>(byte)i).ToArray()),
        // Importes ficticios exclusivos del fixture; no se escriben en configuración operativa.
        ["EncomiendasOperacion:Tarifas:Pequeno"]="11",["EncomiendasOperacion:Tarifas:Mediano"]="22",["EncomiendasOperacion:Tarifas:Grande"]="33"
    }).Build();
    private static JsonElement Json(object o)=>JsonSerializer.SerializeToElement(o,new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static CapturaEncomiendaSolicitud Captura(bool destinatario=false,bool distinto=false)=>new(
        Convert.ToBase64String(RostroSimuladoTests.Png()),destinatario?null:Enumerable.Range(0,1024).Select(i=>Math.Sin(i*.17)*(distinto?-1:1)).ToArray(),
        destinatario?"SYNCHRO-CAPTURA-EVIDENCIA-RETIRO-v1":DescriptorFacialV3.Modelo,
        destinatario?EncomiendasOperativasSql.VersionConsentimientoRetiro:EncomiendasOperativasSql.VersionConsentimiento,PasswordPrueba);

    private sealed record Fixture(int Persona,int Admin,int Terminal,string Casillero,string Usuario,EncomiendasOperativasSql Servicio,IDataProtectionProvider Proteccion);
    private static async Task<Fixture> Preparar()
    {
        var tag=Guid.NewGuid().ToString("N");var usuario="encomienda_test_"+tag;
        var cuentas=new CuentasClientesWeb(new RepositorioClientesWebSql(Configuracion),new PasswordHasher<CuentaClienteWeb>());
        var cuenta=await cuentas.RegistrarAsync(usuario,PasswordPrueba,"+999123456701",CancellationToken.None);
        await using var c=new SqlConnection(Cadena);await c.OpenAsync();Assert.Equal("SynchroDB_Pruebas",c.Database);
        await using var q=new SqlCommand("""
            IF OBJECT_ID(N'dbo.EncomiendasOperativas') IS NULL THROW 59110,'Revisar/aplicar primero la migración aditiva.',1;
            INSERT dbo.Administradores(NombreUsuario,PasswordHash,Activo) OUTPUT INSERTED.Id VALUES(@nombre,N'FIXTURE_NO_LOGIN',1);
            INSERT dbo.Terminales(Codigo,Nombre,Activa) OUTPUT INSERTED.Id VALUES(@terminal,N'Fixture encomiendas: no hardware',1);
            INSERT dbo.Casilleros(Codigo,Tipo,Tamano,Estado) VALUES(@casillero,N'Normal',N'Mediano',N'Disponible');
            """,c);
        var codigo="ET"+tag[..7].ToUpperInvariant();q.Parameters.AddWithValue("@nombre",usuario);q.Parameters.AddWithValue("@terminal","ET-"+tag[..18]);q.Parameters.AddWithValue("@casillero",codigo);
        int admin,terminal;await using(var r=await q.ExecuteReaderAsync()){Assert.True(await r.ReadAsync());admin=r.GetInt32(0);Assert.True(await r.NextResultAsync());Assert.True(await r.ReadAsync());terminal=r.GetInt32(0);}
        var protector=new EphemeralDataProtectionProvider();
        return new(cuenta.PersonaId,admin,terminal,codigo,usuario,new(Configuracion,protector,TimeProvider.System),protector);
    }
    private static async Task Consentir(Fixture f,int id,string rol)
    {
        var d=Json(await f.Servicio.Detalle(id,f.Persona,CancellationToken.None)).GetProperty(rol.ToLowerInvariant());
        await f.Servicio.ConfirmarConsentimiento(id,rol,new(d.GetProperty("telefono").GetString()!,d.GetProperty("codigo").GetString()??"","WA-TEST-"+Guid.NewGuid().ToString("N"),true,true,d.GetProperty("texto").GetString()),f.Admin,false,CancellationToken.None);
    }
    private static async Task<JsonElement> EmitirEntregar(Fixture f,int id,string finalidad)
    {
        var p=Json(await f.Servicio.EmitirPin(id,finalidad,false,f.Admin,CancellationToken.None));
        var telefonoEquivocado=finalidad=="Deposito"?"+999123456702":"+999123456701";
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>f.Servicio.ConfirmarEntregaPin(id,new(p.GetProperty("id").GetGuid(),telefonoEquivocado,"ENVIO-INCORRECTO",true),f.Admin,CancellationToken.None));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>f.Servicio.ConfirmarEntregaPin(id,new(p.GetProperty("id").GetGuid(),p.GetProperty("telefono").GetString()!,"ENVIO-NO-COMPROBADO",false),f.Admin,CancellationToken.None));
        await f.Servicio.ConfirmarEntregaPin(id,new(p.GetProperty("id").GetGuid(),p.GetProperty("telefono").GetString()!,"PIN-ENVIO-TEST-"+Guid.NewGuid().ToString("N"),true),f.Admin,CancellationToken.None);
        return p;
    }

    [Fact]
    public async Task FlujoSqlRealManualProtegePagoConsentimientosPinesEvidenciaYEntrega()
    {
        var f=await Preparar();var s=f.Servicio;var ct=CancellationToken.None;
        int id=Json(await s.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        Assert.Throws<ArgumentException>(()=>Synchro.Api.Services.PreReservaEncomiendaService.Validar(f.Persona,"incorrecto","Mediano"));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Detalle(id,f.Persona+100000,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.EmitirPin(id,"Deposito",false,f.Admin,ct));
        var pago=new PagoEncomiendaSolicitud("Efectivo",22,"RECIBO-TEST-"+Guid.NewGuid().ToString("N"),true,null);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarPago(id,pago,f.Admin,ct));
        await Consentir(f,id,"Remitente");
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>Consentir(f,id,"Destinatario"));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarPago(id,pago with{ImporteHnl=0},f.Admin,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarPago(id,pago with{Metodo="EfectivoSimulado"},f.Admin,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarPago(id,pago with{PagoComprobado=false},f.Admin,ct));
        await s.ConfirmarPago(id,pago,f.Admin,ct);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarPago(id,pago,f.Admin,ct));
        await s.RegistrarReferencia(f.Persona,Captura(),ct);
        var referencia=Json(await s.ConsultarReferencia(f.Persona,ct));Assert.True(referencia.GetProperty("vigente").GetBoolean());
        Assert.True(referencia.GetProperty("revalidarUtc").GetDateTimeOffset()>DateTimeOffset.UtcNow.AddDays(364));
        var deposito=await EmitirEntregar(f,id,"Deposito");
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.EmitirPin(id,"Retiro",false,f.Admin,ct));
        var solicitud=Json(await s.CrearSolicitudDeposito(f.Terminal,f.Casillero,ct)).GetProperty("solicitudId").GetGuid();
        var pinDeposito=deposito.GetProperty("pin").GetString()!;
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.AutorizarDeposito(id,f.Persona,new(solicitud,"000000"==pinDeposito?"000001":"000000",Captura()),ct));
        await using(var c=new SqlConnection(Cadena)){await c.OpenAsync();await using var q=new SqlCommand("SELECT IntentosFallidos FROM dbo.PinesEncomiendaOperativa WHERE Id=@id",c);q.Parameters.AddWithValue("@id",deposito.GetProperty("id").GetGuid());Assert.Equal(1,Convert.ToInt32(await q.ExecuteScalarAsync()));}
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.AutorizarDeposito(id,f.Persona,new(solicitud,pinDeposito,Captura(distinto:true)),ct));
        var autorizada=Json(await s.AutorizarDeposito(id,f.Persona,new(solicitud,pinDeposito,Captura()),ct));Assert.False(autorizada.GetProperty("aperturaRealizada").GetBoolean());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarDeposito(solicitud,f.Terminal,false,ct));
        var concurrentes=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>s.ConfirmarDeposito(solicitud,f.Terminal,true,ct)));
        var dep=Json(concurrentes[0]);Assert.True(dep.GetProperty("depositoConfirmado").GetBoolean());
        Assert.Equal(dep.GetProperty("usoId").GetInt32(),Json(concurrentes[1]).GetProperty("usoId").GetInt32());
        Assert.Equal(dep.GetProperty("usoId").GetInt32(),Json(await s.ConfirmarDeposito(solicitud,f.Terminal,true,ct)).GetProperty("usoId").GetInt32());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.EmitirPin(id,"Retiro",false,f.Admin,ct));
        await Consentir(f,id,"Destinatario");
        Assert.False(Json(await s.ConsultarParaRetiro(id,f.Terminal,ct)).GetProperty("pagoAdicionalRequerido").GetBoolean());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConsultarParaRetiro(id,f.Terminal+100000,ct));
        var retiro=await EmitirEntregar(f,id,"Retiro");Assert.NotEqual(pinDeposito,retiro.GetProperty("pin").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.IdentificarParaRetiro(pinDeposito,f.Terminal,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.IdentificarParaRetiro(retiro.GetProperty("pin").GetString()!,f.Terminal+100000,ct));
        Assert.Equal(id,Json(await s.IdentificarParaRetiro(retiro.GetProperty("pin").GetString()!,f.Terminal,ct)).GetProperty("encomiendaId").GetInt32());
        var ev=Json(await s.CapturarDestinatario(id,f.Terminal,Captura(destinatario:true),ct));Assert.Equal(JsonValueKind.Null,ev.GetProperty("coincidenciaBiometrica").ValueKind);
        var evId=ev.GetProperty("evidenciaId").GetGuid();
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConsultarFotografia(id,evId,f.Admin,"Fixture antes de entregar",ct));
        var entrega=Json(await s.Retirar(id,f.Terminal,new(evId,retiro.GetProperty("pin").GetString()!,true),ct));
        Assert.True(entrega.GetProperty("retiroConfirmado").GetBoolean());Assert.False(entrega.GetProperty("pagoAdicionalRequerido").GetBoolean());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Retirar(id,f.Terminal,new(evId,retiro.GetProperty("pin").GetString()!,true),ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.IdentificarParaRetiro(retiro.GetProperty("pin").GetString()!,f.Terminal,ct));
        Assert.Equal(RostroSimuladoTests.Png(),await s.ConsultarFotografia(id,evId,f.Admin,"Fixture revisión autorizada",ct));
        var detalle=Json(await s.Detalle(id,f.Persona,ct));Assert.Equal("Retirada",detalle.GetProperty("estado").GetString());
        Assert.All(detalle.GetProperty("pines").EnumerateArray(),p=>Assert.Equal("Consumido",p.GetProperty("estado").GetString()));
        Assert.True(detalle.GetProperty("historial").GetArrayLength()>=12);
        await using(var c=new SqlConnection(Cadena)) {
            await c.OpenAsync();await using var q=new SqlCommand("SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo; SELECT FotografiaProtegida,EliminarUtc,EntregaUtc FROM dbo.FotografiasEncomiendaOperativa WHERE EvidenciaId=@ev",c);
            q.Parameters.AddWithValue("@codigo",f.Casillero);q.Parameters.AddWithValue("@ev",evId);
            await using var r=await q.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.Equal("Disponible",r.GetString(0));
            Assert.True(await r.NextResultAsync());Assert.True(await r.ReadAsync());Assert.NotEqual(RostroSimuladoTests.Png(),(byte[])r[0]);Assert.Equal(TimeSpan.FromDays(30),r.GetDateTime(1)-r.GetDateTime(2));
        }
        await s.RevocarReferencia(f.Persona,ct);Assert.False(Json(await s.ConsultarReferencia(f.Persona,ct)).GetProperty("registrada").GetBoolean());
        // Probar conservación con reloj controlado sin alterar fechas o material de otros usuarios.
        var futura=new EncomiendasOperativasSql(Configuracion,f.Proteccion,new RelojPrueba(DateTimeOffset.UtcNow.AddDays(2)));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>futura.ExtenderConservacion(id,evId,f.Admin,"","Sin incidencia",DateTimeOffset.UtcNow.AddDays(31),ct));
        await futura.ExtenderConservacion(id,evId,f.Admin,"INCIDENTE-FIXTURE-"+id,"Incidencia ficticia aislada",DateTimeOffset.UtcNow.AddDays(31),ct);
        await using(var c=new SqlConnection(Cadena)) {
            await c.OpenAsync();await using var q=new SqlCommand("UPDATE dbo.FotografiasEncomiendaOperativa SET EliminarUtc=DATEADD(MINUTE,-1,SYSUTCDATETIME()),ExtensionHastaUtc=DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE EvidenciaId=@ev; SELECT COUNT(*) FROM dbo.AuditoriaEncomiendaOperativa WHERE ReservaId=@id",c);
            q.Parameters.AddWithValue("@ev",evId);q.Parameters.AddWithValue("@id",id);Assert.True(Convert.ToInt32(await q.ExecuteScalarAsync())>=12);
        }
        // El limpiador real se ejecuta con la hora actual: solo expira la fotografía propia ya vencida.
        await s.LimpiarBiometria(ct);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConsultarFotografia(id,evId,f.Admin,"Fixture expirado",ct));
        Assert.Equal("Retirada",Json(await s.Detalle(id,f.Persona,ct)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task RechazoVencimientoYLimiteDePinNoSePuedenSaltar()
    {
        var f=await Preparar();var s=f.Servicio;var ct=CancellationToken.None;
        int rechazo=Json(await s.Crear(f.Persona,"+999123456703","Mediano",ct)).GetProperty("id").GetInt32();
        var d=Json(await s.Detalle(rechazo,f.Persona,ct)).GetProperty("destinatario");
        var rechazoAnticipado=await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(rechazo,"Destinatario",new(d.GetProperty("telefono").GetString()!,d.GetProperty("codigo").GetString()!,"RECHAZO-TEST-"+Guid.NewGuid().ToString("N"),true,false,d.GetProperty("texto").GetString()),f.Admin,true,ct));
        Assert.Equal("DepositoPendiente",rechazoAnticipado.Codigo);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarPago(rechazo,new("Efectivo",22,"TEST",true,null),f.Admin,ct));
        int vencida=Json(await s.Crear(f.Persona,"+999123456704","Mediano",ct)).GetProperty("id").GetInt32();
        await using(var c=new SqlConnection(Cadena)){await c.OpenAsync();await using var q=new SqlCommand("UPDATE dbo.PreReservasEncomiendasWeb SET FechaExpiracion=DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE Id=@id",c);q.Parameters.AddWithValue("@id",vencida);await q.ExecuteNonQueryAsync();}
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>Consentir(f,vencida,"Destinatario"));
        int id=Json(await s.Crear(f.Persona,"+999123456705","Mediano",ct)).GetProperty("id").GetInt32();
        await Consentir(f,id,"Remitente");await s.ConfirmarPago(id,new("Efectivo",22,"PAGO-TEST",true,null),f.Admin,ct);await s.RegistrarReferencia(f.Persona,Captura(),ct);
        var pin=await EmitirEntregar(f,id,"Deposito");var malo=pin.GetProperty("pin").GetString()=="000000"?"000001":"000000";
        var solicitud=Json(await s.CrearSolicitudDeposito(f.Terminal,f.Casillero,ct)).GetProperty("solicitudId").GetGuid();
        for(int i=0;i<5;i++)await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.AutorizarDeposito(id,f.Persona,new(solicitud,malo,Captura()),ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.AutorizarDeposito(id,f.Persona,new(solicitud,pin.GetProperty("pin").GetString()!,Captura()),ct));
        var detalle=Json(await s.Detalle(id,f.Persona,ct));Assert.Equal("Bloqueado",detalle.GetProperty("pines")[0].GetProperty("estado").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarDeposito(solicitud,f.Terminal,true,ct));
        await s.RevocarReferencia(f.Persona,ct);
        await s.RegistrarReferencia(f.Persona,Captura(),ct);
        await using(var c=new SqlConnection(Cadena)) {
            await c.OpenAsync();await using var q=new SqlCommand("UPDATE dbo.ReferenciasEncomiendaV3 SET RevalidarUtc=DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE PersonaId=@persona",c);
            q.Parameters.AddWithValue("@persona",f.Persona);await q.ExecuteNonQueryAsync();
        }
        await s.LimpiarBiometria(ct);Assert.False(Json(await s.ConsultarReferencia(f.Persona,ct)).GetProperty("registrada").GetBoolean());
    }

    [Fact]
    public async Task CuentaReutilizaAceptacionPorTelefonoYDestinatarioAceptaCadaEncomienda()
    {
        var f=await Preparar();var s=f.Servicio;var ct=CancellationToken.None;
        int id=Json(await s.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        int segunda=Json(await s.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        var inicial=Json(await s.Detalle(id,f.Persona,ct));
        Assert.Equal("Pendiente",inicial.GetProperty("remitente").GetProperty("consentimiento").GetString());
        Assert.Equal(JsonValueKind.Null,inicial.GetProperty("compartir").GetProperty("enlaceWhatsApp").ValueKind);
        Assert.Equal(JsonValueKind.Null,inicial.GetProperty("compartir").GetProperty("mensaje").ValueKind);
        // Consultar/copiar un enlace no escribe autorizaciones.
        Assert.Equal("Pendiente",Json(await s.Detalle(id,f.Persona,ct)).GetProperty("destinatario").GetProperty("consentimiento").GetString());
        var aceptacion=new ConsentimientoOperativoSolicitud("+999123456701","","WA-CUENTA-"+Guid.NewGuid().ToString("N"),true,true,"SYNCHRO ACEPTO");
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(id,"Remitente",aceptacion with{Telefono="+999123456702"},f.Admin,false,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(id,"Remitente",aceptacion with{MensajeComprobado=false},f.Admin,false,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(id,"Remitente",aceptacion with{TextoMensaje="SYNCHRO ACEPTO "+id},f.Admin,false,ct));
        await s.ConfirmarConsentimiento(id,"Remitente",aceptacion,f.Admin,false,ct);
        var siguiente=Json(await s.Detalle(segunda,null,ct));
        Assert.Equal("Confirmado",siguiente.GetProperty("remitente").GetProperty("consentimiento").GetString());
        Assert.Equal("CuentaTelefono",siguiente.GetProperty("remitente").GetProperty("alcance").GetString());
        Assert.Equal(f.Usuario,siguiente.GetProperty("remitente").GetProperty("administrador").GetString());
        Assert.Equal(aceptacion.MensajeReferencia,siguiente.GetProperty("remitente").GetProperty("referencia").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>Consentir(f,id,"Destinatario"));
        await s.ConfirmarPago(id,new("Efectivo",22,"PAGO-PRIMERA-"+Guid.NewGuid().ToString("N"),true,null),f.Admin,ct);
        await s.RegistrarReferencia(f.Persona,Captura(),ct);
        var pinDeposito=await EmitirEntregar(f,id,"Deposito");
        var solicitud=Json(await s.CrearSolicitudDeposito(f.Terminal,f.Casillero,ct)).GetProperty("solicitudId").GetGuid();
        await s.AutorizarDeposito(id,f.Persona,new(solicitud,pinDeposito.GetProperty("pin").GetString()!,Captura()),ct);
        await s.ConfirmarDeposito(solicitud,f.Terminal,true,ct);
        var compartida=Json(await s.Detalle(id,f.Persona,ct));
        Assert.Contains("https://wa.me/999123456702?text=",compartida.GetProperty("compartir").GetProperty("enlaceWhatsApp").GetString());
        var parte=compartida.GetProperty("destinatario");
        var destinatario=new ConsentimientoOperativoSolicitud("+999123456702",parte.GetProperty("codigo").GetString()!,"WA-DEST-"+Guid.NewGuid().ToString("N"),true,true,parte.GetProperty("texto").GetString());
        Assert.Contains("https://wa.me/50433182642?text="+Uri.EscapeDataString(destinatario.TextoMensaje!),compartida.GetProperty("compartir").GetProperty("mensaje").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(segunda,"Destinatario",destinatario,f.Admin,false,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(id,"Destinatario",destinatario with{Telefono=aceptacion.Telefono},f.Admin,false,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(id,"Destinatario",destinatario with{TextoMensaje="SYNCHRO ACEPTO"},f.Admin,false,ct));
        await s.ConfirmarConsentimiento(id,"Destinatario",destinatario,f.Admin,false,ct);
        Assert.True(Json(await s.Detalle(id,f.Persona,ct)).GetProperty("condiciones").GetProperty("consentimientosConfirmados").GetBoolean());
        Assert.False(Json(await s.Detalle(segunda,f.Persona,ct)).GetProperty("condiciones").GetProperty("consentimientosConfirmados").GetBoolean());
        // Las comprobaciones de revocación y nuevo teléfono se hacen sobre la segunda reserva, aún sin depósito.
        id=segunda;
        await s.ConfirmarPago(id,new("Efectivo",22,"PAGO-REVOCACION",true,null),f.Admin,ct);
        await s.RevocarConsentimientoCuenta(f.Persona,ct);
        Assert.Equal("Revocado",Json(await s.Detalle(id,f.Persona,ct)).GetProperty("remitente").GetProperty("consentimiento").GetString());
        Assert.Equal(JsonValueKind.Null,Json(await s.ConsultarConsentimientoCuenta(f.Persona,ct)).GetProperty("verificadoUtc").ValueKind);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.EmitirPin(id,"Deposito",false,f.Admin,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(id,"Remitente",aceptacion,f.Admin,false,ct));
        await s.ConfirmarConsentimiento(id,"Remitente",aceptacion with{MensajeReferencia="WA-RENOVADO-"+Guid.NewGuid().ToString("N")},f.Admin,false,ct);
        // Cambiar exclusivamente el teléfono del fixture nunca hereda consentimiento.
        await using(var c=new SqlConnection(Cadena)) {
            await c.OpenAsync();await using var q=new SqlCommand("UPDATE dbo.ClientesWebPrototipo SET Telefono=N'+999123456709' WHERE PersonaId=@persona",c);
            q.Parameters.AddWithValue("@persona",f.Persona);Assert.Equal(1,await q.ExecuteNonQueryAsync());
        }
        Assert.Equal("Pendiente",Json(await s.Detalle(id,f.Persona,ct)).GetProperty("remitente").GetProperty("consentimiento").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.EmitirPin(id,"Deposito",false,f.Admin,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(id,"Remitente",aceptacion with{MensajeReferencia="WA-OTRO"},f.Admin,false,ct));
        await s.ConfirmarConsentimiento(id,"Remitente",aceptacion with{Telefono="+999123456709",MensajeReferencia="WA-NUEVO-"+Guid.NewGuid().ToString("N")},f.Admin,false,ct);
        var emitido=Json(await s.EmitirPin(id,"Deposito",false,f.Admin,ct));
        Assert.Equal("+999123456709",emitido.GetProperty("telefono").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarEntregaPin(id,new(emitido.GetProperty("id").GetGuid(),aceptacion.Telefono,"WA-PIN-VIEJO",true),f.Admin,ct));
        await s.ConfirmarEntregaPin(id,new(emitido.GetProperty("id").GetGuid(),"+999123456709","WA-PIN-NUEVO",true),f.Admin,ct);
    }

    [Fact]
    public async Task AutorizacionDeCuentaNoCaducaConLaEncomiendaUsadaComoContextoAdministrativo()
    {
        var f=await Preparar();var ct=CancellationToken.None;
        int id=Json(await f.Servicio.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        await using(var c=new SqlConnection(Cadena)) {
            await c.OpenAsync();await using var q=new SqlCommand("UPDATE dbo.PreReservasEncomiendasWeb SET FechaExpiracion=DATEADD(MINUTE,-1,SYSUTCDATETIME()) WHERE Id=@id",c);
            q.Parameters.AddWithValue("@id",id);Assert.Equal(1,await q.ExecuteNonQueryAsync());
        }
        await Consentir(f,id,"Remitente");
        Assert.Equal("Confirmado",Json(await f.Servicio.ConsultarConsentimientoCuenta(f.Persona,ct)).GetProperty("consentimiento").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>Consentir(f,id,"Destinatario"));
    }

    [Fact]
    public async Task MensajesAnterioresConservanSuAlcanceSinConvertirseEnAceptacionDeCuenta()
    {
        var f=await Preparar();var s=f.Servicio;var ct=CancellationToken.None;
        int id=Json(await s.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        var anterior=Json(await s.Detalle(id,null,ct));
        async Task ConsentimientoAnterior(string rol) {
            var p=anterior.GetProperty(rol);
            await s.ConfirmarConsentimiento(id,rol,new(p.GetProperty("telefono").GetString()!,p.GetProperty("codigoAnterior").GetString()!,"WA-ANTERIOR-"+Guid.NewGuid().ToString("N"),true,true,p.GetProperty("textoAnterior").GetString()),f.Admin,false,ct);
        }
        await ConsentimientoAnterior("remitente");
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>ConsentimientoAnterior("destinatario"));
        await s.ConfirmarPago(id,new("Efectivo",22,"PAGO-ANTERIOR-"+Guid.NewGuid().ToString("N"),true,null),f.Admin,ct);
        await s.RegistrarReferencia(f.Persona,Captura(),ct);
        var deposito=await EmitirEntregar(f,id,"Deposito");
        var solicitud=Json(await s.CrearSolicitudDeposito(f.Terminal,f.Casillero,ct)).GetProperty("solicitudId").GetGuid();
        await s.AutorizarDeposito(id,f.Persona,new(solicitud,deposito.GetProperty("pin").GetString()!,Captura()),ct);
        await s.ConfirmarDeposito(solicitud,f.Terminal,true,ct);
        await ConsentimientoAnterior("destinatario");
        var actual=Json(await s.Detalle(id,f.Persona,ct));
        Assert.True(actual.GetProperty("condiciones").GetProperty("consentimientosConfirmados").GetBoolean());
        Assert.Equal("EncomiendaAnterior",actual.GetProperty("remitente").GetProperty("alcance").GetString());
        Assert.Equal("Pendiente",Json(await s.ConsultarConsentimientoCuenta(f.Persona,ct)).GetProperty("consentimiento").GetString());
        int segunda=Json(await s.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        Assert.Equal("Pendiente",Json(await s.Detalle(segunda,f.Persona,ct)).GetProperty("remitente").GetProperty("consentimiento").GetString());
        var sinNumero=new ConfigurationBuilder().AddConfiguration(Configuracion).AddInMemoryCollection(new Dictionary<string,string?>{["WhatsAppManual:NumeroE164"]=""}).Build();
        var noConfigurado=new EncomiendasOperativasSql(sinNumero,f.Proteccion,TimeProvider.System);
        var sinEnlace=Json(await noConfigurado.Detalle(segunda,f.Persona,ct));
        Assert.Equal(JsonValueKind.Null,sinEnlace.GetProperty("compartir").GetProperty("enlaceWhatsApp").ValueKind);
        Assert.Equal(JsonValueKind.Null,sinEnlace.GetProperty("compartir").GetProperty("mensaje").ValueKind);
        await s.RevocarConsentimientoCuenta(f.Persona,ct);
        Assert.Equal("Revocado",Json(await s.Detalle(id,f.Persona,ct)).GetProperty("remitente").GetProperty("consentimiento").GetString());
    }

    [Fact]
    public async Task HttpRealExigeHttpsSesionRolesCsrfYPropiedad()
    {
        var f=await Preparar();
        using var fabrica=new FabricaEncomiendas(Configuracion);
        using var anonimo=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonimo.GetAsync("/api/web/encomiendas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonimo.GetAsync("/api/web/encomiendas/consentimiento-cuenta")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonimo.PostAsJsonAsync("/api/terminales/encomiendas/operativas/retiros/identificar",new{pin="123456"})).StatusCode);
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        cliente.DefaultRequestHeaders.Add("X-Test-Rol","Cliente");cliente.DefaultRequestHeaders.Add("X-Test-Id",f.Persona.ToString());
        Assert.Equal(HttpStatusCode.Forbidden,(await cliente.GetAsync("/api/admin/encomiendas/operativas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await cliente.GetAsync("/api/admin/whatsapp-pin/solicitudes")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsJsonAsync("/api/web/encomiendas",new{telefonoDestinatario="+999123456706",tamanoSolicitado="Mediano"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.DeleteAsync("/api/web/encomiendas/consentimiento-cuenta")).StatusCode);
        var csrf=await cliente.GetFromJsonAsync<JsonElement>("/api/web/clientes/csrf-token");
        cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("csrfToken").GetString());
        var creada=await cliente.PostAsJsonAsync("/api/web/encomiendas",new{telefonoDestinatario="+999123456706",tamanoSolicitado="Mediano"});Assert.Equal(HttpStatusCode.OK,creada.StatusCode);
        int id=(await creada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.GetAsync("/api/web/encomiendas?pagina=0")).StatusCode);
        var propia=await cliente.GetFromJsonAsync<JsonElement>($"/api/web/encomiendas?numero={id}&tamanoPagina=1");Assert.Equal(1,propia.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK,(await cliente.GetAsync("/api/web/encomiendas/"+id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsJsonAsync("/api/web/encomiendas/referencia-facial",Captura())).StatusCode);
        cliente.DefaultRequestHeaders.Remove("X-Test-Id");cliente.DefaultRequestHeaders.Add("X-Test-Id",(f.Persona+100000).ToString());
        Assert.Equal(HttpStatusCode.NotFound,(await cliente.GetAsync("/api/web/encomiendas/"+id)).StatusCode);
        var ajena=await cliente.GetFromJsonAsync<JsonElement>($"/api/web/encomiendas?numero={id}");Assert.Equal(0,ajena.GetProperty("total").GetInt32());
        using var operador=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        operador.DefaultRequestHeaders.Add("X-Test-Rol","Operador");operador.DefaultRequestHeaders.Add("X-Test-Id",f.Admin.ToString());
        Assert.Equal(HttpStatusCode.OK,(await operador.GetAsync("/api/admin/encomiendas/operativas/"+id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await operador.GetAsync($"/api/admin/encomiendas/operativas/{id}/evidencias/{Guid.NewGuid()}/foto")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await operador.PostAsJsonAsync($"/api/admin/encomiendas/operativas/{id}/pago/confirmar",new{metodo="Efectivo",importeHnl=22,referencia="HTTP-FIXTURE",pagoComprobado=true})).StatusCode);
        using var inseguro=fabrica.CreateClient(new(){BaseAddress=new Uri("http://localhost"),AllowAutoRedirect=false});
        inseguro.DefaultRequestHeaders.Add("X-Test-Rol","Cliente");inseguro.DefaultRequestHeaders.Add("X-Test-Id",f.Persona.ToString());
        Assert.Equal(HttpStatusCode.BadRequest,(await inseguro.GetAsync("/api/web/encomiendas")).StatusCode);
    }

    private sealed class RelojPrueba(DateTimeOffset ahora):TimeProvider { public override DateTimeOffset GetUtcNow()=>ahora; }

    private sealed class FabricaEncomiendas(IConfiguration config,bool manual=false):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(config.AsEnumerable()).AddInMemoryCollection(new Dictionary<string,string?>{
                ["ProteccionDatos:DirectorioClaves"]=Path.Combine(Path.GetTempPath(),"SYNCHRO","TestsEncomiendas","claves"),
                ["EncomiendasOperacion:LimpiarBiometria"]="false",["Reservas:ProcesarVencimientos"]="false",
                ["ServiciosTemporales:Habilitado"]="false",["WhatsApp:Outbox:Habilitado"]="false",
                ["WhatsApp:SolicitudesInicioWorker:Habilitado"]="false",["WhatsApp:VerificacionOutbox:Habilitado"]="false",
                ["RostroSimulado:LimpiarVencidas"]="false",["WhatsAppManual:Habilitado"]=manual?"true":"false"
            }));
            builder.ConfigureTestServices(s=>{
                for(int i=s.Count-1;i>=0;i--)if(s[i].ServiceType==typeof(IHostedService)&&s[i].ImplementationType?.Namespace?.StartsWith("Synchro.Api")==true)s.RemoveAt(i);
                s.AddAuthentication(AutenticacionEncomiendasPrueba.Esquema).AddScheme<AuthenticationSchemeOptions,AutenticacionEncomiendasPrueba>(AutenticacionEncomiendasPrueba.Esquema,_=>{});
                s.PostConfigure<AuthorizationOptions>(o=>{
                    o.AddPolicy("SoloClientes",new AuthorizationPolicyBuilder(AutenticacionEncomiendasPrueba.Esquema).RequireAuthenticatedUser().RequireRole("Cliente").Build());
                    o.AddPolicy("SoloTerminal",new AuthorizationPolicyBuilder(AutenticacionEncomiendasPrueba.Esquema).RequireAuthenticatedUser().RequireRole("Terminal").Build());
                });
            });
        }
    }
}

// Cabeceras de identidad ficticia existen únicamente en este ensamblado de pruebas.
public sealed class AutenticacionEncomiendasPrueba(IOptionsMonitor<AuthenticationSchemeOptions> o,ILoggerFactory l,UrlEncoder e):AuthenticationHandler<AuthenticationSchemeOptions>(o,l,e)
{
    public const string Esquema="EncomiendasPrueba";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string rol=Request.Headers["X-Test-Rol"].ToString(),id=Request.Headers["X-Test-Id"].ToString();
        if(string.IsNullOrEmpty(rol)||!int.TryParse(id,out _))return Task.FromResult(AuthenticateResult.NoResult());
        var identidad=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id),new Claim(ClaimTypes.Role,rol)},Esquema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad),Esquema)));
    }
}
