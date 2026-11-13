
using System.Data;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Services.WhatsApp;

// Prepara verificaciones a partir de solicitudes reclamadas.
// No envia mensajes ni realiza llamadas a Meta.
internal sealed class ProcesadorSolicitudesInicioWhatsAppSql(
    IConfiguration configuracion,
    IdentidadDestinatarioWhatsAppSql identidad,
    EmisorVerificacionTelefonoSql emisor)
{
    public async Task<bool> PrepararAsync(
        SolicitudInicioWhatsAppTomada solicitud,
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

        string cadena = ConexionSqlAislada.Exigir(
            configuracion.GetConnectionString("SynchroDB"),
            "SynchroDB_Pruebas");

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);

        // La transaccion protege la identidad,
        // la verificacion y el estado de la solicitud.
        await using var transaccion = (SqlTransaction)
            await conexion.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancelacion);

        // Comprobar que el arrendamiento sigue vigente.
        // UPDLOCK y HOLDLOCK impiden que otro proceso
        // modifique esta solicitud mientras se prepara.
        await using (var comprobar = new SqlCommand("""
            SET NOCOUNT ON;

            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 57130, 'Base no autorizada.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.extended_properties
                WHERE class = 0
                  AND name = N'SynchroFixtureDeposito'
            )
                THROW 57131, 'Falta marca de pruebas.', 1;

            SELECT TOP (1) s.EventoId
            FROM dbo.SolicitudesInicioSesionWhatsApp s
                WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.WhatsAppWebhookEventos e
                ON e.Id = s.EventoId
            WHERE s.EventoId = @EventoId
              AND s.ReservaId = @ReservaId
              AND s.Estado = N'Procesando'
              AND s.Arrendamiento = @Arrendamiento
              AND s.ArrendamientoHastaUtc >
                  SYSUTCDATETIME()
              AND s.ExpiraUtc > SYSUTCDATETIME()
              AND s.VerificacionId IS NULL
              AND e.TipoEvento = N'Mensaje'
              AND e.WhatsappId = @NumeroOrigen;
            """, conexion, transaccion))
        {
            AgregarParametrosSolicitud(
                comprobar, solicitud);

            object? encontrado =
                await comprobar.ExecuteScalarAsync(cancelacion);

            if (encontrado is null or DBNull)
            {
                // No actualizar una solicitud cuyo
                // arrendamiento haya vencido o cambiado.
                return false;
            }
        }

        // Validar la identidad otra vez.
        // El consentimiento pudo revocarse desde
        // que se recibio el mensaje.
        int personaId =
            await identidad.ResolverEnTransaccionAsync(
                conexion,
                transaccion,
                solicitud.ReservaId,
                solicitud.NumeroOrigen,
                cancelacion);

        // Generar el codigo y guardar su mensaje cifrado.
        // Este metodo NO confirma la transaccion.
        int verificacionId =
            await emisor.PrepararEnTransaccionAsync(
                conexion,
                transaccion,
                personaId,
                solicitud.ReservaId,
                cancelacion);

        // Vincular la verificacion a la solicitud
        // solamente si conserva su arrendamiento.
        await using (var finalizar = new SqlCommand("""
            SET NOCOUNT ON;

            UPDATE dbo.SolicitudesInicioSesionWhatsApp
            SET Estado = N'Preparada',
                VerificacionId = @VerificacionId,
                Arrendamiento = NULL,
                ArrendamientoHastaUtc = NULL,
                CodigoError = NULL
            WHERE EventoId = @EventoId
              AND ReservaId = @ReservaId
              AND Estado = N'Procesando'
              AND Arrendamiento = @Arrendamiento
              AND ArrendamientoHastaUtc >
                  SYSUTCDATETIME()
              AND ExpiraUtc > SYSUTCDATETIME()
              AND VerificacionId IS NULL;

            SELECT @@ROWCOUNT;
            """, conexion, transaccion))
        {
            AgregarParametrosSolicitud(
                finalizar, solicitud);

            finalizar.Parameters.Add(
                "@VerificacionId", SqlDbType.Int
            ).Value = verificacionId;

            int modificadas = Convert.ToInt32(
                await finalizar.ExecuteScalarAsync(cancelacion));

            if (modificadas != 1)
            {
                // La transaccion completa se revertira,
                // incluidos el codigo y su mensaje cifrado.
                throw new InvalidOperationException(
                    "La solicitud perdio su arrendamiento.");
            }
        }

        // Solo confirmar cuando las tres operaciones
        // estan completas.
        await transaccion.CommitAsync(cancelacion);

        return true;
    }

    private static void AgregarParametrosSolicitud(
        SqlCommand comando,
        SolicitudInicioWhatsAppTomada solicitud)
    {
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
    }
}
