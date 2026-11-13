
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.WhatsApp;

// Solo debe invocarse desde el webhook, despues
// de validar la firma de Meta.
//
// Utiliza la MISMA transaccion del evento recibido.
internal static class RegistroSolicitudesInicioSesionWhatsAppSql
{
    public static async Task<bool> RegistrarAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        string mensajeMetaId,
        string numeroOrigen,
        string? texto,
        CancellationToken cancelacion = default)
    {
        if (!ComandoInicioSesionWhatsApp.IntentarLeer(
                texto, out int reservaId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(mensajeMetaId) ||
            mensajeMetaId.Length > 255 ||
            string.IsNullOrWhiteSpace(numeroOrigen) ||
            numeroOrigen.Length is < 8 or > 15 ||
            numeroOrigen[0] == '0' ||
            !numeroOrigen.All(char.IsAsciiDigit))
        {
            return false;
        }

        // El webhook existente utiliza la clave M:{IdMeta}.
        byte[] claveEvento = SHA256.HashData(
            Encoding.UTF8.GetBytes("M:" + mensajeMetaId));

        try
        {
            await using var comando = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57060, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57061, 'Base sin marca de pruebas.', 1;

                DECLARE @Ahora DATETIME2(3) =
                    SYSUTCDATETIME();

                INSERT INTO dbo.SolicitudesInicioSesionWhatsApp
                    (EventoId, ReservaId, ExpiraUtc)
                SELECT
                    e.Id,
                    r.Id,
                    DATEADD(MINUTE, 5, @Ahora)
                FROM dbo.PreReservasEncomiendasWeb r
                    WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.ConsentimientosPreReservaWhatsApp c
                    WITH (UPDLOCK, HOLDLOCK)
                    ON c.ReservaId = r.Id
                INNER JOIN dbo.WhatsAppWebhookEventos e
                    WITH (UPDLOCK, HOLDLOCK)
                    ON e.ClaveEvento = @ClaveEvento
                WHERE r.Id = @ReservaId
                  AND e.TipoEvento = N'Mensaje'
                  AND e.MensajeId = @MensajeMetaId
                  AND e.WhatsappId = @NumeroOrigen

                  -- Identidad obtenida del webhook firmado.
                  AND c.TelefonoWhatsApp =
                      N'+' + e.WhatsappId

                  AND c.TelefonoWhatsApp =
                      CASE
                          WHEN LEFT(
                              r.TelefonoDestinatario, 1
                          ) = N'+'
                          THEN r.TelefonoDestinatario
                          ELSE N'+' + r.TelefonoDestinatario
                      END

                  -- Consentimiento comprobado.
                  AND c.Estado = N'Confirmado'
                  AND NULLIF(
                      c.MensajeIdConfirmacion, N''
                  ) IS NOT NULL
                  AND c.FechaConfirmacionUtc IS NOT NULL
                  AND c.FechaConfirmacionUtc >=
                      c.FechaCreacionUtc
                  AND c.FechaConfirmacionUtc <=
                      c.FechaExpiracionUtc
                  AND c.FechaConfirmacionUtc <= @Ahora

                  -- Remitente registrado y activo.
                  AND EXISTS (
                      SELECT 1
                      FROM dbo.ClientesWebPrototipo cliente
                      WHERE cliente.PersonaId =
                          r.ClientePersonaId
                        AND cliente.Activa = 1
                  )

                  -- Reserva vigente o encomienda depositada.
                  AND (
                      r.FechaExpiracion > @Ahora
                      OR EXISTS (
                          SELECT 1
                          FROM dbo.DepositosPreReservaEncomienda d
                          WHERE d.ReservaId = r.Id
                      )
                  )

                  -- No registrar dos veces el mismo evento.
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.SolicitudesInicioSesionWhatsApp s
                      WITH (UPDLOCK, HOLDLOCK)
                      WHERE s.EventoId = e.Id
                  )

                  -- Limitar solicitudes repetidas por reserva.
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.SolicitudesInicioSesionWhatsApp s
                      WITH (UPDLOCK, HOLDLOCK)
                      WHERE s.ReservaId = r.Id
                        AND s.CreadoUtc >
                            DATEADD(SECOND, -60, @Ahora)
                  );

                SELECT @@ROWCOUNT AS Registradas;
                """, conexion, transaccion);

            comando.Parameters.Add(
                "@ClaveEvento",
                SqlDbType.VarBinary, 32
            ).Value = claveEvento;

            comando.Parameters.Add(
                "@MensajeMetaId",
                SqlDbType.NVarChar, 255
            ).Value = mensajeMetaId;

            comando.Parameters.Add(
                "@NumeroOrigen",
                SqlDbType.NVarChar, 32
            ).Value = numeroOrigen;

            comando.Parameters.Add(
                "@ReservaId",
                SqlDbType.Int
            ).Value = reservaId;

            int registradas = Convert.ToInt32(
                await comando.ExecuteScalarAsync(cancelacion));

            return registradas == 1;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(claveEvento);
        }
    }
}
