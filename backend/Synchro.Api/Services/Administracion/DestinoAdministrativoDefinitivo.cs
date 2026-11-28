using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Administracion;

// Exclusivo para las consultas administrativas de integracion local.
internal static class DestinoAdministrativoDefinitivo
{
    internal const string ValidacionSql = """
        SELECT CASE WHEN DB_NAME()=N'SynchroDB'
          AND ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')=N'SQLEXPRESS'
          AND ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1)=0
          AND NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
          THEN 1 ELSE 0 END;
        """;

    internal static bool Permitido(string? cadena)
    {
        if(string.IsNullOrWhiteSpace(cadena))return false;
        try
        {
            var c=new SqlConnectionStringBuilder(cadena);
            return c.DataSource.Equals(@".\SQLEXPRESS",StringComparison.OrdinalIgnoreCase)
                && c.InitialCatalog.Equals("SynchroDB",StringComparison.OrdinalIgnoreCase)
                && c.IntegratedSecurity && string.IsNullOrWhiteSpace(c.AttachDBFilename);
        }
        catch(ArgumentException){return false;}
    }
    internal static string Exigir(string? cadena)
        => Permitido(cadena) ? cadena! : throw new InvalidOperationException("Conexion definitiva no autorizada.");
}
