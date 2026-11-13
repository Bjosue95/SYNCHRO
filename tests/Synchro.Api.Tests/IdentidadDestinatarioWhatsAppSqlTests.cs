
using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class IdentidadDestinatarioWhatsAppSqlTests
{
    private static string Cadena =>
        ConexionSqlAislada.Exigir(
            Environment.GetEnvironmentVariable(
                "SYNCHRO_SQL_DEPOSITO_TEST"),
            "SynchroDB_Pruebas");

    private static IdentidadDestinatarioWhatsAppSql Servicio()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SynchroDB"] = Cadena
                })
            .Build();

        return new IdentidadDestinatarioWhatsAppSql(
            configuracion);
    }

    [SqlDepositoFact]
    public async Task CreaDestinatarioSinCuentaWeb()
    {
        await using var datos = await Escenario.CrearAsync();

        int personaId = await Servicio().ResolverAsync(
            datos.ReservaId,
            datos.NumeroOrigen);

        datos.PersonaCreadaId = personaId;

        Assert.True(personaId > 0);

        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();

        await using var comando = new SqlCommand("""
            SELECT COUNT(*)
            FROM dbo.Personas p
            WHERE p.Id = @Id
              AND p.TelefonoWhatsapp = @Telefono
              AND p.TieneCuenta = 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.ClientesWebPrototipo c
                  WHERE c.PersonaId = p.Id
              );
            """, conexion);

        comando.Parameters.Add(
            "@Id", SqlDbType.Int).Value = personaId;

        comando.Parameters.Add(
            "@Telefono", SqlDbType.NVarChar, 20
        ).Value = datos.Telefono;

        Assert.Equal(
            1,
            Convert.ToInt32(
                await comando.ExecuteScalarAsync()));
    }

    [SqlDepositoFact]
    public async Task ReutilizaIdentidadExistente()
    {
        await using var datos =
            await Escenario.CrearAsync(personasExistentes: 1);

        int personaId = await Servicio().ResolverAsync(
            datos.ReservaId,
            datos.NumeroOrigen);

        Assert.Equal(datos.PersonaExistenteId, personaId);
    }

    [SqlDepositoFact]
    public async Task RechazaTelefonoDeOtraPersona()
    {
        await using var datos = await Escenario.CrearAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().ResolverAsync(
                datos.ReservaId,
                "999888777666"));
    }

    [SqlDepositoFact]
    public async Task RechazaConsentimientoRevocado()
    {
        await using var datos = await Escenario.CrearAsync(
            estadoConsentimiento: "Revocado");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().ResolverAsync(
                datos.ReservaId,
                datos.NumeroOrigen));
    }

    [SqlDepositoFact]
    public async Task RechazaIdentidadesDuplicadas()
    {
        await using var datos =
            await Escenario.CrearAsync(personasExistentes: 2);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Servicio().ResolverAsync(
                datos.ReservaId,
                datos.NumeroOrigen));
    }

    private sealed class Escenario : IAsyncDisposable
    {
        public int ReservaId { get; private init; }
        public int RemitenteId { get; private init; }
        public int PersonaExistenteId { get; private init; }
        public int SegundaPersonaId { get; private init; }
        public int PersonaCreadaId { get; set; }

        public required string Telefono { get; init; }
        public required string Usuario { get; init; }

        public string NumeroOrigen => Telefono[1..];

        public static async Task<Escenario> CrearAsync(
            int personasExistentes = 0,
            string estadoConsentimiento = "Confirmado")
        {
            if (personasExistentes is < 0 or > 2)
                throw new ArgumentOutOfRangeException(
                    nameof(personasExistentes));

            if (estadoConsentimiento is not (
                "Confirmado" or "Revocado"))
                throw new ArgumentException(
                    "Estado de prueba invalido.");

            string telefono = "+999" +
                RandomNumberGenerator
                    .GetInt32(1_000_000_000, 2_000_000_000)
                    .ToString();

            string usuario = "test_identidad_" +
                Guid.NewGuid().ToString("N");

            await using var conexion = new SqlConnection(Cadena);
            await conexion.OpenAsync();

            await using var comando = new SqlCommand("""
                SET NOCOUNT ON;
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 56997, 'Base incorrecta.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 56998, 'Falta marca de pruebas.', 1;

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
                        @Telefono,
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
                        @ReservaId,
                        @Telefono,
                        CRYPT_GEN_RANDOM(32),
                        @Estado,
                        DATEADD(MINUTE, -2, SYSUTCDATETIME()),
                        DATEADD(MINUTE, 30, SYSUTCDATETIME()),
                        DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                        N'MSG-' + @Usuario
                    );

                    DECLARE @Persona1 INT = 0;
                    DECLARE @Persona2 INT = 0;

                    IF @Cantidad >= 1
                    BEGIN
                        INSERT dbo.Personas
                            (TelefonoWhatsapp, TieneCuenta)
                        VALUES (@Telefono, 0);

                        SET @Persona1 =
                            CONVERT(INT, SCOPE_IDENTITY());
                    END;

                    IF @Cantidad >= 2
                    BEGIN
                        INSERT dbo.Personas
                            (TelefonoWhatsapp, TieneCuenta)
                        VALUES (@Telefono, 0);

                        SET @Persona2 =
                            CONVERT(INT, SCOPE_IDENTITY());
                    END;

                    COMMIT TRANSACTION;

                    SELECT
                        @ReservaId,
                        @RemitenteId,
                        @Persona1,
                        @Persona2;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;

                    THROW;
                END CATCH;
                """, conexion);

            comando.Parameters.Add(
                "@Telefono", SqlDbType.NVarChar, 20
            ).Value = telefono;

            comando.Parameters.Add(
                "@Usuario", SqlDbType.NVarChar, 80
            ).Value = usuario;

            comando.Parameters.Add(
                "@Cantidad", SqlDbType.Int
            ).Value = personasExistentes;

            comando.Parameters.Add(
                "@Estado", SqlDbType.NVarChar, 20
            ).Value = estadoConsentimiento;

            await using var lector =
                await comando.ExecuteReaderAsync();

            Assert.True(await lector.ReadAsync());

            return new Escenario
            {
                ReservaId = lector.GetInt32(0),
                RemitenteId = lector.GetInt32(1),
                PersonaExistenteId = lector.GetInt32(2),
                SegundaPersonaId = lector.GetInt32(3),
                Telefono = telefono,
                Usuario = usuario
            };
        }

        public async ValueTask DisposeAsync()
        {
            await using var conexion = new SqlConnection(Cadena);
            await conexion.OpenAsync();

            await using var comando = new SqlCommand("""
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 56999, 'Base incorrecta.', 1;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.extended_properties
                    WHERE class = 0
                      AND name = N'SynchroFixtureDeposito'
                )
                    THROW 56998, 'Falta marca de pruebas.', 1;

                BEGIN TRY
                    BEGIN TRANSACTION;

                    DELETE o
                    FROM dbo.OutboxVerificacionTelefono o
                    INNER JOIN dbo.VerificacionesTelefono v
                        ON v.Id = o.VerificacionId
                    WHERE v.PersonaId IN (
                        @Persona1, @Persona2, @PersonaCreada
                    );

                    DELETE FROM dbo.VerificacionesTelefono
                    WHERE PersonaId IN (
                        @Persona1, @Persona2, @PersonaCreada
                    );

                    DELETE dbo.ConsentimientosPreReservaWhatsApp
                    WHERE ReservaId = @ReservaId;

                    DELETE dbo.PreReservasEncomiendasWeb
                    WHERE Id = @ReservaId
                      AND ClientePersonaId = @RemitenteId;

                    DELETE dbo.ClientesWebPrototipo
                    WHERE PersonaId = @RemitenteId
                      AND NombreUsuario = @Usuario;

                    DELETE dbo.Personas
                    WHERE Id IN (
                        @Persona1, @Persona2, @PersonaCreada
                    )
                      AND TelefonoWhatsapp = @Telefono;

                    DELETE dbo.Personas
                    WHERE Id = @RemitenteId
                      AND TelefonoWhatsapp = N'+999000000001';

                    COMMIT TRANSACTION;
                END TRY
                BEGIN CATCH
                    IF XACT_STATE() <> 0
                        ROLLBACK TRANSACTION;

                    THROW;
                END CATCH;
                """, conexion);

            comando.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = ReservaId;

            comando.Parameters.Add(
                "@RemitenteId", SqlDbType.Int
            ).Value = RemitenteId;

            comando.Parameters.Add(
                "@Persona1", SqlDbType.Int
            ).Value = PersonaExistenteId;

            comando.Parameters.Add(
                "@Persona2", SqlDbType.Int
            ).Value = SegundaPersonaId;

            comando.Parameters.Add(
                "@PersonaCreada", SqlDbType.Int
            ).Value = PersonaCreadaId;

            comando.Parameters.Add(
                "@Telefono", SqlDbType.NVarChar, 20
            ).Value = Telefono;

            comando.Parameters.Add(
                "@Usuario", SqlDbType.NVarChar, 80
            ).Value = Usuario;

            await comando.ExecuteNonQueryAsync();
        }
    }
}
