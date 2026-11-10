using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Synchro.Api.Services.Administracion;

namespace Synchro.Api.Tests;

public sealed class UsuariosAdministrativosHttpTests
{
    private static WebApplicationFactory<Program> Fabrica(UsuariosAdministrativosSimulados repo)
        =>new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=>
        {
            b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Administracion:UsuariosHabilitados"]="true"}));
            b.ConfigureTestServices(s=>
            {
                s.RemoveAll<IRepositorioUsuariosAdministrativos>();s.AddSingleton<IRepositorioUsuariosAdministrativos>(repo);
                s.AddAuthentication(o=>{o.DefaultScheme="UsuariosSelectorTest";o.DefaultAuthenticateScheme="UsuariosSelectorTest";o.DefaultChallengeScheme="UsuariosSelectorTest";})
                    .AddPolicyScheme("UsuariosSelectorTest",null,o=>o.ForwardDefaultSelector=h=>h.Request.Headers.ContainsKey("X-Test-Admin")?"AdminTemporalTest":AutenticacionTerminalHttpPruebas.Esquema)
                    .AddScheme<AuthenticationSchemeOptions,AdministradorTemporalPruebas>("AdminTemporalTest",_=>{});
            });
        });
    private static async Task<HttpClient> ClienteAsync(WebApplicationFactory<Program> fabrica,string? rol,bool csrf=true)
    {
        var cliente=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost"),HandleCookies=false,AllowAutoRedirect=false});
        if(rol is not null)cliente.DefaultRequestHeaders.Add("X-Test-Admin",rol);
        if(csrf)
        {
            using var r=await cliente.GetAsync("/api/admin/csrf-token");r.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN",json.RootElement.GetProperty("csrfToken").GetString());
            cliente.DefaultRequestHeaders.Add("Cookie",string.Join("; ",r.Headers.GetValues("Set-Cookie").Select(c=>c.Split(';')[0])));
        }
        return cliente;
    }
    [Fact]
    public async Task AdministradorCreaCambiaRolesYRevocaPasswordSinExponerHash()
    {
        var repo=new UsuariosAdministrativosSimulados();using var fabrica=Fabrica(repo);using var cliente=await ClienteAsync(fabrica,"si");
        using var creado=await cliente.PostAsJsonAsync("/api/admin/usuarios",new{nombreUsuario="auditor_http",contrasena=UsuariosAdministrativosTests.PasswordPrueba,roles=new[]{"Auditor"}});
        creado.EnsureSuccessStatusCode();string cuerpo=await creado.Content.ReadAsStringAsync();Assert.DoesNotContain(UsuariosAdministrativosTests.PasswordPrueba,cuerpo);Assert.DoesNotContain("Hash",cuerpo);
        var usuario=(await creado.Content.ReadFromJsonAsync<UsuarioAdministrativo>())!;
        using var modificado=await cliente.PutAsJsonAsync($"/api/admin/usuarios/{usuario.Id}",new{activo=true,roles=new[]{"Operador"}});modificado.EnsureSuccessStatusCode();
        using var password=await cliente.PostAsJsonAsync($"/api/admin/usuarios/{usuario.Id}/password",new{contrasena="Contrasena-nueva-simulada-123"});password.EnsureSuccessStatusCode();
        using var listado=await cliente.GetAsync("/api/admin/usuarios");listado.EnsureSuccessStatusCode();
        Assert.DoesNotContain(repo.Hashes[usuario.Id],await listado.Content.ReadAsStringAsync());Assert.Equal(3,repo.Usuarios.Single(u=>u.Id==usuario.Id).VersionSesion);
    }
    [Theory]
    [InlineData("operador")]
    [InlineData("auditor")]
    public async Task RolesDeLecturaYOperacionNoAdministranUsuarios(string rol)
    {
        var repo=new UsuariosAdministrativosSimulados();using var fabrica=Fabrica(repo);using var cliente=await ClienteAsync(fabrica,rol);
        using var r=await cliente.PostAsJsonAsync("/api/admin/usuarios",new{nombreUsuario="otro_usuario",contrasena=UsuariosAdministrativosTests.PasswordPrueba,roles=new[]{"Administrador"}});
        Assert.Equal(HttpStatusCode.Forbidden,r.StatusCode);Assert.Single(repo.Usuarios);
    }
    [Fact]
    public async Task CambiosSinCsrfYAnonimosSonRechazados()
    {
        var repo=new UsuariosAdministrativosSimulados();using var fabrica=Fabrica(repo);using var admin=await ClienteAsync(fabrica,"si",false);using var anonimo=await ClienteAsync(fabrica,null,false);
        var solicitud=new{nombreUsuario="otro_usuario",contrasena=UsuariosAdministrativosTests.PasswordPrueba,roles=new[]{"Auditor"}};
        using var csrf=await admin.PostAsJsonAsync("/api/admin/usuarios",solicitud);Assert.Equal(HttpStatusCode.BadRequest,csrf.StatusCode);
        using var sinSesion=await anonimo.PostAsJsonAsync("/api/admin/usuarios",solicitud);Assert.Equal(HttpStatusCode.Unauthorized,sinSesion.StatusCode);Assert.Single(repo.Usuarios);
    }
    [Fact]
    public async Task ApiNoPermiteDesactivarAlUltimoAdministrador()
    {
        var repo=new UsuariosAdministrativosSimulados();using var fabrica=Fabrica(repo);using var cliente=await ClienteAsync(fabrica,"si");
        using var r=await cliente.PutAsJsonAsync("/api/admin/usuarios/7",new{activo=false,roles=Array.Empty<string>()});
        Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);Assert.True(repo.Usuarios.Single().Activo);
    }
}
