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
        var rutas=app.MapGroup("/api/terminales/encomiendas")
            .RequireAuthorization("SoloTerminal").RequireRateLimiting("OperacionesTerminal")
            .AddEndpointFilter<OperacionTerminalSeguraFilter>();

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
                var (captura,error)=await LeerCapturaAsync(http);
                if(error is not null)return error;
                using(captura)
                {
                    try
                    {
                        var evidencia=await new PreparadorEvidenciaFacialRemitente(verificador,reloj)
                            .PrepararAsync(id,reserva.RemitenteId,terminal,captura!,http.RequestAborted);
                        if(evidencia is null)return Results.StatusCode(403);
                        await flujo.GuardarEvidenciaRemitenteAsync(evidencia.Value,hash,http.RequestAborted);
                        return Results.Ok(new{evidenciaId=evidencia.Value.EvidenciaId,venceUtc=evidencia.Value.FechaExpiracionUtc});
                    }
                    finally { Limpiar(captura!); }
                }
            }
            catch(EncomiendaNoDisponibleException){return Results.NotFound();}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Servicio de verificacion no disponible.");}
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
                var(captura,error)=await LeerCapturaAsync(http);
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
                        return Results.Ok(new{evidenciaId=evidencia.Id,venceUtc=evidencia.VenceUtc});
                    }
                    finally{Limpiar(captura!);}
                }
            }
            catch(EncomiendaNoDisponibleException){return Results.NotFound();}
            catch(InvalidOperationException){return Results.Problem(statusCode:503,title:"Servicio de verificacion no disponible.");}
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
    private static IResult NoBiometria()=>Results.Problem(statusCode:503,title:"Verificacion facial confiable no disponible.");
    private static async Task<(MemoryStream? Captura,IResult? Error)> LeerCapturaAsync(HttpContext http)
    {
        const int maximo=256*1024;
        if(!string.Equals(http.Request.ContentType?.Split(';')[0].Trim(),"application/octet-stream",StringComparison.OrdinalIgnoreCase))
            return(null,Results.StatusCode(415));
        if(http.Request.ContentLength>maximo)return(null,Results.StatusCode(413));
        var memoria=new MemoryStream();byte[] buffer=new byte[8192];
        try
        {
            int leidos;
            while((leidos=await http.Request.Body.ReadAsync(buffer,http.RequestAborted))>0)
            {
                if(memoria.Length+leidos>maximo){Limpiar(memoria);memoria.Dispose();return(null,Results.StatusCode(413));}
                memoria.Write(buffer,0,leidos);
            }
            if(memoria.Length==0){memoria.Dispose();return(null,Results.BadRequest());}
            memoria.Position=0;return(memoria,null);
        }
        catch{Limpiar(memoria);memoria.Dispose();throw;}
        finally{CryptographicOperations.ZeroMemory(buffer);}
    }
    private static void Limpiar(MemoryStream memoria)
    {
        if(memoria.TryGetBuffer(out var buffer))CryptographicOperations.ZeroMemory(buffer.AsSpan());
    }
}
