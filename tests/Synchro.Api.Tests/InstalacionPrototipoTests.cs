using Synchro.Api.Database;
namespace Synchro.Api.Tests;
public sealed class InstalacionPrototipoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Server=localhost;Database=SynchroDB;Integrated Security=true")]
    [InlineData("Server=localhost;Database=SynchroDB_Pruebas;AttachDbFilename=C:\\base.mdf;Integrated Security=true")]
    public void RechazaConexionAusentePrincipalYArchivoAdjunto(string? cadena)
        =>Assert.Throws<InvalidOperationException>(()=>InstalacionPrototipo.ExigirConexion(cadena));
    [Fact]
    public void OrdenIncluyeCorreccionDeClientesAntesDelDepositoYNoDuplicaEsquema()
    {
        var archivos=InstalacionPrototipo.Archivos.ToList();
        Assert.Equal(archivos.Count,archivos.Distinct().Count());
        Assert.Equal("Fixtures/EsquemaPrototipo.sql",archivos[0]);
        Assert.True(archivos.FindIndex(x=>x.Contains("ClientesWeb"))<archivos.FindIndex(x=>x.Contains("DepositoEncomienda")));
        Assert.True(archivos.FindIndex(x=>x.Contains("RetiroEncomienda"))<archivos.FindIndex(x=>x.Contains("ReemisionPin")));
        Assert.Equal(2,InstalacionPrototipo.Lotes("SELECT 1;\nGO\nSELECT 2;\ngo\n").Count());
    }
}
