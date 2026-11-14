using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Xunit;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public class ConsultaConsentimientoSqlTests : PruebaHistoricaAislada
{
    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task ReservasSinConsentimientoValidoNoPuedenContinuar()
    {
        const string cadena =
            @"Server=(localdb)\SYNCHRO_Aislado;" +
            "Database=SynchroDB_Pruebas;" +
            "Integrated Security=True;" +
            "Encrypt=True;" +
            "TrustServerCertificate=True;";

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();
        await PruebaHistoricaAislada.VerificarAsync(conexion);

        // Nunca consultar accidentalmente la base principal.
        Assert.Equal("SynchroDB_Pruebas", conexion.Database);

        const string sql = """
            SELECT TOP (50)
                r.Estado AS EstadoReserva,
                r.FechaExpiracion,
                r.TelefonoDestinatario,
                c.Estado AS EstadoConsentimiento,
                c.TelefonoWhatsApp,
                c.FechaCreacionUtc,
                c.FechaExpiracionUtc,
                c.FechaConfirmacionUtc,
                c.MensajeIdConfirmacion
            FROM dbo.PreReservasEncomiendasWeb r
            LEFT JOIN dbo.ConsentimientosPreReservaWhatsApp c
                ON c.ReservaId = r.Id
            WHERE r.Id = @Fixture
            ORDER BY r.Id DESC;
            """;

        // Ocho escenarios propios, sin depender de reservas historicas del usuario.
        foreach (string escenario in new[]{"SinConsentimiento","Pendiente","Confirmado","TelefonoDistinto",
            "FechaFutura","ConsentimientoVencido","ReservaVencida","Revocado"})
        {
        await PruebaHistoricaAislada.VerificarAsync(conexion);
        await using (var preparar=new SqlCommand("""
            DELETE dbo.ConsentimientosPreReservaWhatsApp WHERE ReservaId=@r;
            UPDATE dbo.PreReservasEncomiendasWeb SET FechaCreacion=DATEADD(minute,-5,SYSUTCDATETIME()),FechaExpiracion=
                CASE WHEN @escenario=N'ReservaVencida' THEN DATEADD(second,-1,SYSUTCDATETIME())
                     ELSE DATEADD(hour,1,SYSUTCDATETIME()) END WHERE Id=@r;
            IF @escenario<>N'SinConsentimiento'
                INSERT dbo.ConsentimientosPreReservaWhatsApp(ReservaId,TelefonoWhatsApp,CodigoHash,Estado,
                    FechaCreacionUtc,FechaExpiracionUtc,FechaConfirmacionUtc,MensajeIdConfirmacion)
                VALUES(@r,CASE WHEN @escenario=N'TelefonoDistinto' THEN N'+999000000003' ELSE N'+999000000002' END,
                    CRYPT_GEN_RANDOM(32),CASE WHEN @escenario=N'Pendiente' THEN N'Pendiente'
                        WHEN @escenario=N'Revocado' THEN N'Revocado' ELSE N'Confirmado' END,
                    DATEADD(minute,-5,SYSUTCDATETIME()),
                    CASE WHEN @escenario=N'ConsentimientoVencido' THEN DATEADD(minute,-1,SYSUTCDATETIME())
                        ELSE DATEADD(hour,1,SYSUTCDATETIME()) END,
                    CASE WHEN @escenario=N'Pendiente' THEN NULL
                        WHEN @escenario=N'FechaFutura' THEN DATEADD(minute,1,SYSUTCDATETIME())
                        ELSE SYSUTCDATETIME() END,
                    CASE WHEN @escenario=N'Pendiente' THEN NULL ELSE CONVERT(nvarchar(36),NEWID()) END);
            """,conexion))
        {
            preparar.Parameters.AddWithValue("@r",ReservaFixture);
            preparar.Parameters.AddWithValue("@escenario",escenario);
            await preparar.ExecuteNonQueryAsync();
        }
        await using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@Fixture",ReservaFixture);
        await using var lector = await comando.ExecuteReaderAsync();

        int reservasRevisadas = 0;

        while (await lector.ReadAsync())
        {
            reservasRevisadas++;

            string estadoReserva = lector.GetString(0);
            DateTime venceReserva = lector.GetDateTime(1);
            string telefonoDestinatario = lector.GetString(2);

            string? estadoConsentimiento =
                lector.IsDBNull(3) ? null : lector.GetString(3);

            string? telefonoWhatsApp =
                lector.IsDBNull(4) ? null : lector.GetString(4);

            DateTime? fechaCreacion =
                lector.IsDBNull(5) ? null : lector.GetDateTime(5);

            DateTime? fechaExpiracion =
                lector.IsDBNull(6) ? null : lector.GetDateTime(6);

            DateTime? fechaConfirmacion =
                lector.IsDBNull(7) ? null : lector.GetDateTime(7);

            string? mensajeConfirmacion =
                lector.IsDBNull(8) ? null : lector.GetString(8);

            bool reservaVigente =
                ValidacionDepositoEncomienda.ReservaVigente(
                    estadoReserva,
                    venceReserva,
                    DateTime.UtcNow);

            string telefonoEsperado =
                telefonoDestinatario.StartsWith('+')
                    ? telefonoDestinatario
                    : "+" + telefonoDestinatario;

            bool consentimientoConfirmado =
                telefonoWhatsApp == telefonoEsperado
                && !string.IsNullOrWhiteSpace(mensajeConfirmacion)
                && fechaCreacion.HasValue
                && fechaExpiracion.HasValue
                && ValidacionDepositoEncomienda
                    .ConsentimientoConfirmado(
                        estadoConsentimiento,
                        fechaConfirmacion,
                        fechaCreacion.Value,
                        fechaExpiracion.Value);

            bool puedeContinuar =
                ValidacionDepositoEncomienda
                    .PuedeContinuarVerificacion(
                        reservaVigente,
                        consentimientoConfirmado);

            // Una reserva vencida nunca debe avanzar.
            if (!reservaVigente)
                Assert.False(puedeContinuar);

            // Sin consentimiento confirmado, tampoco.
            if (!consentimientoConfirmado)
                Assert.False(puedeContinuar);

            // Ambas condiciones son obligatorias.
            Assert.Equal(
                reservaVigente && consentimientoConfirmado,
                puedeContinuar);
            Assert.Equal(escenario=="Confirmado",puedeContinuar);
        }

        Assert.True(
            reservasRevisadas > 0,
            "No hay pre-reservas para realizar la prueba.");
        }
    }
}
