
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class RegistroSolicitudesInicioSesionWhatsAppSqlTests
{
    private static string Cadena =>
        ConexionSqlAislada.Exigir(
            Environment.GetEnvironmentVariable(
                "SYNCHRO_SQL_DEPOSITO_TEST"),
            "SynchroDB_Pruebas");

    [SqlDepositoFact]
    public async Task RegistraSoloSolicitudesAutorizadas()
    {
        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();

        await using var transaccion = (SqlTransaction)
            await conexion.BeginTransactionAsync();

        try
        {
            const string numeroCorrecto = "50499990002";
            const string numeroAjeno = "50499990003";

            string usuario = "test_inicio_" +
                Guid.NewGuid().ToString("N");

            int reservaId;

            // Crear remitente, reserva y consentimiento
            // exclusivamente dentro de la transaccion.
            await using (var preparar = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57070, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57071, 'Falta marca de pruebas.', 1;

                INSERT dbo.Personas
                    (TelefonoWhatsapp, TieneCuenta)
                VALUES (N'+50499990001', 1);

                DECLARE @RemitenteId INT =
                    CONVERT(INT, SCOPE_IDENTITY());

                INSERT dbo.ClientesWebPrototipo
                    (PersonaId, NombreUsuario,
                     Telefono, PasswordHash)
                VALUES (
                    @RemitenteId,
                    @Usuario,
                    N'+50499990001',
                    N'HASH_FICTICIO_NO_AUTENTICABLE'
                );

                INSERT dbo.PreReservasEncomiendasWeb
                    (TokenGestionHash,
                     TelefonoRemitente,
                     TelefonoDestinatario,
                     TamanoSolicitado,
                     FechaExpiracion,
                     ClientePersonaId)
                VALUES (
                    CRYPT_GEN_RANDOM(32),
                    N'+50499990001',
                    N'+50499990002',
                    N'Mediano',
                    DATEADD(HOUR, 1, SYSUTCDATETIME()),
                    @RemitenteId
                );

                DECLARE @ReservaId INT =
                    CONVERT(INT, SCOPE_IDENTITY());

                INSERT dbo.ConsentimientosPreReservaWhatsApp
                    (ReservaId,
                     TelefonoWhatsApp,
                     CodigoHash,
                     Estado,
                     FechaCreacionUtc,
                     FechaExpiracionUtc,
                     FechaConfirmacionUtc,
                     MensajeIdConfirmacion)
                VALUES (
                    @ReservaId,
                    N'+50499990002',
                    CRYPT_GEN_RANDOM(32),
                    N'Confirmado',
                    DATEADD(MINUTE, -2, SYSUTCDATETIME()),
                    DATEADD(MINUTE, 30, SYSUTCDATETIME()),
                    DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                    N'CONS-' + @Usuario
                );

                SELECT @ReservaId;
                """, conexion, transaccion))
            {
                preparar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                reservaId = Convert.ToInt32(
                    await preparar.ExecuteScalarAsync());
            }

            string comando = $"SYNCHRO INICIAR {reservaId}";

            // PRUEBA 1: Numero ajeno a la reserva.
            var ajeno = await CrearEventoAsync(
                conexion, transaccion, numeroAjeno);

            bool autorizadoAjeno =
                await RegistroSolicitudesInicioSesionWhatsAppSql
                    .RegistrarAsync(
                        conexion,
                        transaccion,
                        ajeno.MensajeId,
                        numeroAjeno,
                        comando);

            Assert.False(autorizadoAjeno);

            // PRUEBA 2: Consentimiento revocado.
            await CambiarConsentimientoAsync(
                conexion,
                transaccion,
                reservaId,
                "Revocado");

            var revocado = await CrearEventoAsync(
                conexion, transaccion, numeroCorrecto);

            bool autorizadoRevocado =
                await RegistroSolicitudesInicioSesionWhatsAppSql
                    .RegistrarAsync(
                        conexion,
                        transaccion,
                        revocado.MensajeId,
                        numeroCorrecto,
                        comando);

            Assert.False(autorizadoRevocado);

            // Restaurar el consentimiento ficticio
            // para comprobar el escenario permitido.
            await CambiarConsentimientoAsync(
                conexion,
                transaccion,
                reservaId,
                "Confirmado");

            // PRUEBA 3: Destinatario autorizado.
            var valido = await CrearEventoAsync(
                conexion, transaccion, numeroCorrecto);

            bool registrado =
                await RegistroSolicitudesInicioSesionWhatsAppSql
                    .RegistrarAsync(
                        conexion,
                        transaccion,
                        valido.MensajeId,
                        numeroCorrecto,
                        comando);

            Assert.True(registrado);

            // PRUEBA 4: Repetir el mismo mensaje.
            bool duplicado =
                await RegistroSolicitudesInicioSesionWhatsAppSql
                    .RegistrarAsync(
                        conexion,
                        transaccion,
                        valido.MensajeId,
                        numeroCorrecto,
                        comando);

            Assert.False(duplicado);

            // PRUEBA 5: Otro evento dentro de 60 segundos.
            var segundo = await CrearEventoAsync(
                conexion, transaccion, numeroCorrecto);

            bool demasiadoPronto =
                await RegistroSolicitudesInicioSesionWhatsAppSql
                    .RegistrarAsync(
                        conexion,
                        transaccion,
                        segundo.MensajeId,
                        numeroCorrecto,
                        comando);

            Assert.False(demasiadoPronto);

            // Confirmar que existe una sola solicitud
            // y que conserva el identificador correcto.
            await using var comprobar = new SqlCommand("""
                SELECT EventoId, ReservaId, Estado, Intentos
                FROM dbo.SolicitudesInicioSesionWhatsApp
                WHERE ReservaId = @ReservaId;
                """, conexion, transaccion);

            comprobar.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaId;

            await using var lector =
                await comprobar.ExecuteReaderAsync();

            Assert.True(await lector.ReadAsync());

            Assert.Equal(valido.EventoId, lector.GetInt64(0));
            Assert.Equal(reservaId, lector.GetInt32(1));
            Assert.Equal("Pendiente", lector.GetString(2));
            Assert.Equal(0, lector.GetInt32(3));

            Assert.False(await lector.ReadAsync());
        }
        finally
        {
            // No conservar personas, reservas, consentimientos,
            // eventos ni solicitudes ficticias.
            await transaccion.RollbackAsync();
        }
    }

    private static async Task<(long EventoId, string MensajeId)>
        CrearEventoAsync(
            SqlConnection conexion,
            SqlTransaction transaccion,
            string numeroOrigen)
    {
        string mensajeId = "wamid." +
            Guid.NewGuid().ToString("N");

        byte[] clave = SHA256.HashData(
            Encoding.UTF8.GetBytes("M:" + mensajeId));

        try
        {
            await using var comando = new SqlCommand("""
                INSERT dbo.WhatsAppWebhookEventos
                    (ClaveEvento,
                     TipoEvento,
                     MensajeId,
                     WhatsappId)
                OUTPUT INSERTED.Id
                VALUES (
                    @Clave,
                    N'Mensaje',
                    @MensajeId,
                    @NumeroOrigen
                );
                """, conexion, transaccion);

            comando.Parameters.Add(
                "@Clave", SqlDbType.VarBinary, 32
            ).Value = clave;

            comando.Parameters.Add(
                "@MensajeId", SqlDbType.NVarChar, 255
            ).Value = mensajeId;

            comando.Parameters.Add(
                "@NumeroOrigen", SqlDbType.NVarChar, 32
            ).Value = numeroOrigen;

            long eventoId = Convert.ToInt64(
                await comando.ExecuteScalarAsync());

            return (eventoId, mensajeId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clave);
        }
    }

    private static async Task CambiarConsentimientoAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        int reservaId,
        string estado)
    {
        await using var comando = new SqlCommand("""
            UPDATE dbo.ConsentimientosPreReservaWhatsApp
            SET Estado = @Estado
            WHERE ReservaId = @ReservaId;
            """, conexion, transaccion);

        comando.Parameters.Add(
            "@ReservaId", SqlDbType.Int
        ).Value = reservaId;

        comando.Parameters.Add(
            "@Estado", SqlDbType.NVarChar, 20
        ).Value = estado;

        Assert.Equal(
            1,
            await comando.ExecuteNonQueryAsync());
    }
}
