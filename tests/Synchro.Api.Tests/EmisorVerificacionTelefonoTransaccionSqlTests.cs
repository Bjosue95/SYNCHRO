
using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class EmisorVerificacionTelefonoTransaccionSqlTests
{
    [SqlDepositoFact]
    public async Task ReversionEliminaCodigoYMensajeCifrado()
    {
        string cadena = ConexionSqlAislada.Exigir(
            Environment.GetEnvironmentVariable(
                "SYNCHRO_SQL_DEPOSITO_TEST"),
            "SynchroDB_Pruebas");

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SynchroDB"] = cadena,
                    ["VerificacionTelefono:ClaveHmac"] =
                        Convert.ToBase64String(
                            RandomNumberGenerator.GetBytes(32)),
                    ["WhatsApp:PlantillaCodigoInicioSesion"] =
                        "codigo_inicio_sesion"
                })
            .Build();

        var protector = new ProtectorVerificacionTelefono(
            new EphemeralDataProtectionProvider());

        var emisor = new EmisorVerificacionTelefonoSql(
            configuracion,
            protector);

        string remitente = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string destinatario = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string usuario = "test_transaccion_" +
            Guid.NewGuid().ToString("N");

        int remitenteId = 0;
        int destinatarioId = 0;
        int reservaId = 0;
        int verificacionId = 0;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        await using (var transaccion = (SqlTransaction)
            await conexion.BeginTransactionAsync(
                IsolationLevel.Serializable))
        {
            try
            {
                // Todos los datos ficticios se crean dentro
                // de la misma transaccion que se revertira.
                await using (var preparar = new SqlCommand("""
                    SET NOCOUNT ON;
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 57100, 'Base no autorizada.', 1;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.extended_properties
                        WHERE class = 0
                          AND name = N'SynchroFixtureDeposito'
                    )
                        THROW 57101, 'Falta marca de pruebas.', 1;

                    INSERT dbo.Personas
                        (TelefonoWhatsapp, TieneCuenta)
                    VALUES (@Remitente, 1);

                    DECLARE @RemitenteId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ClientesWebPrototipo
                        (PersonaId, NombreUsuario,
                         Telefono, PasswordHash)
                    VALUES (
                        @RemitenteId,
                        @Usuario,
                        @Remitente,
                        N'HASH_FICTICIO_NO_AUTENTICABLE'
                    );

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
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        @Remitente,
                        @Destinatario,
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
                        @Destinatario,
                        CRYPT_GEN_RANDOM(32),
                        N'Confirmado',
                        DATEADD(MINUTE, -2, SYSUTCDATETIME()),
                        DATEADD(MINUTE, 30, SYSUTCDATETIME()),
                        DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                        N'CONS-' + @Usuario
                    );

                    SELECT
                        @RemitenteId,
                        @DestinatarioId,
                        @ReservaId;
                    """, conexion, transaccion))
                {
                    preparar.Parameters.Add(
                        "@Remitente", SqlDbType.NVarChar, 20
                    ).Value = remitente;

                    preparar.Parameters.Add(
                        "@Destinatario", SqlDbType.NVarChar, 20
                    ).Value = destinatario;

                    preparar.Parameters.Add(
                        "@Usuario", SqlDbType.NVarChar, 80
                    ).Value = usuario;

                    await using var lector =
                        await preparar.ExecuteReaderAsync();

                    Assert.True(await lector.ReadAsync());

                    remitenteId = lector.GetInt32(0);
                    destinatarioId = lector.GetInt32(1);
                    reservaId = lector.GetInt32(2);
                }

                // Ejecutar la nueva modalidad del servicio.
                // El metodo NO debe confirmar la transaccion.
                verificacionId =
                    await emisor.PrepararEnTransaccionAsync(
                        conexion,
                        transaccion,
                        destinatarioId,
                        reservaId);

                Assert.True(verificacionId > 0);

                // Antes del ROLLBACK, la verificacion
                // y el mensaje cifrado deben existir.
                await using var comprobar = new SqlCommand("""
                    SELECT COUNT_BIG(*)
                    FROM dbo.VerificacionesTelefono v
                    INNER JOIN dbo.OutboxVerificacionTelefono o
                        ON o.VerificacionId = v.Id
                    WHERE v.Id = @VerificacionId
                      AND v.PersonaId = @DestinatarioId
                      AND v.Estado = N'Pendiente'
                      AND o.Estado = N'Pendiente'
                      AND DATALENGTH(
                          o.ContenidoProtegido
                      ) > 0;
                    """, conexion, transaccion);

                comprobar.Parameters.Add(
                    "@VerificacionId", SqlDbType.Int
                ).Value = verificacionId;

                comprobar.Parameters.Add(
                    "@DestinatarioId", SqlDbType.Int
                ).Value = destinatarioId;

                long encontrados = Convert.ToInt64(
                    await comprobar.ExecuteScalarAsync());

                Assert.Equal(1L, encontrados);
            }
            finally
            {
                // Simular un fallo de la operacion general.
                // No confirmar ningun registro ficticio.
                if (transaccion.Connection is not null)
                {
                    await transaccion.RollbackAsync();
                }
            }
        }

        Assert.True(verificacionId > 0);

        // Comprobar que los registros ya no existen
        // despues de revertir toda la transaccion.
        await using var verificarReversion = new SqlCommand("""
            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 57102, 'Base no autorizada.', 1;

            SELECT
                (
                    SELECT COUNT_BIG(*)
                    FROM dbo.VerificacionesTelefono
                    WHERE Id = @VerificacionId
                ) AS Verificaciones,
                (
                    SELECT COUNT_BIG(*)
                    FROM dbo.OutboxVerificacionTelefono
                    WHERE VerificacionId = @VerificacionId
                ) AS Mensajes,
                (
                    SELECT COUNT_BIG(*)
                    FROM dbo.Personas
                    WHERE Id IN (
                        @RemitenteId,
                        @DestinatarioId
                    )
                ) AS Personas,
                (
                    SELECT COUNT_BIG(*)
                    FROM dbo.PreReservasEncomiendasWeb
                    WHERE Id = @ReservaId
                ) AS Reservas,
                (
                    SELECT COUNT_BIG(*)
                    FROM dbo.ConsentimientosPreReservaWhatsApp
                    WHERE ReservaId = @ReservaId
                ) AS Consentimientos;
            """, conexion);

        verificarReversion.Parameters.Add(
            "@VerificacionId", SqlDbType.Int
        ).Value = verificacionId;

        verificarReversion.Parameters.Add(
            "@RemitenteId", SqlDbType.Int
        ).Value = remitenteId;

        verificarReversion.Parameters.Add(
            "@DestinatarioId", SqlDbType.Int
        ).Value = destinatarioId;

        verificarReversion.Parameters.Add(
            "@ReservaId", SqlDbType.Int
        ).Value = reservaId;

        await using var resultado =
            await verificarReversion.ExecuteReaderAsync();

        Assert.True(await resultado.ReadAsync());

        for (int columna = 0; columna < 5; columna++)
        {
            Assert.Equal(0L, resultado.GetInt64(columna));
        }

        Assert.False(await resultado.ReadAsync());
    }
}
