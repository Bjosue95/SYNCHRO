using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.Administracion;

namespace Synchro.Api.Endpoints;

public sealed record CrearUsuarioAdministrativoSolicitud(string NombreUsuario,string Contrasena,string[] Roles)
{public override string ToString()=>"Alta administrativa protegida";}
public sealed record CambiarAccesoAdministrativoSolicitud(bool Activo,string[] Roles);
public sealed record RestablecerPasswordAdministrativoSolicitud(string Contrasena)
{public override string ToString()=>"Restablecimiento de credenciales protegido";}

public static class UsuariosAdministrativosEndpoints
{
    public static void MapUsuariosAdministrativosEndpoints(this WebApplication app)
    {
        var rutas=app.MapGroup("/api/admin/usuarios").RequireAuthorization("SoloAdministradores")
            .AddEndpointFilter(new ModuloPrototipoHabilitadoFilter(app.Configuration,"Administracion:UsuariosHabilitados"))
            .RequireRateLimiting("OperacionesTerminal");
        rutas.MapGet("/",(HttpContext http,GestionUsuariosAdministrativos gestion)=>
            Ejecutar(()=>gestion.ListarAsync(Actor(http),http.RequestAborted)));
        rutas.MapPost("/",(CrearUsuarioAdministrativoSolicitud datos,HttpContext http,GestionUsuariosAdministrativos gestion)=>
            Ejecutar(()=>gestion.CrearAsync(Actor(http),datos.NombreUsuario,datos.Contrasena,datos.Roles,http.RequestAborted)))
            .AddEndpointFilter<OperacionTerminalSeguraFilter>();
        rutas.MapPut("/{id:int}",(int id,CambiarAccesoAdministrativoSolicitud datos,HttpContext http,GestionUsuariosAdministrativos gestion)=>
            Ejecutar(()=>gestion.ActualizarAsync(Actor(http),id,datos.Activo,datos.Roles,http.RequestAborted)))
            .AddEndpointFilter<OperacionTerminalSeguraFilter>();
        rutas.MapPost("/{id:int}/password",async(int id,RestablecerPasswordAdministrativoSolicitud datos,HttpContext http,GestionUsuariosAdministrativos gestion)=>
            await Ejecutar(async()=>{await gestion.RestablecerPasswordAsync(Actor(http),id,datos.Contrasena,http.RequestAborted);return new{actualizado=true,sesionesRevocadas=true};}))
            .AddEndpointFilter<OperacionTerminalSeguraFilter>();
    }
    private static int Actor(HttpContext http)=>int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier),out int id)&&id>0?id:throw new AccesoAdministrativoRechazadoException();
    private static async Task<IResult> Ejecutar<T>(Func<Task<T>> accion)
    {
        try{return Results.Ok(await accion());}
        catch(AccesoAdministrativoRechazadoException){return Results.StatusCode(403);}
        catch(UsuarioAdministrativoNoEncontradoException){return Results.NotFound();}
        catch(ConflictoUsuarioAdministrativoException){return Results.Conflict(new{mensaje="Cambio incompatible o ultimo administrador activo."});}
        catch(ArgumentException){return Results.BadRequest(new{mensaje="Datos de usuario invalidos."});}
        catch(SqlException ex)when(ex.Number is 2601 or 2627){return Results.Conflict(new{mensaje="Nombre de usuario no disponible."});}
        catch(SqlException){return Results.Problem(statusCode:503,title:"No se pudo confirmar el cambio de usuario.");}
        catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Gestion de usuarios no disponible.");}
    }
}
