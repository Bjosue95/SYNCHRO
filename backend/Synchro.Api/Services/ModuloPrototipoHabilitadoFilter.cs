namespace Synchro.Api.Services;

internal sealed class ModuloPrototipoHabilitadoFilter(IConfiguration config,string clave) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto,EndpointFilterDelegate siguiente)
    {
        contexto.HttpContext.Response.Headers.CacheControl="no-store";
        if(!contexto.HttpContext.Request.IsHttps)return ValueTask.FromResult<object?>(Results.BadRequest(new{mensaje="Se requiere HTTPS."}));
        if(!config.GetValue<bool>(clave))return ValueTask.FromResult<object?>(Results.Problem(statusCode:503,title:"Modulo no configurado."));
        return siguiente(contexto);
    }
}
