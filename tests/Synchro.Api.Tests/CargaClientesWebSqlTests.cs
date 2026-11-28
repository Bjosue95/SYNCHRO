using System.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services.Clientes;
using Xunit.Abstractions;

namespace Synchro.Api.Tests;

public sealed class CargaSqlClientesFactAttribute : FactAttribute
{
    public CargaSqlClientesFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SYNCHRO_CARGA_SQL") != "1")
            Skip = "Carga SQL optativa; requiere copia aislada.";
    }
}

[Collection("SQL Deposito Aislado")]
public sealed class CargaClientesWebSqlTests(ITestOutputHelper salida)
{
    [CargaSqlClientesFact]
    [Trait("Category","SqlAislado")]
    public async Task CargaDeCuentasEnCopiaAislada()
    {
        var cadena = Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST");
        Assert.False(string.IsNullOrWhiteSpace(cadena), "Configura la conexión de la copia de pruebas.");
        var sql = new SqlConnectionStringBuilder(cadena);
        Assert.True(string.Equals(sql.DataSource,@"(localdb)\SYNCHRO_Suite_Test",StringComparison.OrdinalIgnoreCase)
            && sql.InitialCatalog=="SynchroDB_Pruebas" && sql.IntegratedSecurity
            && string.IsNullOrEmpty(sql.AttachDBFilename),"Solo se permite la copia SYNCHRO_Suite_Test, nunca la base de la feria.");
        await using var conexion=new SqlConnection(sql.ConnectionString);
        await conexion.OpenAsync();
        await using(var verificar=new SqlCommand("""
            IF DB_NAME() <> N'SynchroDB_Pruebas' OR NOT EXISTS
               (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
                THROW 56930,'Falta la marca de copia aislada',1;
            """,conexion)) await verificar.ExecuteNonQueryAsync();
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:SynchroDB"]=sql.ConnectionString }).Build();
        var repo=new RepositorioClientesWebSql(cfg);
        const string clave="Carga-local-solo-pruebas-2026";
        foreach(int cantidad in new[]{25,50,100,200})
        {
            string prefijo="carga_"+Guid.NewGuid().ToString("N")+"_";
            string[] nombres=Enumerable.Range(1,cantidad).Select(i=>prefijo+i.ToString("D4")).ToArray();
            string telefono="+999"+Random.Shared.NextInt64(100000000L,999999999L).ToString();
            using var cupos=new SemaphoreSlim(8);
            var total=Stopwatch.StartNew();
            try
            {
                var tiempos=await Task.WhenAll(nombres.Select(nombre=>Task.Run(async ()=>{
                    var reloj=Stopwatch.StartNew();
                    await cupos.WaitAsync();
                    try
                    {
                        var servicio=new CuentasClientesWeb(repo,new PasswordHasher<CuentaClienteWeb>());
                        var creada=await servicio.RegistrarAsync(nombre,clave,telefono,CancellationToken.None);
                        var login=await servicio.AutenticarAsync(nombre,clave,CancellationToken.None);
                        Assert.NotNull(login); Assert.Equal(creada.PersonaId,login!.PersonaId);
                        Assert.NotEqual(clave,creada.PasswordHash);
                        return reloj.Elapsed.TotalMilliseconds;
                    }
                    finally { cupos.Release(); }
                })));
                Array.Sort(tiempos);
                salida.WriteLine($"SQL copia: {cantidad}/{cantidad}; total {total.Elapsed.TotalSeconds:F2}s; p95 {tiempos[(int)Math.Ceiling(cantidad*.95)-1]:F0}ms; max {tiempos[^1]:F0}ms");
            }
            finally
            {
                // Identidad exacta generada por esta prueba; no limpiar por prefijo amplio.
                foreach(var nombre in nombres)
                {
                    await using var limpiar=new SqlCommand("""
                        SET XACT_ABORT ON;
                        BEGIN TRANSACTION;
                        DECLARE @id int;
                        SELECT @id=c.PersonaId FROM dbo.ClientesWebPrototipo c
                        JOIN dbo.Personas p ON p.Id=c.PersonaId
                        WHERE c.NombreUsuario=@nombre AND c.Telefono=@telefono AND p.TelefonoWhatsapp=@telefono;
                        DELETE FROM dbo.ClientesWebPrototipo WHERE PersonaId=@id AND NombreUsuario=@nombre;
                        DELETE FROM dbo.Personas WHERE Id=@id AND TelefonoWhatsapp=@telefono
                          AND NOT EXISTS(SELECT 1 FROM dbo.ClientesWebPrototipo WHERE PersonaId=@id);
                        COMMIT TRANSACTION;
                        """,conexion);
                    limpiar.Parameters.Add("@nombre",SqlDbType.NVarChar,80).Value=nombre;
                    limpiar.Parameters.Add("@telefono",SqlDbType.NVarChar,20).Value=telefono;
                    await limpiar.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
