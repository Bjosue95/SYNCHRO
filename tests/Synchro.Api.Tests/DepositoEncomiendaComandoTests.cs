using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;

namespace Synchro.Api.Tests;

public sealed class DepositoEncomiendaComandoTests
{
    [Theory]
    [InlineData(0, 1, "S01")]
    [InlineData(1, 0, "S01")]
    [InlineData(-1, 1, "S01")]
    [InlineData(1, 1, "")]
    [InlineData(1, 1, " ")]
    [InlineData(1, 1, "S01';--")]
    [InlineData(1, 1, "S01234567890")]
    public void RechazaDatosInvalidos(int reserva, int terminal, string codigo)
        => Assert.Throws<ArgumentException>(() => DepositoEncomiendaService.Validar(
            new(reserva, terminal, Guid.NewGuid(), codigo)));

    [Fact]
    public void RechazaEvidenciaVacia()
        => Assert.Throws<ArgumentException>(() => DepositoEncomiendaService.Validar(
            new(1, 2, Guid.Empty, "S01")));

    [Theory]
    [InlineData("S01")]
    [InlineData("TST-12")]
    public void AceptaIdentificadoresValidos(string codigo)
        => DepositoEncomiendaService.Validar(new(1, 2, Guid.NewGuid(), codigo));

    [Theory]
    [InlineData("SynchroDB")]
    [InlineData("master")]
    [InlineData("SynchroDB_Detalle_Pruebas")]
    public async Task NoAbreUnaBaseAjena(string nombre)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SynchroDB"] = $"Server=127.0.0.1;Database={nombre}"
        }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DepositoEncomiendaService(config)
            .DepositarAsync(new(1, 2, Guid.NewGuid(), "S01")));
    }

    [Fact]
    public async Task CancelacionNoIniciaUnaOperacion()
    {
        var config = new ConfigurationBuilder().Build();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DepositoEncomiendaService(config)
            .DepositarAsync(new(1, 2, Guid.NewGuid(), "S01"), new CancellationToken(true)));
    }

    [Theory]
    [InlineData("SynchroDB_Pruebas")]
    [InlineData("SynchroDB_Detalle_Pruebas")]
    public void ConexionSoloAdmiteBasesAisladas(string nombre)
    {
        string cadena = $"Server=127.0.0.1;Database={nombre}";
        Assert.Equal(cadena, ConexionSqlAislada.Exigir(cadena, nombre));
    }

    [Fact]
    public void NoPermiteAdjuntarArchivoNiAutorizarBasePrincipal()
    {
        Assert.Throws<InvalidOperationException>(() => ConexionSqlAislada.Exigir(
            "Server=.;Database=SynchroDB_Pruebas;AttachDbFilename=C:\\datos.mdf", "SynchroDB_Pruebas"));
        Assert.Throws<InvalidOperationException>(() => ConexionSqlAislada.Exigir(
            "Server=.;Database=SynchroDB", "SynchroDB"));
    }
}
