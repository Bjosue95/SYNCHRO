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

public static class VerificacionesDemoEndpoints
{
    public static void MapVerificacionesDemoEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/demo/verificaciones-telefono",
                async (
                    SolicitarVerificacionTelefonoPeticion datos,
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

                    string? telefono = datos.Telefono?.Trim();

                    if (datos.PersonaId <= 0 ||
                        string.IsNullOrWhiteSpace(telefono) ||
                        telefono.Length > 20)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Datos de verificacion invalidos."
                        });
                    }

                    var cadena = app.Configuration
                        .GetConnectionString("SynchroDB");

                    if (string.IsNullOrWhiteSpace(cadena))
                    {
                        return Results.Problem(
                            "No se encontro la conexion a SynchroDB.");
                    }

                    try
                    {
                        string codigo =
                            CodigoVerificacionTelefono.Generar();

                        byte[] hash =
                            CodigoVerificacionTelefono.CalcularHash(
                                codigo,
                                datos.PersonaId,
                                telefono,
                                app.Configuration);

                        try
                        {
                            await using var conexion =
                                new SqlConnection(cadena);

                            await conexion.OpenAsync();

                            await using var comando = new SqlCommand(
                                "dbo.CrearVerificacionTelefonoPendienteSynchro",
                                conexion);

                            comando.CommandType =
                                System.Data.CommandType.StoredProcedure;

                            comando.Parameters.Add(
                                "@PersonaId",
                                System.Data.SqlDbType.Int
                            ).Value = datos.PersonaId;

                            comando.Parameters.Add(
                                "@Telefono",
                                System.Data.SqlDbType.NVarChar,
                                20
                            ).Value = telefono;

                            comando.Parameters.Add(
                                "@CodigoHash",
                                System.Data.SqlDbType.VarBinary,
                                32
                            ).Value = hash;

                            await using var lector =
                                await comando.ExecuteReaderAsync();

                            if (!await lector.ReadAsync())
                            {
                                return Results.Problem(
                                    "No se registro la verificacion.");
                            }

                            return Results.Ok(new
                            {
                                id = Convert.ToInt32(lector["Id"]),
                                personaId = Convert.ToInt32(
                                    lector["PersonaId"]),
                                estado = lector["Estado"].ToString(),
                                fechaSolicitud = lector["FechaSolicitud"],
                                fechaExpiracion = lector["FechaExpiracion"],
                                mensaje = "Verificacion pendiente registrada."
                            });
                        }
                        finally
                        {
                            System.Security.Cryptography
                                .CryptographicOperations.ZeroMemory(hash);
                        }
                    }
                    catch (SqlException ex) when (
                        ex.Number >= 50100 &&
                        ex.Number <= 50104)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Persona o telefono invalido."
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 50105)
                    {
                        return Results.Conflict(new
                        {
                            mensaje = "Ya existe una verificacion vigente."
                        });
                    }

                    catch (SqlException ex) when (
                        ex.Number == 50106 ||
                        ex.Number == 50107 ||
                        ex.Number == 50108)
                    {
                        return Results.Json(
                            new
                            {
                                mensaje = "Demasiadas solicitudes de verificacion. Intenta mas tarde."
                            },
                            statusCode: StatusCodes.Status429TooManyRequests
                        );
                    }

                    catch (SqlException)
                    {
                        return Results.Problem(
                            "Error al registrar la verificacion.");
                    }
                    catch (FormatException)
                    {
                        return Results.Problem(
                            "Configuracion de seguridad invalida.");
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(
                            "No se pudo cargar la clave de seguridad.");
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapPost(
                "/api/demo/verificaciones-telefono/{id:int}/validar",
                async (
                    int id,
                    ValidarTelefonoPeticion datos,
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

                    if (id <= 0 || datos.PersonaId <= 0 ||
                        string.IsNullOrWhiteSpace(datos.Codigo) ||
                        datos.Codigo.Length != 6 ||
                        !datos.Codigo.All(c => c >= '0' && c <= '9'))
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Datos de verificacion invalidos."
                        });
                    }

                    var cadena = app.Configuration
                        .GetConnectionString("SynchroDB");

                    if (string.IsNullOrWhiteSpace(cadena))
                    {
                        return Results.Problem(
                            "No se encontro la conexion a SynchroDB.");
                    }

                    try
                    {
                        await using var conexion =
                            new SqlConnection(cadena);

                        await conexion.OpenAsync();

                        // Obtener el telefono registrado para esta verificacion.
                        await using var buscar = new SqlCommand(
                            @"SELECT Telefono
                              FROM dbo.VerificacionesTelefono
                              WHERE Id = @Id AND PersonaId = @PersonaId",
                            conexion);

                        buscar.Parameters.Add(
                            "@Id", System.Data.SqlDbType.Int
                        ).Value = id;

                        buscar.Parameters.Add(
                            "@PersonaId", System.Data.SqlDbType.Int
                        ).Value = datos.PersonaId;

                        string? telefono =
                            await buscar.ExecuteScalarAsync() as string;

                        if (telefono is null)
                        {
                            return Results.NotFound(new
                            {
                                mensaje = "Verificacion no encontrada."
                            });
                        }

                        byte[] hash =
                            CodigoVerificacionTelefono.CalcularHash(
                                datos.Codigo,
                                datos.PersonaId,
                                telefono,
                                app.Configuration);

                        try
                        {
                            await using var comando = new SqlCommand(
                                "dbo.ValidarCodigoTelefonoSynchro",
                                conexion);

                            comando.CommandType =
                                System.Data.CommandType.StoredProcedure;

                            comando.Parameters.Add(
                                "@VerificacionId",
                                System.Data.SqlDbType.Int
                            ).Value = id;

                            comando.Parameters.Add(
                                "@PersonaId",
                                System.Data.SqlDbType.Int
                            ).Value = datos.PersonaId;

                            comando.Parameters.Add(
                                "@CodigoHash",
                                System.Data.SqlDbType.VarBinary,
                                32
                            ).Value = hash;

                            await using var lector =
                                await comando.ExecuteReaderAsync();

                            if (!await lector.ReadAsync())
                            {
                                return Results.Problem(
                                    "No se obtuvo respuesta de SQL Server.");
                            }

                            string resultado =
                                lector["Resultado"].ToString() ?? "";

                            var respuesta = new
                            {
                                verificacionId = id,
                                estado = lector["Estado"].ToString(),
                                intentosFallidos = Convert.ToInt32(
                                    lector["IntentosFallidos"]),
                                resultado
                            };

                            return resultado switch
                            {
                                "Verificado" => Results.Ok(respuesta),
                                "Incorrecto" => Results.BadRequest(respuesta),
                                "Bloqueado" => Results.Json(
                                    respuesta, statusCode: 423),
                                "Vencido" => Results.Json(
                                    respuesta, statusCode: 410),
                                "NoDisponible" => Results.Conflict(respuesta),
                                _ => Results.Problem(
                                    "Resultado de verificacion desconocido.")
                            };
                        }
                        finally
                        {
                            System.Security.Cryptography
                                .CryptographicOperations.ZeroMemory(hash);
                        }
                    }
                    catch (SqlException ex) when (ex.Number == 50122)
                    {
                        return Results.NotFound(new
                        {
                            mensaje = "Verificacion no encontrada."
                        });
                    }
                    catch (SqlException ex) when (
                        ex.Number == 50120 || ex.Number == 50121)
                    {
                        return Results.BadRequest(new
                        {
                            mensaje = "Parametros invalidos."
                        });
                    }
                    catch (SqlException)
                    {
                        return Results.Problem(
                            "Error al validar el codigo.");
                    }
                    catch (FormatException)
                    {
                        return Results.Problem(
                            "Configuracion de seguridad invalida.");
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(
                            "No se pudo cargar la configuracion de seguridad.");
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapPost(
                "/api/demo/verificaciones-telefono/prueba-integracion",
                async (
                    HttpContext contexto,
                    IAntiforgery antiforgery) =>
                {
                    if (!contexto.Request.IsHttps)
                        return Results.BadRequest(
                            new { mensaje = "Se requiere HTTPS." });

                    if (!await antiforgery.IsRequestValidAsync(contexto))
                        return Results.BadRequest(
                            new { mensaje = "Token CSRF invalido." });

                    const int personaId = 2;
                    const string telefono = "+12025550142";

                    var cadena = app.Configuration
                        .GetConnectionString("SynchroDB");

                    if (string.IsNullOrWhiteSpace(cadena))
                        return Results.Problem(
                            "Conexion SQL no configurada.");

                    string codigo = CodigoVerificacionTelefono.Generar();

                    byte[] hash;

                    try
                    {
                        hash = CodigoVerificacionTelefono.CalcularHash(
                            codigo, personaId, telefono, app.Configuration);
                    }
                    catch (InvalidOperationException)
                    {
                        return Results.Problem(
                            "Clave HMAC no configurada correctamente.");
                    }
                    catch (FormatException)
                    {
                        return Results.Problem(
                            "Formato de clave HMAC invalido.");
                    }

                    try
                    {
                        await using var conexion = new SqlConnection(cadena);
                        await conexion.OpenAsync();

                        await using var transaccion =
                            (SqlTransaction)await conexion.BeginTransactionAsync();

                        try
                        {
                            int verificacionId;

                            await using (var crear = new SqlCommand(
                                "dbo.CrearVerificacionTelefonoPendienteSynchro",
                                conexion,
                                transaccion))
                            {
                                crear.CommandType =
                                    System.Data.CommandType.StoredProcedure;

                                crear.Parameters.Add("@PersonaId",
                                    System.Data.SqlDbType.Int).Value = personaId;

                                crear.Parameters.Add("@Telefono",
                                    System.Data.SqlDbType.NVarChar, 20)
                                    .Value = telefono;

                                crear.Parameters.Add("@CodigoHash",
                                    System.Data.SqlDbType.VarBinary, 32)
                                    .Value = hash;

                                await using var lector =
                                    await crear.ExecuteReaderAsync();

                                if (!await lector.ReadAsync())
                                    throw new InvalidOperationException(
                                        "No se creo la verificacion.");

                                verificacionId =
                                    Convert.ToInt32(lector["Id"]);
                            }

                            string resultado;

                            await using (var validar = new SqlCommand(
                                "dbo.ValidarCodigoTelefonoSynchro",
                                conexion,
                                transaccion))
                            {
                                validar.CommandType =
                                    System.Data.CommandType.StoredProcedure;

                                validar.Parameters.Add("@VerificacionId",
                                    System.Data.SqlDbType.Int)
                                    .Value = verificacionId;

                                validar.Parameters.Add("@PersonaId",
                                    System.Data.SqlDbType.Int)
                                    .Value = personaId;

                                validar.Parameters.Add("@CodigoHash",
                                    System.Data.SqlDbType.VarBinary, 32)
                                    .Value = hash;

                                await using var lector =
                                    await validar.ExecuteReaderAsync();

                                if (!await lector.ReadAsync())
                                    throw new InvalidOperationException(
                                        "No se obtuvo resultado de validacion.");

                                resultado =
                                    lector["Resultado"].ToString() ?? "";
                            }

                            // La prueba no debe dejar datos verificados.
                            await transaccion.RollbackAsync();

                            return Results.Ok(new
                            {
                                prueba = "Integracion C# y SQL Server",
                                resultado,
                                cambiosGuardados = false
                            });
                        }
                        catch
                        {
                            await transaccion.RollbackAsync();
                            throw;
                        }
                    }
                    catch (SqlException)
                    {
                        return Results.Problem(
                            "Error SQL durante la prueba.");
                    }
                    finally
                    {
                        System.Security.Cryptography
                            .CryptographicOperations.ZeroMemory(hash);
                    }
                })
                .RequireAuthorization("SoloAdministradores");
        }
    }
}
