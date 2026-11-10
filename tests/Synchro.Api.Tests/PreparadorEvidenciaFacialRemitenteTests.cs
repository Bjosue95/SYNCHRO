
using Xunit;

namespace Synchro.Api.Tests;

public class PreparadorEvidenciaFacialRemitenteTests
{
    // Simulador exclusivo de estas pruebas.
    // No se registra como proveedor real en SYNCHRO.
    private sealed class VerificadorFicticio : IVerificadorFacial
    {
        private readonly ResultadoFacial _resultado;

        public int Llamadas { get; private set; }

        public VerificadorFicticio(
            bool rostroCoincide,
            bool pruebaVida)
        {
            _resultado = new ResultadoFacial(
                rostroCoincide, pruebaVida);
        }

        public Task<ResultadoFacial> VerificarAsync(
            int destinatarioId,
            Stream capturaTemporal,
            CancellationToken cancelacion = default)
        {
            cancelacion.ThrowIfCancellationRequested();
            Llamadas++;

            return Task.FromResult(_resultado);
        }
    }

    private static MemoryStream CapturaFicticia()
    {
        return new MemoryStream(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task SinProveedorReal_NoDebeEmitirEvidencia()
    {
        var preparador = new PreparadorEvidenciaFacialRemitente(
            new VerificadorFacialNoDisponible(),
            TimeProvider.System);

        using var captura = CapturaFicticia();

        var evidencia = await preparador.PrepararAsync(
            101, 201, 301, captura);

        Assert.Null(evidencia);
    }

    [Theory]
    [InlineData(0, 201, 301)]
    [InlineData(101, 0, 301)]
    [InlineData(101, 201, 0)]
    public async Task IdentificadoresInvalidos_NoDebeVerificar(
        int reserva,
        int remitente,
        int terminal)
    {
        var verificador = new VerificadorFicticio(true, true);

        var preparador = new PreparadorEvidenciaFacialRemitente(
            verificador, TimeProvider.System);

        using var captura = CapturaFicticia();

        var evidencia = await preparador.PrepararAsync(
            reserva, remitente, terminal, captura);

        Assert.Null(evidencia);
        Assert.Equal(0, verificador.Llamadas);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task BiometriaIncompleta_NoDebeEmitir(
        bool rostro,
        bool pruebaVida)
    {
        var preparador = new PreparadorEvidenciaFacialRemitente(
            new VerificadorFicticio(rostro, pruebaVida),
            TimeProvider.System);

        using var captura = CapturaFicticia();

        var evidencia = await preparador.PrepararAsync(
            101, 201, 301, captura);

        Assert.Null(evidencia);
    }

    [Fact]
    public async Task ResultadoSimuladoAprobado_PreparaDatosCorrectos()
    {
        var preparador = new PreparadorEvidenciaFacialRemitente(
            new VerificadorFicticio(true, true),
            TimeProvider.System);

        using var captura = CapturaFicticia();

        var evidencia = await preparador.PrepararAsync(
            101, 201, 301, captura);

        Assert.True(evidencia.HasValue);

        var resultado = evidencia.Value;

        Assert.NotEqual(Guid.Empty, resultado.EvidenciaId);
        Assert.Equal(101, resultado.ReservaId);
        Assert.Equal(201, resultado.RemitenteId);
        Assert.Equal(301, resultado.TerminalId);
        Assert.False(resultado.Consumida);
        Assert.True(resultado.ResultadoFacial.Aprobado);

        Assert.Equal(
            TimeSpan.FromMinutes(2),
            resultado.FechaExpiracionUtc -
                resultado.FechaEmisionUtc);
    }

    [Fact]
    public async Task CapturaVacia_NoDebeEmitir()
    {
        var verificador = new VerificadorFicticio(true, true);

        var preparador = new PreparadorEvidenciaFacialRemitente(
            verificador, TimeProvider.System);

        using var captura = new MemoryStream();

        var evidencia = await preparador.PrepararAsync(
            101, 201, 301, captura);

        Assert.Null(evidencia);
        Assert.Equal(0, verificador.Llamadas);
    }

    [Fact]
    public async Task Cancelacion_NoDebeContinuar()
    {
        var preparador = new PreparadorEvidenciaFacialRemitente(
            new VerificadorFicticio(true, true),
            TimeProvider.System);

        using var captura = CapturaFicticia();
        using var cancelacion = new CancellationTokenSource();

        cancelacion.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await preparador.PrepararAsync(
                101, 201, 301,
                captura,
                cancelacion.Token));
    }
}
