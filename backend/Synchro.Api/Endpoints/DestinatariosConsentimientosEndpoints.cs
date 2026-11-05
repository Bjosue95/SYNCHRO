using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Synchro.Api.Models;
using Synchro.Api.Endpoints;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Synchro.Api.Endpoints;

public static class DestinatariosConsentimientosEndpoints
{
    public static void MapDestinatariosConsentimientosEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/api/destinatarios/mis-consentimientos-whatsapp",
            async (HttpContext contexto) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                // Identificar al destinatario que inicio sesion.
                string? idTexto = contexto.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(idTexto, out int personaId))
                {
                    return Results.Unauthorized();
                }

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.Problem(
                        "Conexion SQL no configurada.");
                }

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync();

                    await using var comando = new SqlCommand(
                        @"SELECT TOP (20)
                            Id,
                            Estado,
                            Finalidad,
                            FechaRegistro
                          FROM dbo.ConsentimientosWhatsApp
                          WHERE PersonaId = @PersonaId
                          ORDER BY FechaRegistro DESC, Id DESC",
                        conexion);

                    comando.Parameters.Add(
                        "@PersonaId",
                        System.Data.SqlDbType.Int
                    ).Value = personaId;

                    await using var lector =
                        await comando.ExecuteReaderAsync();


                    var consentimientos = new List<object>();

                    string estadoActual = "Sin consentimiento";

                    while (await lector.ReadAsync())
                    {
                        string estado = lector["Estado"].ToString() ?? "";
                        string finalidad = lector["Finalidad"].ToString() ?? "";

                        // Identificar el consentimiento mas reciente.
                        if (estadoActual == "Sin consentimiento" &&
                            finalidad == "NotificacionesEncomienda")
                        {
                            estadoActual = estado;
                        }

                        consentimientos.Add(new
                        {
                            id = Convert.ToInt32(lector["Id"]),
                            estado,
                            finalidad,
                            fechaRegistro = lector["FechaRegistro"]
                        });
                    }

                    contexto.Response.Headers.CacheControl = "no-store";

                    return Results.Ok(new
                    {
                        estadoActual,
                        consentimientos
                    });

                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No se pudieron consultar los consentimientos.");
                }
            }
        )
        .RequireAuthorization("SoloDestinatarios");

        app.MapPost(
            "/api/destinatarios/consentimientos-whatsapp/{id:int}/aceptar",
            async (
                int id,
                AceptarConsentimientoWhatsAppPeticion datos,
                HttpContext contexto,
                IAntiforgery antiforgery) =>
            {
                // Mantener esta funcion en pruebas hasta
                // integrar la verificacion real por WhatsApp.
                if (!app.Environment.IsDevelopment())
                {
                    return Results.StatusCode(503);
                }

                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                if (!await antiforgery.IsRequestValidAsync(contexto))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido."
                    });
                }

                if (id <= 0 ||
                    datos.VerificacionTelefonoId <= 0 ||
                    !datos.ConfirmacionExpresa)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere una aceptacion expresa y una verificacion valida."
                    });
                }

                // Obtener la persona desde su sesion.
                string? personaTexto = contexto.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(personaTexto, out int personaId))
                {
                    return Results.Unauthorized();
                }

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.Problem(
                        "Conexion SQL no configurada.");
                }

                try
                {
                    await using var conexion = new SqlConnection(cadena);
                    await conexion.OpenAsync();

                    // Comprobar que el consentimiento
                    // pertenece al destinatario autenticado.
                    await using (var comprobar = new SqlCommand(
                        @"SELECT COUNT(1)
                          FROM dbo.ConsentimientosWhatsApp
                          WHERE Id = @ConsentimientoId
                            AND PersonaId = @PersonaId",
                        conexion))
                    {
                        comprobar.Parameters.Add(
                            "@ConsentimientoId",
                            System.Data.SqlDbType.Int
                        ).Value = id;

                        comprobar.Parameters.Add(
                            "@PersonaId",
                            System.Data.SqlDbType.Int
                        ).Value = personaId;

                        int encontrados = Convert.ToInt32(
                            await comprobar.ExecuteScalarAsync());

                        if (encontrados != 1)
                        {
                            return Results.NotFound(new
                            {
                                mensaje = "Consentimiento no encontrado."
                            });
                        }
                    }

                    // Registrar la aceptacion mediante SQL Server.
                    await using (var comando = new SqlCommand(
                        "dbo.RegistrarAceptacionConsentimientoWhatsAppSynchro",
                        conexion))
                    {
                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@ConsentimientoId",
                            System.Data.SqlDbType.Int
                        ).Value = id;

                        comando.Parameters.Add(
                            "@VerificacionTelefonoId",
                            System.Data.SqlDbType.Int
                        ).Value = datos.VerificacionTelefonoId;

                        comando.Parameters.Add(
                            "@ConfirmacionExpresa",
                            System.Data.SqlDbType.Bit
                        ).Value = true;

                        await comando.ExecuteNonQueryAsync();
                    }

                    // Confirmar que la aceptacion quedo registrada.
                    await using var verificar = new SqlCommand(
                        @"SELECT COUNT(1)
                          FROM dbo.ConsentimientosWhatsApp
                          WHERE Id = @ConsentimientoId
                            AND PersonaId = @PersonaId
                            AND VerificacionTelefonoId = @VerificacionId
                            AND Estado = N'Aceptado'",
                        conexion);

                    verificar.Parameters.Add(
                        "@ConsentimientoId",
                        System.Data.SqlDbType.Int
                    ).Value = id;

                    verificar.Parameters.Add(
                        "@PersonaId",
                        System.Data.SqlDbType.Int
                    ).Value = personaId;

                    verificar.Parameters.Add(
                        "@VerificacionId",
                        System.Data.SqlDbType.Int
                    ).Value = datos.VerificacionTelefonoId;

                    int aceptados = Convert.ToInt32(
                        await verificar.ExecuteScalarAsync());

                    if (aceptados != 1)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "No se confirmo la aceptacion."
                        });
                    }

                    contexto.Response.Headers.CacheControl = "no-store";

                    return Results.Ok(new
                    {
                        mensaje = "Consentimiento aceptado correctamente."
                    });
                }
                catch (SqlException ex) when (
                    ex.Number >= 50180 && ex.Number <= 50199)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "El consentimiento o la verificacion no cumplen los requisitos."
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No se pudo registrar la aceptacion.");
                }
            }
        )
        .RequireAuthorization("SoloDestinatarios");

        app.MapPost(
            "/api/destinatarios/consentimientos-whatsapp/{id:int}/rechazar",
            async (
                int id,
                HttpContext contexto,
                IAntiforgery antiforgery) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                if (!await antiforgery.IsRequestValidAsync(contexto))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido."
                    });
                }

                string? personaTexto = contexto.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(personaTexto, out int personaId))
                {
                    return Results.Unauthorized();
                }

                if (id <= 0)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Consentimiento invalido."
                    });
                }

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.Problem(
                        "Conexion SQL no configurada.");
                }

                try
                {
                    await using var conexion = new SqlConnection(cadena);
                    await conexion.OpenAsync();

                    // Comprobar que el consentimiento
                    // pertenece al destinatario.
                    await using (var comprobar = new SqlCommand(
                        @"SELECT Estado
                          FROM dbo.ConsentimientosWhatsApp
                          WHERE Id = @Id
                            AND PersonaId = @PersonaId",
                        conexion))
                    {
                        comprobar.Parameters.Add(
                            "@Id", System.Data.SqlDbType.Int
                        ).Value = id;

                        comprobar.Parameters.Add(
                            "@PersonaId", System.Data.SqlDbType.Int
                        ).Value = personaId;

                        string? estado =
                            await comprobar.ExecuteScalarAsync() as string;

                        if (estado is null)
                        {
                            return Results.NotFound(new
                            {
                                mensaje = "Consentimiento no encontrado."
                            });
                        }

                        if (estado != "Pendiente")
                        {
                            return Results.Conflict(new
                            {
                                mensaje = "Solo se puede rechazar un consentimiento pendiente."
                            });
                        }
                    }

                    // Registrar el rechazo en SQL Server.
                    await using var comando = new SqlCommand(
                        "dbo.RechazarConsentimientoWhatsAppSynchro",
                        conexion);

                    comando.CommandType =
                        System.Data.CommandType.StoredProcedure;


                    comando.Parameters.Add(
                        "@ConsentimientoId",
                        System.Data.SqlDbType.Int
                    ).Value = id;

                    // Identificar al destinatario propietario.
                    comando.Parameters.Add(
                        "@PersonaId",
                        System.Data.SqlDbType.Int
                    ).Value = personaId;

                    await comando.ExecuteNonQueryAsync();


                    contexto.Response.Headers.CacheControl = "no-store";

                    return Results.Ok(new
                    {
                        mensaje = "Consentimiento rechazado correctamente."
                    });
                }
                catch (SqlException ex) when (
                    ex.Number >= 50000 && ex.Number < 51000)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "No se pudo rechazar el consentimiento en su estado actual."
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "Error al registrar el rechazo.");
                }
            }
        )
        .RequireAuthorization("SoloDestinatarios");

        app.MapPost(
            "/api/destinatarios/consentimientos-whatsapp/{id:int}/revocar",
            async (
                int id,
                HttpContext contexto,
                IAntiforgery antiforgery) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                if (!await antiforgery.IsRequestValidAsync(contexto))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido."
                    });
                }

                if (id <= 0)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Consentimiento invalido."
                    });
                }

                // Obtener la identidad del destinatario.
                string? personaTexto = contexto.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(personaTexto, out int personaId)
                    || personaId <= 0)
                {
                    return Results.Unauthorized();
                }

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.Problem(
                        "Conexion SQL no configurada.");
                }

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync();

                    await using var comando = new SqlCommand(
                        "dbo.RevocarConsentimientoWhatsAppSynchro",
                        conexion);

                    comando.CommandType =
                        System.Data.CommandType.StoredProcedure;

                    // Identificador del consentimiento.
                    comando.Parameters.Add(
                        "@ConsentimientoId",
                        System.Data.SqlDbType.Int
                    ).Value = id;

                    // Identificador del destinatario autenticado.
                    comando.Parameters.Add(
                        "@PersonaId",
                        System.Data.SqlDbType.Int
                    ).Value = personaId;

                    await comando.ExecuteNonQueryAsync();

                    contexto.Response.Headers.CacheControl = "no-store";

                    return Results.Ok(new
                    {
                        mensaje = "Consentimiento revocado correctamente."
                    });
                }
                catch (SqlException ex) when (ex.Number == 50302)
                {
                    return Results.NotFound(new
                    {
                        mensaje = "Consentimiento no encontrado."
                    });
                }
                catch (SqlException ex) when (
                    ex.Number == 50303 ||
                    ex.Number == 50304 ||
                    ex.Number == 50305)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "El consentimiento no puede revocarse en su estado actual."
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No se pudo revocar el consentimiento.");
                }
            }
        )
        .RequireAuthorization("SoloDestinatarios");

        app.MapPost(
            "/api/destinatarios/reservas/{usoId:int}/solicitar-consentimiento",
            async (
                int usoId,
                HttpContext contexto,
                IAntiforgery antiforgery) =>
            {
                // Funcion habilitada solo para el prototipo.
                if (!app.Environment.IsDevelopment())
                    return Results.StatusCode(503);

                if (!contexto.Request.IsHttps)
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });

                if (!await antiforgery.IsRequestValidAsync(contexto))
                    return Results.BadRequest(new
                    {
                        mensaje = "Token CSRF invalido."
                    });

                string? personaTexto = contexto.User
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!int.TryParse(personaTexto, out int personaId)
                    || personaId <= 0)
                    return Results.Unauthorized();

                if (usoId <= 0)
                    return Results.BadRequest(new
                    {
                        mensaje = "Reserva invalida."
                    });

                string? cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                    return Results.Problem(
                        "Conexion SQL no configurada.");

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync();

                    // Comprobar que la reserva pertenece al destinatario.
                    await using var buscar = new SqlCommand(
                        @"SELECT pe.TelefonoWhatsapp
                          FROM dbo.ParticipantesUso p
                          INNER JOIN dbo.Personas pe
                              ON pe.Id = p.PersonaId
                          WHERE p.UsoId = @UsoId
                            AND p.PersonaId = @PersonaId
                            AND p.Rol = N'Destinatario'",
                        conexion);

                    buscar.Parameters.Add(
                        "@UsoId", System.Data.SqlDbType.Int
                    ).Value = usoId;

                    buscar.Parameters.Add(
                        "@PersonaId", System.Data.SqlDbType.Int
                    ).Value = personaId;

                    string? telefono =
                        await buscar.ExecuteScalarAsync() as string;

                    if (string.IsNullOrWhiteSpace(telefono))
                        return Results.NotFound(new
                        {
                            mensaje = "Reserva no encontrada para este destinatario."
                        });


                    // Buscar un consentimiento existente
                    // del mismo destinatario y telefono.
                    int? consentimientoExistenteId = null;
                    string? estadoExistente = null;

                    await using (var buscarExistente = new SqlCommand(
                        @"SELECT TOP (1) Id, Estado
                          FROM dbo.ConsentimientosWhatsApp
                          WHERE PersonaId = @PersonaId
                            AND Telefono = @Telefono
                            AND Finalidad = N'NotificacionesEncomienda'
                            AND VersionAviso = N'v1-demo'
                            AND Estado IN (N'Pendiente', N'Aceptado')
                            AND FechaRevocacion IS NULL
                          ORDER BY FechaRegistro DESC, Id DESC",
                        conexion))
                    {
                        buscarExistente.Parameters.Add(
                            "@PersonaId",
                            System.Data.SqlDbType.Int
                        ).Value = personaId;

                        buscarExistente.Parameters.Add(
                            "@Telefono",
                            System.Data.SqlDbType.NVarChar, 20
                        ).Value = telefono;

                        await using var lector =
                            await buscarExistente.ExecuteReaderAsync();

                        if (await lector.ReadAsync())
                        {
                            consentimientoExistenteId = lector.GetInt32(0);
                            estadoExistente = lector.GetString(1);
                        }
                    }

                    // Si ya existe, asociarlo a la reserva.
                    if (consentimientoExistenteId.HasValue)
                    {
                        await using var asociarExistente = new SqlCommand(
                            "dbo.AsociarConsentimientoEncomiendaSynchro",
                            conexion);

                        asociarExistente.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        asociarExistente.Parameters.Add(
                            "@UsoId",
                            System.Data.SqlDbType.Int
                        ).Value = usoId;

                        asociarExistente.Parameters.Add(
                            "@ConsentimientoId",
                            System.Data.SqlDbType.Int
                        ).Value = consentimientoExistenteId.Value;

                        await asociarExistente.ExecuteNonQueryAsync();

                        contexto.Response.Headers.CacheControl = "no-store";

                        return Results.Ok(new
                        {
                            id = consentimientoExistenteId.Value,
                            usoId,
                            estado = estadoExistente,
                            reutilizado = true,
                            mensaje = "Consentimiento existente asociado a la reserva."
                        });
                    }

                    // Crear y asociar el consentimiento
                    // dentro de una misma transaccion.
                    await using var transaccion =
                        (SqlTransaction)await conexion.BeginTransactionAsync();

                    int consentimientoId;

                    // PASO 1: Crear consentimiento pendiente.
                    await using (var comando = new SqlCommand(
                        "dbo.CrearConsentimientoPendienteSynchro",
                        conexion,
                        transaccion))
                    {
                        comando.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        comando.Parameters.Add(
                            "@UsoId",
                            System.Data.SqlDbType.Int
                        ).Value = usoId;

                        comando.Parameters.Add(
                            "@Telefono",
                            System.Data.SqlDbType.NVarChar, 20
                        ).Value = telefono;

                        comando.Parameters.Add(
                            "@Origen",
                            System.Data.SqlDbType.NVarChar, 20
                        ).Value = "Web";

                        comando.Parameters.Add(
                            "@VersionAviso",
                            System.Data.SqlDbType.NVarChar, 20
                        ).Value = "v1-demo";

                        await using (var lector =
                            await comando.ExecuteReaderAsync())
                        {
                            if (!await lector.ReadAsync())
                            {
                                return Results.Problem(
                                    "No se pudo crear el consentimiento.");
                            }

                            consentimientoId =
                                Convert.ToInt32(lector["Id"]);
                        }
                    }

                    // PASO 2: Asociar consentimiento a la reserva.
                    await using (var asociar = new SqlCommand(
                        "dbo.AsociarConsentimientoEncomiendaSynchro",
                        conexion,
                        transaccion))
                    {
                        asociar.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        asociar.Parameters.Add(
                            "@UsoId",
                            System.Data.SqlDbType.Int
                        ).Value = usoId;

                        asociar.Parameters.Add(
                            "@ConsentimientoId",
                            System.Data.SqlDbType.Int
                        ).Value = consentimientoId;

                        await asociar.ExecuteNonQueryAsync();
                    }

                    // PASO 3: Confirmar ambas operaciones.
                    await transaccion.CommitAsync();

                    contexto.Response.Headers.CacheControl = "no-store";

                    return Results.Ok(new
                    {
                        id = consentimientoId,
                        usoId,
                        estado = "Pendiente",
                        mensaje = "Consentimiento creado y asociado a la reserva."
                    });

                }
                catch (SqlException ex) when (ex.Number == 50077)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "Ya existe un consentimiento pendiente o aceptado."
                    });
                }
                catch (SqlException ex) when (
                    ex.Number >= 50070 && ex.Number <= 50078)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "La reserva no cumple los requisitos."
                    });
                }

                catch (SqlException ex) when (
                    ex.Number >= 50080 && ex.Number <= 50088)
                {
                    return Results.Conflict(new
                    {
                        mensaje = "No se pudo asociar el consentimiento a la reserva."
                    });
                }

                catch (SqlException)
                {
                    return Results.Problem(
                        "Error al solicitar el consentimiento.");
                }
            }
        )
        .RequireAuthorization("SoloDestinatarios");
    }
}
