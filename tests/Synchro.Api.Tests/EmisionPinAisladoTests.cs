using System.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

public sealed class EmisionPinAisladoTests
{
    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task EmitePinEnBaseAislada()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

        builder.Configuration.AddUserSecrets(
            typeof(PinRetiroDesarrolloService).Assembly,
            optional: false);

        const string cadena =
            @"Server=(localdb)\SYNCHRO_Aislado;" +
            "Database=SynchroDB;" +
            "Integrated Security=True;" +
            "Encrypt=True;" +
            "TrustServerCertificate=True;";

        builder.Configuration["ConnectionStrings:SynchroDB"] =
            cadena;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        // Comprobar que es la copia fisica aislada.
        await using (var verificar = new SqlCommand(
            @"SELECT CASE
                WHEN DB_NAME() = N'SynchroDB'
                 AND EXISTS (
                    SELECT 1 FROM sys.database_files
                    WHERE file_id = 1
                    AND physical_name LIKE
                      N'%\SYNCHRO_Aislado_Datos\SynchroDB_Aislado.mdf'
                 )
                THEN 1 ELSE 0 END",
            conexion))
        {
            Assert.Equal(
                1,
                Convert.ToInt32(
                    await verificar.ExecuteScalarAsync()));
        }

        int usoId = 0;
        int remitenteId = 0;
        int destinatarioId = 0;

        string codigo = "IT" +
            Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        try
        {
            // Preparar una encomienda ficticia.
            await using (var transaccion =
                (SqlTransaction)await conexion
                    .BeginTransactionAsync())
            {
                await using var preparar = new SqlCommand(
                    @"
                    INSERT INTO dbo.Casilleros
                        (Codigo, Tamano, Tipo, Estado)
                    VALUES
                        (@Codigo, N'Mediano',
                         N'Normal', N'Ocupado');

                    INSERT INTO dbo.Personas
                        (Nombre, TelefonoWhatsapp, TieneCuenta)
                    VALUES
                        (N'Remitente prueba aislada', NULL, 0);

                    DECLARE @R INT =
                        CAST(SCOPE_IDENTITY() AS INT);

                    INSERT INTO dbo.Personas
                        (Nombre, TelefonoWhatsapp, TieneCuenta)
                    VALUES
                        (N'Destinatario prueba aislada', NULL, 0);

                    DECLARE @D INT =
                        CAST(SCOPE_IDENTITY() AS INT);

                    INSERT INTO dbo.UsosCasillero
                        (CodigoCasillero, TipoServicio,
                         FechaReserva, FechaIngreso,
                         FechaRetiro, Estado)
                    VALUES
                        (@Codigo, N'Encomienda',
                         DATEADD(MINUTE, -5, SYSUTCDATETIME()),
                         SYSUTCDATETIME(),
                         NULL, N'Ocupado');

                    DECLARE @U INT =
                        CAST(SCOPE_IDENTITY() AS INT);

                    INSERT INTO dbo.ParticipantesUso
                        (UsoId, PersonaId, Rol)
                    VALUES
                        (@U, @R, N'Remitente'),
                        (@U, @D, N'Destinatario');

                    SELECT @U, @R, @D;
                    ",
                    conexion,
                    transaccion);

                preparar.Parameters.Add(
                    "@Codigo", SqlDbType.NVarChar, 10
                ).Value = codigo;

                await using (var lector =
                    await preparar.ExecuteReaderAsync())
                {
                    Assert.True(await lector.ReadAsync());

                    usoId = lector.GetInt32(0);
                    remitenteId = lector.GetInt32(1);
                    destinatarioId = lector.GetInt32(2);
                }

                await transaccion.CommitAsync();
            }

            // Ejecutar el servicio C# real.
            var servicio = new PinRetiroDesarrolloService(
                builder.Configuration,
                builder.Environment);

            string pin = await servicio.EmitirPinAsync(
                usoId,
                destinatarioId,
                vigenciaMinutos: 15);

            // Comprobar formato sin mostrar el PIN.
            Assert.Matches(@"^[0-9]{6}$", pin);

            // Verificar el registro protegido en SQL.
            await using var consultar = new SqlCommand(
                @"SELECT COUNT(*)
                  FROM dbo.CodigosRetiro
                  WHERE UsoId = @UsoId
                    AND DATALENGTH(PinHash) = 32
                    AND DATALENGTH(PinSalt) = 32
                    AND FechaUso IS NULL
                    AND IntentosFallidos = 0",
                conexion);

            consultar.Parameters.Add(
                "@UsoId", SqlDbType.Int
            ).Value = usoId;

            Assert.Equal(
                1,
                Convert.ToInt32(
                    await consultar.ExecuteScalarAsync()));

            // Validar el PIN emitido desde C#.
            string primeraValidacion =
                await servicio.ValidarPinAsync(
                    usoId, destinatarioId, pin);

            Assert.Equal("Aprobado", primeraValidacion);

            // Comprobar que no pueda reutilizarse.
            string segundaValidacion =
                await servicio.ValidarPinAsync(
                    usoId, destinatarioId, pin);

            Assert.Equal("Utilizado", segundaValidacion);

            // Comprobar que SQL registro la evidencia
            // de una unica validacion PIN aprobada.
            await using var verificarEvidencia =
                new SqlCommand(
                    @"SELECT COUNT(*)
                      FROM dbo.VerificacionesRetiro
                      WHERE UsoId = @UsoId
                        AND PersonaId = @DestinatarioId
                        AND Metodo = N'PIN'
                        AND Resultado = N'Aprobado'",
                    conexion);

            verificarEvidencia.Parameters.Add(
                "@UsoId", SqlDbType.Int
            ).Value = usoId;

            verificarEvidencia.Parameters.Add(
                "@DestinatarioId", SqlDbType.Int
            ).Value = destinatarioId;

            Assert.Equal(
                1,
                Convert.ToInt32(
                    await verificarEvidencia.ExecuteScalarAsync()));

        }
        finally
        {
            // Eliminar solo los registros ficticios.
            if (usoId > 0)
            {
                await using var limpiar = new SqlCommand(
                    @"
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB'
                       OR NOT EXISTS (
                           SELECT 1
                           FROM sys.database_files
                           WHERE file_id = 1
                             AND physical_name LIKE
                             N'%\SYNCHRO_Aislado_Datos\SynchroDB_Aislado.mdf'
                       )
                        THROW 51950, 'Base no autorizada.', 1;

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
                    ",
                    conexion);

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
            }
        }
    }
}
