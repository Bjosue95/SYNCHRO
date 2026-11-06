using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

public class PinRetiroIntegracionTests
{
    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task PinGeneradoEnCSharpSeValidaUnaSolaVez()
    {
        IConfiguration configuracion =
            new ConfigurationBuilder()
                .AddUserSecrets(
                    typeof(PinRetiroService).Assembly,
                    optional: false)
                .Build();

        string cadena = configuracion.GetConnectionString(
            "SynchroDBPruebas")
            ?? throw new InvalidOperationException(
                "Falta la conexion de pruebas.");

        var datosConexion =
            new SqlConnectionStringBuilder(cadena);

        Assert.Equal(
            "SynchroDB_Detalle_Pruebas",
            datosConexion.InitialCatalog);

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        Assert.Equal(
            "SynchroDB_Detalle_Pruebas",
            conexion.Database);

        int usoId = 0;
        int remitenteId = 0;
        int destinatarioId = 0;

        string codigo = "IT" +
            Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        byte[]? salt = null;
        byte[]? hash = null;

        try
        {
            // Preparar registros ficticios.
            await using (var transaccion =
                (SqlTransaction)await conexion.BeginTransactionAsync())
            {
                await using var comando = new SqlCommand(@"
                    SET NOCOUNT ON;

                    INSERT INTO dbo.Casilleros
                        (Codigo, Tamano, Tipo, Estado)
                    VALUES
                        (@Codigo, N'Pequeño', N'Normal', N'Ocupado');

                    INSERT INTO dbo.Personas
                        (Nombre, TelefonoWhatsapp, TieneCuenta)
                    VALUES
                        (N'Remitente prueba CSharp', NULL, 0);

                    DECLARE @Remitente INT =
                        CAST(SCOPE_IDENTITY() AS INT);

                    INSERT INTO dbo.Personas
                        (Nombre, TelefonoWhatsapp, TieneCuenta)
                    VALUES
                        (N'Destinatario prueba CSharp', NULL, 0);

                    DECLARE @Destinatario INT =
                        CAST(SCOPE_IDENTITY() AS INT);

                    INSERT INTO dbo.UsosCasillero
                    (
                        CodigoCasillero,
                        TipoServicio,
                        FechaReserva,
                        FechaIngreso,
                        FechaRetiro,
                        Estado
                    )
                    VALUES
                    (
                        @Codigo,
                        N'Encomienda',
                        DATEADD(MINUTE, -5, SYSUTCDATETIME()),
                        SYSUTCDATETIME(),
                        NULL,
                        N'Ocupado'
                    );

                    DECLARE @Uso INT =
                        CAST(SCOPE_IDENTITY() AS INT);

                    INSERT INTO dbo.ParticipantesUso
                        (UsoId, PersonaId, Rol)
                    VALUES
                        (@Uso, @Remitente, N'Remitente'),
                        (@Uso, @Destinatario, N'Destinatario');

                    SELECT @Uso, @Remitente, @Destinatario;
                ", conexion, transaccion);

                comando.Parameters.Add(
                    "@Codigo", SqlDbType.NVarChar, 10
                ).Value = codigo;

                await using (var lector =
                    await comando.ExecuteReaderAsync())
                {
                    Assert.True(await lector.ReadAsync());

                    usoId = lector.GetInt32(0);
                    remitenteId = lector.GetInt32(1);
                    destinatarioId = lector.GetInt32(2);
                }

                await transaccion.CommitAsync();
            }

            // Generar PIN y hash con el codigo real de C#.
            string pin = CodigoPinRetiro.Generar();
            salt = CodigoPinRetiro.GenerarSalt();

            hash = CodigoPinRetiro.CalcularHash(
                pin,
                usoId,
                destinatarioId,
                salt,
                configuracion);

            // Emitir el PIN mediante SQL Server.
            await using (var emitir = new SqlCommand(
                "dbo.EmitirPinRetiroSynchro",
                conexion)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                emitir.Parameters.Add(
                    "@UsoId", SqlDbType.Int
                ).Value = usoId;

                emitir.Parameters.Add(
                    "@DestinatarioId", SqlDbType.Int
                ).Value = destinatarioId;

                emitir.Parameters.Add(
                    "@PinHash", SqlDbType.VarBinary, 32
                ).Value = hash;

                emitir.Parameters.Add(
                    "@PinSalt", SqlDbType.VarBinary, 32
                ).Value = salt;

                emitir.Parameters.Add(
                    "@VigenciaMinutos", SqlDbType.Int
                ).Value = 15;

                await emitir.ExecuteNonQueryAsync();
            }

            // Probar el servicio real de C#.
            var servicio = new PinRetiroService(configuracion);

            string primerResultado =
                await servicio.ValidarPinAsync(
                    usoId, destinatarioId, pin);

            Assert.Equal("Aprobado", primerResultado);

            // El mismo PIN no puede utilizarse nuevamente.
            string segundoResultado =
                await servicio.ValidarPinAsync(
                    usoId, destinatarioId, pin);

            Assert.Equal("Utilizado", segundoResultado);
        }
        finally
        {
            // Eliminar exclusivamente los datos ficticios.
            await using var limpiar = new SqlCommand(@"
                SET XACT_ABORT ON;

                IF DB_NAME() <> N'SynchroDB_Detalle_Pruebas'
                    THROW 51500, 'Base incorrecta.', 1;

                BEGIN TRANSACTION;

                DELETE FROM dbo.VerificacionesRetiro
                WHERE UsoId = @UsoId;

                DELETE FROM dbo.CodigosRetiro
                WHERE UsoId = @UsoId;

                DELETE FROM dbo.ParticipantesUso
                WHERE UsoId = @UsoId;

                DELETE FROM dbo.UsosCasillero
                WHERE Id = @UsoId;

                DELETE FROM dbo.Personas
                WHERE Id IN (@RemitenteId, @DestinatarioId);

                DELETE FROM dbo.Casilleros
                WHERE Codigo = @Codigo;

                COMMIT TRANSACTION;
            ", conexion);

            limpiar.Parameters.Add(
                "@UsoId", SqlDbType.Int
            ).Value = usoId;

            limpiar.Parameters.Add(
                "@RemitenteId", SqlDbType.Int
            ).Value = remitenteId;

            limpiar.Parameters.Add(
                "@DestinatarioId", SqlDbType.Int
            ).Value = destinatarioId;

            limpiar.Parameters.Add(
                "@Codigo", SqlDbType.NVarChar, 10
            ).Value = codigo;

            await limpiar.ExecuteNonQueryAsync();

            if (hash is not null)
                Array.Clear(hash, 0, hash.Length);

            if (salt is not null)
                Array.Clear(salt, 0, salt.Length);
        }
    }
}
