using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace Synchro.Api.Services.Administracion;

// La credencial identifica al proxy privado de la estacion, no al navegador.
internal sealed class TerminalProxyDefinitivo(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IConfiguration cfg) : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    private const string Header="X-Synchro-Estacion-Privada";
    private const string Codigo="SYNCHRO-DEMO-01";
    internal static async Task PrepararAsync(WebApplication app,string cadena,string carpeta)
    {
        var archivo=Path.Combine(carpeta,"tablet-proxy.key");
        if(!File.Exists(archivo))
        {
            var clave=RandomNumberGenerator.GetBytes(32);
            try
            {
                await using var f=new FileStream(archivo,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                byte[] texto=Encoding.ASCII.GetBytes(Convert.ToHexString(clave));
                try{await f.WriteAsync(texto);f.Flush(flushToDisk:true);}finally{CryptographicOperations.ZeroMemory(texto);}
            }
            finally{CryptographicOperations.ZeroMemory(clave);}
        }
        string token=(await File.ReadAllTextAsync(archivo)).Trim();
        if(token.Length!=64||!token.All(char.IsAsciiHexDigit))throw new InvalidOperationException("Credencial privada de estacion invalida.");
        app.Configuration["EstacionDefinitiva:ClaveProxy"]=token;
        await using var c=new SqlConnection(cadena);await c.OpenAsync();
        await using var q=new SqlCommand("""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            IF EXISTS(SELECT 1 FROM dbo.Terminales WITH(UPDLOCK,HOLDLOCK) WHERE Codigo<>N'SYNCHRO-DEMO-01')
            BEGIN ROLLBACK; THROW 59101,'Existe otra estacion. No se modifica.',1; END;
            IF NOT EXISTS(SELECT 1 FROM dbo.Terminales WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=N'SYNCHRO-DEMO-01')
                INSERT dbo.Terminales(Codigo,Nombre,Activa,FechaAutorizacion)
                VALUES(N'SYNCHRO-DEMO-01',N'Estacion de demostracion definitiva',1,SYSUTCDATETIME());
            COMMIT;
            """,c);
        await q.ExecuteNonQueryAsync();
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if(!Request.Headers.TryGetValue(Header,out var valores)||valores.Count!=1)
            return AuthenticateResult.NoResult();
        string? esperado=cfg["EstacionDefinitiva:ClaveProxy"];
        string? recibido=valores[0];
        if(esperado is not{Length:64}||recibido is not{Length:64})
            return AuthenticateResult.Fail("Estacion no autorizada.");
        byte[] x=Encoding.UTF8.GetBytes(esperado),y=Encoding.UTF8.GetBytes(recibido);
        bool valido;
        try{valido=CryptographicOperations.FixedTimeEquals(x,y);}
        finally{CryptographicOperations.ZeroMemory(x);CryptographicOperations.ZeroMemory(y);}
        if(!valido)return AuthenticateResult.Fail("Estacion no autorizada.");
        try
        {
            string cadena=DestinoAdministrativoDefinitivo.Exigir(cfg.GetConnectionString("SynchroDB"));
            await using var c=new SqlConnection(cadena);await c.OpenAsync(Context.RequestAborted);
            await using var q=new SqlCommand("SELECT Id FROM dbo.Terminales WHERE Codigo=@codigo AND Activa=1",c);
            q.Parameters.Add("@codigo",System.Data.SqlDbType.NVarChar,30).Value=Codigo;
            if(await q.ExecuteScalarAsync(Context.RequestAborted) is not int id||id<=0)
                return AuthenticateResult.Fail("Estacion no activa.");
            var identidad=new ClaimsIdentity([new(ClaimTypes.NameIdentifier,id.ToString()),new(ClaimTypes.Role,"Terminal")],Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad),Scheme.Name));
        }
        catch(SqlException){return AuthenticateResult.Fail("Estacion no disponible.");}
    }
}
