using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Synchro.Api.Services.Clientes;

namespace Synchro.Api.Tests;
public sealed class ClientesWebHttpTests
{
    [Fact]
    public async Task CookieFirmadaRequierePasswordCorrectaYCuentaActivaEnCadaSolicitud()
    {
        var cuentas=new ClientesWebSimulados();using var fabrica=new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>
        {s.RemoveAll<IRepositorioClientesWeb>();s.AddSingleton<IRepositorioClientesWeb>(cuentas);}));
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
        var cuentas=new ClientesWebSimulados();using var fabrica=new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>b.ConfigureTestServices(s=>
        {s.RemoveAll<IRepositorioClientesWeb>();s.AddSingleton<IRepositorioClientesWeb>(cuentas);}));
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
}
