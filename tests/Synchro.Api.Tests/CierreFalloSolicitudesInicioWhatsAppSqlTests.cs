
using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class CierreFalloSolicitudesInicioWhatsAppSqlTests
{
    [SqlDepositoFact]
    public async Task CierraFallosSinAceptarArrendamientosAjenos()
    {
        string cadena = ConexionSqlAislada.Exigir(
            Environment.GetEnvironmentVariable(
                "SYNCHRO_SQL_DEPOSITO_TEST"),
            "SynchroDB_Pruebas");

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SynchroDB"] = cadena
                })
            .Build();

        var servicio =
            new CierreFalloSolicitudesInicioWhatsAppSql(
                configuracion);

        string remitente = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string destinatario = "+999" +
            RandomNumberGenerator.GetInt32(
                1_000_000_000, 2_000_000_000);

        string usuario = "test_cierre_" +
            Guid.NewGuid().ToString("N");

        Guid[] arrendamientos =
        [
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        ];

        int remitenteId = 0;
        int reservaId = 0;
        long[] eventos = new long[4];

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        try
        {
            // Crear datos ficticios de cuatro solicitudes.
            await using (var preparar = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57160, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57161, 'Falta marca de pruebas.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    INSERT dbo.Personas
                        (TelefonoWhatsapp, TieneCuenta)
                    VALUES (@Remitente, 1);

                    DECLARE @PersonaId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ClientesWebPrototipo
                        (PersonaId, NombreUsuario,
                         Telefono, PasswordHash)
                    VALUES (
                        @PersonaId, @Usuario, @Remitente,
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
                        @PersonaId
                    );

                    DECLARE @ReservaId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        N'wamid.cierre.1.' + @Usuario,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @E1 BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        N'wamid.cierre.2.' + @Usuario,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @E2 BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        N'wamid.cierre.3.' + @Usuario,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @E3 BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        N'wamid.cierre.4.' + @Usuario,
                        SUBSTRING(@Destinatario, 2, 20)
                    );

                    DECLARE @E4 BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId, Estado,
                         Intentos, ExpiraUtc,
                         Arrendamiento,
                         ArrendamientoHastaUtc)
                    VALUES
                        (@E1, @ReservaId, N'Procesando', 1,
                         DATEADD(MINUTE, 5, SYSUTCDATETIME()),
                         @A1,
                         DATEADD(MINUTE, 3, SYSUTCDATETIME())),

                        (@E2, @ReservaId, N'Procesando', 1,
                         DATEADD(MINUTE, 5, SYSUTCDATETIME()),
                         @A2,
                         DATEADD(MINUTE, 3, SYSUTCDATETIME())),

                        (@E3, @ReservaId, N'Procesando', 3,
                         DATEADD(MINUTE, 5, SYSUTCDATETIME()),
                         @A3,
                         DATEADD(MINUTE, 3, SYSUTCDATETIME()));

                    -- Solicitud vencida, con fechas validas.
                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId, Estado,
                         Intentos, CreadoUtc, ExpiraUtc,
                         Arrendamiento,
                         ArrendamientoHastaUtc)
                    VALUES (
                        @E4, @ReservaId, N'Procesando', 1,
                        DATEADD(MINUTE, -10, SYSUTCDATETIME()),
                        DATEADD(MINUTE, -5, SYSUTCDATETIME()),
                        @A4,
                        DATEADD(MINUTE, 3, SYSUTCDATETIME())
                    );

                    COMMIT TRANSACTION;

                    SELECT
                        @PersonaId, @ReservaId,
                        @E1, @E2, @E3, @E4;
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

                for (int i = 0; i < 4; i++)
                {
                    preparar.Parameters.Add(
                        $"@A{i + 1}",
                        SqlDbType.UniqueIdentifier
                    ).Value = arrendamientos[i];
                }

                await using var lector =
                    await preparar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                remitenteId = lector.GetInt32(0);
                reservaId = lector.GetInt32(1);

                for (int i = 0; i < 4; i++)
                {
                    eventos[i] = lector.GetInt64(i + 2);
                }
            }

            SolicitudInicioWhatsAppTomada Solicitud(int i) =>
                new(
                    eventos[i],
                    reservaId,
                    destinatario[1..],
                    arrendamientos[i]);

            // 1. Un arrendamiento falso no puede cerrar.
            Assert.False(await servicio.CerrarAsync(
                Solicitud(0) with
                {
                    Arrendamiento = Guid.NewGuid()
                },
                ResultadoFalloSolicitudInicio.Rechazada));

            // 2. Rechazo definitivo.
            Assert.True(await servicio.CerrarAsync(
                Solicitud(0),
                ResultadoFalloSolicitudInicio.Rechazada));

            Assert.False(await servicio.CerrarAsync(
                Solicitud(0),
                ResultadoFalloSolicitudInicio.Rechazada));

            // 3. Error temporal: programar reintento.
            Assert.True(await servicio.CerrarAsync(
                Solicitud(1),
                ResultadoFalloSolicitudInicio.Reintento));

            // 4. No permitir mas reintentos al alcanzar tres.
            Assert.True(await servicio.CerrarAsync(
                Solicitud(2),
                ResultadoFalloSolicitudInicio.Reintento));

            // 5. Una solicitud vencida debe caducar.
            Assert.True(await servicio.CerrarAsync(
                Solicitud(3),
                ResultadoFalloSolicitudInicio.Reintento));

            // Comprobar los estados directamente en SQL.
            await using var comprobar = new SqlCommand("""
                SELECT
                    EventoId,
                    Estado,
                    Intentos,
                    CodigoError,
                    CASE
                        WHEN Arrendamiento IS NULL
                         AND ArrendamientoHastaUtc IS NULL
                        THEN CAST(1 AS BIT)
                        ELSE CAST(0 AS BIT)
                    END AS ArrendamientoLiberado,
                    CASE
                        WHEN DisponibleUtc > SYSUTCDATETIME()
                        THEN CAST(1 AS BIT)
                        ELSE CAST(0 AS BIT)
                    END AS ReintentoFuturo
                FROM dbo.SolicitudesInicioSesionWhatsApp
                WHERE ReservaId = @ReservaId
                  AND EventoId IN (@E1, @E2, @E3, @E4);
                """, conexion);

            comprobar.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaId;

            for (int i = 0; i < 4; i++)
            {
                comprobar.Parameters.Add(
                    $"@E{i + 1}", SqlDbType.BigInt
                ).Value = eventos[i];
            }

            var estados = new Dictionary<long,
                (string Estado, int Intentos,
                 string Error, bool Libre, bool Futuro)>();

            await using (var lector =
                await comprobar.ExecuteReaderAsync())
            {
                while (await lector.ReadAsync())
                {
                    estados.Add(
                        lector.GetInt64(0),
                        (
                            lector.GetString(1),
                            lector.GetInt32(2),
                            lector.GetString(3),
                            lector.GetBoolean(4),
                            lector.GetBoolean(5)
                        ));
                }
            }

            Assert.Equal(4, estados.Count);

            Assert.Equal("Rechazada",
                estados[eventos[0]].Estado);

            Assert.Equal("AutorizacionNoValida",
                estados[eventos[0]].Error);

            Assert.Equal("Reintento",
                estados[eventos[1]].Estado);

            Assert.Equal("ErrorReintentable",
                estados[eventos[1]].Error);

            Assert.True(estados[eventos[1]].Futuro);

            Assert.Equal("Indeterminado",
                estados[eventos[2]].Estado);

            Assert.Equal(3,
                estados[eventos[2]].Intentos);

            Assert.Equal("IntentosAgotados",
                estados[eventos[2]].Error);

            Assert.Equal("Caducada",
                estados[eventos[3]].Estado);

            Assert.Equal("SolicitudVencida",
                estados[eventos[3]].Error);

            Assert.All(estados.Values,
                estado => Assert.True(estado.Libre));
        }
        finally
        {
            // Eliminar solo los registros ficticios
            // creados por esta prueba.
            if (remitenteId > 0 &&
                reservaId > 0 &&
                eventos.All(id => id > 0))
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 57162, 'Base no autorizada.', 1;

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.extended_properties
                        WHERE class = 0
                          AND name = N'SynchroFixtureDeposito'
                    )
                        THROW 57163, 'Falta marca de pruebas.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DELETE dbo.SolicitudesInicioSesionWhatsApp
                        WHERE ReservaId = @ReservaId
                          AND EventoId IN (@E1, @E2, @E3, @E4);

                        DELETE dbo.WhatsAppWebhookEventos
                        WHERE Id IN (@E1, @E2, @E3, @E4)
                          AND MensajeId LIKE
                              N'wamid.cierre.%.' + @Usuario;

                        DELETE dbo.PreReservasEncomiendasWeb
                        WHERE Id = @ReservaId
                          AND ClientePersonaId = @RemitenteId;

                        DELETE dbo.ClientesWebPrototipo
                        WHERE PersonaId = @RemitenteId
                          AND NombreUsuario = @Usuario;

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
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                limpiar.Parameters.Add(
                    "@RemitenteId", SqlDbType.Int
                ).Value = remitenteId;

                limpiar.Parameters.Add(
                    "@Remitente", SqlDbType.NVarChar, 20
                ).Value = remitente;

                limpiar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                for (int i = 0; i < 4; i++)
                {
                    limpiar.Parameters.Add(
                        $"@E{i + 1}", SqlDbType.BigInt
                    ).Value = eventos[i];
                }

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}
