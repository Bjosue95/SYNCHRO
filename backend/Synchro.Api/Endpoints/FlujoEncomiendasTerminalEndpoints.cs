using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Endpoints;

public sealed record DepositarEncomiendaTerminalSolicitud(Guid EvidenciaId,string CodigoCasillero);
public sealed record RetirarEncomiendaTerminalSolicitud(Guid EvidenciaId,string Pin)
{
    public override string ToString()=>"Retiro de encomienda protegido";
}

public static class FlujoEncomiendasTerminalEndpoints
{
    public static void MapFlujoEncomiendasTerminalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/web/destinatarios/encomiendas/{id:int}/vincular",async(int id,HttpContext http,IFlujoEncomiendasTerminal flujo)=>
        {
            if(id<=0||!int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier),out int destinatario))return Results.Unauthorized();
            string? telefono=http.User.FindFirstValue("telefono_verificado");if(string.IsNullOrEmpty(telefono))return Results.Unauthorized();
            try
            {
                bool vinculado=await flujo.VincularDestinatarioAsync(id,destinatario,telefono,http.RequestAborted);
                return vinculado?Results.Ok(new{vinculado=true,consentimientoWhatsAppRequerido=true}):Results.NotFound();
            }
            catch(SqlException ex)when(ex.Number==56001){return Results.Conflict();}
            catch(SqlException){return Results.Problem(statusCode:503,title:"No fue posible vincular la identidad.");}
        }).RequireAuthorization("SoloDestinatarios").AddEndpointFilter<OperacionTerminalSeguraFilter>().RequireRateLimiting("OperacionesTerminal");

        var rutas=app.MapGroup("/api/terminales/encomiendas")
            .RequireAuthorization("SoloTerminal").RequireRateLimiting("OperacionesTerminal")
            .AddEndpointFilter<OperacionTerminalSeguraFilter>();

        // Encomiendas exclusivamente reservadas por clientes en la web. No hay
        // ruta de alta de visitas ni de reservas desde la terminal.
        rutas.MapGet("/csrf-token",(HttpContext http,Microsoft.AspNetCore.Antiforgery.IAntiforgery csrf)=>
            Results.Ok(new{csrfToken=csrf.GetAndStoreTokens(http).RequestToken}));

        rutas.MapPost("/reservas/{id:int}/evidencias-remitente",async(int id,HttpContext http,
            IFlujoEncomiendasTerminal flujo,IVerificadorFacial verificador,TimeProvider reloj)=>
        {
            int terminal=Identidad(http.User);
            if(terminal<=0)return Results.Unauthorized();
            byte[]? hash=TokenReserva(http);
            if(id<=0||hash is null)return Results.BadRequest(new{mensaje="Referencia o credencial invalida."});
            try
            {
                if(verificador is VerificadorFacialNoDisponible)return NoBiometria();
                var reserva=await flujo.ObtenerReservaAsync(id,terminal,hash,http.RequestAborted);
                if(reserva is null)return Results.NotFound();
                var (captura,error)=await CapturaRostroHttp.LeerAsync(http);
                if(error is not null)return error;
                using(captura)
                {
                    try
                    {
                        var evidencia=await new PreparadorEvidenciaFacialRemitente(verificador,reloj)
                            .PrepararAsync(id,reserva.RemitenteId,terminal,captura!,http.RequestAborted);
                        if(evidencia is null)return Results.StatusCode(403);
                        await flujo.GuardarEvidenciaRemitenteAsync(evidencia.Value,hash,http.RequestAborted);
                        return Results.Ok(new{evidenciaId=evidencia.Value.EvidenciaId,venceUtc=evidencia.Value.FechaExpiracionUtc,simulado=true});
                    }
                    finally { CapturaRostroHttp.Limpiar(captura!); }
                }
            }
            catch(EncomiendaNoDisponibleException){return Results.NotFound();}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Simulacion de captura no disponible.");}
            catch(System.Text.Json.JsonException){return Results.Problem(statusCode:503,title:"Referencia simulada no disponible.");}
            catch(CryptographicException){return Results.Problem(statusCode:503,title:"Referencia simulada no disponible.");}
            catch(SqlException ex)when(ex.Number is >=55600 and <=56099){return Results.Conflict(new{mensaje="Reserva no disponible."});}
            catch(SqlException){return Results.Problem(statusCode:503,title:"No fue posible verificar la reserva.");}
            finally{CryptographicOperations.ZeroMemory(hash);}
        });

        rutas.MapPost("/reservas/{id:int}/depositar",async(int id,DepositarEncomiendaTerminalSolicitud datos,
            HttpContext http,IFlujoEncomiendasTerminal flujo,IVerificadorFacial verificador)=>
        {
            int terminal=Identidad(http.User);
            if(terminal<=0)return Results.Unauthorized();
            byte[]? hash=TokenReserva(http);
            if(hash is null)return Results.BadRequest(new{mensaje="Credencial invalida."});
            try
            {
                var comando=new DepositoEncomiendaComando(id,terminal,datos.EvidenciaId,datos.CodigoCasillero){TokenGestionHash=hash};
                DepositoEncomiendaService.Validar(comando);
                if(verificador is VerificadorFacialNoDisponible)return NoBiometria();
                if(await flujo.ObtenerReservaAsync(id,terminal,hash,http.RequestAborted) is null)return Results.NotFound();
                var resultado=await flujo.DepositarAsync(comando,http.RequestAborted);
                return Results.Ok(new{usoId=resultado.UsoId,codigoCasillero=resultado.CodigoCasillero,
                    estado="Ocupado",notificacion="Encolada",hardwareSimulado=true});
            }
            catch(EncomiendaNoDisponibleException){return Results.Conflict(new{mensaje="Reserva no disponible."});}
            catch(ArgumentException){return Results.BadRequest(new{mensaje="Datos invalidos."});}
            catch(SqlException ex)when(ex.Number is >=55600 and <=56099){return Results.Conflict(new{mensaje="No se pudo confirmar el deposito."});}
            catch(SqlException){return Results.Problem(statusCode:503,title:"Deposito no confirmado.");}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Servicios de deposito no disponibles.");}
            finally{CryptographicOperations.ZeroMemory(hash);}
        });

        rutas.MapPost("/usos/{id:int}/evidencias-destinatario",async(int id,HttpContext http,
            IFlujoEncomiendasTerminal flujo,IVerificadorFacial verificador,TimeProvider reloj)=>
        {
            int terminal=Identidad(http.User);int destinatario=await DestinatarioAsync(http);
            if(terminal<=0||destinatario<=0)return Results.Unauthorized();
            if(id<=0)return Results.BadRequest();
            if(verificador is VerificadorFacialNoDisponible)return NoBiometria();
            try
            {
                if(!await flujo.PuedeRetirarAsync(id,destinatario,terminal,http.RequestAborted))return Results.NotFound();
                var(captura,error)=await CapturaRostroHttp.LeerAsync(http);
                if(error is not null)return error;
                using(captura)
                {
                    try
                    {
                        var resultado=await verificador.VerificarAsync(destinatario,captura!,http.RequestAborted);
                        if(!resultado.Aprobado)return Results.StatusCode(403);
                        var ahora=reloj.GetUtcNow();
                        var evidencia=new EvidenciaRetiroEncomienda(Guid.NewGuid(),id,destinatario,terminal,ahora,ahora.AddMinutes(2));
                        await flujo.GuardarEvidenciaRetiroAsync(evidencia,http.RequestAborted);
                        return Results.Ok(new{evidenciaId=evidencia.Id,venceUtc=evidencia.VenceUtc,simulado=true});
                    }
                    finally{CapturaRostroHttp.Limpiar(captura!);}
                }
            }
            catch(EncomiendaNoDisponibleException){return Results.NotFound();}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Simulacion de captura no disponible.");}
            catch(System.Text.Json.JsonException){return Results.Problem(statusCode:503,title:"Referencia simulada no disponible.");}
            catch(CryptographicException){return Results.Problem(statusCode:503,title:"Referencia simulada no disponible.");}
            catch(SqlException ex)when(ex.Number==56001){return Results.NotFound();}
            catch(SqlException){return Results.Problem(statusCode:503,title:"No fue posible verificar la operacion.");}
        });

        rutas.MapPost("/usos/{id:int}/retirar",async(int id,RetirarEncomiendaTerminalSolicitud datos,
            HttpContext http,IFlujoEncomiendasTerminal flujo,IVerificadorFacial verificador)=>
        {
            int terminal=Identidad(http.User);int destinatario=await DestinatarioAsync(http);
            if(terminal<=0||destinatario<=0)return Results.Unauthorized();
            if(id<=0||datos.EvidenciaId==Guid.Empty||datos.Pin is not {Length:6}||!datos.Pin.All(char.IsAsciiDigit))return Results.BadRequest();
            if(verificador is VerificadorFacialNoDisponible)return NoBiometria();
            try
            {
                var resultado=await flujo.RetirarAsync(new(id,destinatario,terminal,datos.EvidenciaId,datos.Pin),http.RequestAborted);
                return resultado switch
                {
                    ResultadoRetiroEncomienda.Retirado=>Results.Ok(new{usoId=id,estado="Finalizado",hardwareSimulado=true}),
                    ResultadoRetiroEncomienda.NoDisponible=>Results.NotFound(),
                    _=>Results.Conflict(new{mensaje="No se pudo autorizar el retiro."})
                };
            }
            catch(SqlException){return Results.Problem(statusCode:503,title:"Retiro no confirmado.");}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Servicio de retiro no disponible.");}
            catch(FormatException){return Results.Problem(statusCode:503,title:"Configuracion de retiro no disponible.");}
        });
    }

    private static int Identidad(ClaimsPrincipal? usuario)=>int.TryParse(usuario?.FindFirstValue(ClaimTypes.NameIdentifier),out int id)&&id>0?id:0;
    private static async Task<int> DestinatarioAsync(HttpContext http)
    {
        var sesion=await http.AuthenticateAsync("Destinatarios");
        return sesion.Succeeded&&sesion.Principal?.FindFirstValue("tipo_usuario")=="Destinatario"?Identidad(sesion.Principal):0;
    }
    private static byte[]? TokenReserva(HttpContext http)
    {
        string texto=http.Request.Headers["X-SYNCHRO-RESERVA"].ToString();
        if(texto.Length!=64||!texto.All(char.IsAsciiHexDigit))return null;
        byte[] bytes=Convert.FromHexString(texto);
        try{return SHA256.HashData(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    private static IResult NoBiometria()=>Results.Problem(statusCode:503,title:"Captura facial simulada no disponible en este entorno.");
}
