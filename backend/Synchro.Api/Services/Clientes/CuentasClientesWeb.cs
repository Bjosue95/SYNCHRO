using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Clientes;

internal sealed class CuentaClienteWeb
{
    public int PersonaId { get; set; }
    public string NombreUsuario { get; init; } = "";
    public string Telefono { get; init; } = "";
    public string PasswordHash { get; init; } = "";
    public bool Activa { get; set; } = true;
    public int VersionSesion { get; init; } = 1;
    public override string ToString() => "Cuenta de cliente protegida";
}
public sealed record PerfilClienteWeb(int PersonaId, string NombreUsuario, string Telefono);
internal interface IRepositorioClientesWeb
{
    Task<CuentaClienteWeb?> BuscarAsync(string nombre, CancellationToken token);
    Task<CuentaClienteWeb?> BuscarAsync(int persona, CancellationToken token);
    Task CrearAsync(CuentaClienteWeb cuenta, CancellationToken token);
    Task<bool> EliminarSinVinculosAsync(CuentaClienteWeb cuenta, CancellationToken token);
}
internal sealed class CuentaClienteNoDisponibleException : Exception;
internal sealed class CuentaClienteConHistorialException : Exception;
internal sealed class CuentasClientesWeb(IRepositorioClientesWeb repo, IPasswordHasher<CuentaClienteWeb> hasher)
{
    public static PerfilClienteWeb Perfil(CuentaClienteWeb c) => new(c.PersonaId, c.NombreUsuario, c.Telefono);
    public async Task<CuentaClienteWeb> RegistrarAsync(string? nombre, string? password, string? telefono, CancellationToken token)
    {
        string usuario = ValidacionCredencialesCuenta.Nombre(nombre); ValidacionCredencialesCuenta.Password(password);
        string? numero = telefono?.Trim();
        if (numero is null || !Regex.IsMatch(numero, @"\A\+?[1-9][0-9]{7,14}\z")) throw new ArgumentException("Telefono invalido.");
        numero = numero.StartsWith('+') ? numero : "+" + numero;
        if (await repo.BuscarAsync(usuario, token) is not null) throw new CuentaClienteNoDisponibleException();
        var datos = new CuentaClienteWeb { NombreUsuario = usuario, Telefono = numero };
        var cuenta = new CuentaClienteWeb { NombreUsuario = usuario, Telefono = numero, PasswordHash = hasher.HashPassword(datos, password!) };
        await repo.CrearAsync(cuenta, token); return cuenta;
    }
    public async Task<CuentaClienteWeb?> AutenticarAsync(string? nombre, string? password, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrEmpty(password) || nombre.Length > 80) return null;
        var cuenta = await repo.BuscarAsync(nombre.Trim(), token);
        if (cuenta is null || !cuenta.Activa) return null;
        return hasher.VerifyHashedPassword(cuenta, cuenta.PasswordHash, password) == PasswordVerificationResult.Failed ? null : cuenta;
    }
    public Task<CuentaClienteWeb?> ObtenerAsync(int persona, CancellationToken token) => repo.BuscarAsync(persona, token);
    public async Task<bool> EliminarAsync(int persona, string? password, CancellationToken token)
    {
        if (password is null || password.Length is <12 or >128) return false;
        var cuenta = await repo.BuscarAsync(persona, token);
        if (cuenta is null || !cuenta.Activa ||
            hasher.VerifyHashedPassword(cuenta, cuenta.PasswordHash, password) == PasswordVerificationResult.Failed)
            return false;
        if (!await repo.EliminarSinVinculosAsync(cuenta, token))
            throw new CuentaClienteConHistorialException();
        return true;
    }
}
internal sealed class RepositorioClientesWebSql(IConfiguration config) : IRepositorioClientesWeb
{
    private string Cadena => ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"), "SynchroDB_Pruebas");
    public Task<CuentaClienteWeb?> BuscarAsync(string nombre, CancellationToken token) => BuscarInternoAsync(nombre, null, token);
    public Task<CuentaClienteWeb?> BuscarAsync(int persona, CancellationToken token) => BuscarInternoAsync(null, persona, token);
    private async Task<CuentaClienteWeb?> BuscarInternoAsync(string? nombre, int? persona, CancellationToken token)
    {
        await using var c = new SqlConnection(Cadena); await c.OpenAsync(token);
        await using var q = new SqlCommand("SELECT PersonaId,NombreUsuario,Telefono,PasswordHash,Activa,VersionSesion FROM dbo.ClientesWebPrototipo WHERE (@id IS NOT NULL AND PersonaId=@id)OR(@nombre IS NOT NULL AND NombreUsuario=@nombre)", c);
        q.Parameters.Add("@id", SqlDbType.Int).Value = (object?)persona ?? DBNull.Value; q.Parameters.Add("@nombre", SqlDbType.NVarChar, 80).Value = (object?)nombre ?? DBNull.Value;
        await using var r = await q.ExecuteReaderAsync(token); return await r.ReadAsync(token) ? new() { PersonaId = r.GetInt32(0), NombreUsuario = r.GetString(1), Telefono = r.GetString(2), PasswordHash = r.GetString(3), Activa = r.GetBoolean(4), VersionSesion = r.GetInt32(5) } : null;
    }
    public async Task CrearAsync(CuentaClienteWeb cuenta, CancellationToken token)
    {
        await using var c = new SqlConnection(Cadena); await c.OpenAsync(token); await using var t = (SqlTransaction)await c.BeginTransactionAsync(token);
        await using (var q = new SqlCommand("""
    INSERT INTO dbo.Personas (TelefonoWhatsapp, TieneCuenta)
    OUTPUT INSERTED.Id
    VALUES (@telefono, 1)
    """, c, t))
        {
            q.Parameters.Add("@telefono", SqlDbType.NVarChar, 20)
                .Value = cuenta.Telefono;

            cuenta.PersonaId =
                Convert.ToInt32(await q.ExecuteScalarAsync(token));
        }
        await using (var q = new SqlCommand("INSERT dbo.ClientesWebPrototipo(PersonaId,NombreUsuario,Telefono,PasswordHash,Activa,VersionSesion)VALUES(@id,@nombre,@telefono,@hash,1,1)", c, t))
        {
            q.Parameters.Add("@id", SqlDbType.Int).Value = cuenta.PersonaId; q.Parameters.Add("@nombre", SqlDbType.NVarChar, 80).Value = cuenta.NombreUsuario;
            q.Parameters.Add("@telefono", SqlDbType.NVarChar, 20).Value = cuenta.Telefono; q.Parameters.Add("@hash", SqlDbType.NVarChar, 500).Value = cuenta.PasswordHash;
            await q.ExecuteNonQueryAsync(token);
        }
        await t.CommitAsync(token);
    }
    public async Task<bool> EliminarSinVinculosAsync(CuentaClienteWeb cuenta, CancellationToken token)
    {
        await using var c = new SqlConnection(Cadena);
        await c.OpenAsync(token);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, token);
        // Revalidar la identidad y credencial bajo bloqueo. Las referencias
        // existentes (incluido historial) no se borran ni se desvinculan.
        await using var q = new SqlCommand("""
            SET XACT_ABORT ON;
            IF NOT EXISTS (SELECT 1 FROM dbo.ClientesWebPrototipo WITH(XLOCK,HOLDLOCK)
                WHERE PersonaId=@id AND Activa=1 AND VersionSesion=@version AND PasswordHash=@hash)
            BEGIN SELECT CAST(0 AS bit); RETURN; END;
            IF NOT EXISTS (SELECT 1 FROM dbo.Personas WITH(XLOCK,HOLDLOCK)
                WHERE Id=@id AND TieneCuenta=1 AND TelefonoWhatsapp=@telefono)
            BEGIN SELECT CAST(0 AS bit); RETURN; END;

            -- Inspeccionar relaciones reales, incluso las configuradas con
            -- cascada. No permitir que una eliminación borre operaciones.
            DECLARE @tabla nvarchar(520), @columna sysname, @referencia sysname,
                    @sentencia nvarchar(max), @hay bit;
            DECLARE vinculos CURSOR LOCAL FAST_FORWARD FOR
                SELECT QUOTENAME(OBJECT_SCHEMA_NAME(f.parent_object_id))+N'.'+
                       QUOTENAME(OBJECT_NAME(f.parent_object_id)), pc.name, rc.name
                FROM sys.foreign_key_columns f
                JOIN sys.columns pc ON pc.object_id=f.parent_object_id AND pc.column_id=f.parent_column_id
                JOIN sys.columns rc ON rc.object_id=f.referenced_object_id AND rc.column_id=f.referenced_column_id
                WHERE f.referenced_object_id IN(OBJECT_ID(N'dbo.Personas'),OBJECT_ID(N'dbo.ClientesWebPrototipo'))
                  AND NOT(f.referenced_object_id=OBJECT_ID(N'dbo.Personas')
                      AND f.parent_object_id=OBJECT_ID(N'dbo.ClientesWebPrototipo')
                      AND pc.name=N'PersonaId' AND rc.name=N'Id');
            OPEN vinculos;
            FETCH NEXT FROM vinculos INTO @tabla,@columna,@referencia;
            WHILE @@FETCH_STATUS=0
            BEGIN
                -- No se presupone el significado de una relación nueva.
                IF @referencia NOT IN(N'Id',N'PersonaId') OR @tabla IS NULL
                BEGIN CLOSE vinculos; DEALLOCATE vinculos; SELECT CAST(0 AS bit); RETURN; END;
                SET @hay=0;
                SET @sentencia=N'SELECT @hay=CASE WHEN EXISTS(SELECT 1 FROM '+@tabla+
                    N' WITH(UPDLOCK,HOLDLOCK) WHERE '+QUOTENAME(@columna)+N'=@id) THEN 1 ELSE 0 END;';
                EXEC sys.sp_executesql @sentencia,N'@id int,@hay bit OUTPUT',@id=@id,@hay=@hay OUTPUT;
                IF @hay=1
                BEGIN CLOSE vinculos; DEALLOCATE vinculos; SELECT CAST(0 AS bit); RETURN; END;
                FETCH NEXT FROM vinculos INTO @tabla,@columna,@referencia;
            END;
            CLOSE vinculos;
            DEALLOCATE vinculos;

            DELETE FROM dbo.ClientesWebPrototipo WHERE PersonaId=@id;
            IF @@ROWCOUNT<>1 THROW 56940,'Cuenta no disponible.',1;
            DELETE FROM dbo.Personas WHERE Id=@id;
            IF @@ROWCOUNT<>1 THROW 56941,'Persona no disponible.',1;
            SELECT CAST(1 AS bit);
            """, c, tx);
        q.Parameters.Add("@id", SqlDbType.Int).Value = cuenta.PersonaId;
        q.Parameters.Add("@version", SqlDbType.Int).Value = cuenta.VersionSesion;
        q.Parameters.Add("@hash", SqlDbType.NVarChar, 500).Value = cuenta.PasswordHash;
        q.Parameters.Add("@telefono", SqlDbType.NVarChar, 20).Value = cuenta.Telefono;
        bool eliminado = (bool)(await q.ExecuteScalarAsync(token) ?? false);
        if (eliminado) await tx.CommitAsync(token);
        else await tx.RollbackAsync(token);
        return eliminado;
    }
}
