
using System.Data;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Services.WhatsApp;

internal sealed record SolicitudInicioWhatsAppTomada(
    long EventoId,
    int ReservaId,
    string NumeroOrigen,
    Guid Arrendamiento);

// Este servicio solo reclama solicitudes.
// NO genera codigos, NO envia WhatsApp
// y NO confirma verificaciones telefonicas.
internal sealed class ReclamadorSolicitudesInicioWhatsAppSql(
    IConfiguration configuracion)
{
    public async Task<SolicitudInicioWhatsAppTomada?> TomarAsync(
        CancellationToken cancelacion = default)
    {
        string cadena = ConexionSqlAislada.Exigir(
            configuracion.GetConnectionString("SynchroDB"),
            "SynchroDB_Pruebas");

        Guid arrendamiento = Guid.NewGuid();

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);

        await using var transaccion = (SqlTransaction)
            await conexion.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancelacion);

        await using var comando = new SqlCommand("""
            SET NOCOUNT ON;
            SET XACT_ABORT ON;

            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 57110, 'Base no autorizada.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.extended_properties
                WHERE class = 0
                  AND name = N'SynchroFixtureDeposito'
            )
                THROW 57111, 'Falta marca de pruebas.', 1;

            DECLARE @Ahora DATETIME2(3) =
                SYSUTCDATETIME();

            -- Caducar solicitudes que ya no pueden
            -- procesarse, incluidos arrendamientos antiguos.
            UPDATE dbo.SolicitudesInicioSesionWhatsApp
            SET Estado = N'Caducada',
                Arrendamiento = NULL,
                ArrendamientoHastaUtc = NULL,
                CodigoError = N'SolicitudVencida'
            WHERE Estado IN (
                    N'Pendiente',
                    N'Reintento',
                    N'Procesando'
                )
              AND ExpiraUtc <= @Ahora;

            -- Una solicitud que agoto sus intentos
            -- no debe reclamarse indefinidamente.
            UPDATE dbo.SolicitudesInicioSesionWhatsApp
            SET Estado = N'Indeterminado',
                Arrendamiento = NULL,
                ArrendamientoHastaUtc = NULL,
                CodigoError = N'IntentosAgotados'
            WHERE Intentos >= 3
              AND (
                    Estado = N'Reintento'
                    OR (
                        Estado = N'Procesando'
                        AND ArrendamientoHastaUtc <= @Ahora
                    )
                );

            DECLARE @Tomadas TABLE (
                EventoId BIGINT NOT NULL,
                ReservaId INT NOT NULL,
                Arrendamiento UNIQUEIDENTIFIER NOT NULL
            );

            -- Seleccionar una sola solicitud.
            -- Los bloqueos impiden que dos procesos
            -- reclamen simultaneamente el mismo registro.
            ;WITH Candidata AS (
                SELECT TOP (1) s.EventoId
                FROM dbo.SolicitudesInicioSesionWhatsApp s
                    WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.WhatsAppWebhookEventos e
                    ON e.Id = s.EventoId
                WHERE s.ExpiraUtc > @Ahora
                  AND s.Intentos < 3
                  AND e.TipoEvento = N'Mensaje'
                  AND (
                      (
                          s.Estado IN (
                              N'Pendiente',
                              N'Reintento'
                          )
                          AND s.DisponibleUtc <= @Ahora
                      )
                      OR (
                          s.Estado = N'Procesando'
                          AND s.ArrendamientoHastaUtc
                              <= @Ahora
                      )
                  )
                ORDER BY s.DisponibleUtc, s.EventoId
            )
            UPDATE s
            SET Estado = N'Procesando',
                Intentos = s.Intentos + 1,
                Arrendamiento = @Arrendamiento,
                ArrendamientoHastaUtc =
                    DATEADD(SECOND, 60, @Ahora),
                CodigoError = NULL
            OUTPUT
                INSERTED.EventoId,
                INSERTED.ReservaId,
                INSERTED.Arrendamiento
            INTO @Tomadas
            FROM dbo.SolicitudesInicioSesionWhatsApp s
            INNER JOIN Candidata c
                ON c.EventoId = s.EventoId;

            -- Devolver exclusivamente el numero de origen
            -- guardado previamente por el webhook.
            SELECT
                t.EventoId,
                t.ReservaId,
                e.WhatsappId,
                t.Arrendamiento
            FROM @Tomadas t
            INNER JOIN dbo.WhatsAppWebhookEventos e
                ON e.Id = t.EventoId;
            """, conexion, transaccion);

        comando.Parameters.Add(
            "@Arrendamiento",
            SqlDbType.UniqueIdentifier
        ).Value = arrendamiento;

        SolicitudInicioWhatsAppTomada? solicitud = null;

        await using (var lector =
            await comando.ExecuteReaderAsync(cancelacion))
        {
            if (await lector.ReadAsync(cancelacion))
            {
                solicitud = new SolicitudInicioWhatsAppTomada(
                    lector.GetInt64(0),
                    lector.GetInt32(1),
                    lector.GetString(2),
                    lector.GetGuid(3));

                if (await lector.ReadAsync(cancelacion))
                {
                    throw new InvalidOperationException(
                        "Se reclamo mas de una solicitud.");
                }
            }
        }

        await transaccion.CommitAsync(cancelacion);

        return solicitud;
    }
}
