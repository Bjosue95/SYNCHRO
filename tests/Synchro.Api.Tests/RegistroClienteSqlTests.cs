
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.Clientes;
using System.Data;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class RegistroClienteSqlTests
{
    [SqlDepositoFact]
    [Trait("Category", "SqlAislado")]
    public async Task RegistroCreaPersonaConTelefonoYCuentaVinculada()
    {
        string cadena = ConexionSqlAislada.Exigir(
            Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST"),
            "SynchroDB_Pruebas");

        // Esta prueba ahora incluye eliminación: únicamente la copia aislada.
        Assert.Equal(@"(localdb)\SYNCHRO_Suite_Test",
            new SqlConnectionStringBuilder(cadena).DataSource, StringComparer.OrdinalIgnoreCase);
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SynchroDB"] = cadena
            })
            .Build();

        string usuario = "synchro_test_" + Guid.NewGuid().ToString("N");
        const string telefono = "+999123456780";
        const string contrasena = "Clave-SYNCHRO-Solo-Pruebas-2026";

        var repositorio = new RepositorioClientesWebSql(configuracion);
        var servicio = new CuentasClientesWeb(
            repositorio,
            new PasswordHasher<CuentaClienteWeb>());

        int personaId = 0;

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        await using (var seguridad = new SqlCommand("""
            IF DB_NAME() <> N'SynchroDB_Pruebas'
                THROW 56920, 'Base no autorizada.', 1;

            IF NOT EXISTS (
                SELECT 1 FROM sys.extended_properties
                WHERE class = 0
                  AND name = N'SynchroFixtureDeposito'
            )
                THROW 56921, 'Falta la marca del prototipo.', 1;
            """, conexion))
        {
            await seguridad.ExecuteNonQueryAsync();
        }

        try
        {
            var cuenta = await servicio.RegistrarAsync(
                usuario,
                contrasena,
                telefono,
                CancellationToken.None);

            personaId = cuenta.PersonaId;

            Assert.True(personaId > 0);

            await using var comando = new SqlCommand("""
                SELECT CASE
                    WHEN p.TelefonoWhatsapp = @telefono
                     AND c.Telefono = @telefono
                     AND p.TieneCuenta = 1
                     AND c.Activa = 1
                    THEN 1 ELSE 0 END
                FROM dbo.Personas AS p
                INNER JOIN dbo.ClientesWebPrototipo AS c
                    ON c.PersonaId = p.Id
                WHERE p.Id = @id
                  AND c.NombreUsuario = @usuario;
                """, conexion);

            comando.Parameters.Add("@id", SqlDbType.Int)
                .Value = personaId;

            comando.Parameters.Add("@usuario", SqlDbType.NVarChar, 80)
                .Value = usuario;

            comando.Parameters.Add("@telefono", SqlDbType.NVarChar, 20)
                .Value = telefono;

            object? resultado = await comando.ExecuteScalarAsync();

            Assert.Equal(1, Convert.ToInt32(resultado));
            // Eliminar la propia cuenta sin vínculos usando el repositorio real.
            // No reutilizar esta prueba contra la base operativa.
            Assert.False(await servicio.EliminarAsync(personaId, "Incorrecta-12345", CancellationToken.None));
            Assert.NotNull(await servicio.ObtenerAsync(personaId, CancellationToken.None));
            Assert.True(await servicio.EliminarAsync(personaId, contrasena, CancellationToken.None));
            Assert.Null(await servicio.ObtenerAsync(personaId, CancellationToken.None));
            await using var restante = new SqlCommand("SELECT COUNT(*) FROM dbo.Personas WHERE Id=@id",conexion);
            restante.Parameters.Add("@id",SqlDbType.Int).Value=personaId;
            Assert.Equal(0,Convert.ToInt32(await restante.ExecuteScalarAsync()));
            personaId=0;
        }
        finally
        {
            if (personaId > 0)
            {
                await using var limpiar = new SqlCommand("""
                    SET XACT_ABORT ON;

                    IF DB_NAME() <> N'SynchroDB_Pruebas'
                        THROW 56922, 'Base no autorizada.', 1;

                    BEGIN TRANSACTION;

                    DELETE FROM dbo.ClientesWebPrototipo
                    WHERE PersonaId = @id
                      AND NombreUsuario = @usuario;

                    DELETE FROM dbo.Personas
                    WHERE Id = @id
                      AND TelefonoWhatsapp = @telefono
                      AND TieneCuenta = 1
                      AND NOT EXISTS (
                          SELECT 1
                          FROM dbo.ClientesWebPrototipo
                          WHERE PersonaId = @id
                      );

                    COMMIT TRANSACTION;
                    """, conexion);

                limpiar.Parameters.Add("@id", SqlDbType.Int)
                    .Value = personaId;

                limpiar.Parameters.Add("@usuario", SqlDbType.NVarChar, 80)
                    .Value = usuario;

                limpiar.Parameters.Add("@telefono", SqlDbType.NVarChar, 20)
                    .Value = telefono;

                await limpiar.ExecuteNonQueryAsync();
            }
        }
    }
}
