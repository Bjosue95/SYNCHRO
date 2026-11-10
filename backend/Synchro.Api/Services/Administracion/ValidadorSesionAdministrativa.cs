using System.Security.Claims;

namespace Synchro.Api.Services.Administracion;

internal static class ValidadorSesionAdministrativa
{
    public static bool Aplicar(ClaimsPrincipal principal,AccesoAdministrativo acceso)
    {
        if(principal.Identity is not ClaimsIdentity identidad||!identidad.IsAuthenticated||acceso.Roles is not{Length:>0}||
            acceso.VersionSesion<0||!int.TryParse(principal.FindFirstValue(AccesoAdministrativoSql.ClaimVersion)??"0",out int version)||
            version!=acceso.VersionSesion||acceso.Roles.Any(r=>!GestionUsuariosAdministrativos.RolesPermitidos.Contains(r,StringComparer.Ordinal)))return false;
        foreach(var i in principal.Identities)
            foreach(var rol in i.FindAll(i.RoleClaimType).ToArray())i.RemoveClaim(rol);
        foreach(string rol in acceso.Roles)identidad.AddClaim(new(identidad.RoleClaimType,rol));
        return true;
    }
}
