
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public sealed record SolicitarVerificacionTelefonoPeticion(
    int ReservaId
);

public static class SolicitudesVerificacionTelefonoEndpoints
{
    public static void MapSolicitudesVerificacionTelefonoEndpoints(
        this WebApplication app)
    {
        app.MapPost(
            "/api/destinatarios/verificaciones/solicitar",
            async (
                SolicitarVerificacionTelefonoPeticion datos,
                HttpContext contexto,
                IAntiforgery antiforgery) =>
            {
                contexto.Response.Headers.CacheControl = "no-store";

                // Este flujo permanece cerrado hasta implementar
                // el almacenamiento seguro y envio de los codigos.
                if (!app.Environment.IsDevelopment() ||
                    !app.Configuration.GetValue<bool>(
                        "WhatsApp:VerificacionesHabilitadas"))
                {
                    return Results.StatusCode(503);
                }

                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "HTTPS obligatorio."
                    });
                }

                if (!await antiforgery.IsRequestValidAsync(
                    contexto))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido."
                    });
                }

                if (datos.ReservaId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Solicitud invalida."
                    });
                }

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.StatusCode(503);
                }

                var configuracionSql =
                    new SqlConnectionStringBuilder(cadena);

                if (!string.Equals(
                    configuracionSql.InitialCatalog,
                    "SynchroDB_Pruebas",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Results.StatusCode(503);
                }

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync(
                        contexto.RequestAborted);

                    // La identidad y el telefono se obtienen
                    // exclusivamente desde SQL Server.
                    // El solicitante no puede elegir un numero.
                    await using var consulta = new SqlCommand(
                        """
                        IF DB_NAME() <> N'SynchroDB_Pruebas'
                            THROW 56930, 'Base no autorizada.', 1;

                        SELECT TOP (2)
                            p.Id,
                            p.TelefonoWhatsapp
                        FROM dbo.PreReservasEncomiendasWeb AS r
                        INNER JOIN dbo.ConsentimientosPreReservaWhatsApp AS c
                            ON c.ReservaId = r.Id
                        INNER JOIN dbo.Personas AS p
                            ON p.TelefonoWhatsapp = c.TelefonoWhatsApp
                        INNER JOIN dbo.ClientesWebPrototipo AS cliente
                            ON cliente.PersonaId = p.Id
                           AND cliente.Telefono = c.TelefonoWhatsApp
                        WHERE r.Id = @ReservaId
                          AND cliente.Activa = 1
                          AND c.Estado = N'Confirmado'
                          AND c.MensajeIdConfirmacion IS NOT NULL
                          AND c.FechaConfirmacionUtc IS NOT NULL
                          AND c.FechaConfirmacionUtc >= c.FechaCreacionUtc
                          AND c.FechaConfirmacionUtc <= c.FechaExpiracionUtc
                          AND (
                              r.TelefonoDestinatario = c.TelefonoWhatsApp
                              OR N'+' + r.TelefonoDestinatario =
                                 c.TelefonoWhatsApp
                          )
                          AND (
                              r.FechaExpiracion > SYSUTCDATETIME()
                              OR EXISTS (
                                  SELECT 1
                                  FROM dbo.DepositosPreReservaEncomienda AS d
                                  WHERE d.ReservaId = r.Id
                              )
                          )
                        ORDER BY p.Id;
                        """,
                        conexion);

                    consulta.Parameters.Add(
                        "@ReservaId",
                        System.Data.SqlDbType.Int
                    ).Value = datos.ReservaId;

                    await using var lector =
                        await consulta.ExecuteReaderAsync(
                            contexto.RequestAborted);

                    if (!await lector.ReadAsync(
                        contexto.RequestAborted))
                    {
                        return Results.NotFound(new
                        {
                            mensaje = "Solicitud no disponible."
                        });
                    }

                    int personaId = lector.GetInt32(0);
                    string telefono = lector.GetString(1);

                    // No debe haber dos cuentas distintas
                    // asociadas al mismo destinatario.
                    if (await lector.ReadAsync(
                        contexto.RequestAborted))
                    {
                        return Results.Conflict(new
                        {
                            mensaje =
                                "No se puede determinar una identidad unica."
                        });
                    }

                    // En esta etapa solo comprobamos que
                    // existe una identidad elegible.
                    // NO generamos un codigo ni enviamos WhatsApp.
                    // Se completara cuando exista una cola de
                    // verificacion independiente y probada.
                    return Results.StatusCode(503);
                }
                catch (SqlException)
                {
                    return Results.StatusCode(503);
                }
                catch (InvalidOperationException)
                {
                    return Results.StatusCode(503);
                }
            })
            .AllowAnonymous()
            .RequireRateLimiting("LoginLimitado");
    }
}
