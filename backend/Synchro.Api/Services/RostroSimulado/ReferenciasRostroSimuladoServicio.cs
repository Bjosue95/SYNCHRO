namespace Synchro.Api.Services.RostroSimulado;

internal static class ConfiguracionRostroSimulado
{
    public static bool EntornoPermitido(IHostEnvironment entorno,IConfiguration config)
    {
        if(!entorno.IsDevelopment()||!config.GetValue("RostroSimulado:Habilitado",true))return false;
        try{ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");return true;}
        catch(InvalidOperationException){return false;}
    }
}
