
using Xunit;

namespace Synchro.Api.Tests;

public class EvidenciaFacialRemitenteTests
{
    private static readonly DateTimeOffset Ahora =
        new(2026, 11, 9, 20, 0, 0, TimeSpan.Zero);

    private static EvidenciaFacialRemitente CrearValida()
    {
        return new EvidenciaFacialRemitente(
            Guid.NewGuid(),
            101,
            201,
            301,
            Ahora.AddSeconds(-30),
            Ahora.AddSeconds(90),
            false,
            new ResultadoFacial(true, true));
    }

    private static bool Validar(
        EvidenciaFacialRemitente? evidencia,
        int reserva = 101,
        int remitente = 201,
        int terminal = 301)
    {
        return ValidacionEvidenciaFacialRemitente
            .EsValidaParaContinuar(
                evidencia,
                reserva,
                remitente,
                terminal,
                Ahora);
    }

    [Fact]
    public void EvidenciaCorrecta_CumpleLasReglas()
    {
        Assert.True(Validar(CrearValida()));
    }

    [Fact]
    public void SinEvidencia_DebeRechazar()
    {
        Assert.False(Validar(null));
    }

    [Theory]
    [InlineData(102, 201, 301)]
    [InlineData(101, 202, 301)]
    [InlineData(101, 201, 302)]
    public void OtraReservaPersonaOTerminal_DebeRechazar(
        int reserva,
        int remitente,
        int terminal)
    {
        Assert.False(
            Validar(CrearValida(), reserva, remitente, terminal));
    }

    [Fact]
    public void EvidenciaConsumida_DebeRechazar()
    {
        var evidencia = CrearValida() with
        {
            Consumida = true
        };

        Assert.False(Validar(evidencia));
    }

    [Fact]
    public void EvidenciaVencida_DebeRechazar()
    {
        var evidencia = CrearValida() with
        {
            FechaExpiracionUtc = Ahora.AddSeconds(-1)
        };

        Assert.False(Validar(evidencia));
    }

    [Fact]
    public void EvidenciaEmitidaEnElFuturo_DebeRechazar()
    {
        var evidencia = CrearValida() with
        {
            FechaEmisionUtc = Ahora.AddSeconds(30)
        };

        Assert.False(Validar(evidencia));
    }

    [Fact]
    public void VigenciaSuperiorADosMinutos_DebeRechazar()
    {
        var evidencia = CrearValida() with
        {
            FechaExpiracionUtc = Ahora.AddMinutes(3)
        };

        Assert.False(Validar(evidencia));
    }

    [Fact]
    public void IdentificadorVacio_DebeRechazar()
    {
        var evidencia = CrearValida() with
        {
            EvidenciaId = Guid.Empty
        };

        Assert.False(Validar(evidencia));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void VerificacionFacialIncompleta_DebeRechazar(
        bool rostro,
        bool pruebaVida)
    {
        var evidencia = CrearValida() with
        {
            ResultadoFacial = new ResultadoFacial(
                rostro, pruebaVida)
        };

        Assert.False(Validar(evidencia));
    }

    [Fact]
    public void ExpiracionExacta_DebeRechazar()
    {
        var evidencia = CrearValida() with
        {
            FechaExpiracionUtc = Ahora
        };

        Assert.False(Validar(evidencia));
    }
}
