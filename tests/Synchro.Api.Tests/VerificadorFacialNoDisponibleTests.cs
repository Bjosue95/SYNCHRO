using Xunit;
using Microsoft.Extensions.DependencyInjection;
namespace Synchro.Api.Tests;

public sealed class VerificadorFacialNoDisponibleTests
{
    [Fact]
    public async Task RechazaVerificacionSinProveedorBiometrico()
    {
        var verificador =
            new VerificadorFacialNoDisponible();

        using var captura = new MemoryStream();

        ResultadoFacial resultado =
            await verificador.VerificarAsync(
                destinatarioId: 1,
                capturaTemporal: captura);

        Assert.False(resultado.RostroCoincide);

        Assert.False(resultado.PruebaDeVidaSuperada);

        Assert.False(resultado.Aprobado);
    }

    [Fact]
    public async Task BackendUtilizaVerificadorFacialSeguro()
    {
        using var fabrica = new FabricaSynchroPruebas();

        var verificador = fabrica.Services
            .GetRequiredService<IVerificadorFacial>();

        Assert.IsType<VerificadorFacialNoDisponible>(
            verificador);

        using var captura = new MemoryStream();

        ResultadoFacial resultado =
            await verificador.VerificarAsync(
                destinatarioId: 1,
                capturaTemporal: captura);

        Assert.False(resultado.RostroCoincide);
        Assert.False(resultado.PruebaDeVidaSuperada);
        Assert.False(resultado.Aprobado);
    }

}
