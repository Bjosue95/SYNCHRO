using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services.Encomiendas;

namespace Synchro.Api.Tests;

// Solo INSERT/UPDATE de fixtures propios: no migra ni borra datos SQL.
public sealed partial class EncomiendasOperativasSqlTests
{
    private static async Task<(int Id,Guid Solicitud)> PrepararDeposito(Fixture f)
    {
        var ct=CancellationToken.None;var s=f.Servicio;
        int id=Json(await s.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        await Consentir(f,id,"Remitente");
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>Consentir(f,id,"Destinatario"));
        await s.ConfirmarPago(id,new("Efectivo",22,"RECIBO-PROPIO-"+Guid.NewGuid().ToString("N"),true,null),f.Admin,ct);
        await s.RegistrarReferencia(f.Persona,Captura(),ct);
        var pin=await EmitirEntregar(f,id,"Deposito");
        var solicitud=Json(await s.CrearSolicitudDeposito(f.Terminal,f.Casillero,ct)).GetProperty("solicitudId").GetGuid();
        await s.AutorizarDeposito(id,f.Persona,new(solicitud,pin.GetProperty("pin").GetString()!,Captura()),ct);
        return(id,solicitud);
    }
    private static async Task<long> VersionActual(Fixture f,int id)=>Json(await f.Servicio.Detalle(id,f.Persona,default)).GetProperty("version").GetInt64();

    [Fact]
    public async Task AmpliacionCancelacionRevocaPinSolicitudEInvitacionSinRevocarCuentaNiReembolsar()
    {
        var f=await Preparar();var s=f.Servicio;var ct=CancellationToken.None;var (id,solicitud)=await PrepararDeposito(f);
        var peticion=new CancelarEncomiendaSolicitud("Número de destinatario incorrecto",null,true,await VersionActual(f,id));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Cancelar(id,f.Persona+100000,peticion,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Cancelar(id,f.Persona,peticion with{Version=0},ct));
        await s.Cancelar(id,f.Persona,peticion,ct);
        var repetida=Json(await s.Cancelar(id,f.Persona,peticion,ct));Assert.True(repetida.GetProperty("idempotente").GetBoolean());
        var detalle=Json(await s.Detalle(id,f.Persona,ct));Assert.Equal("Cancelada",detalle.GetProperty("estado").GetString());
        Assert.Equal("Número de destinatario incorrecto",detalle.GetProperty("cancelacion").GetProperty("motivo").GetString());
        Assert.Equal(f.Persona,detalle.GetProperty("cancelacion").GetProperty("responsableId").GetInt32());
        Assert.True(detalle.GetProperty("pagoRequiereRevision").GetBoolean());Assert.Equal("ConfirmadoManual",detalle.GetProperty("pago").GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null,detalle.GetProperty("compartir").GetProperty("enlaceWhatsApp").ValueKind);
        Assert.Equal("Confirmado",Json(await s.ConsultarConsentimientoCuenta(f.Persona,ct)).GetProperty("consentimiento").GetString());
        Assert.DoesNotContain(detalle.GetProperty("seguimiento").EnumerateArray(),e=>e.GetProperty("tipo").GetString()=="AceptacionDestinatario");
        Assert.All(detalle.GetProperty("pines").EnumerateArray(),p=>Assert.Equal("Revocado",p.GetProperty("estado").GetString()));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.EmitirPin(id,"Deposito",false,f.Admin,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.EmitirPin(id,"Retiro",false,f.Admin,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarDeposito(solicitud,f.Terminal,true,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarConsentimiento(id,"Remitente",new("+999123456701","","WA-OTRO",true,true,"SYNCHRO ACEPTO"),f.Admin,false,ct));
        await using var c=new SqlConnection(Cadena);await c.OpenAsync();await using var q=new SqlCommand("""
            SELECT Estado FROM dbo.SolicitudesDepositoEncomiendaOperativa WHERE Id=@solicitud;
            SELECT COUNT(*) FROM dbo.EvidenciasEncomiendaOperativa WHERE ReservaId=@id AND VenceUtc>SYSUTCDATETIME() AND ConsumidaUtc IS NULL;
            SELECT COUNT(*) FROM dbo.AuditoriaEncomiendaOperativa WHERE ReservaId=@id AND Accion=N'EncomiendaCancelada';
            """,c);q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@solicitud",solicitud);
        await using var r=await q.ExecuteReaderAsync();await r.ReadAsync();Assert.Equal("Cancelada",r.GetString(0));
        await r.NextResultAsync();await r.ReadAsync();Assert.Equal(0,r.GetInt32(0));await r.NextResultAsync();await r.ReadAsync();Assert.Equal(1,r.GetInt32(0));
    }

    [Fact]
    public async Task AmpliacionCancelacionValidaMotivoConfirmacionYNoInventaPago()
    {
        var f=await Preparar();var s=f.Servicio;var ct=CancellationToken.None;
        int id=Json(await s.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        var d=new CancelarEncomiendaSolicitud("Otro motivo","Cambio de planes",true,1);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Cancelar(id,f.Persona,d with{Confirmada=false},ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Cancelar(id,f.Persona,d with{Motivo="inválido"},ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Cancelar(id,f.Persona,d with{Detalle=""},ct));
        await s.Cancelar(id,f.Persona,d,ct);
        var detalle=Json(await s.Detalle(id,f.Persona,ct));Assert.False(detalle.GetProperty("pagoRequiereRevision").GetBoolean());
        Assert.Equal("Cambio de planes",detalle.GetProperty("cancelacion").GetProperty("detalle").GetString());
        Assert.Contains(detalle.GetProperty("seguimiento").EnumerateArray(),e=>e.GetProperty("tipo").GetString()=="Cancelada");
    }

    [Fact]
    public async Task AmpliacionDepositoYRetiroConfirmadosImpidenCancelacionOrdinaria()
    {
        var f=await Preparar();var s=f.Servicio;var ct=CancellationToken.None;var(id,solicitud)=await PrepararDeposito(f);
        await s.ConfirmarDeposito(solicitud,f.Terminal,true,ct);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Cancelar(id,f.Persona,new("Ya no desea enviar la encomienda",null,true,1),ct));
        await Consentir(f,id,"Destinatario");
        var pin=await EmitirEntregar(f,id,"Retiro");var ev=Json(await s.CapturarDestinatario(id,f.Terminal,Captura(destinatario:true),ct)).GetProperty("evidenciaId").GetGuid();
        await s.Retirar(id,f.Terminal,new(ev,pin.GetProperty("pin").GetString()!,true),ct);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Cancelar(id,f.Persona,new("Ya no desea enviar la encomienda",null,true,1),ct));
        Assert.True(Json(await s.Detalle(id,f.Persona,ct)).GetProperty("retiradaConfirmada").GetBoolean());
    }

    [Fact]
    public async Task AmpliacionCarreraDepositoCancelacionTieneUnSoloResultadoConsistente()
    {
        var f=await Preparar();var s=f.Servicio;var ct=CancellationToken.None;var(id,solicitud)=await PrepararDeposito(f);
        long version=await VersionActual(f,id);var inicio=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Intentar(Func<Task<object>> accion){await inicio.Task;try{await accion();return true;}catch(OperacionEncomiendaException){return false;}}
        var cancelar=Intentar(()=>s.Cancelar(id,f.Persona,new("Tamaño de casillero incorrecto",null,true,version),ct));
        var depositar=Intentar(()=>s.ConfirmarDeposito(solicitud,f.Terminal,true,ct));inicio.SetResult();
        var resultados=await Task.WhenAll(cancelar,depositar);Assert.Single(resultados,x=>x);
        var detalle=Json(await s.Detalle(id,f.Persona,ct));
        Assert.Equal(resultados[0]?"Cancelada":"Depositada",detalle.GetProperty("estado").GetString());
        Assert.Equal(resultados[0]?JsonValueKind.Null:JsonValueKind.Number,detalle.GetProperty("usoId").ValueKind);
    }

    [Fact]
    public async Task AmpliacionHistorialPaginaMasDeDoscientosIncluyeAnterioresYRespetaPropietarioFechasYNumero()
    {
        var f=await Preparar();var ct=CancellationToken.None;
        await using(var c=new SqlConnection(Cadena)) {
            await c.OpenAsync();await using var q=new SqlCommand("""
                INSERT dbo.PreReservasEncomiendasWeb(TokenGestionHash,TelefonoRemitente,TelefonoDestinatario,TamanoSolicitado,Estado,FechaCreacion,FechaExpiracion,OrigenPreReserva,ClientePersonaId)
                SELECT TOP(205) CRYPT_GEN_RANDOM(32),N'+999123456701',N'+999123456702',N'Mediano',N'PendienteConsentimiento',
                  DATEADD(MINUTE,ROW_NUMBER() OVER(ORDER BY object_id),CONVERT(datetime2,'2026-01-01T18:00:00')),DATEADD(DAY,30,SYSUTCDATETIME()),N'Web',@persona FROM sys.all_objects;
                """,c);q.Parameters.AddWithValue("@persona",f.Persona);Assert.Equal(205,await q.ExecuteNonQueryAsync());
        }
        int actual=Json(await f.Servicio.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        var ids=new HashSet<int>();
        for(int pagina=1;pagina<=5;pagina++) {
            var historial=Json(await f.Servicio.Listar(f.Persona,new(Pagina:pagina,TamanoPagina:50),ct));Assert.Equal(206,historial.GetProperty("total").GetInt32());
            foreach(var item in historial.GetProperty("encomiendas").EnumerateArray())Assert.True(ids.Add(item.GetProperty("id").GetInt32()));
        }
        Assert.Equal(206,ids.Count);Assert.Contains(actual,ids);
        var fechas=Json(await f.Servicio.Listar(f.Persona,new(Desde:new DateOnly(2026,1,1),Hasta:new DateOnly(2026,1,1)),ct));Assert.Equal(205,fechas.GetProperty("total").GetInt32());
        var numero=Json(await f.Servicio.Listar(f.Persona,new(Numero:actual),ct));Assert.Equal(1,numero.GetProperty("total").GetInt32());
        Assert.Equal(0,Json(await f.Servicio.Listar(f.Persona+100000,new(Numero:actual),ct)).GetProperty("total").GetInt32());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>f.Servicio.Detalle(actual,f.Persona+100000,ct));
        int antiguo=ids.First(id=>id!=actual);var detalle=Json(await f.Servicio.Detalle(antiguo,f.Persona,ct));Assert.True(detalle.GetProperty("historica").GetBoolean());
        Assert.DoesNotContain("token",detalle.GetRawText(),StringComparison.OrdinalIgnoreCase);
        await f.Servicio.Cancelar(antiguo,f.Persona,new("Número de destinatario incorrecto",null,true,1),ct);
        Assert.Equal(1,Json(await f.Servicio.Listar(f.Persona,new(Estado:"Cancelada"),ct)).GetProperty("total").GetInt32());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>f.Servicio.Listar(f.Persona,new(Pagina:0),ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>f.Servicio.Listar(f.Persona,new(Desde:new(2026,2,1),Hasta:new(2026,1,1)),ct));
    }

    private static IConfiguration ConfiguracionManual=>new ConfigurationBuilder().AddConfiguration(Configuracion).AddInMemoryCollection(new Dictionary<string,string?>{
        ["WhatsAppManual:Habilitado"]="true",["RetiroPersonal:ClaveHmac"]=Convert.ToBase64String(Enumerable.Range(1,32).Select(i=>(byte)i).ToArray())}).Build();
    private static async Task<(Guid Intento,string Codigo,int Uso)> CrearSolicitudPersonal(Fixture f)
    {
        var id=Guid.NewGuid();string codigo=Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
        await using var c=new SqlConnection(Cadena);await c.OpenAsync();await using var q=new SqlCommand("""
            INSERT dbo.UsosCasillero(CodigoCasillero,TipoServicio,FechaReserva,FechaVencimiento,Estado,OrigenReserva)
            OUTPUT INSERTED.Id VALUES(@casillero,N'ResguardoPersonal',SYSUTCDATETIME(),DATEADD(MINUTE,20,SYSUTCDATETIME()),N'Reservado',N'Terminal');
            UPDATE dbo.Casilleros SET Estado=N'Reservado' WHERE Codigo=@casillero;
            """,c);q.Parameters.AddWithValue("@casillero",f.Casillero);int uso=Convert.ToInt32(await q.ExecuteScalarAsync());
        await using var guardar=new SqlCommand("""
            INSERT dbo.AutorizacionesWhatsAppManuales(Id,CodigoAutorizacion,CodigoCasillero,TokenConsultaHash,Estado,UsoId,VenceUtc)
            VALUES(@id,@codigo,@casillero,CRYPT_GEN_RANDOM(32),N'Pendiente',@uso,DATEADD(MINUTE,20,SYSUTCDATETIME()));
            """,c);guardar.Parameters.AddWithValue("@id",id);guardar.Parameters.AddWithValue("@codigo",codigo);guardar.Parameters.AddWithValue("@casillero",f.Casillero);guardar.Parameters.AddWithValue("@uso",uso);await guardar.ExecuteNonQueryAsync();return(id,codigo,uso);
    }

    [Fact]
    public async Task AmpliacionBandejaUnificaServiciosSinMezclarConsentimientosPinesNiTelefonos()
    {
        var f=await Preparar();var ct=CancellationToken.None;var personal=await CrearSolicitudPersonal(f);
        int id=Json(await f.Servicio.Crear(f.Persona,"+999123456702","Mediano",ct)).GetProperty("id").GetInt32();
        var s=new EncomiendasOperativasSql(ConfiguracionManual,f.Proteccion,TimeProvider.System);
        var encomiendas=Json(await s.BandejaWhatsApp(new(Servicio:"Encomiendas",Referencia:id.ToString()),ct));
        Assert.Equal(1,encomiendas.GetProperty("total").GetInt32());Assert.All(encomiendas.GetProperty("solicitudes").EnumerateArray(),x=>Assert.Equal("Encomiendas",x.GetProperty("servicio").GetString()));
        var cfgEstacion=new ConfigurationBuilder().AddInMemoryCollection(ConfiguracionManual.AsEnumerable())
            .AddInMemoryCollection(new Dictionary<string,string?>{["Administracion:ExcluirFixturesBandejaWhatsApp"]="true"}).Build();
        var estacion=new EncomiendasOperativasSql(cfgEstacion,f.Proteccion,TimeProvider.System);
        var sinFixtures=Json(await estacion.BandejaWhatsApp(new(Servicio:"Encomiendas",Referencia:id.ToString()),ct));
        Assert.Equal(0,sinFixtures.GetProperty("total").GetInt32());
        Assert.Equal(0,sinFixtures.GetProperty("porVerificar").GetInt32());
        Assert.Equal(0,sinFixtures.GetProperty("pinPorEnviar").GetInt32());
        Assert.Empty(sinFixtures.GetProperty("solicitudes").EnumerateArray());
        var personalEstacion=Json(await estacion.BandejaWhatsApp(new(Servicio:"ResguardoPersonal",Referencia:personal.Intento.ToString()),ct));
        Assert.Equal(1,personalEstacion.GetProperty("total").GetInt32());
        var resguardo=Json(await s.BandejaWhatsApp(new(Servicio:"ResguardoPersonal",Referencia:personal.Intento.ToString()),ct));
        Assert.Equal(1,resguardo.GetProperty("total").GetInt32());Assert.Equal("RetiroPersonal",resguardo.GetProperty("solicitudes")[0].GetProperty("finalidad").GetString());
        Assert.Equal(JsonValueKind.Null,resguardo.GetProperty("solicitudes")[0].GetProperty("encomiendaId").ValueKind);
        Assert.DoesNotContain("pinHash",resguardo.GetRawText(),StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("token",resguardo.GetRawText(),StringComparison.OrdinalIgnoreCase);
        using var fabrica=new FabricaEncomiendas(ConfiguracionManual,true);using var admin=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        admin.DefaultRequestHeaders.Add("X-Test-Rol","Operador");admin.DefaultRequestHeaders.Add("X-Test-Id",f.Admin.ToString());
        var csrf=await admin.GetFromJsonAsync<JsonElement>("/api/admin/csrf-token");admin.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("csrfToken").GetString());
        var equivocado=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/confirmar",new{intentoId=personal.Intento,codigoAutorizacion=personal.Codigo,telefonoCliente="50400000001",mensajeComprobado=true,textoMensaje="SYNCHRO ACEPTO "+id,mensajeReferencia="WA-TEST"});Assert.Equal(HttpStatusCode.BadRequest,equivocado.StatusCode);
        var sinReferencia=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/confirmar",new{intentoId=personal.Intento,codigoAutorizacion=personal.Codigo,telefonoCliente="50400000001",mensajeComprobado=true,textoMensaje=$"SYNCHRO ACEPTO {personal.Codigo} RECIBIR PIN DE RETIRO POR WHATSAPP"});Assert.Equal(HttpStatusCode.BadRequest,sinReferencia.StatusCode);
        var confirmado=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/confirmar",new{intentoId=personal.Intento,codigoAutorizacion=personal.Codigo,telefonoCliente="50400000001",mensajeComprobado=true,textoMensaje=$"SYNCHRO ACEPTO {personal.Codigo} RECIBIR PIN DE RETIRO POR WHATSAPP",mensajeReferencia="WA-PERSONAL-"+Guid.NewGuid().ToString("N")});Assert.Equal(HttpStatusCode.OK,confirmado.StatusCode);
        Assert.Equal("Pendiente",Json(await f.Servicio.Detalle(id,f.Persona,ct)).GetProperty("remitente").GetProperty("consentimiento").GetString());
        var malTelefono=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/marcar-enviado",new{intentoId=personal.Intento,envioComprobado=true,telefonoCliente="50400000002",mensajeReferencia="WA-ENVIO-INCORRECTO"});Assert.Equal(HttpStatusCode.Conflict,malTelefono.StatusCode);
        var sinDatosEnvio=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/marcar-enviado",new{intentoId=personal.Intento,envioComprobado=true});Assert.Equal(HttpStatusCode.BadRequest,sinDatosEnvio.StatusCode);
        var noEnviado=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/marcar-enviado",new{intentoId=personal.Intento,envioComprobado=false,telefonoCliente="50400000001",mensajeReferencia="WA-ENVIO-NO"});Assert.Equal(HttpStatusCode.BadRequest,noEnviado.StatusCode);
        var enviado=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/marcar-enviado",new{intentoId=personal.Intento,envioComprobado=true,telefonoCliente="50400000001",mensajeReferencia="WA-ENVIO-PERSONAL"});Assert.Equal(HttpStatusCode.OK,enviado.StatusCode);
        resguardo=Json(await s.BandejaWhatsApp(new(Servicio:"ResguardoPersonal",Telefono:"+50400000001",Referencia:personal.Intento.ToString()),ct));
        Assert.Equal("EnviadoManual",resguardo.GetProperty("solicitudes")[0].GetProperty("entrega").GetString());Assert.NotEqual(JsonValueKind.Null,resguardo.GetProperty("solicitudes")[0].GetProperty("verificadoUtc").ValueKind);
        await Consentir(f,id,"Remitente");Assert.Equal("Confirmado",Json(await s.BandejaWhatsApp(new(Servicio:"Encomiendas",Referencia:id.ToString()),ct)).GetProperty("solicitudes").EnumerateArray().First(x=>x.GetProperty("rol").GetString()=="Remitente").GetProperty("consentimiento").GetString());
    }
}
