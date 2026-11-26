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
        ["ReservasPrototipo:Casilleros:0"]=f.Casillero,["ReservasPrototipo:Ubicacion"]="Inventario de pruebas",["ReservasPrototipo:DuracionesMinutos:0"]="30",
        ["ReservasPrototipo:DuracionesMinutos:1"]="60"}).Build();
    private static CapturaEncomiendaSolicitud CapturaDemostrativa(bool retiro=false)=>new(Convert.ToBase64String(RostroSimuladoTests.Png(retiro)),null,
        "SYNCHRO-CAPTURA-DEMOSTRATIVA-v1",EncomiendasOperativasSql.VersionCapturaDemostrativa);

    [Theory]
    [InlineData("Personal","Normal")]
    [InlineData("Encomienda","Normal")]
    [InlineData("Encomienda","Refrigerado")]
    [InlineData("Encomienda","Caliente")]
    public async Task PrototipoReservaTiposConPinesRealesYCapturasDiferentesSinReferenciaFacial(string servicio,string tipo)
    {
        var f=await Preparar();var cfg=ConfiguracionReserva(f);var s=new EncomiendasOperativasSql(cfg,f.Proteccion,TimeProvider.System);var ct=CancellationToken.None;
        if(tipo!="Normal") {
            var termica=new ConfiguracionServicioTemporal(f.Casillero,tipo=="Refrigerado"?ClaseServicioTemporal.Frio:ClaseServicioTemporal.Calor,null,true,new(){{30,22}},
                tipo=="Refrigerado"?2:50,tipo=="Refrigerado"?8:70,tipo=="Refrigerado"?4:60);
            await using var c=new SqlConnection(Cadena);await c.OpenAsync();await using var q=new SqlCommand("UPDATE dbo.Casilleros SET Tipo=@tipo WHERE Codigo=@codigo; INSERT dbo.CompartimentosServiciosTemporales(Codigo,ConfiguracionJson) VALUES(@codigo,@config);",c);
            q.Parameters.AddWithValue("@tipo",tipo);q.Parameters.AddWithValue("@codigo",f.Casillero);q.Parameters.AddWithValue("@config",JsonSerializer.Serialize(termica));await q.ExecuteNonQueryAsync();
        }
        var catalogo=Json(await s.CatalogoReservas(ct));Assert.Single(catalogo.GetProperty("opciones").EnumerateArray());
        Assert.Equal(tipo,catalogo.GetProperty("opciones")[0].GetProperty("tipo").GetString());
        var solicitud=new CrearReservaPrototipoSolicitud(servicio,tipo,"Mediano","prototipo",30,servicio=="Encomienda"?"+999123456702":null);
        int id=Json(await s.Crear(f.Persona,solicitud.TelefonoDestinatario??"","Mediano",ct,solicitud)).GetProperty("id").GetInt32();
        Assert.Empty(Json(await s.CatalogoReservas(ct)).GetProperty("opciones").EnumerateArray());
        var bandeja=Json(await s.BandejaWhatsApp(new(Servicio:"Encomiendas",Referencia:id.ToString()),ct));
        Assert.Equal(servicio=="Personal"?1:2,bandeja.GetProperty("total").GetInt32());
        Assert.All(bandeja.GetProperty("solicitudes").EnumerateArray(),x=>{Assert.Equal(servicio,x.GetProperty("claseReserva").GetString());Assert.Equal(tipo,x.GetProperty("tipoCasillero").GetString());});
        if(tipo!="Normal"){
            var anterior=new ServicioTemporal(new RepositorioServicioTemporalSql(cfg),cfg,TimeProvider.System);
            await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>anterior.ReservarAsync(f.Casillero,30,null,ct,f.Persona));
        }
        await Consentir(f,id,"Remitente");if(servicio=="Encomienda")await Consentir(f,id,"Destinatario");
        await s.ConfirmarPago(id,new("Efectivo",22,"PAGO-PROTOTIPO-"+Guid.NewGuid().ToString("N"),true,null),f.Admin,ct);
        Assert.False(Json(await s.ConsultarReferencia(f.Persona,ct)).GetProperty("registrada").GetBoolean());
        var deposito=Json(await s.EmitirPin(id,"Deposito",false,f.Admin,ct));
        await s.ConfirmarEntregaPin(id,new(deposito.GetProperty("id").GetGuid(),"+999123456701","WA-DEPOSITO-TEST",true),f.Admin,ct);
        var pinDeposito=deposito.GetProperty("pin").GetString()!;var falso=pinDeposito=="000000"?"000001":"000000";
        await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.DepositarDemostrativo(id,f.Terminal,new(falso,CapturaDemostrativa(),true),ct));
        Assert.Equal("Preparacion",Json(await s.Detalle(id,f.Persona,ct)).GetProperty("estado").GetString());
        await s.DepositarDemostrativo(id,f.Terminal,new(pinDeposito,CapturaDemostrativa(),true),ct);
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
        await s.Retirar(id,f.Terminal,new(evidencia,retiro.GetProperty("pin").GetString()!,true),ct);
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
        var solicitud=new CrearReservaPrototipoSolicitud("Personal",tipo,"Mediano","prototipo",30);
        var rechazo=await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>
            s.Crear(f.Persona,"","Mediano",CancellationToken.None,solicitud));
        Assert.Equal("CasilleroExclusivoEncomiendas",rechazo.Codigo);
        Assert.Equal(400,rechazo.Estado);
    }

    [Fact]
    public async Task PrototipoNoSobreReservaYLiberaAlCancelarOVencer()
    {
        var f=await Preparar();var cfg=ConfiguracionReserva(f);var s=new EncomiendasOperativasSql(cfg,f.Proteccion,TimeProvider.System);var ct=CancellationToken.None;
        var d=new CrearReservaPrototipoSolicitud("Personal","Normal","Mediano","prototipo",30);
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
            var config=new ConfiguracionServicioTemporal(f.Casillero,clase,null,habilitada,new(){{30,22}},termica?2:null,termica?8:null,temperatura);
            await using var q=new SqlCommand("UPDATE dbo.Casilleros SET Tipo=@tipo WHERE Codigo=@codigo; UPDATE dbo.CompartimentosServiciosTemporales SET ConfiguracionJson=@json WHERE Codigo=@codigo; IF @@ROWCOUNT=0 INSERT dbo.CompartimentosServiciosTemporales(Codigo,ConfiguracionJson)VALUES(@codigo,@json);",c);
            q.Parameters.AddWithValue("@tipo",tipo);q.Parameters.AddWithValue("@codigo",f.Casillero);q.Parameters.AddWithValue("@json",JsonSerializer.Serialize(config));await q.ExecuteNonQueryAsync();
            Assert.Empty(Json(await s.CatalogoReservas(ct)).GetProperty("opciones").EnumerateArray());
            var d=new CrearReservaPrototipoSolicitud("Personal",tipo,"Mediano","prototipo",30);
            await Assert.ThrowsAsync<OperacionEncomiendaException>(()=>s.Crear(f.Persona,"","Mediano",ct,d));
        }
    }

    [Fact]
    public async Task PrototipoHttpExigeSesionPropiedadCsrfYTerminalParaCapturas()
    {
        var f=await Preparar();using var fabrica=new FabricaEncomiendas(ConfiguracionReserva(f));var ct=CancellationToken.None;
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        Assert.Equal(HttpStatusCode.Unauthorized,(await cliente.GetAsync("/api/web/reservas/disponibilidad",ct)).StatusCode);
        cliente.DefaultRequestHeaders.Add("X-Test-Rol","Cliente");cliente.DefaultRequestHeaders.Add("X-Test-Id",f.Persona.ToString());
        var d=new CrearReservaPrototipoSolicitud("Personal","Normal","Mediano","prototipo",30);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsJsonAsync("/api/web/reservas",d,ct)).StatusCode);
        var csrf=await cliente.GetFromJsonAsync<JsonElement>("/api/web/clientes/csrf-token",ct);cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN",csrf.GetProperty("csrfToken").GetString());
        var respuesta=await cliente.PostAsJsonAsync("/api/web/reservas",d,ct);Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        int id=(await respuesta.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Forbidden,(await cliente.PostAsJsonAsync($"/api/terminales/reservas/{id}/depositar",new DepositoDemostrativoSolicitud("123456",CapturaDemostrativa(),true),ct)).StatusCode);
        cliente.DefaultRequestHeaders.Remove("X-Test-Id");cliente.DefaultRequestHeaders.Add("X-Test-Id",(f.Persona+100000).ToString());
        Assert.Equal(HttpStatusCode.NotFound,(await cliente.GetAsync($"/api/web/reservas/{id}",ct)).StatusCode);

        await Consentir(f,id,"Remitente");await f.Servicio.ConfirmarPago(id,new("Efectivo",22,"PAGO-HTTP-"+Guid.NewGuid().ToString("N"),true,null),f.Admin,ct);
        var deposito=Json(await f.Servicio.EmitirPin(id,"Deposito",false,f.Admin,ct));
        await f.Servicio.ConfirmarEntregaPin(id,new(deposito.GetProperty("id").GetGuid(),"+999123456701","WA-HTTP-DEPOSITO",true),f.Admin,ct);
        using var terminal=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
        terminal.DefaultRequestHeaders.Add("X-Test-Rol","Terminal");terminal.DefaultRequestHeaders.Add("X-Test-Id",f.Terminal.ToString());
        var peticion=new DepositoDemostrativoSolicitud(deposito.GetProperty("pin").GetString()!,CapturaDemostrativa(),true);
        Assert.Equal(HttpStatusCode.BadRequest,(await terminal.PostAsJsonAsync($"/api/terminales/reservas/{id}/depositar",peticion,ct)).StatusCode);
        var token=await terminal.GetFromJsonAsync<JsonElement>("/api/terminales/reservas/csrf-token",ct);terminal.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,(await terminal.PostAsJsonAsync($"/api/terminales/reservas/{id}/depositar",peticion,ct)).StatusCode);
        terminal.DefaultRequestHeaders.Add("X-SYNCHRO-CONSENTIMIENTO","ACEPTO");
        Assert.Equal(HttpStatusCode.OK,(await terminal.PostAsJsonAsync($"/api/terminales/reservas/{id}/depositar",peticion,ct)).StatusCode);
        var retiro=Json(await f.Servicio.EmitirPin(id,"Retiro",false,f.Admin,ct));
        await f.Servicio.ConfirmarEntregaPin(id,new(retiro.GetProperty("id").GetGuid(),"+999123456701","WA-HTTP-RETIRO",true),f.Admin,ct);
        var identificada=await terminal.PostAsJsonAsync("/api/terminales/reservas/retiros/identificar",new{pin=retiro.GetProperty("pin").GetString()},ct);
        Assert.Equal(HttpStatusCode.OK,identificada.StatusCode);Assert.Equal(id,(await identificada.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("encomiendaId").GetInt32());
        var captura=await terminal.PostAsJsonAsync($"/api/terminales/reservas/{id}/captura-retiro",CapturaDemostrativa(true),ct);Assert.Equal(HttpStatusCode.OK,captura.StatusCode);
        var evidencia=(await captura.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("evidenciaId").GetGuid();
        Assert.Equal(HttpStatusCode.OK,(await terminal.PostAsJsonAsync($"/api/terminales/reservas/{id}/retirar",new RetiroOperativoSolicitud(evidencia,retiro.GetProperty("pin").GetString()!,true),ct)).StatusCode);
    }
}
