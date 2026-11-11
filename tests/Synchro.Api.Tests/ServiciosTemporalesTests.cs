using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services.Temporales;

namespace Synchro.Api.Tests;

internal sealed class RelojServicioTemporal : TimeProvider
{
    private DateTimeOffset _ahora=new(2026,11,10,12,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow()=>_ahora;
    public void Avanzar(TimeSpan tiempo)=>_ahora+=tiempo;
}
internal sealed class RepositorioTemporalSimulado : IRepositorioServicioTemporal
{
    private readonly object _sync=new();
    public Dictionary<string,DatosServicioTemporal> Datos {get;}=new(StringComparer.OrdinalIgnoreCase);
    public List<EventoServicioTemporal> Auditoria {get;}=[];
    public bool TerminalRevocada {get;set;}
    public bool FallarConfirmacion {get;set;}
    public int Incidencias {get;private set;}
    private static T Copia<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static DatosServicioTemporal Copia(DatosServicioTemporal d)
    {
        var copia=new DatosServicioTemporal{Configuracion=Copia(d.Configuracion),EstadoInventario=d.EstadoInventario};
        foreach(var o in d.Operaciones.Values)copia.Operaciones.Add(o.Id,Copia(o));return copia;
    }
    public Task<T> TransaccionAsync<T>(string codigo,Guid? id,int? terminal,Func<DatosServicioTemporal,T> cambio,CancellationToken token,int? clientePersonaId=null)
    {
        token.ThrowIfCancellationRequested();
        lock(_sync)
        {
            if(terminal is not null&&TerminalRevocada||!Datos.TryGetValue(codigo,out var d))throw new ServicioTemporalNoDisponibleException();
            var trabajo=Copia(d);T resultado=cambio(trabajo);
            if(FallarConfirmacion)throw new InvalidOperationException("Fallo sintetico de confirmacion.");
            Assert.InRange(trabajo.Operaciones.Values.Count(o=>o.Ocupa),0,1);
            Datos[codigo]=Copia(trabajo);Auditoria.AddRange(trabajo.Eventos);Incidencias+=trabajo.Incidencias.Count;return Task.FromResult(resultado);
        }
    }
    public Task ConfigurarAsync(ConfiguracionServicioTemporal config,int admin,CancellationToken token)
    {
        lock(_sync)
        {
            if(Datos.TryGetValue(config.Codigo,out var existente))
            {
                if(existente.Configuracion.Clase!=config.Clase||existente.Configuracion.Conector!=config.Conector)throw new ServicioTemporalNoDisponibleException();
                existente.Configuracion=Copia(config);
                if(existente.EstadoInventario is "Disponible" or "Bloqueado")existente.EstadoInventario=config.Habilitado&&config.TemperaturaApta?"Disponible":"Bloqueado";
            }
            else Datos.Add(config.Codigo,new(){Configuracion=Copia(config),EstadoInventario=config.Habilitado&&config.TemperaturaApta?"Disponible":"Bloqueado"});
            return Task.CompletedTask;
        }
    }
    public Task<IReadOnlyList<string>> CodigosAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>(Datos.Keys.ToArray());
    public Task<ReporteServiciosTemporales> ReportarAsync(DateTimeOffset desde,DateTimeOffset hasta,CancellationToken token)
    {
        var operaciones=Datos.Values.SelectMany(d=>d.Operaciones.Values).ToArray();
        long ocupacion=operaciones.Where(o=>o.InicioUtc<hasta&&(o.RetiroUtc is null||o.RetiroUtc>desde))
            .Sum(o=>(long)((o.RetiroUtc is null||o.RetiroUtc>hasta?hasta:o.RetiroUtc.Value)-(o.InicioUtc<desde?desde:o.InicioUtc!.Value)).TotalMinutes);
        return Task.FromResult(new ReporteServiciosTemporales(operaciones.Count(o=>o.CreadaUtc>=desde&&o.CreadaUtc<hasta),
            operaciones.Count(o=>o.Estado is EstadoServicioTemporal.Activa or EstadoServicioTemporal.Agotada),
            operaciones.Count(o=>o.RetiroUtc>=desde&&o.RetiroUtc<hasta),
            operaciones.Where(o=>o.PagoUtc>=desde&&o.PagoUtc<hasta&&!o.PagoAnulado).Sum(o=>o.Monto),Incidencias,ocupacion));
    }
}
public sealed class ServiciosTemporalesTests
{
    internal static IConfiguration Config()=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {["ServiciosTemporales:ClaveHmac"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}).Build();
    internal static ConfiguracionServicioTemporal Compartimento(ClaseServicioTemporal clase=ClaseServicioTemporal.CargaCelular)
        =>new("C01",clase,clase switch{ClaseServicioTemporal.CargaCelular=>"USB-C",ClaseServicioTemporal.CargaLaptop=>"USB-C PD",_=>null},true,
            new(){[30]=10,[60]=18,[90]=25},clase is ClaseServicioTemporal.Frio?2:clase is ClaseServicioTemporal.Calor?40:null,
            clase is ClaseServicioTemporal.Frio?8:clase is ClaseServicioTemporal.Calor?65:null,
            clase is ClaseServicioTemporal.Frio?4:clase is ClaseServicioTemporal.Calor?50:null);
    private static async Task<(ServicioTemporal Servicio,RepositorioTemporalSimulado Repo,RelojServicioTemporal Reloj)> Crear(ClaseServicioTemporal clase=ClaseServicioTemporal.CargaCelular)
    {
        var repo=new RepositorioTemporalSimulado();var reloj=new RelojServicioTemporal();var servicio=new ServicioTemporal(repo,Config(),reloj);
        await servicio.ConfigurarAsync(Compartimento(clase),9,default);return(servicio,repo,reloj);
    }
    [Theory]
    [InlineData(ClaseServicioTemporal.CargaCelular)]
    [InlineData(ClaseServicioTemporal.CargaLaptop)]
    [InlineData(ClaseServicioTemporal.Frio)]
    [InlineData(ClaseServicioTemporal.Calor)]
    [InlineData(ClaseServicioTemporal.Personal)]
    public async Task ReservaPagoDepositoVencimientoYRetiroSeConectanSinCobroPosterior(ClaseServicioTemporal clase)
    {
        var(s,repo,reloj)=await Crear(clase);var reserva=await s.ReservarAsync("C01",30,null,default,41);var id=reserva.Operacion.Id;
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.IniciarAsync("C01",id,reserva.Credencial,1,true,default));
        var pago=await s.PagarAsync("C01",id,reserva.Credencial,Guid.NewGuid(),null,default,41);
        Assert.NotNull(pago.PinRetiro);Assert.Equal(10,pago.Operacion.Monto);
        await s.IniciarAsync("C01",id,reserva.Credencial,1,true,default);reloj.Avanzar(TimeSpan.FromMinutes(31));
        var agotada=await s.ConsultarAsync("C01",id,reserva.Credencial,default);Assert.Equal(EstadoServicioTemporal.Agotada,agotada.Estado);
        Assert.Equal("Ocupado",repo.Datos["C01"].EstadoInventario);
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.CancelarAsync("C01",id,reserva.Credencial,default,41));
        var retiro=await s.RetirarAsync("C01",id,reserva.Credencial,1,pago.PinRetiro!,default);
        Assert.True(retiro.Autorizado);Assert.Equal(10,retiro.Operacion.Monto);Assert.Equal("Disponible",repo.Datos["C01"].EstadoInventario);
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.RetirarAsync("C01",id,reserva.Credencial,1,pago.PinRetiro!,default));
        reloj.Avanzar(TimeSpan.FromMinutes(1));var reporte=await s.ReportarAsync(reloj.GetUtcNow().AddHours(-1),reloj.GetUtcNow(),default);
        Assert.Equal(10,reporte.IngresosSimuladosHnl);Assert.Equal(31,reporte.MinutosOcupadosSimulados);Assert.Equal(1,reporte.Finalizadas);
    }
    [Theory]
    [InlineData(ClaseServicioTemporal.Frio)]
    [InlineData(ClaseServicioTemporal.Calor)]
    public async Task ReservaTermicaAntiguaSinClienteNiOrigenWebNoInicia(ClaseServicioTemporal clase)
    {
        var(s,repo,_)=await Crear(clase);var r=await s.ReservarAsync("C01",30,null,default,41);
        await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),null,default,41);
        repo.Datos["C01"].Operaciones[r.Operacion.Id]=repo.Datos["C01"].Operaciones[r.Operacion.Id]with{ClientePersonaId=null,OrigenReserva=null};
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.IniciarAsync("C01",r.Operacion.Id,r.Credencial,1,true,default));
        Assert.Equal("Reservado",repo.Datos["C01"].EstadoInventario);Assert.Equal(EstadoServicioTemporal.Pagada,repo.Datos["C01"].Operaciones[r.Operacion.Id].Estado);
    }
    [Fact]
    public async Task ReintentoPagoNoDuplicaNiRevelaPinOtraVez()
    {
        var(s,repo,_)=await Crear();var r=await s.ReservarAsync("C01",30,1,default);Guid pago=Guid.NewGuid();
        var primero=await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,pago,1,default);
        var segundo=await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,pago,1,default);
        Assert.NotNull(primero.PinRetiro);Assert.Null(segundo.PinRetiro);Assert.True(segundo.YaConfirmado);
        Assert.Single(repo.Auditoria,e=>e.Accion=="Pago simulado confirmado");
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),1,default));
    }
    [Theory]
    [InlineData(ClaseServicioTemporal.CargaCelular)]
    [InlineData(ClaseServicioTemporal.CargaLaptop)]
    [InlineData(ClaseServicioTemporal.Personal)]
    public async Task PagoEfectivoSimuladoEnTerminalRespetaIdempotenciaYPin(ClaseServicioTemporal clase)
    {
        var(s,repo,_)=await Crear(clase);
        var reserva=await s.ReservarAsync("C01",30,1,default);
        Guid idPago=Guid.NewGuid();
        var primero=await s.PagarAsync("C01",reserva.Operacion.Id,reserva.Credencial,idPago,1,default,metodoPago:"Efectivo");
        var segundo=await s.PagarAsync("C01",reserva.Operacion.Id,reserva.Credencial,idPago,1,default,metodoPago:"Efectivo");
        Assert.Equal(EstadoServicioTemporal.Pagada,primero.Operacion.Estado);
        Assert.Equal("EfectivoSimulado",repo.Datos["C01"].Operaciones[reserva.Operacion.Id].CanalPago);
        Assert.Equal(10,primero.Operacion.Monto);
        Assert.NotNull(primero.PinRetiro);
        Assert.True(segundo.YaConfirmado);
        Assert.Null(segundo.PinRetiro);
        Assert.Single(repo.Auditoria,e=>e.Accion=="Pago simulado confirmado");
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>
            s.PagarAsync("C01",reserva.Operacion.Id,reserva.Credencial,idPago,1,default,metodoPago:"NFC"));
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>
            s.PagarAsync("C01",reserva.Operacion.Id,reserva.Credencial,Guid.NewGuid(),1,default,metodoPago:"Efectivo"));
    }
    [Fact]
    public async Task EfectivoSimuladoNoSeConfirmaDesdeWebNiAceptaMetodosDesconocidos()
    {
        var(s,repo,_)=await Crear();
        var reserva=await s.ReservarAsync("C01",30,1,default);
        await Assert.ThrowsAsync<ArgumentException>(()=>
            s.PagarAsync("C01",reserva.Operacion.Id,reserva.Credencial,Guid.NewGuid(),null,default,metodoPago:"Efectivo"));
        await Assert.ThrowsAsync<ArgumentException>(()=>
            s.PagarAsync("C01",reserva.Operacion.Id,reserva.Credencial,Guid.NewGuid(),1,default,metodoPago:"Transferencia"));
        Assert.Equal(EstadoServicioTemporal.Reservada,repo.Datos["C01"].Operaciones[reserva.Operacion.Id].Estado);
        Assert.Null(repo.Datos["C01"].Operaciones[reserva.Operacion.Id].PagoId);
    }

    [Fact]
    public async Task ReservaPagadaQueVenceSeLiberaYAnulaIngresoSimulado()
    {
        var(s,repo,reloj)=await Crear();var r=await s.ReservarAsync("C01",30,null,default);
        await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),null,default);reloj.Avanzar(TimeSpan.FromMinutes(11));
        await s.DisponibilidadAsync(default);var estado=await s.ConsultarAsync("C01",r.Operacion.Id,r.Credencial,default);
        Assert.Equal(EstadoServicioTemporal.Cancelada,estado.Estado);Assert.True(estado.PagoAnulado);Assert.Equal("Disponible",repo.Datos["C01"].EstadoInventario);
        var reporte=await s.ReportarAsync(reloj.GetUtcNow().AddHours(-1),reloj.GetUtcNow(),default);Assert.Equal(0,reporte.IngresosSimuladosHnl);
    }
    [Fact]
    public async Task PinBloqueadoSeRenuevaConCredencialSinDependerDelTelefonoGuardado()
    {
        var(s,repo,_)=await Crear();var r=await s.ReservarAsync("C01",30,1,default);var p=await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),1,default);
        await s.IniciarAsync("C01",r.Operacion.Id,r.Credencial,1,true,default);
        string incorrecto=p.PinRetiro=="000000"?"000001":"000000";
        for(int i=0;i<5;i++)Assert.False((await s.RetirarAsync("C01",r.Operacion.Id,r.Credencial,1,incorrecto,default)).Autorizado);
        Assert.True((await s.RetirarAsync("C01",r.Operacion.Id,r.Credencial,1,p.PinRetiro!,default)).PinBloqueado);
        var nuevo=await s.RenovarPinAsync("C01",r.Operacion.Id,r.Credencial,1,default);
        Assert.NotEqual(p.PinRetiro,nuevo.PinRetiro);
        Assert.False((await s.RetirarAsync("C01",r.Operacion.Id,r.Credencial,1,p.PinRetiro!,default)).Autorizado);
        Assert.True((await s.RetirarAsync("C01",r.Operacion.Id,r.Credencial,1,nuevo.PinRetiro!,default)).Autorizado);
        Assert.Equal("Disponible",repo.Datos["C01"].EstadoInventario);
    }
    [Fact]
    public async Task CredencialAjenaYTerminalRevocadaNoAbren()
    {
        var(s,repo,_)=await Crear();var r=await s.ReservarAsync("C01",30,1,default);
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.ConsultarAsync("C01",r.Operacion.Id,new string('0',64),default));
        var p=await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),1,default);
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.IniciarAsync("C01",r.Operacion.Id,r.Credencial,2,true,default));
        repo.TerminalRevocada=true;
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.IniciarAsync("C01",r.Operacion.Id,r.Credencial,1,true,default));
        Assert.Equal(EstadoServicioTemporal.Pagada,repo.Datos["C01"].Operaciones[r.Operacion.Id].Estado);
    }
    [Fact]
    public async Task LaptopDebeDeclararseCerradaYUsarConectorCompatible()
    {
        var(s,_,_)=await Crear(ClaseServicioTemporal.CargaLaptop);var r=await s.ReservarAsync("C01",30,1,default);
        await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),1,default);
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.IniciarAsync("C01",r.Operacion.Id,r.Credencial,1,false,default));
        Assert.Throws<ArgumentException>(()=>(Compartimento(ClaseServicioTemporal.CargaLaptop)with{Conector="Lightning"}).Validar());
    }
    [Fact]
    public async Task TemperaturaFueraDeRangoImpideReservarYUnaIncidenciaNoImpideRetirar()
    {
        var(s,repo,_)=await Crear(ClaseServicioTemporal.Frio);
        await s.ConfigurarAsync(Compartimento(ClaseServicioTemporal.Frio)with{TemperaturaSimulada=20},9,default);
        await Assert.ThrowsAsync<ServicioTemporalNoDisponibleException>(()=>s.ReservarAsync("C01",30,null,default,41));
        await s.ConfigurarAsync(Compartimento(ClaseServicioTemporal.Frio),9,default);
        var r=await s.ReservarAsync("C01",30,null,default,41);var p=await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),null,default,41);
        await s.IniciarAsync("C01",r.Operacion.Id,r.Credencial,1,true,default);await s.RegistrarIncidenciaAsync("C01","Simulacion de fallo de sensor",9,default);
        Assert.Equal("Ocupado",repo.Datos["C01"].EstadoInventario);
        Assert.True((await s.RetirarAsync("C01",r.Operacion.Id,r.Credencial,1,p.PinRetiro!,default)).Autorizado);
        Assert.Equal("Bloqueado",repo.Datos["C01"].EstadoInventario);
    }
    [Fact]
    public async Task TemperaturaSimuladaFueraDeRangoRegistraUnaIncidenciaSinDuplicarla()
    {
        var(s,repo,_)=await Crear(ClaseServicioTemporal.Frio);
        await s.ConfigurarAsync(Compartimento(ClaseServicioTemporal.Frio)with{TemperaturaSimulada=20},9,default);
        await s.DisponibilidadAsync(default);await s.DisponibilidadAsync(default);
        Assert.Equal(1,repo.Incidencias);Assert.False(repo.Datos["C01"].Configuracion.Habilitado);
        Assert.Equal("Bloqueado",repo.Datos["C01"].EstadoInventario);
    }
    [Fact]
    public async Task ReintentoDeInicioNoExtiendeElTiempoPagado()
    {
        var(s,repo,reloj)=await Crear();var r=await s.ReservarAsync("C01",30,1,default);
        await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),1,default);
        var inicial=await s.IniciarAsync("C01",r.Operacion.Id,r.Credencial,1,true,default);reloj.Avanzar(TimeSpan.FromMinutes(10));
        var repetida=await s.IniciarAsync("C01",r.Operacion.Id,r.Credencial,1,true,default);
        Assert.Equal(inicial.InicioUtc,repetida.InicioUtc);Assert.Equal(inicial.FinContratadoUtc,repetida.FinContratadoUtc);
        Assert.Single(repo.Auditoria,e=>e.Accion=="Deposito e inicio simulados");
    }
    [Fact]
    public async Task FalloDePersistenciaNoDejaCobroNiOcupacionParcial()
    {
        var(s,repo,_)=await Crear();var r=await s.ReservarAsync("C01",30,1,default);repo.FallarConfirmacion=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),1,default));
        var actual=repo.Datos["C01"].Operaciones[r.Operacion.Id];Assert.Equal(EstadoServicioTemporal.Reservada,actual.Estado);Assert.Null(actual.PinHash);Assert.Null(actual.PagoId);
        Assert.DoesNotContain(repo.Auditoria,e=>e.Accion=="Pago simulado confirmado");
    }
    [Fact]
    public async Task TarifaNuevaNoModificaElPrecioContratado()
    {
        var(s,_,_)=await Crear();var r=await s.ReservarAsync("C01",30,1,default);
        await s.ConfigurarAsync(Compartimento()with{Tarifas=new(){[30]=100,[60]=180,[90]=250}},9,default);
        var p=await s.PagarAsync("C01",r.Operacion.Id,r.Credencial,Guid.NewGuid(),1,default);Assert.Equal(10,p.Operacion.Monto);
    }
    [Theory]
    [InlineData(15)]
    [InlineData(45)]
    [InlineData(120)]
    public async Task CargaSoloOfrece30_60_90Minutos(int minutos)
    {var(s,_,_)=await Crear();await Assert.ThrowsAsync<ArgumentException>(()=>s.ReservarAsync("C01",minutos,null,default));}
    [Fact]
    public async Task TresReservasConcurrentesSoloOcupanUnCompartimento()
    {
        var(s,repo,_)=await Crear();
        async Task<bool> Intentar(){try{await s.ReservarAsync("C01",30,null,default);return true;}catch(ServicioTemporalNoDisponibleException){return false;}}
        var resultados=await Task.WhenAll(Task.Run(Intentar),Task.Run(Intentar),Task.Run(Intentar));Assert.Single(resultados,r=>r);
        Assert.Single(repo.Datos["C01"].Operaciones.Values,o=>o.Ocupa);
    }
}
