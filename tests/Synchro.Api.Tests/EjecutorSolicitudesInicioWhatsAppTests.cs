
using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class EjecutorSolicitudesInicioWhatsAppTests
{
    [SqlDepositoFact]
    public async Task EjecutaUnaSolicitudSinDuplicarVerificacion()
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

        var reclamador =
            new ReclamadorSolicitudesInicioWhatsAppSql(
                configuracion);

        var procesador =
            new ProcesadorSolicitudesInicioWhatsAppSql(
                configuracion,
                new IdentidadDestinatarioWhatsAppSql(
                    configuracion),
                new EmisorVerificacionTelefonoSql(
                    configuracion, protector));

        var cierre =
            new CierreFalloSolicitudesInicioWhatsAppSql(
                configuracion);

        var ejecutor = new EjecutorSolicitudesInicioWhatsApp(
            reclamador,
            procesador,
            cierre,
            NullLogger<EjecutorSolicitudesInicioWhatsApp>
                .Instance);

        string remitente = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string destinatario = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string usuario = "test_ejecutor_" +
            Guid.NewGuid().ToString("N");

        string mensajeId = "wamid.ejecutor." +
            Guid.NewGuid().ToString("N");
        string mensajeRevocadoId = mensajeId + ".revocado";

        int remitenteId = 0;
        int reservaId = 0;
        long eventoId = 0;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        try
        {
            // Preparar una solicitud pendiente ficticia.
            await using (var preparar = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57170, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57171, 'Falta marca de pruebas.', 1;

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
                        N'Mensaje',
                        @MensajeId,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @EventoId BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId, ExpiraUtc)
                    VALUES (
                        @EventoId,
                        @ReservaId,
                        DATEADD(MINUTE, 5, SYSUTCDATETIME())
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

                await using var lector =
                    await preparar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                remitenteId = lector.GetInt32(0);
                reservaId = lector.GetInt32(1);
                eventoId = lector.GetInt64(2);
            }

            // Ejecutar el flujo completo.
            var primerResultado =
                await ejecutor.EjecutarUnaAsync();

            Assert.Equal(
                ResultadoEjecucionInicioWhatsApp.Preparada,
                primerResultado);

            // La solicitud ya preparada no debe
            // reclamarse nuevamente.
            var segundoResultado =
                await ejecutor.EjecutarUnaAsync();

            Assert.Equal(
                ResultadoEjecucionInicioWhatsApp.SinSolicitudes,
                segundoResultado);

            // Comprobar el estado final en SQL.
            await using var comprobar = new SqlCommand("""
                SELECT
                    s.Estado,
                    s.Intentos,
                    s.Arrendamiento,
                    s.VerificacionId,
                    v.Telefono,
                    v.Estado,
                    o.Id,
                    o.ContenidoProtegido,
                    o.Estado
                FROM dbo.SolicitudesInicioSesionWhatsApp s
                INNER JOIN dbo.VerificacionesTelefono v
                    ON v.Id = s.VerificacionId
                INNER JOIN dbo.OutboxVerificacionTelefono o
                    ON o.VerificacionId = v.Id
                WHERE s.EventoId = @EventoId
                  AND s.ReservaId = @ReservaId;
                """, conexion);

            comprobar.Parameters.Add(
                "@EventoId", SqlDbType.BigInt
            ).Value = eventoId;

            comprobar.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaId;

            await using var resultado =
                await comprobar.ExecuteReaderAsync();

            Assert.True(await resultado.ReadAsync());

            Assert.Equal("Preparada", resultado.GetString(0));
            Assert.Equal(1, resultado.GetInt32(1));
            Assert.True(resultado.IsDBNull(2));

            int verificacionId = resultado.GetInt32(3);

            Assert.True(verificacionId > 0);
            Assert.Equal(
                destinatario, resultado.GetString(4));

            Assert.Equal("Pendiente", resultado.GetString(5));

            Guid idMensaje = resultado.GetGuid(6);
            byte[] cifrado = (byte[])resultado[7];

            try
            {
                Assert.Equal(
                    "Pendiente", resultado.GetString(8));

                var mensaje = protector.Abrir(
                    idMensaje,
                    verificacionId,
                    cifrado);

                Assert.Equal(
                    destinatario, mensaje.Telefono);

                Assert.Equal(
                    "codigo_inicio_sesion",
                    mensaje.Plantilla);

                Assert.Single(mensaje.Variables);

                Assert.Equal(
                    6, mensaje.Variables[0].Length);

                Assert.All(
                    mensaje.Variables[0],
                    caracter => Assert.InRange(
                        caracter, '0', '9'));

                Assert.False(await resultado.ReadAsync());
            }
            finally
            {
                CryptographicOperations.ZeroMemory(cifrado);
            }

            // Cerrar el lector antes de ejecutar
            // nuevas operaciones en la misma conexion.
            await resultado.DisposeAsync();

            // SEGUNDO ESCENARIO:

            // SEGUNDO ESCENARIO:
            // Una nueva solicitud llega cuando el
            // destinatario ya revoco su consentimiento.
            long eventoRevocadoId;

            await using (var crear = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57180, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57181, 'Falta marca de pruebas.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    UPDATE dbo.ConsentimientosPreReservaWhatsApp
                    SET Estado = N'Revocado'
                    WHERE ReservaId = @ReservaId
                      AND TelefonoWhatsApp = @Destinatario
                      AND Estado = N'Confirmado';

                    IF @@ROWCOUNT <> 1
                        THROW 57182,
                            'No se revoco el consentimiento.',
                            1;

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        @MensajeRevocadoId,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @NuevoEventoId BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId, ExpiraUtc)
                    VALUES (
                        @NuevoEventoId,
                        @ReservaId,
                        DATEADD(MINUTE, 5, SYSUTCDATETIME())
                    );

                    COMMIT TRANSACTION;

                    SELECT @NuevoEventoId;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                """, conexion))
            {
                crear.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                crear.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                crear.Parameters.Add(
                    "@MensajeRevocadoId", SqlDbType.NVarChar, 255
                ).Value = mensajeRevocadoId;

                eventoRevocadoId = Convert.ToInt64(
                    await crear.ExecuteScalarAsync());
            }

            // El ejecutor no debe preparar otro codigo.
            Assert.Equal(
                ResultadoEjecucionInicioWhatsApp.Rechazada,
                await ejecutor.EjecutarUnaAsync());

            // Confirmar que se libero el arrendamiento,
            // sin crear una nueva verificacion.
            await using (var verificar = new SqlCommand("""
                SELECT
                    s.Estado,
                    s.Intentos,
                    s.Arrendamiento,
                    s.VerificacionId,
                    (
                        SELECT COUNT(*)
                        FROM dbo.VerificacionesTelefono
                        WHERE Telefono = @Destinatario
                          AND Finalidad =
                              N'InicioSesionDestinatario'
                    ) AS TotalVerificaciones,
                    (
                        SELECT COUNT(*)
                        FROM dbo.OutboxVerificacionTelefono o
                        INNER JOIN dbo.VerificacionesTelefono v
                            ON v.Id = o.VerificacionId
                        WHERE v.Telefono = @Destinatario
                          AND v.Finalidad =
                              N'InicioSesionDestinatario'
                    ) AS TotalMensajes
                FROM dbo.SolicitudesInicioSesionWhatsApp s
                WHERE s.EventoId = @EventoId
                  AND s.ReservaId = @ReservaId;
                """, conexion))
            {
                verificar.Parameters.Add(
                    "@EventoId", SqlDbType.BigInt
                ).Value = eventoRevocadoId;

                verificar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                verificar.Parameters.Add(
                    "@Destinatario", SqlDbType.NVarChar, 20
                ).Value = destinatario;

                await using var lector =
                    await verificar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                Assert.Equal(
                    "Rechazada", lector.GetString(0));

                Assert.Equal(1, lector.GetInt32(1));

                Assert.True(lector.IsDBNull(2));
                Assert.True(lector.IsDBNull(3));

                // Sigue existiendo solamente el codigo
                // y el mensaje del escenario anterior.
                Assert.Equal(1, lector.GetInt32(4));
                Assert.Equal(1, lector.GetInt32(5));

                Assert.False(await lector.ReadAsync());
            }

            // No debe reclamarse otra vez.
            Assert.Equal(
                ResultadoEjecucionInicioWhatsApp.SinSolicitudes,
                await ejecutor.EjecutarUnaAsync());

        }
        finally
        {
            // Limpiar solamente los registros ficticios.
            if (remitenteId > 0 &&
                reservaId > 0 &&
                eventoId > 0)
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 57172, 'Base no autorizada.', 1;

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.extended_properties
                        WHERE class = 0
                          AND name = N'SynchroFixtureDeposito'
                    )
                        THROW 57173, 'Falta marca de pruebas.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                                                DELETE s
                        FROM dbo.SolicitudesInicioSesionWhatsApp s
                        INNER JOIN dbo.WhatsAppWebhookEventos e
                            ON e.Id = s.EventoId
                        WHERE s.ReservaId = @ReservaId
                          AND e.MensajeId IN (
                              @MensajeId,
                              @MensajeRevocadoId
                          );

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
                        WHERE MensajeId IN (
                            @MensajeId,
                            @MensajeRevocadoId
                        )
                          AND WhatsappId =
                              SUBSTRING(@Destinatario, 2, 20);

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

                limpiar.Parameters.Add(
                    "@MensajeRevocadoId", SqlDbType.NVarChar, 255
                ).Value = mensajeRevocadoId;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}
