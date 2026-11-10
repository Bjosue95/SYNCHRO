
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Synchro.Api.Tests;

public class ConfiguracionFacialServidorTests
{
    [Fact]
    public void Servidor_UtilizaVerificadorNoDisponible()
    {
        using var fabrica = new FabricaTerminalCookieReal();

        var verificador = fabrica.Services
            .GetRequiredService<IVerificadorFacial>();

        Assert.IsType<VerificadorFacialNoDisponible>(
            verificador);
    }

    [Fact]
    public async Task ConfiguracionActual_NoEmiteEvidenciaFacial()
    {
        using var fabrica = new FabricaTerminalCookieReal();

        var verificador = fabrica.Services
            .GetRequiredService<IVerificadorFacial>();

        var preparador =
            new PreparadorEvidenciaFacialRemitente(
                verificador,
                TimeProvider.System);

        using var captura = new MemoryStream(
            new byte[] { 1, 2, 3 });

        var evidencia = await preparador.PrepararAsync(
            101,
            201,
            301,
            captura);

        Assert.Null(evidencia);
    }
}
