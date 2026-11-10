using Microsoft.AspNetCore.Identity;
using Synchro.Api.Models;

namespace Synchro.Api.Services.Administracion;

internal sealed record AccesoAdministrativo(string[] Roles,int VersionSesion);
public sealed record UsuarioAdministrativo(int Id,string NombreUsuario,bool Activo,string[] Roles);
internal sealed class UsuarioAdministrativoEstado
{
    public int Id {get;set;}
    public string Nombre {get;init;}="";
    public bool Activo {get;set;}=true;
    public string[] Roles {get;set;}=[];
    public int VersionSesion {get;set;}
    public bool Modificado {get;set;}
    public string? NuevoPasswordHash {get;set;}
    public string Accion {get;set;}="";
    public override string ToString()=>"Usuario administrativo protegido";
}
internal interface IRepositorioUsuariosAdministrativos
{
    Task<T> TransaccionAsync<T>(int actor,Func<List<UsuarioAdministrativoEstado>,T> cambio,CancellationToken token);
}
internal sealed class AccesoAdministrativoRechazadoException : Exception;
internal sealed class UsuarioAdministrativoNoEncontradoException : Exception;
internal sealed class ConflictoUsuarioAdministrativoException : Exception;

internal sealed class GestionUsuariosAdministrativos(IRepositorioUsuariosAdministrativos repo,IPasswordHasher<Administrador> hasher)
{
    internal static readonly string[] RolesPermitidos=["Administrador","Operador","Auditor"];
    private static string[] RolesValidos(string[]? roles,bool permitirVacio)
    {
        if(roles is null||roles.Length>3||!permitirVacio&&roles.Length==0||roles.Any(r=>!RolesPermitidos.Contains(r,StringComparer.Ordinal)))
            throw new ArgumentException("Roles invalidos.");
        return roles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
    private static void Password(string? password)
    {ValidacionCredencialesCuenta.Password(password);}
    private static void Actor(int actor){if(actor<=0)throw new ArgumentException("Actor invalido.");}
    private static UsuarioAdministrativo Vista(UsuarioAdministrativoEstado u)=>new(u.Id,u.Nombre,u.Activo,u.Roles);
    private static void ConservarAdministrador(IEnumerable<UsuarioAdministrativoEstado> usuarios)
    {if(!usuarios.Any(u=>u.Activo&&u.Roles.Contains("Administrador",StringComparer.Ordinal)))throw new ConflictoUsuarioAdministrativoException();}
    public async Task<IReadOnlyList<UsuarioAdministrativo>> ListarAsync(int actor,CancellationToken token)
    {Actor(actor);return await repo.TransaccionAsync<IReadOnlyList<UsuarioAdministrativo>>(actor,lista=>lista.Select(Vista).ToArray(),token);}
    public async Task<UsuarioAdministrativo> CrearAsync(int actor,string? nombre,string? password,string[]? roles,CancellationToken token)
    {
        Actor(actor);Password(password);string usuario=ValidacionCredencialesCuenta.Nombre(nombre);
        string[] asignados=RolesValidos(roles,false);
        string hash=hasher.HashPassword(new Administrador{NombreUsuario=usuario},password!);
        var creada=await repo.TransaccionAsync(actor,lista=>
        {
            if(lista.Any(u=>string.Equals(u.Nombre,usuario,StringComparison.OrdinalIgnoreCase)))throw new ConflictoUsuarioAdministrativoException();
            var nueva=new UsuarioAdministrativoEstado{Nombre=usuario,Roles=asignados,VersionSesion=1,Modificado=true,NuevoPasswordHash=hash,Accion="Usuario creado"};
            lista.Add(nueva);ConservarAdministrador(lista);return nueva;
        },token);
        return Vista(creada);
    }
    public async Task<UsuarioAdministrativo> ActualizarAsync(int actor,int id,bool activo,string[]? roles,CancellationToken token)
    {
        Actor(actor);if(id<=0)throw new ArgumentException("Usuario invalido.");string[] asignados=RolesValidos(roles,true);
        var actualizada=await repo.TransaccionAsync(actor,lista=>
        {
            var u=lista.SingleOrDefault(u=>u.Id==id)??throw new UsuarioAdministrativoNoEncontradoException();
            u.Activo=activo;u.Roles=asignados;u.VersionSesion++;u.Modificado=true;u.Accion="Permisos o actividad actualizados";
            ConservarAdministrador(lista);return u;
        },token);return Vista(actualizada);
    }
    public async Task RestablecerPasswordAsync(int actor,int id,string? password,CancellationToken token)
    {
        Actor(actor);Password(password);if(id<=0)throw new ArgumentException("Usuario invalido.");
        await repo.TransaccionAsync(actor,lista=>
        {
            var u=lista.SingleOrDefault(u=>u.Id==id)??throw new UsuarioAdministrativoNoEncontradoException();
            u.NuevoPasswordHash=hasher.HashPassword(new Administrador{Id=id,NombreUsuario=u.Nombre},password!);
            u.VersionSesion++;u.Modificado=true;u.Accion="Contrasena restablecida";return true;
        },token);
    }
}
