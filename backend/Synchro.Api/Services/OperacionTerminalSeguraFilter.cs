using Microsoft.AspNetCore.Antiforgery;

namespace Synchro.Api.Services;

// La autorizacion SoloTerminal se ejecuta antes del filtro.
// Reutilizable para operaciones mutables que se autentican por cookie.
internal sealed class OperacionTerminalSeguraFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        HttpContext contexto = invocation.HttpContext;
        contexto.Response.Headers.CacheControl = "no-store";
        if (!contexto.Request.IsHttps)
            return Results.BadRequest(new { mensaje = "Se requiere HTTPS." });
        if (!HttpMethods.IsGet(contexto.Request.Method) && !HttpMethods.IsHead(contexto.Request.Method) &&
            !HttpMethods.IsOptions(contexto.Request.Method) && !await antiforgery.IsRequestValidAsync(contexto))
            return Results.BadRequest(new { mensaje = "Token CSRF invalido o ausente." });
        return await next(invocation);
    }
}
