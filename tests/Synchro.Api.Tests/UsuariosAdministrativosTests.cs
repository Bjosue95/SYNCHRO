using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Synchro.Api.Models;
using Synchro.Api.Services.Administracion;

namespace Synchro.Api.Tests;

internal sealed class UsuariosAdministrativosSimulados : IRepositorioUsuariosAdministrativos
{
    private readonly object _sync=new();
    public List<UsuarioAdministrativoEstado> Usuarios {get;private set;}=[new(){Id=7,Nombre="root-prueba",Roles=["Administrador"]}];
    public Dictionary<int,string> Hashes {get;}=[];
    public List<string> Auditoria {get;}=[];
    public bool FallarConfirmacion {get;set;}
    public Task<T> TransaccionAsync<T>(int actor,Func<List<UsuarioAdministrativoEstado>,T> cambio,CancellationToken token)
    {
        lock(_sync)
        {
            if(!Usuarios.Any(u=>u.Id==actor&&u.Activo&&u.Roles.Contains("Administrador")))throw new AccesoAdministrativoRechazadoException();
            var copia=Usuarios.Select(u=>new UsuarioAdministrativoEstado{Id=u.Id,Nombre=u.Nombre,Activo=u.Activo,Roles=u.Roles.ToArray(),VersionSesion=u.VersionSesion}).ToList();
            T resultado=cambio(copia);if(FallarConfirmacion)throw new InvalidOperationException("Fallo sintetico de confirmacion.");
            foreach(var u in copia.Where(u=>u.Modificado))
            {
                if(u.Id==0)u.Id=copia.Max(u=>u.Id)+1;
                if(u.NuevoPasswordHash is not null)Hashes[u.Id]=u.NuevoPasswordHash;
                Auditoria.Add(u.Accion);
            }
            Usuarios=copia.Select(u=>new UsuarioAdministrativoEstado{Id=u.Id,Nombre=u.Nombre,Activo=u.Activo,Roles=u.Roles.ToArray(),VersionSesion=u.VersionSesion}).ToList();
            return Task.FromResult(resultado);
        }
    }
}
public sealed class UsuariosAdministrativosTests
{
    internal const string PasswordPrueba="Solo-pruebas-Administracion-123";
    private static ClaimsPrincipal Principal(string rol="Administrador",int? version=null)
    {
        var identidad=new ClaimsIdentity([new(ClaimTypes.NameIdentifier,"8"),new(ClaimTypes.Role,rol)],"Cookies");
        if(version is not null)identidad.AddClaim(new(AccesoAdministrativoSql.ClaimVersion,version.Value.ToString()));return new(identidad);
    }
    [Fact]
    public async Task CuentaCreadaNoAlmacenaPasswordPlanoYNoConcedeAdministradorPorDefecto()
    {
        var repo=new UsuariosAdministrativosSimulados();var hasher=new PasswordHasher<Administrador>();var servicio=new GestionUsuariosAdministrativos(repo,hasher);
        var usuario=await servicio.CrearAsync(7,"auditor_prueba",PasswordPrueba,["Auditor"],default);
        Assert.Equal(new[]{"Auditor"},usuario.Roles);Assert.NotEqual(PasswordPrueba,repo.Hashes[usuario.Id]);
        Assert.Equal(PasswordVerificationResult.Success,hasher.VerifyHashedPassword(new(),repo.Hashes[usuario.Id],PasswordPrueba));
        Assert.False(ValidadorSesionAdministrativa.Aplicar(Principal(version:1),new([],1)));
    }
    [Fact]
    public async Task NoSeDesactivaNiSeEliminaRolDelUltimoAdministrador()
    {
        var repo=new UsuariosAdministrativosSimulados();var servicio=new GestionUsuariosAdministrativos(repo,new PasswordHasher<Administrador>());
        await Assert.ThrowsAsync<ConflictoUsuarioAdministrativoException>(()=>servicio.ActualizarAsync(7,7,false,[],default));
        await Assert.ThrowsAsync<ConflictoUsuarioAdministrativoException>(()=>servicio.ActualizarAsync(7,7,true,["Auditor"],default));
        Assert.True(repo.Usuarios.Single().Activo);Assert.Equal(new[]{"Administrador"},repo.Usuarios.Single().Roles);Assert.Empty(repo.Auditoria);
    }
    [Fact]
    public async Task DosDesactivacionesConcurrentesConservanUnAdministrador()
    {
        var repo=new UsuariosAdministrativosSimulados();var servicio=new GestionUsuariosAdministrativos(repo,new PasswordHasher<Administrador>());
        var segundo=await servicio.CrearAsync(7,"segundo_admin",PasswordPrueba,["Administrador"],default);
        async Task<bool> Desactivar(int id){try{await servicio.ActualizarAsync(id,id,false,[],default);return true;}catch(ConflictoUsuarioAdministrativoException){return false;}}
        var resultados=await Task.WhenAll(Task.Run(()=>Desactivar(7)),Task.Run(()=>Desactivar(segundo.Id)));
        Assert.Single(resultados,r=>r);Assert.Single(repo.Usuarios,u=>u.Activo&&u.Roles.Contains("Administrador"));
    }
    [Fact]
    public async Task CambiosDePermisosODePasswordRevocanLasSesionesAnteriores()
    {
        var repo=new UsuariosAdministrativosSimulados();var servicio=new GestionUsuariosAdministrativos(repo,new PasswordHasher<Administrador>());
        var usuario=await servicio.CrearAsync(7,"operador_prueba",PasswordPrueba,["Operador"],default);
        var estado=repo.Usuarios.Single(u=>u.Id==usuario.Id);int original=estado.VersionSesion;
        await servicio.ActualizarAsync(7,usuario.Id,true,["Auditor"],default);estado=repo.Usuarios.Single(u=>u.Id==usuario.Id);
        Assert.False(ValidadorSesionAdministrativa.Aplicar(Principal("Operador",original),new(estado.Roles,estado.VersionSesion)));
        int permisos=estado.VersionSesion;
        await servicio.RestablecerPasswordAsync(7,usuario.Id,"Nueva-clave-solo-pruebas-456",default);estado=repo.Usuarios.Single(u=>u.Id==usuario.Id);
        Assert.False(ValidadorSesionAdministrativa.Aplicar(Principal("Auditor",permisos),new(estado.Roles,estado.VersionSesion)));
    }
    [Fact]
    public void RevalidacionSustituyeRolesDelCookiePorLosDelServidor()
    {
        var principal=Principal(version:3);
        Assert.True(ValidadorSesionAdministrativa.Aplicar(principal,new(["Auditor"],3)));
        Assert.True(principal.IsInRole("Auditor"));Assert.False(principal.IsInRole("Administrador"));
        Assert.True(ValidadorSesionAdministrativa.Aplicar(Principal(),new(["Administrador"],0)));
        Assert.False(ValidadorSesionAdministrativa.Aplicar(Principal(),new(["Administrador"],1)));
    }
    [Theory]
    [InlineData("SuperAdministrador")]
    [InlineData("administrador")]
    public async Task RolesDesconocidosNoSeConviertenEnPrivilegios(string rol)
    {
        var servicio=new GestionUsuariosAdministrativos(new UsuariosAdministrativosSimulados(),new PasswordHasher<Administrador>());
        await Assert.ThrowsAsync<ArgumentException>(()=>servicio.CrearAsync(7,"usuario_prueba",PasswordPrueba,[rol],default));
        Assert.False(ValidadorSesionAdministrativa.Aplicar(Principal(version:1),new([rol],1)));
    }
    [Fact]
    public async Task FalloDePersistenciaNoDejaUsuarioNiCredencialesParciales()
    {
        var repo=new UsuariosAdministrativosSimulados{FallarConfirmacion=true};var servicio=new GestionUsuariosAdministrativos(repo,new PasswordHasher<Administrador>());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>servicio.CrearAsync(7,"usuario_prueba",PasswordPrueba,["Auditor"],default));
        Assert.Single(repo.Usuarios);Assert.Empty(repo.Hashes);Assert.Empty(repo.Auditoria);
    }
}
