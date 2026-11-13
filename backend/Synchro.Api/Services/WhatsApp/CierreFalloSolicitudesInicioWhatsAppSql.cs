
using System.Data;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Services.WhatsApp;

internal enum ResultadoFalloSolicitudInicio
{
    Rechazada,
    Reintento,
    Indeterminado
}

// Cierra solicitudes que no pudieron prepararse.
// Solo modifica la solicitud cuyo arrendamiento
// sigue perteneciendo al proceso autorizado.
//
// No genera codigos ni envia WhatsApp.
internal sealed class CierreFalloSolicitudesInicioWhatsAppSql(
    IConfiguration configuracion)
{
    public async Task<bool> CerrarAsync(
        SolicitudInicioWhatsAppTomada solicitud,
        ResultadoFalloSolicitudInicio resultado,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (solicitud.EventoId <= 0 ||
            solicitud.ReservaId <= 0 ||
            solicitud.Arrendamiento == Guid.Empty ||
            string.IsNullOrWhiteSpace(solicitud.NumeroOrigen) ||
            solicitud.NumeroOrigen.Length is < 8 or > 15 ||
            solicitud.NumeroOrigen[0] == '0' ||
            !solicitud.NumeroOrigen.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(
                "Solicitud reclamada invalida.");
        }

        if (resultado is not (
            ResultadoFalloSolicitudInicio.Rechazada or
            ResultadoFalloSolicitudInicio.Reintento or
            ResultadoFalloSolicitudInicio.Indeterminado))
        {
            throw new ArgumentOutOfRangeException(
                nameof(resultado));
        }

        string cadena = ConexionSqlAislada.Exigir(
            configuracion.GetConnectionString("SynchroDB"),
            "SynchroDB_Pruebas");

        string decision = resultado.ToString();

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);

        await using var comando = new SqlCommand("""
            SET NOCOUNT ON;
            SET XACT_ABORT ON;

            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 57150, 'Base no autorizada.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.extended_properties
                WHERE class = 0
                  AND name = N'SynchroFixtureDeposito'
            )
                THROW 57151, 'Falta marca de pruebas.', 1;

            DECLARE @Ahora DATETIME2(3) =
                SYSUTCDATETIME();

            UPDATE s
            SET Estado =
                    CASE
                        WHEN s.ExpiraUtc <= @Ahora
                            THEN N'Caducada'

                        WHEN @Decision = N'Rechazada'
                            THEN N'Rechazada'

                        WHEN @Decision = N'Indeterminado'
                            THEN N'Indeterminado'

                        WHEN s.Intentos >= 3
                            THEN N'Indeterminado'

                        WHEN DATEADD(
                            SECOND,
                            s.Intentos * 15,
                            @Ahora
                        ) >= s.ExpiraUtc
                            THEN N'Caducada'

                        ELSE N'Reintento'
                    END,

                DisponibleUtc =
                    CASE
                        WHEN @Decision = N'Reintento'
                             AND s.Intentos < 3
                        THEN DATEADD(
                            SECOND,
                            s.Intentos * 15,
                            @Ahora
                        )
                        ELSE s.DisponibleUtc
                    END,

                CodigoError =
                    CASE
                        WHEN s.ExpiraUtc <= @Ahora
                            THEN N'SolicitudVencida'

                        WHEN @Decision = N'Rechazada'
                            THEN N'AutorizacionNoValida'

                        WHEN @Decision = N'Indeterminado'
                            THEN N'ResultadoNoConfirmado'

                        WHEN s.Intentos >= 3
                            THEN N'IntentosAgotados'

                        WHEN DATEADD(
                            SECOND,
                            s.Intentos * 15,
                            @Ahora
                        ) >= s.ExpiraUtc
                            THEN N'SinTiempoParaReintentar'

                        ELSE N'ErrorReintentable'
                    END,

                Arrendamiento = NULL,
                ArrendamientoHastaUtc = NULL

            FROM dbo.SolicitudesInicioSesionWhatsApp s
            INNER JOIN dbo.WhatsAppWebhookEventos e
                ON e.Id = s.EventoId

            WHERE s.EventoId = @EventoId
              AND s.ReservaId = @ReservaId
              AND s.Estado = N'Procesando'
              AND s.Arrendamiento = @Arrendamiento
              AND s.ArrendamientoHastaUtc > @Ahora
              AND s.VerificacionId IS NULL
              AND e.TipoEvento = N'Mensaje'
              AND e.WhatsappId = @NumeroOrigen;

            SELECT @@ROWCOUNT;
            """, conexion);

        comando.Parameters.Add(
            "@EventoId", SqlDbType.BigInt
        ).Value = solicitud.EventoId;

        comando.Parameters.Add(
            "@ReservaId", SqlDbType.Int
        ).Value = solicitud.ReservaId;

        comando.Parameters.Add(
            "@Arrendamiento", SqlDbType.UniqueIdentifier
        ).Value = solicitud.Arrendamiento;

        comando.Parameters.Add(
            "@NumeroOrigen", SqlDbType.NVarChar, 32
        ).Value = solicitud.NumeroOrigen;

        comando.Parameters.Add(
            "@Decision", SqlDbType.NVarChar, 20
        ).Value = decision;

        int modificadas = Convert.ToInt32(
            await comando.ExecuteScalarAsync(cancelacion));

        return modificadas == 1;
    }
}