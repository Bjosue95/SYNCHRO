using System.Security.Cryptography;
using Synchro.Api.Services.RostroSimulado;

namespace Synchro.Api.Services;

internal static class CapturaRostroHttp
{
    public static async Task<(MemoryStream? Captura,IResult? Error)> LeerAsync(HttpContext http)
    {
        if(!string.Equals(http.Request.ContentType?.Split(';')[0].Trim(),"application/octet-stream",StringComparison.OrdinalIgnoreCase))return(null,Results.StatusCode(415));
        if(http.Request.ContentLength>PatronVisualSimulado.MaximoBytes)return(null,Results.StatusCode(413));
        var memoria=new MemoriaProtegida();byte[] buffer=new byte[8192];
        try
        {
            int n;while((n=await http.Request.Body.ReadAsync(buffer,http.RequestAborted))>0)
            {
                if(memoria.Length+n>PatronVisualSimulado.MaximoBytes){Limpiar(memoria);memoria.Dispose();return(null,Results.StatusCode(413));}
                memoria.Write(buffer,0,n);
            }
            if(memoria.Length==0){memoria.Dispose();return(null,Results.BadRequest());}memoria.Position=0;return(memoria,null);
        }
        catch{Limpiar(memoria);memoria.Dispose();throw;}
        finally{CryptographicOperations.ZeroMemory(buffer);}
    }
    public static void Limpiar(MemoryStream memoria)
    {if(memoria.TryGetBuffer(out var datos))CryptographicOperations.ZeroMemory(datos.AsSpan());}
}
