
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;

namespace Synchro.Api.Services.WhatsApp;

// Uso interno exclusivo del webhook con firma de Meta validada.
// Nunca utilizar numeroOrigen proporcionado por un navegador.
internal sealed class IdentidadDestinatarioWhatsAppSql(
    IConfiguration configuracion)
{
    public async Task<int> ResolverAsync(
        int reservaId,
        string numeroOrigen,
        CancellationToken cancelacion = default)
    {
        if (reservaId <= 0 ||
            string.IsNullOrWhiteSpace(numeroOrigen) ||
            numeroOrigen.Length is < 8 or > 15 ||
            numeroOrigen[0] == '0' ||
            !numeroOrigen.All(char.IsAsciiDigit))
        {
            throw new ArgumentException(
                "Solicitud de identidad invalida.");
        }

        string cadena = ConexionSqlAislada.Exigir(
            configuracion.GetConnectionString("SynchroDB"),
            "SynchroDB_Pruebas");

        string telefono = "+" + numeroOrigen;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);

        await using var transaccion = (SqlTransaction)
            await conexion.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancelacion);

        // Verificar la base y la autorizacion del telefono.
        await using (var comprobar = new SqlCommand("""
            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 56995, 'Base no autorizada.', 1;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.extended_properties
                WHERE class = 0
                  AND name = N'SynchroFixtureDeposito'
            )
                THROW 56996, 'Base sin marca de pruebas.', 1;

            SELECT c.TelefonoWhatsApp
            FROM dbo.PreReservasEncomiendasWeb AS r
                WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.ConsentimientosPreReservaWhatsApp AS c
                WITH (UPDLOCK, HOLDLOCK)
                ON c.ReservaId = r.Id
            WHERE r.Id = @ReservaId
              AND c.TelefonoWhatsApp = @Telefono
              AND c.Estado = N'Confirmado'
              AND NULLIF(c.MensajeIdConfirmacion, N'')
                    IS NOT NULL
              AND c.FechaConfirmacionUtc IS NOT NULL
              AND c.FechaConfirmacionUtc >= c.FechaCreacionUtc
              AND c.FechaConfirmacionUtc <= c.FechaExpiracionUtc
              AND c.FechaConfirmacionUtc <= SYSUTCDATETIME()
              AND c.TelefonoWhatsApp =
                  CASE
                      WHEN LEFT(r.TelefonoDestinatario, 1) = N'+'
                      THEN r.TelefonoDestinatario
                      ELSE N'+' + r.TelefonoDestinatario
                  END
              AND (
                  r.FechaExpiracion > SYSUTCDATETIME()
                  OR EXISTS (
                      SELECT 1
                      FROM dbo.DepositosPreReservaEncomienda AS d
                      WHERE d.ReservaId = r.Id
                  )
              );
            """, conexion, transaccion))
        {
            comprobar.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaId;

            comprobar.Parameters.Add(
                "@Telefono", SqlDbType.NVarChar, 20
            ).Value = telefono;

            object? resultado =
                await comprobar.ExecuteScalarAsync(cancelacion);

            if (resultado is not string)
            {
                throw new InvalidOperationException(
                    "Identidad no autorizada.");
            }
        }

        // Impedir seleccionar arbitrariamente entre varias
        // personas que tengan asociado el mismo telefono.
        var personas = new List<int>(2);

        await using (var buscar = new SqlCommand("""
            SELECT TOP (2) Id
            FROM dbo.Personas WITH (UPDLOCK, HOLDLOCK)
            WHERE TelefonoWhatsapp = @Telefono
            ORDER BY Id;
            """, conexion, transaccion))
        {
            buscar.Parameters.Add(
                "@Telefono", SqlDbType.NVarChar, 20
            ).Value = telefono;

            await using var lector =
                await buscar.ExecuteReaderAsync(cancelacion);

            while (await lector.ReadAsync(cancelacion))
            {
                personas.Add(lector.GetInt32(0));
            }
        }

        if (personas.Count > 1)
        {
            throw new InvalidOperationException(
                "Identidad telefonica duplicada.");
        }

        int personaId;

        if (personas.Count == 1)
        {
            personaId = personas[0];
        }
        else
        {
            // No se crea una cuenta web.
            // Solo un registro basico de destinatario.
            await using var crear = new SqlCommand("""
                INSERT INTO dbo.Personas
                    (TelefonoWhatsapp, TieneCuenta)
                OUTPUT INSERTED.Id
                VALUES (@Telefono, 0);
                """, conexion, transaccion);

            crear.Parameters.Add(
                "@Telefono", SqlDbType.NVarChar, 20
            ).Value = telefono;

            personaId = Convert.ToInt32(
                await crear.ExecuteScalarAsync(cancelacion));
        }

        await transaccion.CommitAsync(cancelacion);

        return personaId;
    }
}