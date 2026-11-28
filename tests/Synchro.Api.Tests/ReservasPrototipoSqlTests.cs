using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services.Encomiendas;
using Synchro.Api.Services.Temporales;
using Synchro.Api.Models;

namespace Synchro.Api.Tests;

// Fixtures propios acotados por código. Sin migraciones, borrados ni cambios en otros servicios.
public sealed partial class EncomiendasOperativasSqlTests
{
    [Fact]
    public void InventarioFisicoDeTabletYReservasTieneDiezCodigosSinDuplicados()
    {
        string[] esperados = [
            "S01", "S02", "M03", "M04", "L05",
            "M06", "M07", "M08", "M09", "L10"
        ];
        Assert.Equal(esperados, InventarioEstacionPrototipo.Codigos);
        Assert.Equal(10, InventarioEstacionPrototipo.Codigos.Distinct().Count());
        Assert.Equal("Frio", InventarioEstacionPrototipo.TipoReal("M04", "Normal"));
        Assert.Equal("Calor", InventarioEstacionPrototipo.TipoReal("M09", "Normal"));
        Assert.Equal("Normal", InventarioEstacionPrototipo.TipoReal("M03", "Normal"));
    }

    private static IConfiguration ConfiguracionReserva(Fixture f)=>new ConfigurationBuilder().AddConfiguration(Configuracion).AddInMemoryCollection(new Dictionary<string,string?>{
        ["ReservasPrototipo:TerminalId"]=f.Terminal.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["ReservasPrototipo:Casilleros:0"]=f.Casillero,["ReservasPrototipo:Ubicacion"]="Inventario de pruebas",["ReservasPrototipo:DuracionesMinutos:0"]="60",
        ["ReservasPrototipo:DuracionesMinutos:1"]="120",["ReservasPrototipo:TarifasNormales:Mediano:60"]="22"}).Build();
    private static CapturaEncomiendaSolicitud CapturaDemostrativa(bool retiro=false)=>new(Convert.ToBase64String(RostroSimuladoTests.Png(retiro)),null,
        "SYNCHRO-CAPTURA-DEMOSTRATIVA-v1",EncomiendasOperativasSql.VersionCapturaDemostrativa);

    [Theory]
    [InlineData("Encomienda","Normal")]
    [InlineData("Encomienda","Refrigerado")]
    [InlineData("Encomienda","Caliente")]
    public async Task PrototipoReservaTiposConPinesRealesYCapturasDiferentesSinReferenciaFacial(string servicio,string tipo)
    {
        var f=await Preparar();var cfg=ConfiguracionReserva(f);var s=new EncomiendasOperativasSql(cfg,f.Proteccion,TimeProvider.System);var ct=CancellationToken.None;
        if(tipo!="Normal") {
            var termica=new ConfiguracionServicioTemporal(f.Casillero,tipo=="Refrigerado"?ClaseServicioTemporal.Frio:ClaseServicioTemporal.Calor,null,true,new(){{60,22}},
                tipo=="Refrigerado"?2:50,tipo=="Refrigerado"?8:70,tipo=="Refrigerado"?4:60);
            await using var c=new SqlConnection(Cadena);await c.OpenAsync();await using var q=new SqlCommand("UPDATE dbo.Casilleros SET Tipo=@tipo WHERE Codigo=@codigo; INSERT dbo.CompartimentosServiciosTemporales(Codigo,ConfiguracionJson) VALUES(@codigo,@config);",c);
            q.Parameters.AddWithValue("@tipo",tipo);q.Parameters.AddWithValue("@codigo",f.Casillero);q.Parameters.AddWithValue("@config",JsonSerializer.Serialize(termica));await q.ExecuteNonQueryAsync();
        }
        var catalogo=Json(await s.CatalogoReservas(ct));Assert.Single(catalogo.GetProperty("opciones").EnumerateArray());
        Assert.Equal(tipo,catalogo.GetProperty("opciones")[0].GetProperty("tipo").GetString());
        var solicitud=new CrearReservaPrototipoSolicitud(servicio,tipo,"Mediano","prototipo",60,servicio=="Encomienda"?"+999123456702":null);
        int id=Json(await s.Crear(f.Persona,solicitud.TelefonoDestinatario??"","Mediano",ct,solicitud)).GetProperty("id").GetInt32();
        Assert.Empty(Json(await s.CatalogoReservas(ct)).GetProperty("opciones").EnumerateArray());
        var bandeja=Json(await s.BandejaWhatsApp(new(Servicio:"Encomiendas",Referencia:id.ToString()),ct));
        Assert.Equal(1,bandeja.GetProperty("total").GetInt32());
        Assert.All(bandeja.GetProperty("solicitudes").EnumerateArray(),x=>{Assert.Equal(servicio,x.GetProperty("claseReserva").GetString());Assert.Equal(tipo,x.GetProperty("tipoCasillero").GetString());});
        if(tipo!="Normal"){
            var anterior=new ServicioTemporal(new RepositorioServicioTemporalSql(cfg),cfg,TimeProvider.System);
            await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>anterior.ReservarAsync(f.Casillero,60,null,ct,f.Persona));
        }
        await Consentir(f,id,"Remitente");
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>Consentir(f,id,"Destinatario"));
        await s.ConfirmarPago(id,new("Efectivo",22,"PAGO-PROTOTIPO-"+Guid.NewGuid().ToString("N"),true,null),f.Admin,ct);
        Assert.False(Json(await s.ConsultarReferencia(f.Persona,ct)).GetProperty("registrada").GetBoolean());
        var deposito=Json(await s.EmitirPin(id,"Deposito",false,f.Admin,ct));
        await s.ConfirmarEntregaPin(id,new(deposito.GetProperty("id").GetGuid(),"+999123456701","WA-DEPOSITO-TEST",true),f.Admin,ct);
        var pinDeposito=deposito.GetProperty("pin").GetString()!;var falso=pinDeposito=="000000"?"000001":"000000";
        var identificacion=Json(await s.IdentificarParaDeposito(pinDeposito,f.Terminal,ct));
        Assert.Equal(id,identificacion.GetProperty("reservaId").GetInt32());
        Assert.Equal(servicio,identificacion.GetProperty("servicio").GetString());
        Assert.Equal(tipo,identificacion.GetProperty("tipoCasillero").GetString());
        var terminalErronea=new EncomiendasOperativasSql(new ConfigurationBuilder()
            .AddConfiguration(cfg)
            .AddInMemoryCollection(new Dictionary<string,string?> {
                ["ReservasPrototipo:TerminalId"]=(f.Terminal+100000).ToString()
            }).Build(),f.Proteccion,TimeProvider.System);
        var rechazo=await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>
            terminalErronea.IdentificarParaDeposito(pinDeposito,f.Terminal,ct));
        Assert.Equal("TerminalNoAutorizada",rechazo.Codigo);
        var rechazoDirecto=await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>
            terminalErronea.DepositarDemostrativo(id,f.Terminal,
                new(pinDeposito,CapturaDemostrativa(),true),ct));
        Assert.Equal("TerminalNoAutorizada",rechazoDirecto.Codigo);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.DepositarDemostrativo(id,f.Terminal,new(falso,CapturaDemostrativa(),true),ct));
        Assert.Equal("Preparacion",Json(await s.Detalle(id,f.Persona,ct)).GetProperty("estado").GetString());
        await s.DepositarDemostrativo(id,f.Terminal,new(pinDeposito,CapturaDemostrativa(),true),ct);
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.EmitirPin(id,"Retiro",false,f.Admin,ct));
        await Consentir(f,id,"Destinatario");
        var retiro=Json(await s.EmitirPin(id,"Retiro",false,f.Admin,ct));Assert.NotEqual(pinDeposito,retiro.GetProperty("pin").GetString());
        var telefonoRetiro=servicio=="Personal"?"+999123456701":"+999123456702";
        Assert.Equal(telefonoRetiro,retiro.GetProperty("telefono").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarEntregaPin(id,new(retiro.GetProperty("id").GetGuid(),servicio=="Personal"?"+999123456702":"+999123456701","WA-ERROR",true),f.Admin,ct));
        await s.ConfirmarEntregaPin(id,new(retiro.GetProperty("id").GetGuid(),telefonoRetiro,"WA-RETIRO-TEST",true),f.Admin,ct);
        var identificada=Json(await s.IdentificarParaRetiro(retiro.GetProperty("pin").GetString()!,f.Terminal,ct));
        Assert.Equal(id,identificada.GetProperty("encomiendaId").GetInt32());Assert.False(identificada.GetProperty("comparacionFacialRequerida").GetBoolean());
        Assert.Equal(EncomiendasOperativasSql.VersionCapturaDemostrativa,identificada.GetProperty("versionConsentimientoRetiro").GetString());
        var evidencia=Json(await s.CapturarDestinatario(id,f.Terminal,CapturaDemostrativa(retiro:true),ct)).GetProperty("evidenciaId").GetGuid();
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Retirar(id,f.Terminal,new(evidencia,pinDeposito,true),ct));
        var autorizada=Json(await s.AutorizarRecogidaEncomienda(id,f.Terminal,new(evidencia,retiro.GetProperty("pin").GetString()!,false),ct));
        Assert.True(autorizada.GetProperty("retiroAutorizado").GetBoolean());
        Assert.False(autorizada.GetProperty("retiroConfirmado").GetBoolean());
        Assert.Equal("Ocupado",autorizada.GetProperty("estadoCasillero").GetString());
        Assert.Equal("Depositada",Json(await s.Detalle(id,f.Persona,ct)).GetProperty("estado").GetString());
        string permiso=autorizada.GetProperty("autorizacionRetiro").GetString()!;
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarRecogidaEncomienda(id,f.Terminal,permiso,false,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarRecogidaEncomienda(id,f.Terminal,new string('X',64),true,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.ConfirmarRecogidaEncomienda(id,f.Terminal+10000,permiso,true,ct));
        Assert.True(Json(await s.ConfirmarRecogidaEncomienda(id,f.Terminal,permiso,true,ct)).GetProperty("retiroConfirmado").GetBoolean());
        Assert.True(Json(await s.ConfirmarRecogidaEncomienda(id,f.Terminal,permiso,true,ct)).GetProperty("idempotente").GetBoolean());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Retirar(id,f.Terminal,new(evidencia,retiro.GetProperty("pin").GetString()!,true),ct));
        var detalle=Json(await s.Detalle(id,f.Persona,ct));Assert.True(detalle.GetProperty("retiradaConfirmada").GetBoolean());
        Assert.All(detalle.GetProperty("evidencias").EnumerateArray(),e=>Assert.NotEqual(JsonValueKind.True,e.GetProperty("coincidenciaBiometrica").ValueKind));
        await using var lectura=new SqlConnection(Cadena);await lectura.OpenAsync();await using var comprobar=new SqlCommand("SELECT COUNT(*) FROM dbo.ReferenciasEncomiendaV3 WHERE PersonaId=@persona; SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",lectura);
        comprobar.Parameters.AddWithValue("@persona",f.Persona);comprobar.Parameters.AddWithValue("@codigo",f.Casillero);await using var r=await comprobar.ExecuteReaderAsync();await r.ReadAsync();Assert.Equal(0,r.GetInt32(0));await r.NextResultAsync();await r.ReadAsync();Assert.Equal("Disponible",r.GetString(0));
    }

    [Theory]
    [InlineData("Refrigerado")]
    [InlineData("Caliente")]
    public async Task ReservaPersonalRechazaTermicosAunqueElClienteLosSoliciteDirectamente(string tipo)
    {
        var f=await Preparar();
        var s=new EncomiendasOperativasSql(ConfiguracionReserva(f),f.Proteccion,TimeProvider.System);
        var solicitud=new CrearReservaPrototipoSolicitud("Personal",tipo,"Mediano","prototipo",60);
        var rechazo=await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>
            s.Crear(f.Persona,"","Mediano",CancellationToken.None,solicitud));
        Assert.Equal("CasilleroExclusivoEncomiendas",rechazo.Codigo);
        Assert.Equal(400,rechazo.Estado);
    }

    [Theory]
    [InlineData("Personal","Remitente","Tablet")]
    [InlineData("Personal","Destinatario","Tablet")]
    [InlineData("Encomienda","Remitente","Tablet")]
    public async Task RechazaCombinacionesDePagadorYCanalNoPermitidas(
        string servicio,string pagador,string canal)
    {
        var f=await Preparar();
        var s=new EncomiendasOperativasSql(ConfiguracionReserva(f),f.Proteccion,TimeProvider.System);
        var d=new CrearReservaPrototipoSolicitud(servicio,"Normal","Mediano",
            "prototipo",60,servicio=="Personal"?null:"+999123456702",pagador,canal);
        var error=await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>
            s.Crear(f.Persona,d.TelefonoDestinatario??"","Mediano",CancellationToken.None,d));
        Assert.Equal("PreferenciaPagoInvalida",error.Codigo);
    }

    [Fact]
    public async Task PagoWebDePruebaSeEnviaAlAdministradorSinRecibirTarjeta()
    {
        var f=await Preparar();
        var cfg=ConfiguracionReserva(f);
        var s=new EncomiendasOperativasSql(cfg,f.Proteccion,TimeProvider.System);
        var ct=CancellationToken.None;
        var seleccion=new CrearReservaPrototipoSolicitud(
            "Encomienda","Normal","Mediano","prototipo",60,
            "+999123456702","Remitente","WebTarjeta");
        int id=Json(await s.Crear(f.Persona,seleccion.TelefonoDestinatario!,"Mediano",ct,seleccion))
            .GetProperty("id").GetInt32();
        var detalle=Json(await s.Detalle(id,f.Persona,ct));
        Assert.Equal("Remitente",detalle.GetProperty("reserva").GetProperty("pagador").GetString());
        Assert.Equal("WebTarjeta",detalle.GetProperty("reserva").GetProperty("modalidadPago").GetString());
        var cobros=Json(await s.CobrosReservaPendientes(ct)).GetProperty("cobros").EnumerateArray().ToArray();
        var item=Assert.Single(cobros.Where(x=>x.GetProperty("reservaId").GetInt32()==id));
        Assert.Equal("Web",item.GetProperty("origen").GetString());
        Assert.Equal("Tarjeta",item.GetProperty("metodoPago").GetString());
        await Consentir(f,id,"Remitente");
        var recibido=Json(await s.ConfirmarPagoReservaDemo(id,new(22,null,true),f.Admin,ct));
        Assert.Equal("ConfirmadoManual",recibido.GetProperty("estado").GetString());
        Assert.DoesNotContain(Json(await s.CobrosReservaPendientes(ct)).GetProperty("cobros").EnumerateArray(),
            x=>x.GetProperty("reservaId").GetInt32()==id);
        var auditado=Json(await s.Detalle(id,f.Persona,ct));
        var texto=JsonSerializer.Serialize(auditado);
        Assert.DoesNotContain("4242424242424242",texto);
        Assert.DoesNotContain("12/30",texto);
    }

    [Fact]
    public async Task DestinatarioPagaDespuesDelDepositoEnTabletAntesDeRetirar()
    {
        var f=await Preparar();
        var cfg=ConfiguracionReserva(f);
        var s=new EncomiendasOperativasSql(cfg,f.Proteccion,TimeProvider.System);
        var ct=CancellationToken.None;
        var seleccion=new CrearReservaPrototipoSolicitud(
            "Encomienda","Normal","Mediano","prototipo",60,
            "+999123456702","Destinatario","Tablet");
        int id=Json(await s.Crear(f.Persona,seleccion.TelefonoDestinatario!,"Mediano",ct,seleccion))
            .GetProperty("id").GetInt32();
        await Consentir(f,id,"Remitente");
        var condiciones=Json(await s.Detalle(id,f.Persona,ct)).GetProperty("condiciones");
        Assert.True(condiciones.GetProperty("pagoDiferidoDestinatario").GetBoolean());
        Assert.False(condiciones.GetProperty("pagoConfirmado").GetBoolean());
        var solicitudPin=Json(await s.SolicitarPinDeposito(id,f.Persona,ct));
        Assert.True(solicitudPin.GetProperty("solicitudRegistrada").GetBoolean());
        Assert.True(Json(await s.SolicitarPinDeposito(id,f.Persona,ct)).GetProperty("yaExistia").GetBoolean());
        var deposito=Json(await s.EmitirPin(id,"Deposito",false,f.Admin,ct));
        var pinDeposito=deposito.GetProperty("pin").GetString()!;
        await s.ConfirmarEntregaPin(id,new(deposito.GetProperty("id").GetGuid(),
            "+999123456701","WA-DIFERIDO-DEPOSITO",true),f.Admin,ct);
        await s.DepositarDemostrativo(id,f.Terminal,
            new(pinDeposito,CapturaDemostrativa(),true),ct);
        Assert.Equal("Depositada",Json(await s.Detalle(id,f.Persona,ct))
            .GetProperty("estado").GetString());
        await Consentir(f,id,"Destinatario");
        var retiro=Json(await s.EmitirPin(id,"Retiro",false,f.Admin,ct));
        var pinRetiro=retiro.GetProperty("pin").GetString()!;
        await s.ConfirmarEntregaPin(id,new(retiro.GetProperty("id").GetGuid(),
            "+999123456702","WA-DIFERIDO-RETIRO",true),f.Admin,ct);
        var identificado=Json(await s.IdentificarParaRetiro(pinRetiro,f.Terminal,ct));
        Assert.True(identificado.GetProperty("pagoAdicionalRequerido").GetBoolean());
        Assert.False(identificado.GetProperty("pagoConfirmado").GetBoolean());
        var foto=Json(await s.CapturarDestinatario(id,f.Terminal,CapturaDemostrativa(true),ct))
            .GetProperty("evidenciaId").GetGuid();
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>
            s.AutorizarRecogidaEncomienda(id,f.Terminal,new(foto,pinRetiro,false),ct));
        Assert.DoesNotContain(Json(await s.CobrosReservaPendientes(ct)).GetProperty("cobros").EnumerateArray(),
            x=>x.GetProperty("reservaId").GetInt32()==id);
        var solicitud=Json(await s.SolicitarPagoReservaTablet(id,f.Terminal,
            new(pinRetiro,"Tarjeta"),ct));
        Assert.False(solicitud.GetProperty("pagoConfirmado").GetBoolean());
        Assert.False(solicitud.GetProperty("pagoRealProcesado").GetBoolean());
        var pendiente=Assert.Single(Json(await s.CobrosReservaPendientes(ct))
            .GetProperty("cobros").EnumerateArray().Where(x=>x.GetProperty("reservaId").GetInt32()==id));
        Assert.Equal("Tablet",pendiente.GetProperty("origen").GetString());
        await s.ConfirmarPagoReservaDemo(id,new(22,null,true),f.Admin,ct);
        var estado=Json(await s.EstadoPagoReservaTablet(id,f.Terminal,ct));
        Assert.True(estado.GetProperty("pagoConfirmado").GetBoolean());
        var autorizada=Json(await s.AutorizarRecogidaEncomienda(id,f.Terminal,new(foto,pinRetiro,false),ct));
        Assert.False(autorizada.GetProperty("retiroConfirmado").GetBoolean());
        await s.ConfirmarRecogidaEncomienda(id,f.Terminal,autorizada.GetProperty("autorizacionRetiro").GetString(),true,ct);
        Assert.True(Json(await s.Detalle(id,f.Persona,ct)).GetProperty("retiradaConfirmada").GetBoolean());
    }

    [Fact]
    public async Task PrototipoNoSobreReservaYLiberaAlCancelarOVencer()
    {
        var f=await Preparar();var cfg=ConfiguracionReserva(f);var s=new EncomiendasOperativasSql(cfg,f.Proteccion,TimeProvider.System);var ct=CancellationToken.None;
        var d=new CrearReservaPrototipoSolicitud("Personal","Normal","Mediano","prototipo",60);
        async Task<int?> Crear(){try{return Json(await s.Crear(f.Persona,"","Mediano",ct,d)).GetProperty("id").GetInt32();}catch(OperacionEncomiendaException){return null;}}
        var intentos=await Task.WhenAll(Crear(),Crear());Assert.Single(intentos,id=>id.HasValue);int id=intentos.First(i=>i.HasValue)!.Value;
        await s.Cancelar(id,f.Persona,new("Ya no desea utilizar el casillero",null,true,1),ct);
        Assert.Single(Json(await s.CatalogoReservas(ct)).GetProperty("opciones").EnumerateArray());
        int segunda=Json(await s.Crear(f.Persona,"","Mediano",ct,d)).GetProperty("id").GetInt32();
        var futura=new EncomiendasOperativasSql(cfg,f.Proteccion,new RelojPrueba(DateTimeOffset.UtcNow.AddDays(2)));
        Assert.Single(Json(await futura.CatalogoReservas(ct)).GetProperty("opciones").EnumerateArray());
        Assert.Equal("Vencida",Json(await futura.Detalle(segunda,f.Persona,ct)).GetProperty("estado").GetString());
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Crear(f.Persona,"","Mediano",ct,d with{Minutos=999}));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Crear(f.Persona,"","Mediano",ct,d with{UbicacionId="inventada"}));
    }

    [Fact]
    public async Task PrototipoRespetaConfiguracionDeshabilitadaYTemperaturaFueraDeRango()
    {
        var f=await Preparar();var cfg=ConfiguracionReserva(f);var s=new EncomiendasOperativasSql(cfg,f.Proteccion,TimeProvider.System);var ct=CancellationToken.None;
        await using var c=new SqlConnection(Cadena);await c.OpenAsync();
        foreach(var (tipo,clase,habilitada,temperatura) in new[]{("Normal",ClaseServicioTemporal.Personal,false,(decimal?)null),("Refrigerado",ClaseServicioTemporal.Frio,false,(decimal?)4),("Refrigerado",ClaseServicioTemporal.Frio,true,(decimal?)20)}){
            var termica=clase==ClaseServicioTemporal.Frio;
            var config=new ConfiguracionServicioTemporal(f.Casillero,clase,null,habilitada,new(){{60,22}},termica?2:null,termica?8:null,temperatura);
            await using var q=new SqlCommand("UPDATE dbo.Casilleros SET Tipo=@tipo WHERE Codigo=@codigo; UPDATE dbo.CompartimentosServiciosTemporales SET ConfiguracionJson=@json WHERE Codigo=@codigo; IF @@ROWCOUNT=0 INSERT dbo.CompartimentosServiciosTemporales(Codigo,ConfiguracionJson)VALUES(@codigo,@json);",c);
            q.Parameters.AddWithValue("@tipo",tipo);q.Parameters.AddWithValue("@codigo",f.Casillero);q.Parameters.AddWithValue("@json",JsonSerializer.Serialize(config));await q.ExecuteNonQueryAsync();
            Assert.Empty(Json(await s.CatalogoReservas(ct)).GetProperty("opciones").EnumerateArray());
            var d=new CrearReservaPrototipoSolicitud("Personal",tipo,"Mediano","prototipo",60);
            await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Crear(f.Persona,"","Mediano",ct,d));
        }
    }

    [Fact]
    public async Task PrototipoHttpExigeSesionPropiedadCsrfYTerminalParaCapturas()
    {
        var f=await Preparar();var cfg=new ConfigurationBuilder().AddConfiguration(ConfiguracionReserva(f)).AddInMemoryCollection(new Dictionary<string,string?>{["RostroV3:RetiroHabilitado"]="true"}).Build();using var fabrica=new FabricaEncomiendas(cfg,manual:true);var ct=CancellationToken.None;
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        Assert.Equal(HttpStatusCode.Unauthorized,(await cliente.GetAsync("/api/web/reservas/disponibilidad",ct)).StatusCode);
        cliente.DefaultRequestHeaders.Add("X-Test-Rol","Cliente");cliente.DefaultRequestHeaders.Add("X-Test-Id",f.Persona.ToString());
        var d=new CrearReservaPrototipoSolicitud("Personal","Normal","Mediano","prototipo",60);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsJsonAsync("/api/web/reservas",d,ct)).StatusCode);
        var csrf=await cliente.GetFromJsonAsync<JsonElement>("/api/web/clientes/csrf-token",ct);cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("csrfToken").GetString());
        var respuesta=await cliente.PostAsJsonAsync("/api/web/reservas",d,ct);Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        int id=(await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Forbidden,(await cliente.PostAsJsonAsync($"/api/terminales/reservas/{id}/depositar",new DepositoDemostrativoSolicitud("123456",CapturaDemostrativa(),true),ct)).StatusCode);
        cliente.DefaultRequestHeaders.Remove("X-Test-Id");cliente.DefaultRequestHeaders.Add("X-Test-Id",(f.Persona+100000).ToString());
        Assert.Equal(HttpStatusCode.NotFound,(await cliente.GetAsync($"/api/web/reservas/{id}",ct)).StatusCode);

        await Consentir(f,id,"Remitente");
        var servicioPrueba=new EncomiendasOperativasSql(cfg,f.Proteccion,TimeProvider.System);
        var tarifa=Json(await servicioPrueba.Detalle(id,f.Persona,ct)).GetProperty("pago").GetProperty("tarifaConfigurada").GetDecimal();
        await servicioPrueba.ConfirmarPago(id,new("Efectivo",tarifa,"PAGO-HTTP-"+Guid.NewGuid().ToString("N"),true,null),f.Admin,ct);
        var deposito=Json(await f.Servicio.EmitirPin(id,"Deposito",false,f.Admin,ct));
        await f.Servicio.ConfirmarEntregaPin(id,new(deposito.GetProperty("id").GetGuid(),"+999123456701","WA-HTTP-DEPOSITO",true),f.Admin,ct);
        using var terminal=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        terminal.DefaultRequestHeaders.Add("X-Test-Rol","Terminal");terminal.DefaultRequestHeaders.Add("X-Test-Id",f.Terminal.ToString());
        var descriptor=Enumerable.Range(0,1024).Select(i=>Math.Sin(i*.173)).ToArray();
        var peticion=new DepositoDemostrativoSolicitud(deposito.GetProperty("pin").GetString()!,new(Convert.ToBase64String(RostroSimuladoTests.Png()),descriptor,"SYNCHRO-HUMANJS-EMBEDDING-1024-v3",EncomiendasOperativasSql.VersionFacialPersonalWeb),true);
        Assert.Equal(HttpStatusCode.BadRequest,(await terminal.PostAsJsonAsync($"/api/terminales/reservas/{id}/depositar",peticion,ct)).StatusCode);
        var token=await terminal.GetFromJsonAsync<JsonElement>("/api/terminales/reservas/csrf-token",ct);terminal.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,(await terminal.PostAsJsonAsync($"/api/terminales/reservas/{id}/depositar",peticion,ct)).StatusCode);
        terminal.DefaultRequestHeaders.Add("X-SYNCHRO-CONSENTIMIENTO","ACEPTO");
        Assert.Equal(HttpStatusCode.OK,(await terminal.PostAsJsonAsync($"/api/terminales/reservas/{id}/depositar",peticion,ct)).StatusCode);
        var original=deposito.GetProperty("pin").GetString()!;
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>f.Servicio.EmitirPin(id,"Retiro",false,f.Admin,ct));
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>f.Servicio.IdentificarParaDeposito(original,f.Terminal,ct));
        var detalle=Json(await f.Servicio.Detalle(id,f.Persona,ct));
        Assert.Equal("Depositada",detalle.GetProperty("estado").GetString());
        Assert.Single(detalle.GetProperty("pines").EnumerateArray());
        Assert.Equal("Entregado",detalle.GetProperty("pines")[0].GetProperty("estado").GetString());

        // El mismo endpoint de "Mis pertenencias" compara V3 y valida el PIN original.
        var csrfManual=await terminal.GetFromJsonAsync<JsonElement>("/api/terminales/guardar/whatsapp/csrf-token",ct);
        terminal.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        terminal.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrfManual.GetProperty("csrfToken").GetString());
        const string ruta="/api/terminales/guardar/whatsapp/retiro";
        var rostro=await terminal.PostAsJsonAsync(ruta+"/rostro-v3",new{descriptor},ct);
        Assert.Equal(HttpStatusCode.OK,rostro.StatusCode);
        var tokenFacial=(await rostro.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("tokenFacial").GetString()!;
        var falso=original=="000000"?"000001":"000000";
        var rechazo=await terminal.PostAsJsonAsync(ruta+"/pin/prevalidar",new{pin=falso,tokenFacial},ct);
        Assert.False(rechazo.IsSuccessStatusCode);
        var resultado=await terminal.PostAsJsonAsync(ruta+"/pin/prevalidar",new{pin=original,tokenFacial},ct);
        Assert.Equal(HttpStatusCode.OK,resultado.StatusCode);
        var cerrado=await resultado.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.True(cerrado.GetProperty("retiroAutorizado").GetBoolean());
        Assert.False(cerrado.GetProperty("retiroConfirmado").GetBoolean());
        Assert.Equal("Ocupado",cerrado.GetProperty("estadoCasillero").GetString());
        Assert.False(cerrado.GetProperty("pagoPendiente").GetBoolean());
        var permiso=cerrado.GetProperty("autorizacionRetiro").GetString();
        var antes=Json(await f.Servicio.Detalle(id,f.Persona,ct));
        Assert.Equal("Depositada",antes.GetProperty("estado").GetString());
        Assert.Equal("Entregado",antes.GetProperty("pines")[0].GetProperty("estado").GetString());
        Assert.False((await terminal.PostAsJsonAsync(ruta+"/reserva/confirmar",new{autorizacionRetiro=permiso,retiroComprobado=false},ct)).IsSuccessStatusCode);
        Assert.False((await terminal.PostAsJsonAsync(ruta+"/reserva/confirmar",new{autorizacionRetiro=new string('X',64),retiroComprobado=true},ct)).IsSuccessStatusCode);
        var fin=await terminal.PostAsJsonAsync(ruta+"/reserva/confirmar",new{autorizacionRetiro=permiso,retiroComprobado=true},ct);
        Assert.Equal(HttpStatusCode.OK,fin.StatusCode);
        Assert.True((await fin.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("retiroConfirmado").GetBoolean());
        var reintento=await terminal.PostAsJsonAsync(ruta+"/reserva/confirmar",new{autorizacionRetiro=permiso,retiroComprobado=true},ct);
        Assert.Equal(HttpStatusCode.OK,reintento.StatusCode);
        Assert.True((await reintento.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("idempotente").GetBoolean());
        Assert.False((await terminal.PostAsJsonAsync(ruta+"/pin/prevalidar",new{pin=original,tokenFacial},ct)).IsSuccessStatusCode);
        detalle=Json(await f.Servicio.Detalle(id,f.Persona,ct));
        Assert.True(detalle.GetProperty("retiradaConfirmada").GetBoolean());
        Assert.Equal("Consumido",detalle.GetProperty("pines")[0].GetProperty("estado").GetString());
        Assert.NotEqual(JsonValueKind.Null,detalle.GetProperty("depositoUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null,detalle.GetProperty("entregaUtc").ValueKind);
    }
}
