
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;

namespace Synchro.Api.Services.WhatsApp;

// Uso interno exclusivo de procesos autorizados.
// El telefono debe proceder del webhook con firma validada,
// nunca de un navegador.
internal sealed class IdentidadDestinatarioWhatsAppSql(
    IConfiguration configuracion)
{
    // Conserva la operacion independiente utilizada
    // por el codigo y las pruebas existentes.
    public async Task<int> ResolverAsync(
        int reservaId,
        string numeroOrigen,
        CancellationToken cancelacion = default)
    {
        ValidarSolicitud(reservaId, numeroOrigen);

        string cadena = ConexionSqlAislada.Exigir(
            configuracion.GetConnectionString("SynchroDB"),
            "SynchroDB_Pruebas");

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);

        await using var transaccion = (SqlTransaction)
            await conexion.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancelacion);

        int personaId = await ResolverEnTransaccionAsync(
            conexion,
            transaccion,
            reservaId,
            numeroOrigen,
            cancelacion);

        await transaccion.CommitAsync(cancelacion);

        return personaId;
    }

    // Nueva modalidad: reutiliza una transaccion existente.
    // NO confirma ni revierte la transaccion del llamador.
    // El futuro procesador decidira cuando hacer COMMIT.
    internal async Task<int> ResolverEnTransaccionAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        int reservaId,
        string numeroOrigen,
        CancellationToken cancelacion = default)
    {
        ValidarSolicitud(reservaId, numeroOrigen);

        ArgumentNullException.ThrowIfNull(conexion);
        ArgumentNullException.ThrowIfNull(transaccion);

        if (conexion.State != ConnectionState.Open ||
            transaccion.Connection != conexion)
        {
            throw new InvalidOperationException(
                "La transaccion SQL no esta disponible.");
        }

        ConexionSqlAislada.Exigir(
            conexion.ConnectionString,
            "SynchroDB_Pruebas");

        string telefono = "+" + numeroOrigen;

        // Comprobar nuevamente el consentimiento y la
        // coincidencia del telefono al procesar la solicitud.
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
                throw new AutorizacionDestinatarioWhatsAppException();
            }
        }

        // No elegir arbitrariamente entre personas
        // que tengan registrado el mismo telefono.
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

        if (personas.Count == 1)
        {
            return personas[0];
        }

        // Crear un registro basico de destinatario,
        // sin crearle una cuenta web.
        //
        // Esta insercion queda pendiente dentro
        // de la transaccion recibida.
        await using var crear = new SqlCommand("""
            INSERT INTO dbo.Personas
                (TelefonoWhatsapp, TieneCuenta)
            OUTPUT INSERTED.Id
            VALUES (@Telefono, 0);
            """, conexion, transaccion);

        crear.Parameters.Add(
            "@Telefono", SqlDbType.NVarChar, 20
        ).Value = telefono;

        return Convert.ToInt32(
            await crear.ExecuteScalarAsync(cancelacion));
    }

    private static void ValidarSolicitud(
        int reservaId,
        string? numeroOrigen)
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
    }
}
// Error confirmado de autorizacion.
// No representa un fallo temporal de SQL Server.
internal sealed class AutorizacionDestinatarioWhatsAppException
    : InvalidOperationException
{
    public AutorizacionDestinatarioWhatsAppException()
        : base("Identidad no autorizada.")
    {
    }
}