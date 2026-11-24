using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.Clientes;
using Synchro.Api.Services.Encomiendas;

namespace Synchro.Api.Endpoints;

internal sealed record CrearEncomiendaOperativaSolicitud(string TelefonoDestinatario,string TamanoSolicitado);
internal sealed record EmitirPinOperativoSolicitud(bool Reemplazar);
internal sealed record CrearSolicitudDepositoOperativa(string CodigoCasillero);
internal sealed record ConfirmarDepositoOperativa(bool DepositoComprobado);
internal sealed record ExtenderEvidenciaOperativa(string Incidencia,string Motivo,DateTimeOffset HastaUtc);

public static class EncomiendasOperativasEndpoints
{
    public static void MapEncomiendasOperativasEndpoints(this WebApplication app)
    {
        var web=app.MapGroup("/api/web/encomiendas").RequireAuthorization("SoloClientes")
            .AddEndpointFilter<OperacionTerminalSeguraFilter>().RequireRateLimiting("EncomiendasOperativas");
        web.MapGet("/configuracion",(EncomiendasOperativasSql s)=>Results.Ok(s.Configuracion()));
        web.MapGet("",(HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.Listar(Identidad(h),h.RequestAborted),h));
        web.MapPost("",(CrearEncomiendaOperativaSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.Crear(Identidad(h),d.TelefonoDestinatario,d.TamanoSolicitado,h.RequestAborted),h));
        web.MapGet("/{id:int}",(int id,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.Detalle(id,Identidad(h),h.RequestAborted),h));
        web.MapGet("/referencia-facial",(HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.ConsultarReferencia(Identidad(h),h.RequestAborted),h));
        web.MapPost("/referencia-facial",(CapturaEncomiendaSolicitud d,HttpContext h,EncomiendasOperativasSql s,CuentasClientesWeb cuentas)=>Ejecutar(async()=>{
            Consentimiento(h);int persona=Identidad(h);var cuenta=await cuentas.ObtenerAsync(persona,h.RequestAborted);
            if(cuenta is null||string.IsNullOrEmpty(d.Contrasena)||await cuentas.AutenticarAsync(cuenta.NombreUsuario,d.Contrasena,h.RequestAborted) is null)
                throw new OperacionEncomiendaException("ReautenticacionRequerida","Confirma tu contraseña actual para registrar la referencia.",403);
            return await s.RegistrarReferencia(persona,d,h.RequestAborted);
        },h));
        web.MapDelete("/referencia-facial",(HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.RevocarReferencia(Identidad(h),h.RequestAborted),h));
        web.MapPost("/{id:int}/deposito/autorizar",(int id,AutorizarDepositoEncomiendaSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>{
            Consentimiento(h);return s.AutorizarDeposito(id,Identidad(h),d,h.RequestAborted);
        },h));

        var admin=app.MapGroup("/api/admin/encomiendas/operativas").RequireAuthorization("OperarServicios")
            .AddEndpointFilter<OperacionTerminalSeguraFilter>().RequireRateLimiting("EncomiendasOperativas");
        admin.MapGet("",(HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.Listar(null,h.RequestAborted),h));
        admin.MapGet("/{id:int}",(int id,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.Detalle(id,null,h.RequestAborted),h));
        admin.MapPost("/{id:int}/consentimientos/{rol}/confirmar",(int id,string rol,ConsentimientoOperativoSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.ConfirmarConsentimiento(id,rol,d,Identidad(h),false,h.RequestAborted),h));
        admin.MapPost("/{id:int}/consentimientos/{rol}/rechazar",(int id,string rol,ConsentimientoOperativoSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.ConfirmarConsentimiento(id,rol,d,Identidad(h),true,h.RequestAborted),h));
        admin.MapPost("/{id:int}/pago/confirmar",(int id,PagoEncomiendaSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.ConfirmarPago(id,d,Identidad(h),h.RequestAborted),h));
        admin.MapPost("/{id:int}/pines/{finalidad}/emitir",(int id,string finalidad,EmitirPinOperativoSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.EmitirPin(id,finalidad,d.Reemplazar,Identidad(h),h.RequestAborted),h));
        admin.MapPost("/{id:int}/pines/entrega",(int id,EntregaPinEncomiendaSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.ConfirmarEntregaPin(id,d,Identidad(h),h.RequestAborted),h));
        admin.MapPost("/{id:int}/evidencias/{evidencia:guid}/conservacion",(int id,Guid evidencia,ExtenderEvidenciaOperativa d,HttpContext h,EncomiendasOperativasSql s)=>Ejecutar(()=>s.ExtenderConservacion(id,evidencia,Identidad(h),d.Incidencia,d.Motivo,d.HastaUtc,h.RequestAborted),h)).RequireAuthorization("SoloAdministradores");
        admin.MapGet("/{id:int}/evidencias/{evidencia:guid}/foto",async(int id,Guid evidencia,HttpContext h,EncomiendasOperativasSql s)=>{
            try {
                var foto=await s.ConsultarFotografia(id,evidencia,Identidad(h),h.Request.Headers["X-SYNCHRO-MOTIVO"].ToString(),h.RequestAborted);
                h.Response.Headers.CacheControl="no-store";h.Response.ContentType="image/png";
                try{await h.Response.Body.WriteAsync(foto,h.RequestAborted);}finally{CryptographicOperations.ZeroMemory(foto);}
                return Results.Empty;
            }catch(OperacionEncomiendaException e){return Rechazo(e);}catch(SqlException){return NoDisponible();}catch(CryptographicException){return NoDisponible();}
        }).RequireAuthorization("SoloAdministradores");

        // Contrato preparado para una integración posterior; no modifica la aplicación tablet.
        var terminal=app.MapGroup("/api/terminales/encomiendas/operativas").RequireAuthorization("SoloTerminal")
            .AddEndpointFilter<OperacionTerminalSeguraFilter>().RequireRateLimiting("OperacionesTerminal");
        terminal.MapGet("/csrf-token",(HttpContext h,IAntiforgery csrf)=>Results.Ok(new{csrfToken=csrf.GetAndStoreTokens(h).RequestToken}));
        terminal.MapGet("/{id:int}",(int id,HttpContext h,EncomiendasOperativasSql s)=>Terminal(h,s,()=>s.ConsultarParaRetiro(id,Identidad(h),h.RequestAborted)));
        terminal.MapPost("/depositos/solicitudes",(CrearSolicitudDepositoOperativa d,HttpContext h,EncomiendasOperativasSql s)=>Terminal(h,s,()=>s.CrearSolicitudDeposito(Identidad(h),d.CodigoCasillero,h.RequestAborted)));
        terminal.MapGet("/depositos/solicitudes/{id:guid}",(Guid id,HttpContext h,EncomiendasOperativasSql s)=>Terminal(h,s,()=>s.ConsultarSolicitudDeposito(id,Identidad(h),h.RequestAborted)));
        terminal.MapPost("/depositos/solicitudes/{id:guid}/confirmar",(Guid id,ConfirmarDepositoOperativa d,HttpContext h,EncomiendasOperativasSql s)=>Terminal(h,s,()=>s.ConfirmarDeposito(id,Identidad(h),d.DepositoComprobado,h.RequestAborted)));
        terminal.MapPost("/{id:int}/evidencias-retiro",(int id,CapturaEncomiendaSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Terminal(h,s,()=>{
            Consentimiento(h);return s.CapturarDestinatario(id,Identidad(h),d,h.RequestAborted);
        }));
        terminal.MapPost("/{id:int}/retirar",(int id,RetiroOperativoSolicitud d,HttpContext h,EncomiendasOperativasSql s)=>Terminal(h,s,()=>s.Retirar(id,Identidad(h),d,h.RequestAborted)));
    }
    private static int Identidad(HttpContext h)=>int.TryParse(h.User.FindFirstValue(ClaimTypes.NameIdentifier),out int id)&&id>0?id:throw new OperacionEncomiendaException("SesionRequerida","Sesión requerida.",401);
    private static void Consentimiento(HttpContext h)
    {if(h.Request.Headers["X-SYNCHRO-CONSENTIMIENTO"]!="ACEPTO")throw new OperacionEncomiendaException("ConsentimientoRequerido","Debes declarar el consentimiento biométrico informado.",400);}
    private static IResult Rechazo(OperacionEncomiendaException e)=>Results.Json(new{codigo=e.Codigo,mensaje=e.Message},statusCode:e.Estado);
    private static IResult NoDisponible()=>Results.Problem(statusCode:503,title:"No se pudo confirmar el resultado. Actualiza el estado de la encomienda antes de reintentar.");
    private static Task<IResult> Terminal(HttpContext h,EncomiendasOperativasSql s,Func<Task<object>> accion)=>Ejecutar(async()=>{
        await s.ValidarTerminal(Identidad(h),h.RequestAborted);return await accion();
    },h);
    private static async Task<IResult> Ejecutar(Func<Task<object>> accion,HttpContext h)
    {
        h.Response.Headers.CacheControl="no-store";
        try{return Results.Ok(await accion());}
        catch(OperacionEncomiendaException e){return Rechazo(e);}
        catch(SqlException e)when(e.Number is 2601 or 2627){return Results.Conflict(new{mensaje="La operación ya fue registrada o la referencia ya se utilizó."});}
        catch(SqlException){return NoDisponible();}
        catch(CryptographicException){return NoDisponible();}
        catch(InvalidOperationException){return NoDisponible();}
        catch(ArgumentException){return Results.BadRequest(new{mensaje="Datos de encomienda no válidos."});}
    }
}
