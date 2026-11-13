
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class ReclamadorSolicitudesInicioWhatsAppSqlTests
{
    [SqlDepositoFact]
    public async Task EvitaDuplicadosYRecuperaArrendamientos()
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

        var reclamador =
            new ReclamadorSolicitudesInicioWhatsAppSql(
                configuracion);

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        string usuario = "test_reclamador_" +
            Guid.NewGuid().ToString("N");

        int remitenteId = 0;
        int reservaId = 0;
        long eventoVigente = 0;
        long eventoVencido = 0;

        try
        {
            // Preparar dos solicitudes ficticias:
            // una vigente y otra ya caducada.
            await using (var preparar = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 57120, 'Base no autorizada.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 57121, 'Falta marca de pruebas.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    INSERT dbo.Personas
                        (TelefonoWhatsapp, TieneCuenta)
                    VALUES (N'+999000000001', 1);

                    DECLARE @RemitenteId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.ClientesWebPrototipo
                        (PersonaId, NombreUsuario,
                         Telefono, PasswordHash)
                    VALUES (
                        @RemitenteId,
                        @Usuario,
                        N'+999000000001',
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
                        N'+999000000001',
                        N'+999123456789',
                        N'Mediano',
                        DATEADD(HOUR, 1, SYSUTCDATETIME()),
                        @RemitenteId
                    );

                    DECLARE @ReservaId INT =
                        CONVERT(INT, SCOPE_IDENTITY());

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        N'wamid.vigente.' + @Usuario,
                        N'999123456789'
                    );

                    DECLARE @Vigente BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.WhatsAppWebhookEventos
                        (ClaveEvento, TipoEvento,
                         MensajeId, WhatsappId)
                    VALUES (
                        CRYPT_GEN_RANDOM(32),
                        N'Mensaje',
                        N'wamid.vencido.' + @Usuario,
                        N'999123456789'
                    );

                    DECLARE @Vencido BIGINT =
                        CONVERT(BIGINT, SCOPE_IDENTITY());

                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId, ExpiraUtc)
                    VALUES (
                        @Vigente,
                        @ReservaId,
                        DATEADD(MINUTE, 5, SYSUTCDATETIME())
                    );

                    INSERT dbo.SolicitudesInicioSesionWhatsApp
                        (EventoId, ReservaId,
                         CreadoUtc, ExpiraUtc)
                    VALUES (
                        @Vencido,
                        @ReservaId,
                        DATEADD(
                            MINUTE, -10, SYSUTCDATETIME()
                        ),
                        DATEADD(
                            MINUTE, -5, SYSUTCDATETIME()
                        )
                    );

                    COMMIT TRANSACTION;

                    SELECT
                        @RemitenteId,
                        @ReservaId,
                        @Vigente,
                        @Vencido;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;
                    THROW;
                END CATCH;
                """, conexion))
            {
                preparar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                await using var lector =
                    await preparar.ExecuteReaderAsync();

                Assert.True(await lector.ReadAsync());

                remitenteId = lector.GetInt32(0);
                reservaId = lector.GetInt32(1);
                eventoVigente = lector.GetInt64(2);
                eventoVencido = lector.GetInt64(3);
            }

            // PRUEBA 1:
            // Dos procesos intentan reclamar simultaneamente.
            var resultados = await Task.WhenAll(
                reclamador.TomarAsync(),
                reclamador.TomarAsync());

            var primera = Assert.Single(
                resultados.OfType<
                    SolicitudInicioWhatsAppTomada>());

            Assert.Equal(eventoVigente, primera.EventoId);
            Assert.Equal(reservaId, primera.ReservaId);
            Assert.Equal(
                "999123456789", primera.NumeroOrigen);

            Assert.NotEqual(
                Guid.Empty, primera.Arrendamiento);

            // PRUEBA 2:
            // Un arrendamiento vigente no puede
            // reclamarse nuevamente.
            Assert.Null(await reclamador.TomarAsync());

            // PRUEBA 3:
            // Simular que el servidor se interrumpio
            // y el primer arrendamiento ya vencio.
            await VencerArrendamientoAsync(
                conexion,
                eventoVigente,
                primera.Arrendamiento);

            var segunda = await reclamador.TomarAsync();

            Assert.NotNull(segunda);
            Assert.Equal(eventoVigente, segunda.EventoId);

            Assert.NotEqual(
                primera.Arrendamiento,
                segunda.Arrendamiento);

            // PRUEBA 4:
            // Recuperar una tercera y ultima vez.
            await VencerArrendamientoAsync(
                conexion,
                eventoVigente,
                segunda.Arrendamiento);

            var tercera = await reclamador.TomarAsync();

            Assert.NotNull(tercera);
            Assert.Equal(eventoVigente, tercera.EventoId);

            await VencerArrendamientoAsync(
                conexion,
                eventoVigente,
                tercera.Arrendamiento);

            // PRUEBA 5:
            // Agotados los tres intentos, no
            // debe volver a reclamarse.
            Assert.Null(await reclamador.TomarAsync());

            // Comprobar el estado definitivo en SQL.
            await using var comprobar = new SqlCommand("""
                SELECT
                    Estado,
                    Intentos,
                    Arrendamiento,
                    ArrendamientoHastaUtc
                FROM dbo.SolicitudesInicioSesionWhatsApp
                WHERE EventoId = @Vigente;

                SELECT Estado, Intentos
                FROM dbo.SolicitudesInicioSesionWhatsApp
                WHERE EventoId = @Vencido;
                """, conexion);

            comprobar.Parameters.Add(
                "@Vigente", SqlDbType.BigInt
            ).Value = eventoVigente;

            comprobar.Parameters.Add(
                "@Vencido", SqlDbType.BigInt
            ).Value = eventoVencido;

            await using var lectorEstado =
                await comprobar.ExecuteReaderAsync();

            Assert.True(await lectorEstado.ReadAsync());

            Assert.Equal(
                "Indeterminado",
                lectorEstado.GetString(0));

            Assert.Equal(3, lectorEstado.GetInt32(1));
            Assert.True(lectorEstado.IsDBNull(2));
            Assert.True(lectorEstado.IsDBNull(3));
            Assert.False(await lectorEstado.ReadAsync());

            Assert.True(
                await lectorEstado.NextResultAsync());

            Assert.True(await lectorEstado.ReadAsync());

            Assert.Equal(
                "Caducada",
                lectorEstado.GetString(0));

            Assert.Equal(0, lectorEstado.GetInt32(1));
            Assert.False(await lectorEstado.ReadAsync());
        }
        finally
        {
            // Eliminar exclusivamente los datos ficticios
            // identificados por esta prueba.
            if (remitenteId > 0 &&
                reservaId > 0 &&
                eventoVigente > 0 &&
                eventoVencido > 0)
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 57122, 'Base no autorizada.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DELETE dbo.SolicitudesInicioSesionWhatsApp
                        WHERE EventoId IN (
                            @Vigente, @Vencido
                        )
                          AND ReservaId = @ReservaId;

                        DELETE dbo.WhatsAppWebhookEventos
                        WHERE Id IN (@Vigente, @Vencido)
                          AND MensajeId IN (
                              N'wamid.vigente.' + @Usuario,
                              N'wamid.vencido.' + @Usuario
                          );

                        DELETE dbo.PreReservasEncomiendasWeb
                        WHERE Id = @ReservaId
                          AND ClientePersonaId = @RemitenteId;

                        DELETE dbo.ClientesWebPrototipo
                        WHERE PersonaId = @RemitenteId
                          AND NombreUsuario = @Usuario;

                        DELETE dbo.Personas
                        WHERE Id = @RemitenteId
                          AND TelefonoWhatsapp =
                              N'+999000000001';

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF XACT_STATE() <> 0
                            ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH;
                    """, conexion);

                limpiar.Parameters.Add(
                    "@Vigente", SqlDbType.BigInt
                ).Value = eventoVigente;

                limpiar.Parameters.Add(
                    "@Vencido", SqlDbType.BigInt
                ).Value = eventoVencido;

                limpiar.Parameters.Add(
                    "@ReservaId", SqlDbType.Int
                ).Value = reservaId;

                limpiar.Parameters.Add(
                    "@RemitenteId", SqlDbType.Int
                ).Value = remitenteId;

                limpiar.Parameters.Add(
                    "@Usuario", SqlDbType.NVarChar, 80
                ).Value = usuario;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task VencerArrendamientoAsync(
        SqlConnection conexion,
        long eventoId,
        Guid arrendamiento)
    {
        // Exclusivamente para simular un reinicio
        // sin esperar 60 segundos reales.
        await using var comando = new SqlCommand("""
            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 57123, 'Base no autorizada.', 1;

            UPDATE dbo.SolicitudesInicioSesionWhatsApp
            SET ArrendamientoHastaUtc =
                DATEADD(
                    SECOND, -2, SYSUTCDATETIME()
                )
            WHERE EventoId = @EventoId
              AND Estado = N'Procesando'
              AND Arrendamiento = @Arrendamiento;
            """, conexion);

        comando.Parameters.Add(
            "@EventoId", SqlDbType.BigInt
        ).Value = eventoId;

        comando.Parameters.Add(
            "@Arrendamiento", SqlDbType.UniqueIdentifier
        ).Value = arrendamiento;

        Assert.Equal(
            1,
            await comando.ExecuteNonQueryAsync());
    }
}
