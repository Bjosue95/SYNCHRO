using System.Data;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Administracion;

internal static class AccesoAdministrativoSql
{
    public const string ClaimVersion="synchro_version_sesion";
    public static async Task<AccesoAdministrativo> ObtenerAsync(SqlConnection c,int id,CancellationToken token,SqlTransaction? transaccion=null)
    {
        // La compatibilidad con cuentas anteriores no convierte en administrador
        // a una cuenta gestionada que se haya quedado sin roles.
        await using var q=new SqlCommand("""
            IF OBJECT_ID(N'dbo.AccesosAdministrativosGestionados',N'U') IS NULL
                SELECT N'Administrador' AS Rol,0 AS VersionSesion;
            ELSE
                EXEC sys.sp_executesql N'
                    IF EXISTS(SELECT 1 FROM dbo.AccesosAdministrativosGestionados WHERE AdministradorId=@id)
                        SELECT r.Rol,g.VersionSesion FROM dbo.AccesosAdministrativosGestionados g
                        LEFT JOIN dbo.RolesAdministrativosPrototipo r ON r.AdministradorId=g.AdministradorId
                        WHERE g.AdministradorId=@id;
                    ELSE SELECT N''Administrador'' AS Rol,0 AS VersionSesion;',N'@id INT',@id=@id;
            """,c,transaccion);
        q.Parameters.Add("@id",SqlDbType.Int).Value=id;var roles=new List<string>();int version=0;
        await using var r=await q.ExecuteReaderAsync(token);
        while(await r.ReadAsync(token))
        {version=r.GetInt32(1);if(!r.IsDBNull(0)&&GestionUsuariosAdministrativos.RolesPermitidos.Contains(r.GetString(0),StringComparer.Ordinal))roles.Add(r.GetString(0));}
        return new(roles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),version);
    }
}
internal sealed class RepositorioUsuariosAdministrativosSql(IConfiguration config) : IRepositorioUsuariosAdministrativos
{
    public async Task<T> TransaccionAsync<T>(int actor,Func<List<UsuarioAdministrativoEstado>,T> cambio,CancellationToken token)
    {
        string cadena=ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");
        await using var c=new SqlConnection(cadena);await c.OpenAsync(token);await using var t=(SqlTransaction)await c.BeginTransactionAsync(token);
        var usuarios=new List<UsuarioAdministrativoEstado>();
        await using(var q=new SqlCommand("SELECT Id,NombreUsuario,Activo FROM dbo.Administradores WITH(UPDLOCK,HOLDLOCK) ORDER BY Id",c,t))
        {
            await using var r=await q.ExecuteReaderAsync(token);
            while(await r.ReadAsync(token))usuarios.Add(new(){Id=r.GetInt32(0),Nombre=r.GetString(1),Activo=r.GetBoolean(2)});
        }
        foreach(var u in usuarios)
        {var acceso=await AccesoAdministrativoSql.ObtenerAsync(c,u.Id,token,t);u.Roles=acceso.Roles;u.VersionSesion=acceso.VersionSesion;}
        if(!usuarios.Any(u=>u.Id==actor&&u.Activo&&u.Roles.Contains("Administrador",StringComparer.Ordinal)))
            throw new AccesoAdministrativoRechazadoException();
        T resultado=cambio(usuarios);
        foreach(var u in usuarios.Where(u=>u.Modificado))
        {
            if(u.Id==0)
            {
                await using var q=new SqlCommand("INSERT dbo.Administradores(NombreUsuario,PasswordHash,Activo)OUTPUT INSERTED.Id VALUES(@nombre,@hash,@activo)",c,t);
                q.Parameters.Add("@nombre",SqlDbType.NVarChar,80).Value=u.Nombre;q.Parameters.Add("@hash",SqlDbType.NVarChar,500).Value=u.NuevoPasswordHash;
                q.Parameters.Add("@activo",SqlDbType.Bit).Value=u.Activo;u.Id=Convert.ToInt32(await q.ExecuteScalarAsync(token));
            }
            else
            {
                await using var q=new SqlCommand("UPDATE dbo.Administradores SET Activo=@activo,PasswordHash=COALESCE(@hash,PasswordHash)WHERE Id=@id;IF @@ROWCOUNT<>1 THROW 56301,'Usuario no disponible.',1;",c,t);
                q.Parameters.Add("@id",SqlDbType.Int).Value=u.Id;q.Parameters.Add("@activo",SqlDbType.Bit).Value=u.Activo;
                q.Parameters.Add("@hash",SqlDbType.NVarChar,500).Value=(object?)u.NuevoPasswordHash??DBNull.Value;await q.ExecuteNonQueryAsync(token);
            }
            await using(var q=new SqlCommand("""
                UPDATE dbo.AccesosAdministrativosGestionados SET VersionSesion=@version WHERE AdministradorId=@id;
                IF @@ROWCOUNT=0 INSERT dbo.AccesosAdministrativosGestionados(AdministradorId,VersionSesion)VALUES(@id,@version);
                DELETE dbo.RolesAdministrativosPrototipo WHERE AdministradorId=@id;
                """,c,t))
            {q.Parameters.Add("@id",SqlDbType.Int).Value=u.Id;q.Parameters.Add("@version",SqlDbType.Int).Value=u.VersionSesion;await q.ExecuteNonQueryAsync(token);}
            foreach(string rol in u.Roles)
            {
                await using var q=new SqlCommand("INSERT dbo.RolesAdministrativosPrototipo(AdministradorId,Rol)VALUES(@id,@rol)",c,t);
                q.Parameters.Add("@id",SqlDbType.Int).Value=u.Id;q.Parameters.Add("@rol",SqlDbType.NVarChar,20).Value=rol;await q.ExecuteNonQueryAsync(token);
            }
            await using(var q=new SqlCommand("INSERT dbo.AuditoriaUsuariosAdministrativos(AdministradorId,ActorId,Accion,FechaUtc)VALUES(@id,@actor,@accion,SYSUTCDATETIME())",c,t))
            {q.Parameters.Add("@id",SqlDbType.Int).Value=u.Id;q.Parameters.Add("@actor",SqlDbType.Int).Value=actor;q.Parameters.Add("@accion",SqlDbType.NVarChar,80).Value=u.Accion;await q.ExecuteNonQueryAsync(token);}
        }
        await t.CommitAsync(token);return resultado;
    }
}
