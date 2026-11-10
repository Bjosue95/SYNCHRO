using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Endpoints;

public static class RostroSimuladoEndpoints
{
    public static void MapRostroSimuladoEndpoints(this WebApplication app)
    {
        foreach(var (ruta,politica)in new[]{("/api/web/clientes/rostro-simulado","SoloClientes"),("/api/web/destinatarios/rostro-simulado","SoloDestinatarios")})
        {
            var grupo=app.MapGroup(ruta).RequireAuthorization(politica).RequireRateLimiting("OperacionesTerminal")
                .AddEndpointFilter<OperacionTerminalSeguraFilter>();
            grupo.MapPost("/referencia",async(HttpContext http,IReferenciasRostroSimulado referencias,IVerificadorFacial verificador,TimeProvider reloj,IConfiguration config)=>
            {
                if(verificador is not VerificadorFacialSimulado)return Results.Problem(statusCode:503,title:"Simulacion de rostro deshabilitada.");
                if(http.Request.Headers["X-SYNCHRO-CONSENTIMIENTO"].ToString()!="ACEPTO")return Results.BadRequest(new{mensaje="Se requiere consentimiento para la referencia de demostracion."});
                int persona=int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var(captura,error)=await CapturaRostroHttp.LeerAsync(http);if(error is not null)return error;
                using(captura)
                {
                    byte[]? imagen=null;double[]? patron=null;
                    try
                    {
                        imagen=captura!.ToArray();patron=PatronVisualSimulado.Extraer(imagen);
                        if(patron is null)return Results.BadRequest(new{mensaje="Captura PNG de demostracion invalida."});
                        int horas=config.GetValue("RostroSimulado:VigenciaHoras",24);
                        if(horas is <1 or >168)return Results.Problem(statusCode:503,title:"Vigencia de demostracion no configurada.");
                        var vence=reloj.GetUtcNow().AddHours(horas);
                        await referencias.GuardarAsync(new(persona,patron,vence),http.RequestAborted);
                        return Results.Ok(new{simulado=true,modelo=PatronVisualSimulado.Modelo,venceUtc=vence,imagenGuardada=false});
                    }
                    catch(SqlException){return Results.Problem(statusCode:503,title:"Referencia de demostracion no disponible.");}
                    catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Referencia de demostracion no configurada.");}
                    finally{if(imagen is not null)CryptographicOperations.ZeroMemory(imagen);if(patron is not null)Array.Clear(patron);CapturaRostroHttp.Limpiar(captura!);}
                }
            });
            grupo.MapDelete("/referencia",async(HttpContext http,IReferenciasRostroSimulado referencias)=>
            {
                try{await referencias.EliminarAsync(int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),http.RequestAborted);return Results.Ok(new{eliminada=true});}
                catch(SqlException){return Results.Problem(statusCode:503,title:"No se pudo eliminar la referencia de demostracion.");}
            });
        }
    }
}
