using Synchro.Api.Services.Clientes;
namespace Synchro.Api.Tests;
internal sealed class ClientesWebSimulados : IRepositorioClientesWeb
{
    private readonly object _sync=new();
    public Dictionary<int,CuentaClienteWeb> Cuentas {get;}=[];
    public Task<CuentaClienteWeb?> BuscarAsync(string nombre,CancellationToken token)
    {lock(_sync)return Task.FromResult(Cuentas.Values.SingleOrDefault(c=>string.Equals(c.NombreUsuario,nombre,StringComparison.OrdinalIgnoreCase)));}
    public Task<CuentaClienteWeb?> BuscarAsync(int persona,CancellationToken token)
    {lock(_sync)return Task.FromResult(Cuentas.GetValueOrDefault(persona));}
    public Task CrearAsync(CuentaClienteWeb cuenta,CancellationToken token)
    {
        lock(_sync)
        {if(Cuentas.Values.Any(c=>string.Equals(c.NombreUsuario,cuenta.NombreUsuario,StringComparison.OrdinalIgnoreCase)))throw new CuentaClienteNoDisponibleException();
         cuenta.PersonaId=Cuentas.Count==0?41:Cuentas.Keys.Max()+1;Cuentas.Add(cuenta.PersonaId,cuenta);return Task.CompletedTask;}
    }
}
