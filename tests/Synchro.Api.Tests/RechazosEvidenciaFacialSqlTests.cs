
using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Synchro.Api.Tests;

[Collection("SQL Vinculacion Terminal")]
public class RechazosEvidenciaFacialSqlTests
{
    private const string Cadena =
        @"Server=(localdb)\MSSQLLocalDB;" +
        "Database=SynchroDB_Pruebas;" +
        "Integrated Security=True;" +
        "Encrypt=True;" +
        "TrustServerCertificate=True;";

    [Theory]
    [InlineData("ReservaAjena")]
    [InlineData("RemitenteAjeno")]
    [InlineData("TerminalAjena")]
    [InlineData("TerminalDesactivada")]
    [InlineData("ReservaCancelada")]
    [InlineData("ReservaVencida")]
    [InlineData("EvidenciaVencida")]
    [InlineData("EvidenciaFutura")]
    [InlineData("RostroNoCoincide")]
    [InlineData("PruebaDeVidaFallida")]
    [Trait("Category", "LocalDb")]
    public async Task EvidenciaInvalida_DebeRechazarse(
        string escenario)
    {
        await using var conexion = new SqlConnection(Cadena);
        await conexion.OpenAsync();

        // Seguridad: nunca utilizar la base principal.
        Assert.Equal("SynchroDB_Pruebas", conexion.Database);

        // Todos los datos ficticios quedaran en una transaccion
        // que no confirmaremos.
        await using var transaccion =
            (SqlTransaction)await conexion.BeginTransactionAsync();

        try
        {
            byte[] hashFicticio =
                RandomNumberGenerator.GetBytes(32);

            string codigoTerminal =
                "TST-NEG-" + Guid.NewGuid().ToString("N")[..20];

            Guid evidenciaId = Guid.NewGuid();

            int reservaId;
            int terminalId;

            // 1. Crear reserva ficticia.
            await using (var cmd = new SqlCommand("""
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
                    DATEADD(MINUTE, -5, SYSUTCDATETIME()),
                    DATEADD(HOUR, 1, SYSUTCDATETIME())
                );
                """, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Hash", SqlDbType.VarBinary, 32
                ).Value = hashFicticio;

                reservaId = Convert.ToInt32(
                    await cmd.ExecuteScalarAsync());
            }

            CryptographicOperations.ZeroMemory(hashFicticio);

            // 2. Crear terminal ficticia activa.
            await using (var cmd = new SqlCommand("""
                INSERT INTO dbo.Terminales
                    (Codigo, Nombre, Activa)
                OUTPUT INSERTED.Id
                VALUES
                    (@Codigo, N'SYNCHRO_TEST_RECHAZOS', 1);
                """, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Codigo", SqlDbType.NVarChar, 30
                ).Value = codigoTerminal;

                terminalId = Convert.ToInt32(
                    await cmd.ExecuteScalarAsync());
            }

            // 3. Crear evidencia simulada.
            await using (var cmd = new SqlCommand("""
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
                    501,
                    @Terminal,
                    DATEADD(SECOND, -10, SYSUTCDATETIME()),
                    DATEADD(SECOND, 90, SYSUTCDATETIME()),
                    1,
                    1,
                    0
                );
                """, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Evidencia", SqlDbType.UniqueIdentifier
                ).Value = evidenciaId;

                cmd.Parameters.Add(
                    "@Reserva", SqlDbType.Int
                ).Value = reservaId;

                cmd.Parameters.Add(
                    "@Terminal", SqlDbType.Int
                ).Value = terminalId;

                await cmd.ExecuteNonQueryAsync();
            }

            // 4. Alterar solo el dato del escenario.
            string? cambio = escenario switch
            {
                "TerminalDesactivada" => """
                    UPDATE dbo.Terminales
                    SET Activa = 0
                    WHERE Id = @Terminal;
                    """,

                "ReservaCancelada" => """
                    UPDATE dbo.PreReservasEncomiendasWeb
                    SET Estado = N'Cancelada'
                    WHERE Id = @Reserva;
                    """,

                "ReservaVencida" => """
                    UPDATE dbo.PreReservasEncomiendasWeb
                    SET FechaCreacion =
                            DATEADD(HOUR, -3, SYSUTCDATETIME()),
                        FechaExpiracion =
                            DATEADD(HOUR, -1, SYSUTCDATETIME())
                    WHERE Id = @Reserva;
                    """,

                "EvidenciaVencida" => """
                    UPDATE dbo.EvidenciasFacialesRemitente
                    SET FechaEmisionUtc =
                            DATEADD(SECOND, -150, SYSUTCDATETIME()),
                        FechaExpiracionUtc =
                            DATEADD(SECOND, -60, SYSUTCDATETIME())
                    WHERE EvidenciaId = @Evidencia;
                    """,

                "EvidenciaFutura" => """
                    UPDATE dbo.EvidenciasFacialesRemitente
                    SET FechaEmisionUtc =
                            DATEADD(SECOND, 30, SYSUTCDATETIME()),
                        FechaExpiracionUtc =
                            DATEADD(SECOND, 120, SYSUTCDATETIME())
                    WHERE EvidenciaId = @Evidencia;
                    """,

                "RostroNoCoincide" => """
                    UPDATE dbo.EvidenciasFacialesRemitente
                    SET RostroCoincide = 0
                    WHERE EvidenciaId = @Evidencia;
                    """,

                "PruebaDeVidaFallida" => """
                    UPDATE dbo.EvidenciasFacialesRemitente
                    SET PruebaDeVidaSuperada = 0
                    WHERE EvidenciaId = @Evidencia;
                    """,

                _ => null
            };

            if (cambio is not null)
            {
                await using var modificar =
                    new SqlCommand(
                        cambio, conexion, transaccion);

                modificar.Parameters.Add(
                    "@Reserva", SqlDbType.Int
                ).Value = reservaId;

                modificar.Parameters.Add(
                    "@Terminal", SqlDbType.Int
                ).Value = terminalId;

                modificar.Parameters.Add(
                    "@Evidencia", SqlDbType.UniqueIdentifier
                ).Value = evidenciaId;

                Assert.Equal(
                    1,
                    await modificar.ExecuteNonQueryAsync());
            }

            // 5. Preparar los identificadores que se enviaran.
            int reservaSolicitada = escenario == "ReservaAjena"
                ? int.MaxValue
                : reservaId;

            int remitenteSolicitado = escenario == "RemitenteAjeno"
                ? int.MaxValue
                : 501;

            int terminalSolicitada = escenario == "TerminalAjena"
                ? int.MaxValue
                : terminalId;

            // 6. Intentar consumir la evidencia.
            await using var consumir = new SqlCommand(
                "dbo.ConsumirEvidenciaFacialRemitenteSynchro",
                conexion,
                transaccion);

            consumir.CommandType = CommandType.StoredProcedure;

            consumir.Parameters.Add(
                "@EvidenciaId", SqlDbType.UniqueIdentifier
            ).Value = evidenciaId;

            consumir.Parameters.Add(
                "@ReservaId", SqlDbType.Int
            ).Value = reservaSolicitada;

            consumir.Parameters.Add(
                "@RemitenteId", SqlDbType.Int
            ).Value = remitenteSolicitado;

            consumir.Parameters.Add(
                "@TerminalId", SqlDbType.Int
            ).Value = terminalSolicitada;

            // Todos estos escenarios deben provocar
            // el rechazo controlado 55405.
            var error = await Assert.ThrowsAsync<SqlException>(
                async () =>
                {
                    await consumir.ExecuteScalarAsync();
                });

            Assert.Equal(55405, error.Number);
        }
        finally
        {
            // Nunca confirmar los datos ficticios.
            // Si SQL ya revirtio la transaccion por THROW,
            // RollbackAsync puede informar que finalizo.
            try
            {
                await transaccion.RollbackAsync();
            }
            catch (InvalidOperationException)
            {
                // SQL Server ya revirtio la transaccion.
            }
        }
    }
}
