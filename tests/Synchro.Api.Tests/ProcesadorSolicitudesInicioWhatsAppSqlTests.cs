
using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class ProcesadorSolicitudesInicioWhatsAppSqlTests
{
    [SqlDepositoFact]
    public async Task PreparaUnaSolaVerificacionConArrendamientoValido()
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

        var procesador = new ProcesadorSolicitudesInicioWhatsAppSql(
            configuracion,
            new IdentidadDestinatarioWhatsAppSql(configuracion),
            new EmisorVerificacionTelefonoSql(
                configuracion, protector));

        string remitente = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string destinatario = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string usuario = "test_procesador_" +
            Guid.NewGuid().ToString("N");

        string mensajeId = "wamid.test." +
            Guid.NewGuid().ToString("N");

        Guid arrendamiento = Guid.NewGuid();

        int remitenteId = 0;
        int reservaId = 0;
        long eventoId = 0;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        try
        {
            // Crear una solicitud ficticia ya reclamada.
            await using (var preparar = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57140, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57141, 'Falta marca de pruebas.', 1;

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
                    VALUES (
                        @RemitenteId, @Usuario,
                        @Remitente,
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
                        @Remitente, @Destinatario,
                        N'Mediano',
                        DATEADD(HOUR, 1, SYSUTCDATETIME()),
                        @RemitenteId
                    );

                    DECLARE @ReservaId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ConsentimientosPreReservaWhatsApp
                        (ReservaId, TelefonoWhatsApp,
                         CodigoHash, Estado,
                         FechaCreacionUtc,
                         FechaExpiracionUtc,
                         FechaConfirmacionUtc,
                         MensajeIdConfirmacion)
                    VALUES (
                        @ReservaId, @Destinatario,
                        CRYPT_GEN_RANDOM(32),
                        N'Confirmado',
                        DATEADD(MINUTE, -2, SYSUTCDATETIME()),
                        DATEADD(MINUTE, 30, SYSUTCDATETIME()),
                        DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                        N'CONS-' + @Usuario
                    );

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje', @MensajeId,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @EventoId BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId,
                         Estado, Intentos, ExpiraUtc,
                         Arrendamiento, ArrendamientoHastaUtc)
                    VALUES (
                        @EventoId, @ReservaId,
                        N'Procesando', 1,
                        DATEADD(MINUTE, 5, SYSUTCDATETIME()),
                        @Arrendamiento,
                        DATEADD(MINUTE, 3, SYSUTCDATETIME())
                    );

                    COMMIT TRANSACTION;

                    SELECT @RemitenteId, @ReservaId, @EventoId;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                """, conexion))
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

                preparar.Parameters.Add(
                    "@MensajeId", SqlDbType.NVarChar, 255
                ).Value = mensajeId;

                preparar.Parameters.Add(
                    "@Arrendamiento", SqlDbType.UniqueIdentifier
                ).Value = arrendamiento;

                await using var lector =
                    await preparar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                remitenteId = lector.GetInt32(0);
                reservaId = lector.GetInt32(1);
                eventoId = lector.GetInt64(2);
            }

            var solicitud = new SolicitudInicioWhatsAppTomada(
                eventoId,
                reservaId,
                destinatario[1..],
                arrendamiento);

            // PRUEBA 1: un arrendamiento ajeno no sirve.
            var solicitudAjena = solicitud with
            {
                Arrendamiento = Guid.NewGuid()
            };

            Assert.False(
                await procesador.PrepararAsync(solicitudAjena));

            // PRUEBA ADICIONAL:
            // El destinatario revoca su consentimiento
            // despues de que la solicitud fue reclamada.
            await using (var revocar = new SqlCommand("""
                UPDATE dbo.ConsentimientosPreReservaWhatsApp
                SET Estado = N'Revocado'
                WHERE ReservaId = @ReservaId
                  AND TelefonoWhatsApp = @Destinatario
                  AND Estado = N'Confirmado';
                """, conexion))
            {
                revocar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                revocar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                Assert.Equal(
                    1,
                    await revocar.ExecuteNonQueryAsync());
            }

            // La preparacion debe ser rechazada.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => procesador.PrepararAsync(solicitud));

            // El intento rechazado no debe crear
            // personas, codigos ni mensajes cifrados.
            await using (var verificar = new SqlCommand("""
                SELECT
                    (
                        SELECT COUNT(*)
                        FROM dbo.Personas
                        WHERE TelefonoWhatsapp = @Destinatario
                    ) AS Personas,
                    (
                        SELECT COUNT(*)
                        FROM dbo.VerificacionesTelefono
                        WHERE Telefono = @Destinatario
                          AND Finalidad =
                              N'InicioSesionDestinatario'
                    ) AS Verificaciones,
                    (
                        SELECT COUNT(*)
                        FROM dbo.SolicitudesInicioSesionWhatsApp
                        WHERE EventoId = @EventoId
                          AND Estado = N'Procesando'
                          AND VerificacionId IS NULL
                    ) AS SolicitudIntacta;
                """, conexion))
            {
                verificar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                verificar.Parameters.Add(
                    "@EventoId", SqlDbType.BigInt
                ).Value = eventoId;

                await using var resultado =
                    await verificar.ExecuteReaderAsync();

                Assert.True(await resultado.ReadAsync());

                Assert.Equal(0, resultado.GetInt32(0));
                Assert.Equal(0, resultado.GetInt32(1));
                Assert.Equal(1, resultado.GetInt32(2));
            }

            // Restaurar el consentimiento ficticio
            // para continuar con la prueba original.
            await using (var restaurar = new SqlCommand("""
                UPDATE dbo.ConsentimientosPreReservaWhatsApp
                SET Estado = N'Confirmado'
                WHERE ReservaId = @ReservaId
                  AND TelefonoWhatsApp = @Destinatario
                  AND Estado = N'Revocado';
                """, conexion))
            {
                restaurar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                restaurar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                Assert.Equal(
                    1,
                    await restaurar.ExecuteNonQueryAsync());
            }
            // PRUEBA 2: preparar correctamente.
            Assert.True(
                await procesador.PrepararAsync(solicitud));

            // PRUEBA 3: no preparar dos veces.
            Assert.False(
                await procesador.PrepararAsync(solicitud));

            // Verificar la transaccion ya confirmada.
            await using var comprobar = new SqlCommand("""
                SELECT
                    s.Estado,
                    s.Intentos,
                    s.Arrendamiento,
                    s.VerificacionId,
                    v.PersonaId,
                    v.Telefono,
                    v.Estado,
                    o.Id,
                    o.ContenidoProtegido,
                    o.Estado,
                    p.TieneCuenta
                FROM dbo.SolicitudesInicioSesionWhatsApp s
                INNER JOIN dbo.VerificacionesTelefono v
                    ON v.Id = s.VerificacionId
                INNER JOIN dbo.OutboxVerificacionTelefono o
                    ON o.VerificacionId = v.Id
                INNER JOIN dbo.Personas p
                    ON p.Id = v.PersonaId
                WHERE s.EventoId = @EventoId
                  AND s.ReservaId = @ReservaId;
                """, conexion);

            comprobar.Parameters.Add(
                "@EventoId", SqlDbType.BigInt
            ).Value = eventoId;

            comprobar.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaId;

            await using (var resultado =
                await comprobar.ExecuteReaderAsync())
            {
                Assert.True(await resultado.ReadAsync());

                Assert.Equal("Preparada", resultado.GetString(0));
                Assert.Equal(1, resultado.GetInt32(1));
                Assert.True(resultado.IsDBNull(2));

                int verificacionId = resultado.GetInt32(3);

                Assert.True(verificacionId > 0);
                Assert.True(resultado.GetInt32(4) > 0);

                Assert.Equal(
                    destinatario, resultado.GetString(5));

                Assert.Equal(
                    "Pendiente", resultado.GetString(6));

                Guid idMensajeCifrado = resultado.GetGuid(7);
                byte[] cifrado = (byte[])resultado[8];

                try
                {
                    Assert.Equal(
                        "Pendiente", resultado.GetString(9));

                    // Un destinatario no necesita cuenta web.
                    Assert.False(resultado.GetBoolean(10));

                    var mensaje = protector.Abrir(
                        idMensajeCifrado,
                        verificacionId,
                        cifrado);

                    Assert.Equal(
                        destinatario, mensaje.Telefono);

                    Assert.Equal(
                        "codigo_inicio_sesion",
                        mensaje.Plantilla);

                    Assert.Single(mensaje.Variables);
                    Assert.Equal(6, mensaje.Variables[0].Length);

                    Assert.All(
                        mensaje.Variables[0],
                        caracter => Assert.InRange(
                            caracter, '0', '9'));

                    // No existe otra verificacion vinculada.
                    Assert.False(await resultado.ReadAsync());
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(cifrado);
                }
            }
        }
        finally
        {
            // Limpiar solo los datos identificados
            // por esta prueba en la base aislada.
            if (remitenteId > 0 &&
                reservaId > 0 &&
                eventoId > 0)
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 57142, 'Base no autorizada.', 1;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.extended_properties
                        WHERE class = 0
                          AND name = N'SynchroFixtureDeposito'
                    )
                        THROW 57143, 'Falta marca de pruebas.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DELETE dbo.SolicitudesInicioSesionWhatsApp
                        WHERE EventoId = @EventoId
                          AND ReservaId = @ReservaId;

                        DELETE o
                        FROM dbo.OutboxVerificacionTelefono o
                        INNER JOIN dbo.VerificacionesTelefono v
                            ON v.Id = o.VerificacionId
                        WHERE v.Telefono = @Destinatario
                          AND v.Finalidad =
                              N'InicioSesionDestinatario';

                        DELETE dbo.VerificacionesTelefono
                        WHERE Telefono = @Destinatario
                          AND Finalidad =
                              N'InicioSesionDestinatario';

                        DELETE dbo.WhatsAppWebhookEventos
                        WHERE Id = @EventoId
                          AND MensajeId = @MensajeId;

                        DELETE dbo.ConsentimientosPreReservaWhatsApp
                        WHERE ReservaId = @ReservaId
                          AND TelefonoWhatsApp = @Destinatario;

                        DELETE dbo.PreReservasEncomiendasWeb
                        WHERE Id = @ReservaId
                          AND ClientePersonaId = @RemitenteId;

                        DELETE dbo.ClientesWebPrototipo
                        WHERE PersonaId = @RemitenteId
                          AND NombreUsuario = @Usuario;

                        DELETE dbo.Personas
                        WHERE TelefonoWhatsapp = @Destinatario
                          AND TieneCuenta = 0;

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
                    "@EventoId", SqlDbType.BigInt
                ).Value = eventoId;

                limpiar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                limpiar.Parameters.Add(
                    "@RemitenteId", SqlDbType.Int
                ).Value = remitenteId;

                limpiar.Parameters.Add(
                    "@Remitente", SqlDbType.NVarChar, 20
                ).Value = remitente;

                limpiar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                limpiar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                limpiar.Parameters.Add(
                    "@MensajeId", SqlDbType.NVarChar, 255
                ).Value = mensajeId;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}
