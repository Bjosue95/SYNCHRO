namespace Synchro.Api.Models;


public class TarifaCarga
{
    public string TipoDispositivo { get; set; } = "";
    public int MinutosBase { get; set; }
    public decimal PrecioBase { get; set; }
    public int MinutosAdicionales { get; set; }
    public decimal PrecioAdicional { get; set; }
}