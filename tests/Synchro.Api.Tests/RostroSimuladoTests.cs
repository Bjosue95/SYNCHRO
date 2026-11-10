using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Synchro.Api.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Synchro.Api.Services.Clientes;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Tests;

public sealed class RostroSimuladoTests
{
    private sealed class Referencias : IReferenciasRostroSimulado
    {
        public readonly Dictionary<int,ReferenciaRostroSimulado> Datos=[];
        public Task GuardarAsync(ReferenciaRostroSimulado r,CancellationToken t)
        {Datos[r.PersonaId]=r with{Patron=(double[])r.Patron.Clone()};return Task.CompletedTask;}
        public Task<ReferenciaRostroSimulado?> ObtenerAsync(int persona,CancellationToken t)
            =>Task.FromResult(Datos.TryGetValue(persona,out var r)?r with{Patron=(double[])r.Patron.Clone()}:null);
        public Task EliminarAsync(int persona,CancellationToken t){Datos.Remove(persona);return Task.CompletedTask;}
        public Task EliminarVencidasAsync(CancellationToken t)
        {foreach(int p in Datos.Where(x=>x.Value.VenceUtc<=DateTimeOffset.UtcNow).Select(x=>x.Key).ToArray())Datos.Remove(p);return Task.CompletedTask;}
    }
    internal static byte[] Png(bool inverso=false,bool uniforme=false)
    {
        using var imagen=new MemoryStream();imagen.Write(new byte[]{137,80,78,71,13,10,26,10});
        void Chunk(string nombre,byte[] datos)
        {
            byte[] contenido=System.Text.Encoding.ASCII.GetBytes(nombre).Concat(datos).ToArray();
            Span<byte> numero=stackalloc byte[4];BinaryPrimitives.WriteInt32BigEndian(numero,datos.Length);imagen.Write(numero);
            imagen.Write(contenido);BinaryPrimitives.WriteUInt32BigEndian(numero,PatronVisualSimulado.Crc(contenido));imagen.Write(numero);
        }
        byte[] cabecera=new byte[13];BinaryPrimitives.WriteInt32BigEndian(cabecera.AsSpan(0,4),64);BinaryPrimitives.WriteInt32BigEndian(cabecera.AsSpan(4,4),64);cabecera[8]=8;
        Chunk("IHDR",cabecera);using var comprimido=new MemoryStream();
        using(var zip=new ZLibStream(comprimido,CompressionLevel.Fastest,true))
            for(int y=0;y<64;y++){zip.WriteByte(0);for(int x=0;x<64;x++)zip.WriteByte((byte)(uniforme?128:((x<32)^inverso?230:20)));}
        Chunk("IDAT",comprimido.ToArray());Chunk("IEND",[]);return imagen.ToArray();
    }
    [Fact]
    public async Task ComparaCadaPersonaConSuPropiaReferenciaSinProveedorExterno()
    {
        var referencias=new Referencias();byte[] remitente=Png(),destinatario=Png(true);
        await referencias.GuardarAsync(new(41,PatronVisualSimulado.Extraer(remitente)!,DateTimeOffset.UtcNow.AddHours(1)),default);
        await referencias.GuardarAsync(new(42,PatronVisualSimulado.Extraer(destinatario)!,DateTimeOffset.UtcNow.AddHours(1)),default);
        var verificador=new VerificadorFacialSimulado(referencias,new ConfigurationBuilder().Build(),TimeProvider.System);
        Assert.True((await verificador.VerificarAsync(41,new MemoryStream(remitente))).Aprobado);
        Assert.True((await verificador.VerificarAsync(42,new MemoryStream(destinatario))).Aprobado);
        Assert.False((await verificador.VerificarAsync(42,new MemoryStream(remitente))).Aprobado);
        Assert.False((await verificador.VerificarAsync(41,new MemoryStream(destinatario))).Aprobado);
        Assert.False((await verificador.VerificarAsync(99,new MemoryStream(remitente))).Aprobado);
        // La limpieza del adaptador no destruye la referencia persistida.
        Assert.True((await verificador.VerificarAsync(41,new MemoryStream(remitente))).Aprobado);
        referencias.Datos[41]=referencias.Datos[41] with{VenceUtc=DateTimeOffset.UtcNow.AddSeconds(-1)};
        Assert.False((await verificador.VerificarAsync(41,new MemoryStream(remitente))).Aprobado);
        await referencias.EliminarAsync(42,default);
        Assert.False((await verificador.VerificarAsync(42,new MemoryStream(destinatario))).Aprobado);
    }
    [Fact]
    public void RechazaCapturaCorruptaUniformeDesbordadaYDatosQueNoSonImagen()
    {
        Assert.Null(PatronVisualSimulado.Extraer(Png(uniforme:true)));
        Assert.Null(PatronVisualSimulado.Extraer("aprobado=true"u8.ToArray()));
        Assert.Null(PatronVisualSimulado.Extraer(new byte[PatronVisualSimulado.MaximoBytes+1]));
        byte[] corrupta=Png();corrupta[20]^=1;Assert.Null(PatronVisualSimulado.Extraer(corrupta));
        Assert.Null(PatronVisualSimulado.Extraer(Png()[..^1]));
    }
    [Fact]
    public async Task CuentaRealEnrolaSoloSuReferenciaConConsentimientoYLuegoPuedeEliminarla()
    {
        var referencias=new Referencias();using var fabrica=new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>
        {
            s.RemoveAll<IRepositorioClientesWeb>();s.AddSingleton<IRepositorioClientesWeb>(new ClientesWebSimulados());
            s.RemoveAll<IReferenciasRostroSimulado>();s.AddSingleton<IReferenciasRostroSimulado>(referencias);
        }));
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        using var anonima=await cliente.PostAsync("/api/web/clientes/rostro-simulado/referencia",new ByteArrayContent(Png()));
        Assert.Equal(HttpStatusCode.Unauthorized,anonima.StatusCode);
        await PreReservasWebTabletTests.RegistrarAsync(cliente);
        async Task<HttpResponseMessage> Enrolar()
        {
            var contenido=new ByteArrayContent(Png());contenido.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");
            return await cliente.PostAsync("/api/web/clientes/rostro-simulado/referencia?personaId=999",contenido);
        }
        using var sinConsentimiento=await Enrolar();Assert.Equal(HttpStatusCode.BadRequest,sinConsentimiento.StatusCode);Assert.Empty(referencias.Datos);
        cliente.DefaultRequestHeaders.Add("X-SYNCHRO-CONSENTIMIENTO","ACEPTO");
        using var respuesta=await Enrolar();Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        Assert.Contains("\"simulado\":true",await respuesta.Content.ReadAsStringAsync());Assert.True(referencias.Datos.ContainsKey(41));Assert.False(referencias.Datos.ContainsKey(999));
        Assert.True(respuesta.Headers.CacheControl?.NoStore);
        using var eliminada=await cliente.DeleteAsync("/api/web/clientes/rostro-simulado/referencia");Assert.Equal(HttpStatusCode.OK,eliminada.StatusCode);Assert.Empty(referencias.Datos);
    }
    [Fact]
    public async Task TerminalDepositaYRetiraConPngDePersonasDistintasYConservaPinYConsentimiento()
    {
        var referencias=new Referencias();var repo=new RepositorioEncomiendasSimulado();
        await referencias.GuardarAsync(new(41,PatronVisualSimulado.Extraer(Png())!,DateTimeOffset.UtcNow.AddHours(1)),default);
        await referencias.GuardarAsync(new(42,PatronVisualSimulado.Extraer(Png(true))!,DateTimeOffset.UtcNow.AddHours(1)),default);
        using var fabrica=new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>
        {
            s.RemoveAll<IFlujoEncomiendasTerminal>();s.AddSingleton<IFlujoEncomiendasTerminal>(repo);
            s.RemoveAll<IReferenciasRostroSimulado>();s.AddSingleton<IReferenciasRostroSimulado>(referencias);
            s.PostConfigure<CookieAuthenticationOptions>("Destinatarios",o=>o.Events.OnValidatePrincipal=_=>Task.CompletedTask);
        }));
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        cliente.DefaultRequestHeaders.Add("X-Synchro-Test-Terminal","autorizada");cliente.DefaultRequestHeaders.Add("X-SYNCHRO-RESERVA",new string('A',64));
        await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/terminales/encomiendas/csrf-token");
        async Task<HttpResponseMessage> Capturar(string ruta,bool inverso)
        {
            var contenido=new ByteArrayContent(Png(inverso));contenido.Headers.ContentType=new("application/octet-stream");return await cliente.PostAsync(ruta,contenido);
        }
        const string remitente="/api/terminales/encomiendas/reservas/1/evidencias-remitente";
        repo.Consentimiento=false;
        using var sinConsentir=await Capturar(remitente,false);Assert.Equal(HttpStatusCode.NotFound,sinConsentir.StatusCode);Assert.Empty(repo.Remitentes);
        repo.Consentimiento=true;
        using var captura=await Capturar(remitente,false);Assert.Equal(HttpStatusCode.OK,captura.StatusCode);
        using var emision=JsonDocument.Parse(await captura.Content.ReadAsStringAsync());var evidencia=emision.RootElement.GetProperty("evidenciaId").GetGuid();
        using var deposito=await cliente.PostAsJsonAsync("/api/terminales/encomiendas/reservas/1/depositar",new{evidenciaId=evidencia,codigoCasillero="S01"});Assert.Equal(HttpStatusCode.OK,deposito.StatusCode);Assert.Equal(1,repo.Notificaciones);
        var opciones=fabrica.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Destinatarios");
        var principal=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,"42"),new("tipo_usuario","Destinatario")],"Destinatarios"));
        string ticket=opciones.TicketDataFormat.Protect(new AuthenticationTicket(principal,new(),"Destinatarios"));
        cliente.DefaultRequestHeaders.Add("Cookie",opciones.Cookie.Name+"="+ticket);
        const string destinatario="/api/terminales/encomiendas/usos/17/evidencias-destinatario";
        using var personaEquivocada=await Capturar(destinatario,false);Assert.Equal(HttpStatusCode.Forbidden,personaEquivocada.StatusCode);Assert.Empty(repo.Destinatarios);
        using var correcta=await Capturar(destinatario,true);Assert.Equal(HttpStatusCode.OK,correcta.StatusCode);
        using var rostro=JsonDocument.Parse(await correcta.Content.ReadAsStringAsync());var evidenciaRetiro=rostro.RootElement.GetProperty("evidenciaId").GetGuid();
        using var pinIncorrecto=await cliente.PostAsJsonAsync("/api/terminales/encomiendas/usos/17/retirar",new{evidenciaId=evidenciaRetiro,pin="000000"});Assert.Equal(HttpStatusCode.Conflict,pinIncorrecto.StatusCode);Assert.True(repo.Ocupado);
        using var retiro=await cliente.PostAsJsonAsync("/api/terminales/encomiendas/usos/17/retirar",new{evidenciaId=evidenciaRetiro,pin="654321"});Assert.Equal(HttpStatusCode.OK,retiro.StatusCode);Assert.True(repo.PinConsumido);Assert.True(repo.Retirado);
    }

}
