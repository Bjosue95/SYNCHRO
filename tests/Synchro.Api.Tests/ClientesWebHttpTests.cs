using System.Net;
using System.Net.Http.Json;
using Synchro.Api.Services.Clientes;

namespace Synchro.Api.Tests;
public sealed class ClientesWebHttpTests
{
    [Fact]
    public async Task CookieFirmadaRequierePasswordCorrectaYCuentaActivaEnCadaSolicitud()
    {
        var cuentas=new ClientesWebSimulados();using var fabrica=new FabricaClientesWebHttpPruebas(cuentas);
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        await PreReservasWebTabletTests.RegistrarAsync(cliente);
        Assert.DoesNotContain("Clave-cliente",cuentas.Cuentas[41].PasswordHash);
        using var sesion=await cliente.GetAsync("/api/web/clientes/mi-sesion");Assert.Equal(HttpStatusCode.OK,sesion.StatusCode);
        using var logout=await cliente.PostAsync("/api/web/clientes/logout",null);Assert.Equal(HttpStatusCode.OK,logout.StatusCode);
        using var sinSesion=await cliente.GetAsync("/api/web/clientes/mi-sesion");Assert.Equal(HttpStatusCode.Unauthorized,sinSesion.StatusCode);
        await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/web/clientes/acceso/csrf-token");
        using var incorrecta=await cliente.PostAsJsonAsync("/api/web/clientes/acceso/login",new{nombreUsuario="cliente_prueba",contrasena="Incorrecta-12345"});Assert.Equal(HttpStatusCode.Unauthorized,incorrecta.StatusCode);
        using var correcta=await cliente.PostAsJsonAsync("/api/web/clientes/acceso/login",new{nombreUsuario="cliente_prueba",contrasena="Clave-cliente-solo-pruebas-123"});Assert.Equal(HttpStatusCode.OK,correcta.StatusCode);
        cuentas.Cuentas[41].Activa=false;
        using var revocada=await cliente.GetAsync("/api/web/clientes/mi-sesion");Assert.Equal(HttpStatusCode.Unauthorized,revocada.StatusCode);
        using var inactiva=await cliente.PostAsJsonAsync("/api/web/clientes/acceso/login",new{nombreUsuario="cliente_prueba",contrasena="Clave-cliente-solo-pruebas-123"});Assert.Equal(HttpStatusCode.Unauthorized,inactiva.StatusCode);
    }
    [Fact]
    public async Task RegistroValidaCsrfPasswordTelefonoYDuplicadoSinCrearPersonasAdicionales()
    {
        var cuentas=new ClientesWebSimulados();using var fabrica=new FabricaClientesWebHttpPruebas(cuentas);
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        const string ruta="/api/web/clientes/acceso/registro";
        using var sinCsrf=await cliente.PostAsJsonAsync(ruta,new{nombreUsuario="cliente_prueba",contrasena="Clave-cliente-solo-pruebas-123",telefono="+50499990001"});Assert.Equal(HttpStatusCode.BadRequest,sinCsrf.StatusCode);
        await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/web/clientes/acceso/csrf-token");
        using var debil=await cliente.PostAsJsonAsync(ruta,new{nombreUsuario="cliente_prueba",contrasena="123",telefono="+50499990001"});Assert.Equal(HttpStatusCode.BadRequest,debil.StatusCode);
        using var telefono=await cliente.PostAsJsonAsync(ruta,new{nombreUsuario="cliente_prueba",contrasena="Clave-cliente-solo-pruebas-123",telefono="no-numero"});Assert.Equal(HttpStatusCode.BadRequest,telefono.StatusCode);Assert.Empty(cuentas.Cuentas);
        await PreReservasWebTabletTests.RegistrarAsync(cliente);
        await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/web/clientes/acceso/csrf-token");
        using var duplicada=await cliente.PostAsJsonAsync(ruta,new{nombreUsuario="cliente_prueba",contrasena="Clave-cliente-solo-pruebas-123",telefono="+50499990001"});Assert.Equal(HttpStatusCode.Conflict,duplicada.StatusCode);Assert.Single(cuentas.Cuentas);
    }
    [Fact]
    public async Task EliminarCuentaExigeSesionCsrfClaveYNoTocaOtrasCuentas()
    {
        var cuentas=new ClientesWebSimulados();
        using var fabrica=new FabricaClientesWebHttpPruebas(cuentas,feria:true);
        using var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        using var otro=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        const string ruta="/api/web/clientes/eliminar-cuenta", clave="Clave-cliente-solo-pruebas-123";
        using var anonima=await cliente.PostAsJsonAsync(ruta,new{contrasena=clave});
        Assert.Equal(HttpStatusCode.Unauthorized,anonima.StatusCode);
        await PreReservasWebTabletTests.RegistrarAsync(cliente,"cliente_borrar");
        await PreReservasWebTabletTests.RegistrarAsync(otro,"cliente_conservar","+50499990002");
        cliente.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var sinCsrf=await cliente.PostAsJsonAsync(ruta,new{contrasena=clave});
        Assert.Equal(HttpStatusCode.BadRequest,sinCsrf.StatusCode);
        await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/web/clientes/csrf-token");
        using var claveMala=await cliente.PostAsJsonAsync(ruta,new{contrasena="Incorrecta-12345"});
        Assert.Equal(HttpStatusCode.BadRequest,claveMala.StatusCode);
        cuentas.ConVinculos.Add(41);
        using var vinculada=await cliente.PostAsJsonAsync(ruta,new{contrasena=clave});
        Assert.Equal(HttpStatusCode.Conflict,vinculada.StatusCode);
        Assert.Equal(2,cuentas.Cuentas.Count);
        cuentas.ConVinculos.Clear();
        using var eliminada=await cliente.PostAsJsonAsync(ruta,new{contrasena=clave,personaId=42});
        Assert.Equal(HttpStatusCode.OK,eliminada.StatusCode);
        Assert.False(cuentas.Cuentas.ContainsKey(41));
        Assert.True(cuentas.Cuentas.ContainsKey(42));
        using var sinSesion=await cliente.GetAsync("/api/web/clientes/mi-sesion");
        Assert.Equal(HttpStatusCode.Unauthorized,sinSesion.StatusCode);
        using var otraSesion=await otro.GetAsync("/api/web/clientes/mi-sesion");
        Assert.Equal(HttpStatusCode.OK,otraSesion.StatusCode);
        await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/web/clientes/acceso/csrf-token");
        using var login=await cliente.PostAsJsonAsync("/api/web/clientes/acceso/login",new{nombreUsuario="cliente_borrar",contrasena=clave});
        Assert.Equal(HttpStatusCode.Unauthorized,login.StatusCode);
    }
}
