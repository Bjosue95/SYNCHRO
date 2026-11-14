
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class EmisorVerificacionTelefonoSqlTests
{
    [SqlDepositoFact]
    [Trait("Category", "SqlAislado")]
    public async Task CreaVerificacionYMensajeCifradoSinEnviarWhatsApp()
    {
        string cadena = ConexionSqlAislada.Exigir(
            Environment.GetEnvironmentVariable(
                "SYNCHRO_SQL_DEPOSITO_TEST"),
            "SynchroDB_Pruebas");

        string clave = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SynchroDB"] = cadena,
                    ["VerificacionTelefono:ClaveHmac"] = clave,
                    ["WhatsApp:PlantillaCodigoInicioSesion"] =
                        "codigo_inicio_sesion"
                })
            .Build();

        var protector = new ProtectorVerificacionTelefono(
            new EphemeralDataProtectionProvider());

        var emisor = new EmisorVerificacionTelefonoSql(
            configuracion,
            protector);

        string usuario = "synchro_otp_" +
            Guid.NewGuid().ToString("N");

        const string telefonoRemitente = "+50499990001";
        const string telefonoDestinatario = "+50499990002";

        int remitenteId = 0;
        int destinatarioId = 0;
        int reservaId = 0;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        try
        {
            // Crear exclusivamente datos ficticios.
            await using (var preparar = new SqlCommand("""
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 56992, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 56993, 'Falta marca de pruebas.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    INSERT dbo.Personas
                        (TelefonoWhatsapp, TieneCuenta)
                    VALUES (@Remitente, 1);

                    DECLARE @RemitenteId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ClientesWebPrototipo
                        (PersonaId, NombreUsuario,
                         Telefono, PasswordHash)
                    VALUES
                        (@RemitenteId, @Usuario,
                         @Remitente, N'HASH_FICTICIO_NO_AUTENTICABLE');

                    INSERT dbo.Personas
                        (TelefonoWhatsapp, TieneCuenta)
                    VALUES (@Destinatario, 0);

                    DECLARE @DestinatarioId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.PreReservasEncomiendasWeb
                        (TokenGestionHash,
                         TelefonoRemitente,
                         TelefonoDestinatario,
                         TamanoSolicitado,
                         FechaExpiracion,
                         ClientePersonaId)
                    VALUES
                        (CRYPT_GEN_RANDOM(32),
                         @Remitente,
                         @Destinatario,
                         N'Mediano',
                         DATEADD(HOUR, 1, SYSUTCDATETIME()),
                         @RemitenteId);

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
                    VALUES
                        (@ReservaId,
                         @Destinatario,
                         CRYPT_GEN_RANDOM(32),
                         N'Confirmado',
                         DATEADD(MINUTE, -2, SYSUTCDATETIME()),
                         DATEADD(MINUTE, 30, SYSUTCDATETIME()),
                         DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                         N'MSG-' + @Usuario);

                    COMMIT TRANSACTION;

                    SELECT
                        @RemitenteId,
                        @DestinatarioId,
                        @ReservaId;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                """, conexion))
            {
                preparar.Parameters.Add(
                    "@Remitente", SqlDbType.NVarChar, 20)
                    .Value = telefonoRemitente;

                preparar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20)
                    .Value = telefonoDestinatario;

                preparar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80)
                    .Value = usuario;

                await using var lector =
                    await preparar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                remitenteId = lector.GetInt32(0);
                destinatarioId = lector.GetInt32(1);
                reservaId = lector.GetInt32(2);
            }

            // Ejecutar el servicio C# real.
            int verificacionId = await emisor.PrepararAsync(
                destinatarioId,
                reservaId);

            Assert.True(verificacionId > 0);

            // Comprobar lo que realmente se guardo en SQL.
            await using var comprobar = new SqlCommand("""
                SELECT
                    v.CodigoHash,
                    v.Telefono,
                    v.Finalidad,
                    v.Estado,
                    o.Id,
                    o.ContenidoProtegido,
                    o.TelefonoHash,
                    o.Estado,
                    o.Intentos
                FROM dbo.VerificacionesTelefono AS v
                INNER JOIN dbo.OutboxVerificacionTelefono AS o
                    ON o.VerificacionId = v.Id
                WHERE v.Id = @Id
                  AND v.PersonaId = @Persona;
                """, conexion);

            comprobar.Parameters.Add(
                "@Id", SqlDbType.Int)
                .Value = verificacionId;

            comprobar.Parameters.Add(
                "@Persona", SqlDbType.Int)
                .Value = destinatarioId;

            await using var resultado =
                await comprobar.ExecuteReaderAsync();

            Assert.True(await resultado.ReadAsync());

            byte[] hashGuardado = (byte[])resultado[0];
            string telefonoGuardado = resultado.GetString(1);

            Assert.Equal(
                telefonoDestinatario,
                telefonoGuardado);

            Assert.Equal(
                "InicioSesionDestinatario",
                resultado.GetString(2));

            Assert.Equal("Pendiente", resultado.GetString(3));

            Guid mensajeId = resultado.GetGuid(4);
            byte[] cifrado = (byte[])resultado[5];
            byte[] telefonoHashGuardado = (byte[])resultado[6];

            Assert.Equal("Pendiente", resultado.GetString(7));
            Assert.Equal(0, resultado.GetInt32(8));

            try
            {
                var mensaje = protector.Abrir(
                    mensajeId,
                    verificacionId,
                    cifrado);

                Assert.Equal(
                    telefonoDestinatario,
                    mensaje.Telefono);

                Assert.Equal(
                    "codigo_inicio_sesion",
                    mensaje.Plantilla);

                Assert.Equal(2, mensaje.Variables.Length);

                string codigo = mensaje.Variables[0];

                Assert.Equal(6, codigo.Length);
                Assert.All(codigo, caracter =>
                    Assert.InRange(caracter, '0', '9'));

                byte[] hashCalculado =
                    CodigoVerificacionTelefono.CalcularHash(
                        codigo,
                        destinatarioId,
                        telefonoGuardado,
                        configuracion);

                try
                {
                    Assert.True(
                        CryptographicOperations.FixedTimeEquals(
                            hashGuardado,
                            hashCalculado));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(
                        hashCalculado);
                }

                byte[] bytesTelefono =
                    Encoding.Unicode.GetBytes(
                        telefonoDestinatario);

                try
                {
                    byte[] hashTelefono =
                        SHA256.HashData(bytesTelefono);

                    try
                    {
                        Assert.Equal(
                            telefonoHashGuardado,
                            hashTelefono);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(
                            hashTelefono);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(
                        bytesTelefono);
                }

                Assert.False(await resultado.ReadAsync());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(cifrado);
                CryptographicOperations.ZeroMemory(hashGuardado);
                CryptographicOperations.ZeroMemory(
                    telefonoHashGuardado);
            }
        }
        finally
        {
            // Eliminar solamente los registros de esta prueba.
            if (remitenteId > 0 &&
                destinatarioId > 0 &&
                reservaId > 0)
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 56994, 'Base no autorizada.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DELETE o
                        FROM dbo.OutboxVerificacionTelefono o
                        INNER JOIN dbo.VerificacionesTelefono v
                            ON v.Id = o.VerificacionId
                        WHERE v.PersonaId = @DestinatarioId;

                        DELETE dbo.VerificacionesTelefono
                        WHERE PersonaId = @DestinatarioId;

                        DELETE dbo.ConsentimientosPreReservaWhatsApp
                        WHERE ReservaId = @ReservaId;

                        DELETE dbo.PreReservasEncomiendasWeb
                        WHERE Id = @ReservaId
                          AND ClientePersonaId = @RemitenteId;

                        DELETE dbo.ClientesWebPrototipo
                        WHERE PersonaId = @RemitenteId
                          AND NombreUsuario = @Usuario;

                        DELETE dbo.Personas
                        WHERE Id = @DestinatarioId
                          AND TelefonoWhatsapp = @Destinatario;

                        DELETE dbo.Personas
                        WHERE Id = @RemitenteId
                          AND TelefonoWhatsapp = @Remitente;

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF XACT_STATE() <> 0
                            ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH;
                    """, conexion);

                limpiar.Parameters.Add(
                    "@DestinatarioId", SqlDbType.Int)
                    .Value = destinatarioId;

                limpiar.Parameters.Add(
                    "@RemitenteId", SqlDbType.Int)
                    .Value = remitenteId;

                limpiar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int)
                    .Value = reservaId;

                limpiar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80)
                    .Value = usuario;

                limpiar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20)
                    .Value = telefonoDestinatario;

                limpiar.Parameters.Add(
                    "@Remitente", SqlDbType.NVarChar, 20)
                    .Value = telefonoRemitente;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}
