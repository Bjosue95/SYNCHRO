
using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public record AprobarVinculacionTerminalSolicitud(
    int SolicitudId,
    string? CodigoVisual
);

public static class AprobacionTerminalEndpoints
{
    public static void MapAprobacionTerminalEndpoints(
        this WebApplication app)
    {
        app.MapGet("/api/admin/terminales/vinculacion/listar", async (
            HttpContext contexto) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new { mensaje = "Se requiere HTTPS." });
            var cadena=app.Configuration.GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem("Base de datos no configurada.");
            try
            {
                var terminales=new List<object>();
                await using var conexion=new SqlConnection(cadena);
                await conexion.OpenAsync(contexto.RequestAborted);
                await using var cmd=new SqlCommand(
                    "SELECT Id,Codigo,Nombre,Activa FROM dbo.Terminales ORDER BY Id",conexion);
                await using var lector=await cmd.ExecuteReaderAsync(contexto.RequestAborted);
                while(await lector.ReadAsync(contexto.RequestAborted))
                    terminales.Add(new {
                        id=lector.GetInt32(0),
                        codigo=lector.GetString(1),
                        nombre=lector.GetString(2),
                        activa=lector.GetBoolean(3)
                    });
                return Results.Ok(new {terminales});
            }
            catch(SqlException)
            {
                return Results.Problem("No fue posible consultar las terminales.");
            }
        })
        .RequireAuthorization("SoloAdministradores");

        app.MapPost(
            "/api/admin/terminales/vinculacion/aprobar",
            async (
                AprobarVinculacionTerminalSolicitud solicitud,
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

                var codigo = solicitud.CodigoVisual;

                if (solicitud.SolicitudId <= 0 ||
                    codigo is null ||
                    codigo.Length != 8 ||
                    codigo.Any(c => c < '0' || c > '9'))
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Datos de vinculacion invalidos."
                    });
                }

                var idTexto = contexto.User
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                if (!int.TryParse(idTexto, out int administradorId))
                {
                    return Results.Unauthorized();
                }

                var cadena = app.Configuration
                    .GetConnectionString("SynchroDB");

                if (string.IsNullOrWhiteSpace(cadena))
                {
                    return Results.Problem(
                        "Conexion a la base de datos no configurada.");
                }

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync();

                    await using var comando = new SqlCommand(
                        """
                        UPDATE solicitud
                        SET
                            Estado = N'Aprobada',
                            FechaAprobacion = SYSUTCDATETIME(),
                            AdministradorId = @administradorId
                        FROM dbo.SolicitudesVinculacionTerminal
                            AS solicitud
                        INNER JOIN dbo.Terminales AS terminal
                            ON terminal.Id = solicitud.TerminalId
                        WHERE solicitud.Id = @solicitudId
                          AND solicitud.CodigoVisual = @codigo
                          AND solicitud.Estado = N'Pendiente'
                          AND solicitud.FechaExpiracion >
                              SYSUTCDATETIME();

                        SELECT @@ROWCOUNT;
                        """,
                        conexion);

                    comando.Parameters.Add(
                        "@administradorId",
                        SqlDbType.Int).Value = administradorId;

                    comando.Parameters.Add(
                        "@solicitudId",
                        SqlDbType.Int).Value = solicitud.SolicitudId;

                    comando.Parameters.Add(
                        "@codigo",
                        SqlDbType.NVarChar,
                        8).Value = codigo;

                    int actualizadas = Convert.ToInt32(
                        await comando.ExecuteScalarAsync());

                    if (actualizadas != 1)
                    {
                        return Results.Conflict(new
                        {
                            mensaje =
                                "La solicitud no es valida o ha vencido."
                        });
                    }

                    return Results.Ok(new
                    {
                        mensaje = "Solicitud aprobada correctamente.",
                        estado = "Aprobada"
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No se pudo aprobar la vinculacion.");
                }
            })
            .RequireAuthorization("SoloAdministradores")
            .RequireRateLimiting("LoginLimitado");
    }
}