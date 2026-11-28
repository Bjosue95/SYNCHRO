using Microsoft.Data.SqlClient;
using Synchro.Api.Services.Administracion;

namespace Synchro.Api.Endpoints;

internal static class VerificarBaseCargaDefinitiva
{
    internal static async Task<bool> ValidarAsync(SqlConnection db,CancellationToken ct)
    {
        await using var q=new SqlCommand(DestinoAdministrativoDefinitivo.ValidacionSql,db);
        if(Convert.ToInt32(await q.ExecuteScalarAsync(ct))!=1)return false;
        q.CommandText="""
            SELECT CASE WHEN OBJECT_ID(N'dbo.SesionesCargaSynchro',N'U') IS NOT NULL
                AND OBJECT_ID(N'dbo.CredencialesCargaSynchro',N'U') IS NOT NULL
                AND OBJECT_ID(N'dbo.EspaciosCarga',N'U') IS NOT NULL
                AND OBJECT_ID(N'dbo.CobrosRetiroCargaSynchro',N'U') IS NOT NULL
                AND OBJECT_ID(N'dbo.TarifasCargaSynchro',N'U') IS NOT NULL THEN 1 ELSE 0 END;
            """;
        return Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1;
    }
}
