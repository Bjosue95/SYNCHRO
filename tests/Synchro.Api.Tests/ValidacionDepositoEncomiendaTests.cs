
using Synchro.Api.Services;
using Xunit;

namespace Synchro.Api.Tests;

public class ValidacionDepositoEncomiendaTests
{
    private static readonly DateTime Ahora =
        new(2026, 11, 9, 20, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("PendienteConsentimiento", true)]
    [InlineData("Vencida", false)]
    [InlineData("Cancelada", false)]
    [InlineData("Confirmado", false)]
    public void Reserva_DebeTenerEstadoValido(
        string estado, bool esperado)
    {
        bool resultado =
            ValidacionDepositoEncomienda.ReservaVigente(
                estado,
                Ahora.AddHours(1),
                Ahora);

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Reserva_DebeEstarDentroDeSuVigencia(
        int minutos, bool esperado)
    {
        bool resultado =
            ValidacionDepositoEncomienda.ReservaVigente(
                "PendienteConsentimiento",
                Ahora.AddMinutes(minutos),
                Ahora);

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("Confirmado", true)]
    [InlineData("Pendiente", false)]
    [InlineData("Revocado", false)]
    [InlineData("Rechazado", false)]
    public void Consentimiento_DebeEstarConfirmado(
        string estado, bool esperado)
    {
        bool resultado =
            ValidacionDepositoEncomienda
                .ConsentimientoConfirmado(
                    estado,
                    Ahora,
                    Ahora.AddMinutes(-5),
                    Ahora.AddMinutes(10), Ahora);

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Consentimiento_SinFechaConfirmacion_DebeRechazarse()
    {
        bool resultado =
            ValidacionDepositoEncomienda
                .ConsentimientoConfirmado(
                    "Confirmado",
                    null,
                    Ahora.AddMinutes(-5),
                    Ahora.AddMinutes(10), Ahora);

        Assert.False(resultado);
    }

    [Fact]
    public void Consentimiento_ConfirmadoFueraDePlazo_DebeRechazarse()
    {
        bool resultado =
            ValidacionDepositoEncomienda
                .ConsentimientoConfirmado(
                    "Confirmado",
                    Ahora,
                    Ahora.AddMinutes(-20),
                    Ahora.AddMinutes(-5), Ahora);

        Assert.False(resultado);
    }

    [Theory]
    [InlineData(-1,true)]
    [InlineData(0,true)]
    [InlineData(1,false)]
    public void Consentimiento_NoPuedeConfirmarseEnElFuturo(int minutos,bool esperado)
    {
        Assert.Equal(esperado,ValidacionDepositoEncomienda.ConsentimientoConfirmado(
            "Confirmado",Ahora.AddMinutes(minutos),Ahora.AddMinutes(-5),Ahora.AddMinutes(10),Ahora));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Continuar_RequiereAmbasCondiciones(
        bool reserva,
        bool consentimiento,
        bool esperado)
    {
        bool resultado =
            ValidacionDepositoEncomienda
                .PuedeContinuarVerificacion(
                    reserva, consentimiento);

        Assert.Equal(esperado, resultado);
    }
}
