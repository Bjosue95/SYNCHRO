using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Synchro.Api.Services.Clientes;
using Xunit.Abstractions;

namespace Synchro.Api.Tests;

public sealed class CargaClientesWebHttpTests(ITestOutputHelper salida)
{
    [Theory]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public async Task RegistroLoginYSesionConcurrentesEnFeria(int usuarios)
    {
        var cuentas = new ClientesWebSimulados();
        using var fabrica = new FabricaClientesWebHttpPruebas(cuentas, feria: true);
        using var arranque = fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        const string clave = "Carga-local-solo-pruebas-2026";
        var inicio = Stopwatch.StartNew();
        var duraciones = await Task.WhenAll(Enumerable.Range(1,usuarios).Select(async i =>
        {
            using var cliente = fabrica.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var reloj = Stopwatch.StartNew();
            string nombre = $"carga_web_{i:D4}";
            await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/web/clientes/acceso/csrf-token");
            using var alta = await cliente.PostAsJsonAsync("/api/web/clientes/acceso/registro",
                new { nombreUsuario=nombre, contrasena=clave, telefono=$"+99912345{i:D4}" });
            Assert.Equal(HttpStatusCode.OK,alta.StatusCode);
            await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/web/clientes/csrf-token");
            using var logout = await cliente.PostAsync("/api/web/clientes/logout",null);
            Assert.Equal(HttpStatusCode.OK,logout.StatusCode);
            await PreReservasWebTabletTests.CsrfAsync(cliente,"/api/web/clientes/acceso/csrf-token");
            using var login = await cliente.PostAsJsonAsync("/api/web/clientes/acceso/login",new { nombreUsuario=nombre,contrasena=clave });
            Assert.Equal(HttpStatusCode.OK,login.StatusCode);
            using var sesion = await cliente.GetAsync("/api/web/clientes/mi-sesion");
            Assert.Equal(HttpStatusCode.OK,sesion.StatusCode);
            var perfil=await sesion.Content.ReadFromJsonAsync<PerfilClienteWeb>();
            Assert.Equal(nombre,perfil!.NombreUsuario);
            return reloj.Elapsed.TotalMilliseconds;
        }));
        Assert.Equal(usuarios,cuentas.Cuentas.Count);
        Array.Sort(duraciones);
        salida.WriteLine($"HTTP en memoria: {usuarios}/{usuarios}, total {inicio.Elapsed.TotalSeconds:F2}s, p95 recorrido {duraciones[(int)Math.Ceiling(usuarios*.95)-1]:F0}ms, max {duraciones[^1]:F0}ms");
    }

    [Fact]
    public void CuotaPorNombreNoSeEvitaCambiandoMayusculas()
    {
        using var limites=new LimitesAccesoClientes();
        for(int i=0;i<5;i++) { using var intento=limites.IntentoLogin("carga_test"); Assert.True(intento.IsAcquired); }
        using var rechazado=limites.IntentoLogin("CARGA_TEST"); Assert.False(rechazado.IsAcquired);
        using var otro=limites.IntentoLogin("otro_test"); Assert.True(otro.IsAcquired);
    }
}
