
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Services.WhatsApp;

// Servicio interno. No recibe telefonos desde HTTP.
// La identidad debe autorizarse antes de invocar este servicio.
internal sealed class EmisorVerificacionTelefonoSql(
    IConfiguration configuracion,
    ProtectorVerificacionTelefono protector)
{

    public async Task<int> PrepararAsync(
        int personaId,
        int reservaId,
        CancellationToken cancelacion = default)
    {
        if (personaId <= 0 || reservaId <= 0)
            throw new ArgumentException(
                "Referencias invalidas.");

        string cadena = ConexionSqlAislada.Exigir(
            configuracion.GetConnectionString("SynchroDB"),
            "SynchroDB_Pruebas");

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);

        await using var transaccion = (SqlTransaction)
            await conexion.BeginTransactionAsync(cancelacion);

        int verificacionId = await PrepararEnTransaccionAsync(
            conexion,
            transaccion,
            personaId,
            reservaId,
            cancelacion);

        // El metodo original conserva su comportamiento:
        // confirma cuando todo termina correctamente.
        await transaccion.CommitAsync(cancelacion);

        return verificacionId;
    }

    // Nueva modalidad para el futuro procesador de solicitudes.
    // Reutiliza la conexion y transaccion del llamador.
    internal async Task<int> PrepararEnTransaccionAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        int personaId,
        int reservaId,
        CancellationToken cancelacion = default)
    {
        if (personaId <= 0 || reservaId <= 0)
            throw new ArgumentException(
                "Referencias invalidas.");

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

        string? plantilla = configuracion[
            "WhatsApp:PlantillaCodigoInicioSesion"];

        if (!MensajeWhatsApp.PlantillaEsValida(plantilla))
            throw new InvalidOperationException(
                "Plantilla WhatsApp no configurada.");


        // Solo una reserva con consentimiento confirmado
        // permite preparar un codigo para su destinatario.
        string telefono;

        await using (var buscar = new SqlCommand("""
            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 56990, 'Base no autorizada.', 1;

            SELECT p.TelefonoWhatsapp
            FROM dbo.PreReservasEncomiendasWeb AS r
                WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.ConsentimientosPreReservaWhatsApp AS c
                WITH (UPDLOCK, HOLDLOCK)
                ON c.ReservaId = r.Id
            INNER JOIN dbo.Personas AS p
                WITH (UPDLOCK, HOLDLOCK)
                ON p.Id = @PersonaId
               AND p.TelefonoWhatsapp = c.TelefonoWhatsApp
            WHERE r.Id = @ReservaId
              AND c.Estado = N'Confirmado'
              AND c.MensajeIdConfirmacion IS NOT NULL
              AND c.MensajeIdConfirmacion <> N''
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
            buscar.Parameters.Add(
                "@PersonaId", SqlDbType.Int
            ).Value = personaId;

            buscar.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaId;

            telefono = (string?)await buscar
                .ExecuteScalarAsync(cancelacion)
                ?? throw new InvalidOperationException(
                    "Destinatario o consentimiento no disponible.");
        }

        string codigo = CodigoVerificacionTelefono.Generar();

        byte[] hash = CodigoVerificacionTelefono.CalcularHash(
            codigo,
            personaId,
            telefono,
            configuracion);

        int verificacionId;
        DateTime vencimiento;

        try
        {
            await using var crear = new SqlCommand(
                "dbo.CrearVerificacionTelefonoPendienteSynchro",
                conexion,
                transaccion)
            {
                CommandType = CommandType.StoredProcedure
            };

            crear.Parameters.Add(
                "@PersonaId", SqlDbType.Int
            ).Value = personaId;

            crear.Parameters.Add(
                "@Telefono", SqlDbType.NVarChar, 20
            ).Value = telefono;

            crear.Parameters.Add(
                "@CodigoHash", SqlDbType.VarBinary, 32
            ).Value = hash;

            crear.Parameters.Add(
                "@Finalidad", SqlDbType.NVarChar, 40
            ).Value = "InicioSesionDestinatario";

            await using var lector = await crear
                .ExecuteReaderAsync(cancelacion);

            if (!await lector.ReadAsync(cancelacion))
                throw new InvalidOperationException(
                    "No se creo la verificacion.");

            verificacionId = lector.GetInt32(
                lector.GetOrdinal("Id"));

            vencimiento = lector.GetDateTime(
                lector.GetOrdinal("FechaExpiracion"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }

        Guid mensajeId = Guid.NewGuid();

        var mensaje = new MensajeWhatsApp(
            telefono,
            plantilla!,
            [codigo]);

        byte[] cifrado = protector.Proteger(
            mensajeId,
            verificacionId,
            mensaje);

        byte[] bytesTelefono =
            Encoding.Unicode.GetBytes(telefono);

        try
        {
            byte[] telefonoHash =
                SHA256.HashData(bytesTelefono);

            try
            {
                await using var encolar = new SqlCommand("""
                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 56991, 'Base no autorizada.', 1;

                    INSERT INTO dbo.OutboxVerificacionTelefono
                        (Id, VerificacionId, TelefonoHash,
                         ContenidoProtegido, ExpiraUtc)
                    VALUES
                        (@MensajeId, @VerificacionId,
                         @TelefonoHash, @Cifrado, @Vencimiento);
                    """, conexion, transaccion);

                encolar.Parameters.Add(
                    "@MensajeId", SqlDbType.UniqueIdentifier
                ).Value = mensajeId;

                encolar.Parameters.Add(
                    "@VerificacionId", SqlDbType.Int
                ).Value = verificacionId;

                encolar.Parameters.Add(
                    "@TelefonoHash", SqlDbType.VarBinary, 32
                ).Value = telefonoHash;

                encolar.Parameters.Add(
                    "@Cifrado", SqlDbType.VarBinary, -1
                ).Value = cifrado;

                encolar.Parameters.Add(
                    "@Vencimiento", SqlDbType.DateTime2
                ).Value = vencimiento;

                await encolar.ExecuteNonQueryAsync(
                    cancelacion);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    telefonoHash);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                bytesTelefono);

            CryptographicOperations.ZeroMemory(
                cifrado);
        }

        // La transaccion pertenece al llamador.
        // Nunca devolver el codigo.
        return verificacionId;
    }
}
