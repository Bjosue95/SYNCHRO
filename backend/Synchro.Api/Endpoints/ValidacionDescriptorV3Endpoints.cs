using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Endpoints;

internal sealed record SolicitudValidacionDescriptorV3(double[]? Descriptor);

internal static class ValidacionDescriptorV3Endpoints
{
    internal static void MapValidacionDescriptorV3(this WebApplication app,bool definitiva=false)
    {
        // Este metodo se registra solo desde WhatsAppManualEndpoints,
        // ya restringido a Development y SynchroDB_Pruebas.
        var ruta=app.MapPost(
            "/api/terminales/guardar/whatsapp/v3/validar",
            async (HttpContext http, IAntiforgery csrf) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);

            if (http.Request.Headers[
                "X-SYNCHRO-CONSENTIMIENTO"].ToString() != "ACEPTO")
                return Results.BadRequest(new {
                    mensaje = "Consentimiento requerido."
                });

            string? tipo = http.Request.ContentType?
                .Split(';')[0].Trim();

            if (!string.Equals(
                tipo, "application/json",
                StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new {
                    mensaje = "Formato de solicitud incorrecto."
                });

            const int maxBytes = 65536;

            if (http.Features.Get<IHttpMaxRequestBodySizeFeature>()
                is { IsReadOnly: false } limite)
                limite.MaxRequestBodySize = maxBytes;

            if (http.Request.ContentLength is > maxBytes)
                return Results.StatusCode(413);

            SolicitudValidacionDescriptorV3? solicitud = null;
            double[]? normalizado = null;

            try
            {
                solicitud = await http.Request
                    .ReadFromJsonAsync<SolicitudValidacionDescriptorV3>(
                        cancellationToken: http.RequestAborted);

                normalizado = DescriptorFacialV3.Normalizar(
                    solicitud?.Descriptor);

                if (normalizado is null)
                    return Results.BadRequest(new {
                        mensaje = "Descriptor V3 invalido."
                    });

                // Esta ruta NO identifica personas, guarda referencias
                // ni emite autorizaciones de retiro.
                return Results.Ok(new {
                    valido = true,
                    modelo = DescriptorFacialV3.Modelo
                });
            }
            catch (JsonException)
            {
                return Results.BadRequest(new {
                    mensaje = "Datos V3 incorrectos."
                });
            }
            finally
            {
                if (normalizado is not null)
                    Array.Clear(normalizado);

                if (solicitud?.Descriptor is not null)
                    Array.Clear(solicitud.Descriptor);
            }
        })
        .RequireRateLimiting("OperacionesTerminal");
        if(definitiva)ruta.RequireAuthorization("SoloTerminal");else ruta.AllowAnonymous();
    }
}