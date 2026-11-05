
namespace Synchro.Api.Endpoints;

public static class EstadoEndpoints
{
    public static IEndpointRouteBuilder MapEstadoEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/estado", () => new
        {
            proyecto = "SYNCHRO",
            estado = "en desarrollo"
        });

        return endpoints;
    }
}
