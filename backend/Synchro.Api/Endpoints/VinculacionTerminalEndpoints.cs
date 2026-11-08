
using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

public record SolicitarVinculacionTerminalSolicitud(
    string? CodigoTerminal
);

public static class VinculacionTerminalEndpoints
{
    public static void MapVinculacionTerminalEndpoints(
        this WebApplication app)
    {
        app.MapPost(
            "/api/terminales/vinculacion/solicitar",
            async (
                SolicitarVinculacionTerminalSolicitud solicitud,
                HttpContext contexto) =>
            {
                if (!contexto.Request.IsHttps)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Se requiere HTTPS."
                    });
                }

                var codigo = solicitud.CodigoTerminal?.Trim();

                if (string.IsNullOrWhiteSpace(codigo)
                    || codigo.Length > 30)
                {
                    return Results.BadRequest(new
                    {
                        mensaje = "Codigo de terminal invalido."
                    });
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

                    await using var transaccion =
                        (SqlTransaction)await conexion
                            .BeginTransactionAsync(
                                IsolationLevel.Serializable);

                    int terminalId;
                    bool activa;

                    await using (var consultar = new SqlCommand(
                        """
                        SELECT Id, Activa
                        FROM dbo.Terminales
                            WITH (UPDLOCK, HOLDLOCK)
                        WHERE Codigo = @codigo;
                        """,
                        conexion,
                        transaccion))
                    {
                        consultar.Parameters.Add(
                            "@codigo",
                            SqlDbType.NVarChar,
                            30).Value = codigo;

                        await using var lector =
                            await consultar.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                        {
                            return Results.NotFound(new
                            {
                                mensaje = "Terminal no registrada."
                            });
                        }

                        terminalId = lector.GetInt32(0);
                        activa = lector.GetBoolean(1);
                    }

                    if (activa)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "La terminal ya esta autorizada."
                        });
                    }

                    await using (var consultar = new SqlCommand(
                        """
                        SELECT COUNT(*)
                        FROM dbo.SolicitudesVinculacionTerminal
                        WHERE TerminalId = @terminalId
                          AND Estado IN (
                              N'Pendiente',
                              N'Aprobada'
                          )
                          AND FechaExpiracion > SYSUTCDATETIME();
                        """,
                        conexion,
                        transaccion))
                    {
                        consultar.Parameters.Add(
                            "@terminalId",
                            SqlDbType.Int).Value = terminalId;

                        var pendientes = Convert.ToInt32(
                            await consultar.ExecuteScalarAsync());

                        if (pendientes > 0)
                        {
                            return Results.Conflict(new
                            {
                                mensaje =
                                    "Ya existe una solicitud vigente."
                            });
                        }
                    }

                    // Código visible para el administrador
                    var codigoVisual =
                        RandomNumberGenerator
                            .GetInt32(0, 100000000)
                            .ToString("D8");

                    // Secreto aleatorio de 256 bits
                    var token = RandomNumberGenerator.GetBytes(32);

                    // Guardar solamente su huella
                    var tokenHash = SHA256.HashData(token);

                    var fechaExpiracion =
                        DateTime.UtcNow.AddMinutes(5);

                    int solicitudId;

                    await using (var insertar = new SqlCommand(
                        """
                        INSERT INTO
                            dbo.SolicitudesVinculacionTerminal
                        (
                            TerminalId,
                            CodigoVisual,
                            TokenHash,
                            Estado,
                            FechaExpiracion
                        )
                        OUTPUT INSERTED.Id
                        VALUES
                        (
                            @terminalId,
                            @codigoVisual,
                            @tokenHash,
                            N'Pendiente',
                            @fechaExpiracion
                        );
                        """,
                        conexion,
                        transaccion))
                    {
                        insertar.Parameters.Add(
                            "@terminalId",
                            SqlDbType.Int).Value = terminalId;

                        insertar.Parameters.Add(
                            "@codigoVisual",
                            SqlDbType.NVarChar,
                            8).Value = codigoVisual;

                        insertar.Parameters.Add(
                            "@tokenHash",
                            SqlDbType.VarBinary,
                            32).Value = tokenHash;

                        insertar.Parameters.Add(
                            "@fechaExpiracion",
                            SqlDbType.DateTime2
                        ).Value = fechaExpiracion;

                        solicitudId = Convert.ToInt32(
                            await insertar.ExecuteScalarAsync());
                    }

                    await transaccion.CommitAsync();

                    return Results.Ok(new
                    {
                        solicitudId,
                        codigoVisual,
                        token = Convert.ToBase64String(token),
                        fechaExpiracion,
                        estado = "Pendiente"
                    });
                }
                catch (SqlException)
                {
                    return Results.Problem(
                        "No se pudo crear la solicitud de vinculacion.");
                }
            })
            .RequireRateLimiting("LoginLimitado");
    }
}