using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

internal sealed class RelojPrototipo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()=>ahora;
}

public sealed class OutboxWhatsAppTests
{
    private static readonly DateTimeOffset Ahora = new(2026,11,10,12,0,0,TimeSpan.Zero);
    private static EnvioPendienteWhatsApp Envio(int intentos=1)=>new(Guid.NewGuid(),Guid.NewGuid(),1,[],intentos,Ahora.AddMinutes(30));

    [Fact]
    public void CifradoAutenticaContenidoOperacionYReserva()
    {
        var protector=new ProtectorMensajeWhatsApp(new EphemeralDataProtectionProvider());
        var id=Guid.NewGuid();var mensaje=new MensajeWhatsApp("+50499990001","pin_retiro",["123456"]);
        byte[] cifrado=protector.Proteger(id,1,mensaje);
        var abierto=protector.Abrir(id,1,cifrado);
        Assert.Equal(mensaje.Telefono,abierto.Telefono);Assert.Equal(mensaje.Variables,abierto.Variables);
        Assert.Throws<CryptographicException>(()=>protector.Abrir(Guid.NewGuid(),1,cifrado));
        Assert.Throws<CryptographicException>(()=>protector.Abrir(id,2,cifrado));
        cifrado[^1]^=1;
        Assert.Throws<CryptographicException>(()=>protector.Abrir(id,1,cifrado));
        Assert.DoesNotContain(mensaje.Telefono,mensaje.ToString());Assert.DoesNotContain("123456",mensaje.ToString());
    }

    [Fact]
    public void ClavesDeOtroServidorNoPuedenAbrirElMensaje()
    {
        var id=Guid.NewGuid();
        var original=new ProtectorMensajeWhatsApp(new EphemeralDataProtectionProvider());
        var otro=new ProtectorMensajeWhatsApp(new EphemeralDataProtectionProvider());
        byte[] cifrado=original.Proteger(id,1,new("+50499990001","pin_retiro",["123456"]));
        Assert.Throws<CryptographicException>(()=>otro.Abrir(id,1,cifrado));
    }

    [Fact]
    public void LimiteDeMetaRespetaRetryAfterYLaCaducidad()
    {
        var envio=Envio();var resultado=new WhatsAppResultado(false,null,"limite",FalloWhatsApp.Reintentable,TimeSpan.FromMinutes(5));
        var cierre=PoliticaEnvioWhatsApp.Decidir(envio,resultado,Ahora);
        Assert.Equal(EstadoEnvioWhatsApp.Reintento,cierre.Estado);Assert.Equal(Ahora.AddMinutes(5),cierre.ReintentarUtc);
        Assert.Equal(EstadoEnvioWhatsApp.Caducado,PoliticaEnvioWhatsApp.Decidir(envio with {ExpiraUtc=Ahora.AddMinutes(4)},resultado,Ahora).Estado);
        Assert.Equal(EstadoEnvioWhatsApp.Fallido,PoliticaEnvioWhatsApp.Decidir(envio with {Intentos=5},resultado,Ahora).Estado);
    }

    [Theory]
    [InlineData(1,30)]
    [InlineData(2,60)]
    [InlineData(3,120)]
    [InlineData(4,240)]
    public void EspaciaLosReintentos(int intento,int segundos)
    {
        var cierre=PoliticaEnvioWhatsApp.Decidir(Envio(intento),new(false,null,"limite",FalloWhatsApp.Reintentable),Ahora);
        Assert.Equal(Ahora.AddSeconds(segundos),cierre.ReintentarUtc);
    }

    [Theory]
    [InlineData(FalloWhatsApp.Indeterminado,"Indeterminado")]
    [InlineData(FalloWhatsApp.Rechazado,"Fallido")]
    [InlineData(FalloWhatsApp.Configuracion,"Fallido")]
    [InlineData(FalloWhatsApp.DatosInvalidos,"Fallido")]
    public void NoReintentaResultadosInseguros(FalloWhatsApp fallo,string esperado)
    {
        var cierre=PoliticaEnvioWhatsApp.Decidir(Envio(),new(false,null,"error",fallo),Ahora);
        Assert.Equal(esperado,cierre.Estado.ToString());Assert.Null(cierre.ReintentarUtc);
    }

    private sealed class Almacen(EnvioPendienteWhatsApp? envio) : IAlmacenOutboxWhatsApp
    {
        public CierreEnvioWhatsApp? Cierre { get;private set; }
        public Task<EnvioPendienteWhatsApp?> TomarAsync(CancellationToken token)=>Task.FromResult(envio);
        public Task CerrarAsync(EnvioPendienteWhatsApp e,CierreEnvioWhatsApp cierre,CancellationToken token)
        { Cierre=cierre;return Task.CompletedTask; }
    }
    private sealed class Cliente(WhatsAppResultado resultado) : IWhatsAppCloudService
    {
        public int Intentos { get;private set; }
        public Task<WhatsAppResultado> EnviarPlantillaAsync(string telefono,string plantilla,IReadOnlyList<string> variables,CancellationToken token=default)
        { Intentos++;return Task.FromResult(resultado); }
    }

    [Theory]
    [InlineData(false,false,"Aceptado",1)]
    [InlineData(true,false,"Caducado",0)]
    [InlineData(false,true,"Fallido",0)]
    public async Task ProcesadorSoloEnviaMensajesVigentesYAutenticos(bool vencido,bool alterado,string esperado,int intentos)
    {
        var protector=new ProtectorMensajeWhatsApp(new EphemeralDataProtectionProvider());
        var envio=Envio();
        envio=envio with {Cifrado=protector.Proteger(envio.Id,envio.ReservaId,new("+50499990001","pin_retiro",["123456"]))};
        if (vencido)envio=envio with {ExpiraUtc=Ahora};
        if (alterado)envio.Cifrado[^1]^=1;
        var almacen=new Almacen(envio);var cliente=new Cliente(new(true,"wamid.prueba","aceptado"));
        await new ProcesadorOutboxWhatsApp(almacen,protector,cliente,new RelojPrototipo(Ahora)).ProcesarUnoAsync();
        Assert.Equal(esperado,almacen.Cierre!.Estado.ToString());Assert.Equal(intentos,cliente.Intentos);
    }

    [Fact]
    public async Task ColaVaciaNoRealizaEnvios()
    {
        var cliente=new Cliente(new(true,"wamid.prueba","aceptado"));var almacen=new Almacen(null);
        var procesador=new ProcesadorOutboxWhatsApp(almacen,new(new EphemeralDataProtectionProvider()),cliente,new RelojPrototipo(Ahora));
        Assert.False(await procesador.ProcesarUnoAsync());Assert.Equal(0,cliente.Intentos);Assert.Null(almacen.Cierre);
    }

    [Fact]
    public void PinEstaVinculadoAUsoYDestinatarioConVigenciaLimitada()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["RetiroPin:ClaveHmac"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }).Build();
        using var pin=new PreparadorPinEncomienda(config,new RelojPrototipo(Ahora)).Crear(1,2);
        Assert.Matches("^[0-9]{6}$",pin.Pin);Assert.Equal(Ahora.AddMinutes(30),pin.VenceUtc);
        Assert.Equal(pin.Hash,CodigoPinRetiro.CalcularHash(pin.Pin,1,2,pin.Salt,config));
        Assert.NotEqual(pin.Hash,CodigoPinRetiro.CalcularHash(pin.Pin,2,2,pin.Salt,config));
        Assert.NotEqual(pin.Hash,CodigoPinRetiro.CalcularHash(pin.Pin,1,3,pin.Salt,config));
        Assert.DoesNotContain(pin.Pin,pin.ToString());
        pin.Dispose();Assert.All(pin.Hash,b=>Assert.Equal((byte)0,b));Assert.All(pin.Salt,b=>Assert.Equal((byte)0,b));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1441")]
    public void PinRechazaVigenciaFueraDelLimite(string minutos)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["RetiroPin:VigenciaMinutos"]=minutos }).Build();
        Assert.Throws<InvalidOperationException>(()=>new PreparadorPinEncomienda(config,new RelojPrototipo(Ahora)).Crear(1,2));
    }
}
