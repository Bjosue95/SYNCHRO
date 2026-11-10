using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Clientes;

internal sealed class CuentaClienteWeb
{
    public int PersonaId {get;set;}
    public string NombreUsuario {get;init;}="";
    public string Telefono {get;init;}="";
    public string PasswordHash {get;init;}="";
    public bool Activa {get;set;}=true;
    public int VersionSesion {get;init;}=1;
    public override string ToString()=>"Cuenta de cliente protegida";
}
public sealed record PerfilClienteWeb(int PersonaId,string NombreUsuario,string Telefono);
internal interface IRepositorioClientesWeb
{
    Task<CuentaClienteWeb?> BuscarAsync(string nombre,CancellationToken token);
    Task<CuentaClienteWeb?> BuscarAsync(int persona,CancellationToken token);
    Task CrearAsync(CuentaClienteWeb cuenta,CancellationToken token);
}
internal sealed class CuentaClienteNoDisponibleException : Exception;
internal sealed class CuentasClientesWeb(IRepositorioClientesWeb repo,IPasswordHasher<CuentaClienteWeb> hasher)
{
    public static PerfilClienteWeb Perfil(CuentaClienteWeb c)=>new(c.PersonaId,c.NombreUsuario,c.Telefono);
    public async Task<CuentaClienteWeb> RegistrarAsync(string? nombre,string? password,string? telefono,CancellationToken token)
    {
        string usuario=ValidacionCredencialesCuenta.Nombre(nombre);ValidacionCredencialesCuenta.Password(password);
        string? numero=telefono?.Trim();
        if(numero is null||!Regex.IsMatch(numero,@"\A\+?[1-9][0-9]{7,14}\z"))throw new ArgumentException("Telefono invalido.");
        numero=numero.StartsWith('+')?numero:"+"+numero;
        if(await repo.BuscarAsync(usuario,token)is not null)throw new CuentaClienteNoDisponibleException();
        var datos=new CuentaClienteWeb{NombreUsuario=usuario,Telefono=numero};
        var cuenta=new CuentaClienteWeb{NombreUsuario=usuario,Telefono=numero,PasswordHash=hasher.HashPassword(datos,password!)};
        await repo.CrearAsync(cuenta,token);return cuenta;
    }
    public async Task<CuentaClienteWeb?> AutenticarAsync(string? nombre,string? password,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(nombre)||string.IsNullOrEmpty(password)||nombre.Length>80)return null;
        var cuenta=await repo.BuscarAsync(nombre.Trim(),token);
        if(cuenta is null||!cuenta.Activa)return null;
        return hasher.VerifyHashedPassword(cuenta,cuenta.PasswordHash,password)==PasswordVerificationResult.Failed?null:cuenta;
    }
    public Task<CuentaClienteWeb?> ObtenerAsync(int persona,CancellationToken token)=>repo.BuscarAsync(persona,token);
}
internal sealed class RepositorioClientesWebSql(IConfiguration config) : IRepositorioClientesWeb
{
    private string Cadena=>ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");
    public Task<CuentaClienteWeb?> BuscarAsync(string nombre,CancellationToken token)=>BuscarInternoAsync(nombre,null,token);
    public Task<CuentaClienteWeb?> BuscarAsync(int persona,CancellationToken token)=>BuscarInternoAsync(null,persona,token);
    private async Task<CuentaClienteWeb?> BuscarInternoAsync(string? nombre,int? persona,CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var q=new SqlCommand("SELECT PersonaId,NombreUsuario,Telefono,PasswordHash,Activa,VersionSesion FROM dbo.ClientesWebPrototipo WHERE (@id IS NOT NULL AND PersonaId=@id)OR(@nombre IS NOT NULL AND NombreUsuario=@nombre)",c);
        q.Parameters.Add("@id",SqlDbType.Int).Value=(object?)persona??DBNull.Value;q.Parameters.Add("@nombre",SqlDbType.NVarChar,80).Value=(object?)nombre??DBNull.Value;
        await using var r=await q.ExecuteReaderAsync(token);return await r.ReadAsync(token)?new(){PersonaId=r.GetInt32(0),NombreUsuario=r.GetString(1),Telefono=r.GetString(2),PasswordHash=r.GetString(3),Activa=r.GetBoolean(4),VersionSesion=r.GetInt32(5)}:null;
    }
    public async Task CrearAsync(CuentaClienteWeb cuenta,CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);await using var t=(SqlTransaction)await c.BeginTransactionAsync(token);
        await using(var q=new SqlCommand("INSERT dbo.Personas OUTPUT INSERTED.Id DEFAULT VALUES",c,t))
            cuenta.PersonaId=Convert.ToInt32(await q.ExecuteScalarAsync(token));
        await using(var q=new SqlCommand("INSERT dbo.ClientesWebPrototipo(PersonaId,NombreUsuario,Telefono,PasswordHash,Activa,VersionSesion)VALUES(@id,@nombre,@telefono,@hash,1,1)",c,t))
        {
            q.Parameters.Add("@id",SqlDbType.Int).Value=cuenta.PersonaId;q.Parameters.Add("@nombre",SqlDbType.NVarChar,80).Value=cuenta.NombreUsuario;
            q.Parameters.Add("@telefono",SqlDbType.NVarChar,20).Value=cuenta.Telefono;q.Parameters.Add("@hash",SqlDbType.NVarChar,500).Value=cuenta.PasswordHash;
            await q.ExecuteNonQueryAsync(token);
        }
        await t.CommitAsync(token);
    }
}
