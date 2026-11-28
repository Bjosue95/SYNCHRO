using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services;

internal static class DiagnosticoBaseDefinitiva
{
    private const string Conexion = @"Server=.\SQLEXPRESS;Database=SynchroDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15";

    internal static async Task<int> VerificarAsync()
    {
        try
        {
            await using var c = new SqlConnection(Conexion);
            await c.OpenAsync();
            await using var q = new SqlCommand("""
                SET NOCOUNT ON;
                IF DB_NAME()<>N'SynchroDB'
                   OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS'
                   OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1)<>0
                    THROW 59050,'Destino no autorizado.',1;
                SELECT
                    (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0),
                    (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped=0),
                    (SELECT COUNT(*) FROM sys.foreign_keys),
                    (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1 AND is_ms_shipped=0),
                    (SELECT COUNT(*) FROM dbo.Casilleros),
                    (SELECT COUNT(*) FROM dbo.EspaciosCarga),
                    (SELECT COUNT(*) FROM dbo.TarifasCargaSynchro),
                    (SELECT COUNT(*) FROM dbo.CompartimentosServiciosTemporales WHERE Codigo IN(N'M04',N'M09') AND ISJSON(ConfiguracionJson)=1),
                    (SELECT COUNT(*) FROM dbo.Administradores a
                      JOIN dbo.RolesAdministrativosPrototipo r ON r.AdministradorId=a.Id
                      JOIN dbo.AccesosAdministrativosGestionados g ON g.AdministradorId=a.Id
                      WHERE a.Activo=1 AND r.Rol=N'Administrador' AND g.VersionSesion>=1),
                    (SELECT COUNT(*) FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito'),
                    (SELECT COUNT(*) FROM sys.procedures p JOIN sys.sql_modules m ON m.object_id=p.object_id
                      WHERE m.definition LIKE N'%SynchroDB[_]Pruebas%'),
                    (SELECT COUNT(*) FROM sys.procedures p JOIN sys.sql_modules m ON m.object_id=p.object_id
                      WHERE m.definition LIKE N'%SynchroFixtureDeposito%');
                """, c);
            q.CommandTimeout = 30;
            await using var r = await q.ExecuteReaderAsync();
            if (!await r.ReadAsync()) throw new InvalidOperationException("Sin resultado del diagnóstico.");
            string[] etiquetas = ["Tablas","Procedimientos","Relaciones","Triggers","Casilleros","Espacios de carga","Tarifas de carga","Configuraciones termicas","Administradores activos con rol explicito","Marcadores de fixture","Procedimientos con referencia a base de pruebas","Procedimientos con marcador de fixture"];
            int[] esperado = [55,22,81,1,10,8,3,2];
            Console.WriteLine("=== SYNCHRO | Diagnostico de conexion definitiva ===");
            Console.WriteLine("[OK] Microsoft.Data.SqlClient conectado a .\\SQLEXPRESS / SynchroDB");
            bool listo=true;
            for (int i=0;i<etiquetas.Length;i++)
            {
                int valor=r.GetInt32(i);
                bool ok=i<esperado.Length ? valor==esperado[i] : i==8 ? valor>=1 : i==9 ? valor==0 : true;
                if(!ok) listo=false;
                string estado=i>=10 ? "INFO" : ok ? "OK" : "REVISAR";
                Console.WriteLine($"[{estado}] {etiquetas[i]}: {valor}");
            }
            Console.WriteLine(listo ? "[OK] Conexion y estructura inicial verificadas." : "[REVISAR] La estructura o configuracion no coincide con la instalacion inicial.");
            Console.WriteLine("Solo lectura. No se iniciaron HTTP, trabajadores, pagos ni operaciones.");
            return listo?0:1;
        }
        catch(Exception e) when(e is SqlException or InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine(e is SqlException sql ? $"Diagnostico SQL detenido. Codigo: {sql.Number}." : "Diagnostico detenido. Revisar configuracion y estructura.");
            return 1;
        }
    }
}
