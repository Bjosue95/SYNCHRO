using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.RostroSimulado;

internal sealed record ReferenciaRostroSimulado(int PersonaId,double[] Patron,DateTimeOffset VenceUtc)
{
    public string Modelo {get;init;}=PatronVisualSimulado.Modelo;
    public override string ToString()=>"Referencia visual simulada protegida";
}
internal interface IReferenciasRostroSimulado
{
    Task GuardarAsync(ReferenciaRostroSimulado referencia,CancellationToken token);
    Task<ReferenciaRostroSimulado?> ObtenerAsync(int personaId,CancellationToken token);
    Task EliminarAsync(int personaId,CancellationToken token);
    Task EliminarVencidasAsync(CancellationToken token);
}
internal sealed class ReferenciasRostroSimuladoSql(IConfiguration config,IDataProtectionProvider proteccion,TimeProvider reloj) : IReferenciasRostroSimulado
{
    private string Cadena=>ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");
    private IDataProtector Protector(int persona)=>proteccion.CreateProtector("SYNCHRO.RostroSimulado.v1",persona.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public async Task GuardarAsync(ReferenciaRostroSimulado r,CancellationToken token)
    {
        byte[] plano=JsonSerializer.SerializeToUtf8Bytes(r);byte[] cifrado;
        try{cifrado=Protector(r.PersonaId).Protect(plano);}finally{CryptographicOperations.ZeroMemory(plano);}
        try
        {
            await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);await using var t=(SqlTransaction)await c.BeginTransactionAsync(token);
            await using var q=new SqlCommand("""
                UPDATE dbo.ReferenciasRostroSimulado WITH(UPDLOCK,HOLDLOCK) SET PatronProtegido=@patron,VenceUtc=@vence,ActualizadaUtc=@ahora,Modelo=@modelo WHERE PersonaId=@persona;
                IF @@ROWCOUNT=0 INSERT dbo.ReferenciasRostroSimulado(PersonaId,PatronProtegido,VenceUtc,ActualizadaUtc,Modelo)VALUES(@persona,@patron,@vence,@ahora,@modelo);
                """,c,t);
            q.Parameters.Add("@modelo",SqlDbType.NVarChar,60).Value=PatronVisualSimulado.Modelo;
            q.Parameters.Add("@persona",SqlDbType.Int).Value=r.PersonaId;q.Parameters.Add("@patron",SqlDbType.VarBinary,-1).Value=cifrado;
            q.Parameters.Add("@vence",SqlDbType.DateTime2).Value=r.VenceUtc.UtcDateTime;q.Parameters.Add("@ahora",SqlDbType.DateTime2).Value=reloj.GetUtcNow().UtcDateTime;
            await q.ExecuteNonQueryAsync(token);await t.CommitAsync(token);
        }
        finally{CryptographicOperations.ZeroMemory(cifrado);}
    }
    public async Task<ReferenciaRostroSimulado?> ObtenerAsync(int persona,CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var q=new SqlCommand("SELECT PatronProtegido FROM dbo.ReferenciasRostroSimulado WHERE PersonaId=@persona AND VenceUtc>@ahora AND Modelo=@modelo",c);
        q.Parameters.Add("@modelo",SqlDbType.NVarChar,60).Value=PatronVisualSimulado.Modelo;
        q.Parameters.Add("@persona",SqlDbType.Int).Value=persona;q.Parameters.Add("@ahora",SqlDbType.DateTime2).Value=reloj.GetUtcNow().UtcDateTime;
        if(await q.ExecuteScalarAsync(token)is not byte[] cifrado)return null;
        byte[] plano=Protector(persona).Unprotect(cifrado);
        try
        {
            var r=JsonSerializer.Deserialize<ReferenciaRostroSimulado>(plano);
            return r is not null&&r.PersonaId==persona&&r.Modelo==PatronVisualSimulado.Modelo&&r.VenceUtc>reloj.GetUtcNow()?r:null;
        }
        finally{CryptographicOperations.ZeroMemory(plano);CryptographicOperations.ZeroMemory(cifrado);}
    }
    public Task EliminarAsync(int persona,CancellationToken token)=>BorrarAsync(persona,token);
    public Task EliminarVencidasAsync(CancellationToken token)=>BorrarAsync(null,token);
    private async Task BorrarAsync(int? persona,CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var q=new SqlCommand("DELETE dbo.ReferenciasRostroSimulado WHERE (@persona IS NOT NULL AND PersonaId=@persona)OR(@persona IS NULL AND VenceUtc<=@ahora)",c);
        q.Parameters.Add("@persona",SqlDbType.Int).Value=(object?)persona??DBNull.Value;q.Parameters.Add("@ahora",SqlDbType.DateTime2).Value=reloj.GetUtcNow().UtcDateTime;await q.ExecuteNonQueryAsync(token);
    }
}
internal sealed class VerificadorFacialSimulado(IReferenciasRostroSimulado referencias,IConfiguration config,TimeProvider reloj) : IVerificadorFacial
{
    public async Task<ResultadoFacial> VerificarAsync(int persona,Stream captura,CancellationToken token=default)
    {
        if(persona<=0||!captura.CanRead)return new(false,false);
        using var memoria=new MemoriaProtegida();byte[] buffer=new byte[8192];byte[]? imagen=null;double[]? patron=null;ReferenciaRostroSimulado? referencia=null;
        try
        {
            int n;while((n=await captura.ReadAsync(buffer,token))>0)
            {if(memoria.Length+n>PatronVisualSimulado.MaximoBytes)return new(false,false);memoria.Write(buffer,0,n);}
            imagen=memoria.ToArray();patron=PatronVisualSimulado.Extraer(imagen);if(patron is null)return new(false,false);
            referencia=await referencias.ObtenerAsync(persona,token);
            if(referencia is null||referencia.PersonaId!=persona||referencia.Modelo!=PatronVisualSimulado.Modelo||referencia.VenceUtc<=reloj.GetUtcNow())return new(false,false);
            double umbral=config.GetValue("RostroSimulado:UmbralDistancia",0.25d);
            if(!double.IsFinite(umbral)||umbral is <0 or >1)throw new InvalidOperationException("Umbral simulado invalido.");
            double? distancia=ComparadorDescriptoresFaciales.CalcularDistancia(referencia.Patron,patron);
            // Campo legado PruebaDeVidaSuperada: aqui significa validacion
            // SIMULADA de captura. No se realiza ni se afirma prueba de vida fisica.
            return new(distancia.HasValue&&distancia<=umbral,true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);if(imagen is not null)CryptographicOperations.ZeroMemory(imagen);
            if(memoria.TryGetBuffer(out var datos))CryptographicOperations.ZeroMemory(datos.AsSpan());
            if(patron is not null)Array.Clear(patron);if(referencia is not null)Array.Clear(referencia.Patron);
        }
    }
}
