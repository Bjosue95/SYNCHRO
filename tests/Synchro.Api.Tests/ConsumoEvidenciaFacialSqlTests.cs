
using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public class ConsumoEvidenciaFacialSqlTests : PruebaHistoricaAislada
{
    private const string Cadena =
        @"Server=(localdb)\SYNCHRO_Aislado;" +
        "Database=SynchroDB_Pruebas;" +
        "Integrated Security=True;" +
        "Encrypt=True;" +
        "TrustServerCertificate=True;";

    private const int RemitenteFicticio = 501;

    private static async Task InsertarEvidenciaAsync(
        SqlConnection conexion,
        Guid evidenciaId,
        int reservaId,
        int terminalId)
    {
        await using var comando = new SqlCommand("""
            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 55501, 'Base incorrecta.', 1;

            INSERT INTO dbo.EvidenciasFacialesRemitente
            (
                EvidenciaId,
                ReservaId,
                RemitenteId,
                TerminalId,
                FechaEmisionUtc,
                FechaExpiracionUtc,
                RostroCoincide,
                PruebaDeVidaSuperada,
                Consumida
            )
            VALUES
            (
                @Evidencia,
                @Reserva,
                @Remitente,
                @Terminal,
                DATEADD(SECOND, -10, SYSUTCDATETIME()),
                DATEADD(SECOND, 90, SYSUTCDATETIME()),
                1,
                1,
                0
            );
            """, conexion);

        comando.Parameters.Add(
            "@Evidencia", SqlDbType.UniqueIdentifier
        ).Value = evidenciaId;

        comando.Parameters.Add(
            "@Reserva", SqlDbType.Int
        ).Value = reservaId;

        comando.Parameters.Add(
            "@Remitente", SqlDbType.Int
        ).Value = RemitenteFicticio;

        comando.Parameters.Add(
            "@Terminal", SqlDbType.Int
        ).Value = terminalId;

        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ConsumirAsync(
        Guid evidenciaId,
        int reservaId,
        int terminalId)
    {
        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();
        await PruebaHistoricaAislada.VerificarAsync(conexion);

        if (conexion.Database != "SynchroDB_Pruebas")
            throw new InvalidOperationException(
                "Base de datos de pruebas incorrecta.");

        await using var comando = new SqlCommand(
            "dbo.ConsumirEvidenciaFacialRemitenteSynchro",
            conexion);

        comando.CommandType = CommandType.StoredProcedure;

        comando.Parameters.Add(
            "@EvidenciaId", SqlDbType.UniqueIdentifier
        ).Value = evidenciaId;

        comando.Parameters.Add(
            "@ReservaId", SqlDbType.Int
        ).Value = reservaId;

        comando.Parameters.Add(
            "@RemitenteId", SqlDbType.Int
        ).Value = RemitenteFicticio;

        comando.Parameters.Add(
            "@TerminalId", SqlDbType.Int
        ).Value = terminalId;

        try
        {
            object? resultado = await comando.ExecuteScalarAsync();

            Assert.Equal(evidenciaId, Assert.IsType<Guid>(resultado));

            return true;
        }
        catch (SqlException ex) when (ex.Number == 55405)
        {
            // Rechazo esperado de evidencia ya consumida.
            return false;
        }
    }

    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task ConsumoUnicoYConcurrente_DebeSerSeguro()
    {
        string codigoTerminal =
            "TST-EV-" + Guid.NewGuid().ToString("N")[..20];

        Guid evidenciaUnica = Guid.NewGuid();
        Guid evidenciaConcurrente = Guid.NewGuid();

        byte[] hash = SHA256.HashData(
            RandomNumberGenerator.GetBytes(32));

        int reservaId = 0;
        int terminalId = 0;

        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();
        await PruebaHistoricaAislada.VerificarAsync(conexion);

        Assert.Equal(
            "SynchroDB_Pruebas",
            conexion.Database);

        try
        {
            // 1. Crear una reserva ficticia.
            await using (var crear = new SqlCommand("""
                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 55502, 'Base incorrecta.', 1;

                INSERT INTO dbo.PreReservasEncomiendasWeb
                (
                    TokenGestionHash,
                    TelefonoRemitente,
                    TelefonoDestinatario,
                    TamanoSolicitado,
                    Estado,
                    FechaCreacion,
                    FechaExpiracion
                )
                OUTPUT INSERTED.Id
                VALUES
                (
                    @Hash,
                    N'+50499990001',
                    N'+50499990002',
                    N'Mediano',
                    N'PendienteConsentimiento',
                    DATEADD(MINUTE, -1, SYSUTCDATETIME()),
                    DATEADD(HOUR, 1, SYSUTCDATETIME())
                );
                """, conexion))
            {
                crear.Parameters.Add(
                    "@Hash", SqlDbType.VarBinary, 32
                ).Value = hash;

                reservaId = Convert.ToInt32(
                    await crear.ExecuteScalarAsync());
            }

            // 2. Crear una terminal ficticia activa.
            await using (var crear = new SqlCommand("""
                INSERT INTO dbo.Terminales
                    (Codigo, Nombre, Activa)
                OUTPUT INSERTED.Id
                VALUES
                    (@Codigo, N'SYNCHRO_TEST_CONSUMO', 1);
                """, conexion))
            {
                crear.Parameters.Add(
                    "@Codigo", SqlDbType.NVarChar, 30
                ).Value = codigoTerminal;

                terminalId = Convert.ToInt32(
                    await crear.ExecuteScalarAsync());
            }

            // 3. Insertar dos evidencias simuladas.
            await InsertarEvidenciaAsync(
                conexion, evidenciaUnica, reservaId, terminalId);

            await InsertarEvidenciaAsync(
                conexion, evidenciaConcurrente, reservaId, terminalId);

            // 4. Primer consumo: permitido.
            Assert.True(await ConsumirAsync(
                evidenciaUnica, reservaId, terminalId));

            // 5. Segundo consumo: rechazado.
            Assert.False(await ConsumirAsync(
                evidenciaUnica, reservaId, terminalId));

            // 6. Dos solicitudes simultaneas.
            Task<bool> primera = ConsumirAsync(
                evidenciaConcurrente, reservaId, terminalId);

            Task<bool> segunda = ConsumirAsync(
                evidenciaConcurrente, reservaId, terminalId);

            bool[] resultados = await Task.WhenAll(
                primera, segunda);

            // Exactamente una debe conseguirlo.
            Assert.Equal(1, resultados.Count(x => x));

            // 7. Verificar ambas evidencias en SQL.
            await using var verificar = new SqlCommand("""
                SELECT COUNT(*)
                FROM dbo.EvidenciasFacialesRemitente
                WHERE EvidenciaId IN (@Una, @Concurrente)
                  AND Consumida = 1
                  AND FechaConsumoUtc IS NOT NULL;
                """, conexion);

            verificar.Parameters.Add(
                "@Una", SqlDbType.UniqueIdentifier
            ).Value = evidenciaUnica;

            verificar.Parameters.Add(
                "@Concurrente", SqlDbType.UniqueIdentifier
            ).Value = evidenciaConcurrente;

            Assert.Equal(
                2,
                Convert.ToInt32(
                    await verificar.ExecuteScalarAsync()));
        }
        finally
        {
            // Eliminar exclusivamente nuestros registros ficticios.
            try
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 55503, 'Base incorrecta.', 1;

                    BEGIN TRY
                        BEGIN TRANSACTION;

                        DELETE FROM dbo.EvidenciasFacialesRemitente
                        WHERE EvidenciaId IN (@Una, @Concurrente)
                          AND ReservaId = @Reserva;

                        DELETE FROM dbo.PreReservasEncomiendasWeb
                        WHERE Id = @Reserva
                          AND TokenGestionHash = @Hash;

                        DELETE FROM dbo.Terminales
                        WHERE Id = @Terminal
                          AND Codigo = @Codigo
                          AND Nombre = N'SYNCHRO_TEST_CONSUMO';

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0
                            ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH;
                    """, conexion);

                limpiar.Parameters.Add(
                    "@Una", SqlDbType.UniqueIdentifier
                ).Value = evidenciaUnica;

                limpiar.Parameters.Add(
                    "@Concurrente", SqlDbType.UniqueIdentifier
                ).Value = evidenciaConcurrente;

                limpiar.Parameters.Add(
                    "@Reserva", SqlDbType.Int
                ).Value = reservaId;

                limpiar.Parameters.Add(
                    "@Terminal", SqlDbType.Int
                ).Value = terminalId;

                limpiar.Parameters.Add(
                    "@Hash", SqlDbType.VarBinary, 32
                ).Value = hash;

                limpiar.Parameters.Add(
                    "@Codigo", SqlDbType.NVarChar, 30
                ).Value = codigoTerminal;

                await limpiar.ExecuteNonQueryAsync();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
    }
}