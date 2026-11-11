using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.Temporales;

namespace Synchro.Api.Endpoints;

public sealed record ReservarServicioTemporalSolicitud(int Minutos);
public sealed record PagarServicioTemporalSolicitud(Guid IdOperacion,string? MetodoPago=null);
public sealed record IniciarServicioTemporalSolicitud(bool EquipoCerrado);
public sealed record RetirarServicioTemporalSolicitud(string Pin)
{
    public override string ToString()=>"Solicitud protegida";
}
public sealed record IncidenciaServicioTemporalSolicitud(string Descripcion);

public static class ServiciosTemporalesEndpoints
{
    public static void MapServiciosTemporalesEndpoints(this WebApplication app)
    {
        var lectura=app.MapGroup("/api/servicios").AddEndpointFilter(new ModuloPrototipoHabilitadoFilter(app.Configuration,"ServiciosTemporales:Habilitado"))
            .RequireRateLimiting("OperacionesTerminal");
        lectura.MapGet("/csrf-token",(HttpContext http,IAntiforgery csrf)=>Results.Ok(new{csrfToken=csrf.GetAndStoreTokens(http).RequestToken}));
        lectura.MapGet("/compartimentos",(HttpContext http,ServicioTemporal servicio)=>Ejecutar(http,()=>servicio.DisponibilidadAsync(http.RequestAborted)));
        lectura.MapGet("/{codigo}/reservas/{id:guid}",(string codigo,Guid id,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.ConsultarAsync(codigo,id,Credencial(http),http.RequestAborted)));

        var web=app.MapGroup("/api/web/servicios").AddEndpointFilter(new ModuloPrototipoHabilitadoFilter(app.Configuration,"ServiciosTemporales:Habilitado"))
            .AddEndpointFilter(async(context,next)=>
            {
                var http=context.HttpContext;var cuenta=await http.AuthenticateAsync("Clientes");
                if(cuenta.Succeeded&&cuenta.Principal?.IsInRole("Cliente")==true&&
                    int.TryParse(cuenta.Principal.FindFirstValue(ClaimTypes.NameIdentifier),out int cliente)&&cliente>0)
                {http.User=cuenta.Principal;http.Items["Synchro.ClienteWebAutenticado"]=cliente;}
                return await next(context);
            }).AddEndpointFilter<OperacionTerminalSeguraFilter>().RequireRateLimiting("OperacionesTerminal");
        web.MapGet("/csrf-token",(HttpContext http,IAntiforgery csrf)=>Results.Ok(new{csrfToken=csrf.GetAndStoreTokens(http).RequestToken}));
        web.MapPost("/{codigo}/reservas",(string codigo,ReservarServicioTemporalSolicitud datos,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.ReservarAsync(codigo,datos.Minutos,null,http.RequestAborted,ClienteWeb(http))));
        web.MapPost("/{codigo}/reservas/{id:guid}/pagar",(string codigo,Guid id,PagarServicioTemporalSolicitud datos,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.PagarAsync(codigo,id,Credencial(http),datos.IdOperacion,null,http.RequestAborted,ClienteWeb(http),datos.MetodoPago)));
        web.MapPost("/{codigo}/reservas/{id:guid}/cancelar",(string codigo,Guid id,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.CancelarAsync(codigo,id,Credencial(http),http.RequestAborted,ClienteWeb(http))));

        var terminal=app.MapGroup("/api/terminales/servicios").RequireAuthorization("SoloTerminal")
            .AddEndpointFilter(new ModuloPrototipoHabilitadoFilter(app.Configuration,"ServiciosTemporales:Habilitado")).AddEndpointFilter<OperacionTerminalSeguraFilter>()
            .RequireRateLimiting("OperacionesTerminal");
        terminal.MapGet("/csrf-token",(HttpContext http,IAntiforgery csrf)=>Results.Ok(new{csrfToken=csrf.GetAndStoreTokens(http).RequestToken}));
        terminal.MapPost("/{codigo}/reservas",(string codigo,ReservarServicioTemporalSolicitud datos,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.ReservarAsync(codigo,datos.Minutos,Identidad(http),http.RequestAborted)));
        terminal.MapPost("/{codigo}/reservas/{id:guid}/pagar",(string codigo,Guid id,PagarServicioTemporalSolicitud datos,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.PagarAsync(codigo,id,Credencial(http),datos.IdOperacion,Identidad(http),http.RequestAborted,metodoPago:datos.MetodoPago)));
        terminal.MapPost("/{codigo}/reservas/{id:guid}/iniciar",(string codigo,Guid id,IniciarServicioTemporalSolicitud datos,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.IniciarAsync(codigo,id,Credencial(http),Identidad(http),datos.EquipoCerrado,http.RequestAborted)));
        terminal.MapPost("/{codigo}/reservas/{id:guid}/retirar",(string codigo,Guid id,RetirarServicioTemporalSolicitud datos,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.RetirarAsync(codigo,id,Credencial(http),Identidad(http),datos.Pin,http.RequestAborted),
                r=>r.Autorizado?Results.Ok(r):Results.Conflict(new{mensaje="Retiro no autorizado.",pinBloqueado=r.PinBloqueado})));
        terminal.MapPost("/{codigo}/reservas/{id:guid}/pin/renovar",(string codigo,Guid id,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.RenovarPinAsync(codigo,id,Credencial(http),Identidad(http),http.RequestAborted)));
        terminal.MapPost("/{codigo}/reservas/{id:guid}/cancelar",(string codigo,Guid id,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.CancelarAsync(codigo,id,Credencial(http),http.RequestAborted)));

        var admin=app.MapGroup("/api/admin/servicios").RequireAuthorization("SesionAdministrativa")
            .AddEndpointFilter(new ModuloPrototipoHabilitadoFilter(app.Configuration,"ServiciosTemporales:Habilitado")).RequireRateLimiting("OperacionesTerminal");
        admin.MapPut("/compartimentos/{codigo}",async(string codigo,ConfiguracionServicioTemporal datos,HttpContext http,ServicioTemporal servicio)=>
            await Ejecutar(http,async()=>{await servicio.ConfigurarAsync(datos with{Codigo=codigo},Identidad(http),http.RequestAborted);return new{codigo,configurado=true,hardwareSimulado=true};}))
            .AddEndpointFilter<OperacionTerminalSeguraFilter>().RequireAuthorization("SoloAdministradores");
        admin.MapPost("/compartimentos/{codigo}/incidencias",async(string codigo,IncidenciaServicioTemporalSolicitud datos,HttpContext http,ServicioTemporal servicio)=>
            await Ejecutar(http,async()=>{await servicio.RegistrarIncidenciaAsync(codigo,datos.Descripcion,Identidad(http),http.RequestAborted);return new{codigo,registrada=true};}))
            .AddEndpointFilter<OperacionTerminalSeguraFilter>().RequireAuthorization("OperarServicios");
        admin.MapGet("/reportes",(DateTimeOffset desde,DateTimeOffset hasta,HttpContext http,ServicioTemporal servicio)=>
            Ejecutar(http,()=>servicio.ReportarAsync(desde,hasta,http.RequestAborted))).RequireAuthorization("VerReportes");
    }
    private static int Identidad(HttpContext http)=>int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)&&id>0?id:
        throw new ServicioTemporalNoDisponibleException();
    private static int? ClienteWeb(HttpContext http)=>http.Items["Synchro.ClienteWebAutenticado"]is int id?id:null;
    private static string Credencial(HttpContext http)=>http.Request.Headers["X-SYNCHRO-SERVICIO"].ToString();
    private static async Task<IResult> Ejecutar<T>(HttpContext http,Func<Task<T>> accion,Func<T,IResult>? representar=null)
    {
        http.Response.Headers.CacheControl="no-store";
        try{T resultado=await accion();return representar is null?Results.Ok(resultado):representar(resultado);}
        catch(ClienteWebRequeridoException){return Results.Unauthorized();}
        catch(ReservaSoloWebException){return Results.Json(new{mensaje="Frio y calor se reservan y pagan exclusivamente desde la cuenta web del cliente."},statusCode:403);}
        catch(ServicioTemporalNoDisponibleException ex)
        {return ex.Conflicto?Results.Conflict(new{mensaje="El estado actual no permite esta operacion."}):Results.NotFound(new{mensaje="Operacion o compartimento no disponible."});}
        catch(ArgumentException){return Results.BadRequest(new{mensaje="Datos de servicio invalidos."});}
        catch(FormatException){return Results.Problem(statusCode:503,title:"Configuracion de credenciales no disponible.");}
        catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Servicio temporal no disponible.");}
        catch(JsonException){return Results.Problem(statusCode:503,title:"Datos de servicio no disponibles.");}
        catch(SqlException){return Results.Problem(statusCode:503,title:"No se pudo confirmar la operacion.");}
    }
}
