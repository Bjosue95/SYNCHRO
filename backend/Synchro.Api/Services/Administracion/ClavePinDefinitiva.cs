using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;

namespace Synchro.Api.Services.Administracion;

internal static class ClavePinDefinitiva
{
    internal static async Task CargarAsync(WebApplication app,string conexion,string carpeta,bool personal=false,bool carga=false)
    {
        var protector=app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(carga?"SYNCHRO.Definitivo.Carga.v1":personal?"SYNCHRO.Definitivo.RetiroPersonal.v1":"SYNCHRO.Definitivo.RetiroPin.v1");
        var archivo=Path.Combine(carpeta,carga?"Carga.hmac.protegido":personal?"RetiroPersonal.hmac.protegido":"RetiroPin.hmac.protegido");
        byte[]? clave=null;
        try
        {
            if(File.Exists(archivo))
            {
                clave=protector.Unprotect(await File.ReadAllBytesAsync(archivo));
            }
            else
            {
                await using var c=new SqlConnection(conexion);
                await c.OpenAsync();
                await using var q=new SqlCommand(carga?"SELECT COUNT(*) FROM dbo.CredencialesCargaSynchro":personal?"SELECT COUNT(*) FROM dbo.CodigosRetiroPersonal":"SELECT COUNT(*) FROM dbo.PinesEncomiendaOperativa",c);
                if(Convert.ToInt32(await q.ExecuteScalarAsync())!=0)
                    throw new InvalidOperationException("Falta la clave original de PIN. Recuperar el archivo protegido, no generar otro.");
                clave=RandomNumberGenerator.GetBytes(32);
                byte[] cifrado=protector.Protect(clave);
                try
                {
                    await using var f=new FileStream(archivo,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                    await f.WriteAsync(cifrado);
                    f.Flush(flushToDisk:true);
                }
                finally{CryptographicOperations.ZeroMemory(cifrado);}
            }
            if(clave.Length!=32)throw new InvalidOperationException("Longitud de clave de PIN invalida.");
            app.Configuration[carga?"CargaDispositivos:ClaveHmac":personal?"RetiroPersonal:ClaveHmac":"RetiroPin:ClaveHmac"]=Convert.ToBase64String(clave);
        }
        finally{if(clave is not null)CryptographicOperations.ZeroMemory(clave);}
    }
}
