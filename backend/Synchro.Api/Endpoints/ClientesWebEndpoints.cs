using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.Clientes;

namespace Synchro.Api.Endpoints;

public sealed record RegistrarClienteWebSolicitud(string NombreUsuario,string Contrasena,string Telefono)
{public override string ToString()=>"Registro de cliente protegido";}
public sealed record LoginClienteWebSolicitud(string NombreUsuario,string Contrasena)
{public override string ToString()=>"Acceso de cliente protegido";}

public sealed record EliminarClienteWebSolicitud(string? Contrasena)
{public override string ToString()=>"Eliminación de cuenta protegida";}

public static class ClientesWebEndpoints
{
    public static void MapClientesWebEndpoints(this WebApplication app)
    {
        bool feria = LimitesAccesoClientes.ModoFeria(app);
        var acceso=app.MapGroup("/api/web/clientes/acceso");
        acceso.MapGet("/csrf-token",(HttpContext http,IAntiforgery csrf)=>
        {
            if(!http.Request.IsHttps)return Results.BadRequest();http.Response.Headers.CacheControl="no-store";
            http.User=new(new ClaimsIdentity());return Results.Ok(new{csrfToken=csrf.GetAndStoreTokens(http).RequestToken});
        }).RequireRateLimiting(feria ? "ClientesFeriaCsrf" : "OperacionesTerminal");
        acceso.MapPost("/registro",async(RegistrarClienteWebSolicitud datos,HttpContext http,IAntiforgery csrf,CuentasClientesWeb cuentas,LimitesAccesoClientes limites)=>
        {
            if(!await AccesoValidoAsync(http,csrf))return Results.BadRequest(new{mensaje="HTTPS y CSRF obligatorios."});
            try
            {
                using var cupo = await limites.EntrarAsync(http.RequestAborted);
                if (!cupo.IsAcquired) return Results.StatusCode(429);
                var cuenta=await cuentas.RegistrarAsync(datos.NombreUsuario,datos.Contrasena,datos.Telefono,http.RequestAborted);
                await SesionAsync(http,cuenta);return Results.Ok(CuentasClientesWeb.Perfil(cuenta));
            }
            catch(CuentaClienteNoDisponibleException){return Results.Conflict(new{mensaje="Nombre de usuario no disponible."});}
            catch(ArgumentException){return Results.BadRequest(new{mensaje="Datos de registro invalidos."});}
            catch(SqlException ex)when(ex.Number is 2601 or 2627){return Results.Conflict(new{mensaje="Nombre de usuario no disponible."});}
            catch(SqlException){return Results.Problem(statusCode:503,title:"Registro de clientes no disponible.");}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Registro de clientes no configurado.");}
        }).RequireRateLimiting(feria ? "ClientesFeriaRegistro" : "LoginLimitado");
        acceso.MapPost("/login",async(LoginClienteWebSolicitud datos,HttpContext http,IAntiforgery csrf,CuentasClientesWeb cuentas,LimitesAccesoClientes limites)=>
        {
            if(!await AccesoValidoAsync(http,csrf))return Results.BadRequest(new{mensaje="HTTPS y CSRF obligatorios."});
            try
            {
                using var intento = limites.IntentoLogin(datos.NombreUsuario);
                if (!intento.IsAcquired) return Results.StatusCode(429);
                using var cupo = await limites.EntrarAsync(http.RequestAborted);
                if (!cupo.IsAcquired) return Results.StatusCode(429);
                var cuenta=await cuentas.AutenticarAsync(datos.NombreUsuario,datos.Contrasena,http.RequestAborted);
                if(cuenta is null)return Results.Unauthorized();await SesionAsync(http,cuenta);return Results.Ok(CuentasClientesWeb.Perfil(cuenta));
            }
            catch(SqlException){return Results.Problem(statusCode:503,title:"Acceso de clientes no disponible.");}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Acceso de clientes no configurado.");}
        }).RequireRateLimiting(feria ? "ClientesFeriaLogin" : "LoginLimitado");
        var privadas=app.MapGroup("/api/web/clientes").RequireAuthorization("SoloClientes").AddEndpointFilter<OperacionTerminalSeguraFilter>();
        var csrfPrivado = privadas.MapGet("/csrf-token",(HttpContext http,IAntiforgery csrf)=>Results.Ok(new{csrfToken=csrf.GetAndStoreTokens(http).RequestToken}));
        if (feria) csrfPrivado.RequireRateLimiting("ClientesFeriaCsrf");
        var sesion = privadas.MapGet("/mi-sesion",async(HttpContext http,CuentasClientesWeb cuentas)=>
        {
            int persona=int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var cuenta=await cuentas.ObtenerAsync(persona,http.RequestAborted);return cuenta is null?Results.Unauthorized():Results.Ok(CuentasClientesWeb.Perfil(cuenta));
        });
        var logout = privadas.MapPost("/logout",async(HttpContext http)=>{await http.SignOutAsync("Clientes");return Results.Ok(new{cerrada=true});});
        privadas.MapPost("/eliminar-cuenta", async(EliminarClienteWebSolicitud datos,HttpContext http,CuentasClientesWeb cuentas,LimitesAccesoClientes limites) =>
        {
            int persona=int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            using var intento = limites.IntentoLogin(http.User.Identity?.Name);
            if(!intento.IsAcquired) return Results.StatusCode(429);
            try
            {
                using var cupo = await limites.EntrarAsync(http.RequestAborted);
                if(!cupo.IsAcquired) return Results.StatusCode(429);
                if(!await cuentas.EliminarAsync(persona,datos.Contrasena,http.RequestAborted))
                    return Results.BadRequest(new{mensaje="La contraseña no es correcta."});
                await http.SignOutAsync("Clientes");
                return Results.Ok(new{eliminada=true});
            }
            catch(CuentaClienteConHistorialException)
            {return Results.Conflict(new{mensaje="Esta cuenta tiene datos vinculados o cambió durante la solicitud. Contacta con SYNCHRO para revisar su eliminación."});}
            catch(SqlException ex) when(ex.Number==547)
            {return Results.Conflict(new{mensaje="Esta cuenta tiene registros vinculados. Contacta con SYNCHRO para revisar su eliminación."});}
            catch(SqlException){return Results.Problem(statusCode:503,title:"No se pudo eliminar la cuenta. Inténtalo nuevamente.");}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"El servicio no está disponible.");}
        }).RequireRateLimiting(feria ? "ClientesFeriaEscrituras" : "LoginLimitado");
        if (feria) { sesion.RequireRateLimiting("ClientesFeriaSesion"); logout.RequireRateLimiting("ClientesFeriaEscrituras"); }
    }
    private static async Task<bool> AccesoValidoAsync(HttpContext http,IAntiforgery csrf)
    {
        http.Response.Headers.CacheControl="no-store";http.User=new(new ClaimsIdentity());
        return http.Request.IsHttps&&await csrf.IsRequestValidAsync(http);
    }
    private static Task SesionAsync(HttpContext http,CuentaClienteWeb cuenta)=>http.SignInAsync("Clientes",new ClaimsPrincipal(new ClaimsIdentity(
        [new(ClaimTypes.NameIdentifier,cuenta.PersonaId.ToString()),new(ClaimTypes.Name,cuenta.NombreUsuario),new(ClaimTypes.Role,"Cliente"),new("cliente_version",cuenta.VersionSesion.ToString())],"Clientes")),
        new AuthenticationProperties{IsPersistent=false,ExpiresUtc=DateTimeOffset.UtcNow.AddMinutes(30)});
}
