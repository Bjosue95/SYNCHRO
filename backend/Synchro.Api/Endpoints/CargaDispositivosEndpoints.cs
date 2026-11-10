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
using Synchro.Api.Services;

namespace Synchro.Api.Endpoints;

public static class CargaDispositivosEndpoints
{
    public static void MapCargaDispositivosEndpoints(this WebApplication app)
    {
        var operaciones = app.MapGroup("")
            .RequireAuthorization("SoloTerminal")
            .RequireRateLimiting("OperacionesTerminal")
            .AddEndpointFilter<OperacionTerminalSeguraFilter>();

        app.MapGet("/api/espacios-carga", async () =>
        {
            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            var lista = new List<EspacioCarga>();

            using var comando = new SqlCommand("SELECT Codigo, EStado FROM EspaciosCarga ORDER BY Codigo", conexion);

            using var lector = await comando.ExecuteReaderAsync();

            while (await lector.ReadAsync())
            {
                lista.Add(new EspacioCarga
                {
                    Codigo = lector.GetString(0),
                    Estado = lector.GetString(1)
                });
            }

            return Results.Ok(lista);
        });

        operaciones.MapPost("/api/espacios-carga/{codigo}/reservar", async (string codigo) =>
        {

            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            using var comando = new SqlCommand(@"UPDATE EspaciosCarga SET Estado = N'Reservado' WHERE Codigo = @codigo AND Estado = N'Disponible'", conexion);

            comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

            var filasActualizadas = await comando.ExecuteNonQueryAsync();

            if (filasActualizadas == 0)
            {
                return Results.Conflict(new
                {
                    mensaje = "Espacio de carga no encontrado o no disponible"
                });
            }

            return Results.Ok(new
            {
                mensaje = "Espacio de carga reservado correctamente",
                codigo = codigo,
                estado = "Reservado"
            });
        });

        operaciones.MapPost("/api/espacios-carga/{codigo}/iniciar", async (string codigo, IniciarCargaSolicitud solicitud) =>
        {
            if (solicitud.TipoDispositivo is not ("Celular" or "Laptop"))
            {
                return Results.BadRequest(new
                {
                    mensaje = "El dispositivo debe ser Celular o Laptop"
                });
            }

            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            using var transaccion = conexion.BeginTransaction();

            using var actualizar = new SqlCommand(@"UPDATE ESpaciosCarga SET Estado = N'Ocupado' WHERE Codigo = @codigo ANd Estado = N'Reservado'", conexion, transaccion);

            actualizar.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

            var filas = await actualizar.ExecuteNonQueryAsync();

            if (filas == 0)
            {
                transaccion.Rollback();

                return Results.Conflict(new
                {
                    mensaje = "el espacio no esta reservado"
                });
            }

            using var insertar = new SqlCommand(@"
            INSERT INTO dbo.SesionesCarga (CodigoEspacio, TipoDispositivo)
            OUTPUT INSERTED.Id
            VALUES (@codigo, @tipoDispositivo)",
             conexion, transaccion);

            insertar.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

            insertar.Parameters.Add("@tipoDispositivo", System.Data.SqlDbType.NVarChar, 20).Value = solicitud.TipoDispositivo!;

            var idSesion = Convert.ToInt32(await insertar.ExecuteScalarAsync());

            transaccion.Commit();

            return Results.Ok(new
            {
                mensaje = " Sesion de carga iniciada correctamente",
                codigo = codigo,
                estado = "Ocupado",
                idSesion = idSesion
            });
        });

        operaciones.MapPost("/api/sesiones-carga/{id}/finalizar", async (int id) =>
        {
            var cadena = app.Configuration.GetConnectionString("SynchroDB");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            using var transaccion = conexion.BeginTransaction();

            using var finalizar = new SqlCommand(@"UPDATE SesionesCarga SET Fin = SYSDATETIME() OUTPUT INSERTED.CodigoEspacio WHERE Id = @id AND Fin IS NULL", conexion, transaccion);

            finalizar.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;

            var codigo = await finalizar.ExecuteScalarAsync() as string;

            if (codigo == null)
            {
                transaccion.Rollback();

                return Results.Conflict(new
                {
                    mensaje = "sesion no encontrada o ya finalizada"
                });
            }

            using var liberar = new SqlCommand(@"UPDATE EspaciosCarga SET Estado = N'Disponible' WHERE Codigo = @codigo AND Estado = N'Ocupado'", conexion, transaccion);

            liberar.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

            var filas = await liberar.ExecuteNonQueryAsync();

            if (filas != 1)
            {
                transaccion.Rollback();

                return Results.Conflict(new
                {
                    mensaje = "No pudo liberar el espacio de carga"
                });
            }


            using var registrarCobro = new SqlCommand(@"
            INSERT INTO dbo.CobrosCarga (
                SesionCargaId,
                TipoDispositivo,
                MinutosFacturados,
                MinutosBaseAplicados,
                PrecioBaseAplicado,
                MinutosAdicionalesAplicados,
                PrecioAdicionalAplicado,
                MontoTotal
            )
            OUTPUT INSERTED.MontoTotal
            SELECT
                S.Id,
                S.TipoDispositivo,
                T.MinutosBase + B.Bloques * T.MinutosAdicionales,
                T.MinutosBase,
                T.PrecioBase,
                T.MinutosAdicionales,
                T.PrecioAdicional,
                T.PrecioBase + B.Bloques * T.PrecioAdicional
            FROM dbo.SesionesCarga AS S
            INNER JOIN dbo.TarifasCarga AS T
                ON S.TipoDispositivo = T.TipoDispositivo
            CROSS APPLY (
                SELECT DATEDIFF_BIG(SECOND, S.Inicio, S.Fin)
                    AS Segundos
            ) AS D
            CROSS APPLY (
                SELECT CASE
                    WHEN D.Segundos <= T.MinutosBase * 60
                        THEN 0
                    ELSE (
                        D.Segundos - T.MinutosBase * 60
                        + T.MinutosAdicionales * 60 - 1
                    ) / (T.MinutosAdicionales * 60)
                END AS Bloques
            ) AS B
            WHERE S.Id = @id AND S.Fin IS NOT NULL",
                conexion, transaccion);

            registrarCobro.Parameters.Add(
                "@id", System.Data.SqlDbType.Int).Value = id;

            var monto = await registrarCobro.ExecuteScalarAsync();

            if (monto == null)
            {
                transaccion.Rollback();

                return Results.Conflict(new
                {
                    mensaje = "No se pudo calcular el cobro. Verifique la tarifa."
                });
            }


            using var registrarPago = new SqlCommand(@"
            INSERT INTO dbo.Pagos (
                SesionCargaId,
                Monto,
                Estado
            )
            SELECT
                SesionCargaId,
                MontoTotal,
                N'Pendiente'
            FROM dbo.CobrosCarga
            WHERE SesionCargaId = @id",
                conexion, transaccion);

            registrarPago.Parameters.Add(
                "@id", System.Data.SqlDbType.Int).Value = id;

            var filasPago = await registrarPago.ExecuteNonQueryAsync();

            if (filasPago != 1)
            {
                transaccion.Rollback();

                return Results.Conflict(new
                {
                    mensaje = "No se pudo registrar el pago pendiente."
                });
            }


            transaccion.Commit();

            return Results.Ok(new
            {
                mensaje = "Sesion finalizada correctamente",
                idSesion = id,
                estado = "Disponible",
                montoTotal = Convert.ToDecimal(monto)
            });


        });

        app.MapGet("/api/tarifas-carga", async () =>
        {
            var cadena = app.Configuration.GetConnectionString("SynchroDb");

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            var tarifas = new List<TarifaCarga>();

            using var comando = new SqlCommand(@"SELECT TipoDispositivo, MinutosBase, PrecioBase, MinutosAdicionales, PrecioAdicional FROM TarifasCarga", conexion);


            using var lector = await comando.ExecuteReaderAsync();

            while (await lector.ReadAsync())
            {
                tarifas.Add(new TarifaCarga
                {
                    TipoDispositivo = lector.GetString(0),
                    MinutosBase = lector.GetInt32(1),
                    PrecioBase = lector.GetDecimal(2),
                    MinutosAdicionales = lector.GetInt32(3),
                    PrecioAdicional = lector.GetDecimal(4)
                });
            }

            return Results.Ok(tarifas);

        });
    }
}
