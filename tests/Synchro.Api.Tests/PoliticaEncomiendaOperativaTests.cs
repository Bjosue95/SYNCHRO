using Synchro.Api.Services.Encomiendas;

namespace Synchro.Api.Tests;

public sealed class PoliticaEncomiendaOperativaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private static CondicionesEncomiendaOperativa Encomienda => new(17, 101, 202,
        EstadoEncomiendaOperativa.ListaParaDeposito, true,
        EstadoConsentimientoEncomienda.Confirmado, EstadoConsentimientoEncomienda.Confirmado,
        true, EstadoPagoEncomienda.ConfirmadoManual, Ahora.AddHours(1));
    private static CondicionesPinEncomiendaOperativa Pin => new(
        FinalidadPinEncomienda.Deposito, true, true, false, false, 0, 5, Ahora.AddMinutes(30));
    private static EvidenciaEncomiendaOperativa Evidencia => new(Guid.NewGuid(), 17, 7, 101,
        TipoEvidenciaEncomienda.VerificacionRemitente, Ahora.AddSeconds(-10), Ahora.AddSeconds(110), false, true, true);

    private static DecisionEncomiendaOperativa Depositar(
        CondicionesEncomiendaOperativa? encomienda = null,
        CondicionesPinEncomiendaOperativa? pin = null,
        EvidenciaEncomiendaOperativa? evidencia = null,
        int terminal = 7, bool activa = true, bool autorizada = true, bool disponible = true) =>
        PoliticaEncomiendaOperativa.Depositar(encomienda ?? Encomienda, pin ?? Pin,
            evidencia ?? Evidencia, terminal, activa, autorizada, disponible, Ahora);

    private static DecisionEncomiendaOperativa Retirar(
        CondicionesEncomiendaOperativa? encomienda = null,
        CondicionesPinEncomiendaOperativa? pin = null,
        EvidenciaEncomiendaOperativa? evidencia = null,
        bool terminalCorresponde = true) => PoliticaEncomiendaOperativa.Retirar(
            encomienda ?? Encomienda with { Estado = EstadoEncomiendaOperativa.Depositada },
            pin ?? Pin with { Finalidad = FinalidadPinEncomienda.Retiro },
            evidencia ?? Evidencia with { Tipo = TipoEvidenciaEncomienda.CapturaDestinatario, PersonaId = 202, ComparacionRemitenteAprobada = false },
            7, true, terminalCorresponde, Ahora);

    [Fact]
    public void DepositoExigeTodosLosFactoresDelServidor() => Assert.True(Depositar().Permitida);

    [Theory]
    [InlineData("Remitente", "Pendiente")]
    [InlineData("Remitente", "Rechazado")]
    [InlineData("Remitente", "Revocado")]
    [InlineData("Remitente", "Vencido")]
    [InlineData("Destinatario", "Pendiente")]
    [InlineData("Destinatario", "Rechazado")]
    [InlineData("Destinatario", "Revocado")]
    [InlineData("Destinatario", "Vencido")]
    public void ConsentimientosSonIndependientes(string rol, string estado)
    {
        var consentimiento = Enum.Parse<EstadoConsentimientoEncomienda>(estado);
        var datos = rol == "Remitente" ? Encomienda with { ConsentimientoRemitente = consentimiento }
            : Encomienda with { ConsentimientoDestinatario = consentimiento };
        var decision = Depositar(datos);
        Assert.False(decision.Permitida);
        Assert.Equal($"Consentimiento{rol}Pendiente", decision.Codigo);
    }

    [Theory]
    [InlineData("NoConfigurado")]
    [InlineData("Pendiente")]
    [InlineData("ConfirmadoSimulado")]
    [InlineData("Rechazado")]
    public void PagoSimuladoOAusenteNuncaHabilitaDeposito(string estado)
    {
        var decision = Depositar(Encomienda with { Pago = Enum.Parse<EstadoPagoEncomienda>(estado) });
        Assert.False(decision.Permitida);
        Assert.Equal("PagoNoConfirmado", decision.Codigo);
    }

    [Fact]
    public void AceptacionDelDestinatarioNoSeDeduceDelConsentimientoDeMensajes()
    {
        Assert.Equal("AceptacionDestinatarioPendiente", Depositar(Encomienda with { DestinatarioAceptoEncomienda = false }).Codigo);
    }

    [Fact]
    public void ReservaVencidaNoAutorizaDeposito()
    {
        Assert.Equal("ReservaVencida", Depositar(Encomienda with { ReservaVenceUtc = Ahora }).Codigo);
    }

    [Fact]
    public void PinRetiroNoSirveParaDepositarYPinDepositoNoSirveParaRetirar()
    {
        Assert.Equal("PinNoValido", Depositar(pin: Pin with { Finalidad = FinalidadPinEncomienda.Retiro }).Codigo);
        Assert.Equal("PinNoValido", Retirar(pin: Pin).Codigo);
    }

    [Fact]
    public void PinIncorrectoConsumidoRevocadoNoEntregadoBloqueadoOVencidoSeRechaza()
    {
        foreach (var pin in new[] {
            Pin with { HashVerificado = false }, Pin with { Consumido = true },
            Pin with { Revocado = true }, Pin with { EntregaManualConfirmada = false },
            Pin with { IntentosFallidos = 5 }, Pin with { MaximoIntentos = 0 },
            Pin with { MaximoIntentos = 6 },
            Pin with { IntentosFallidos = -1 }, Pin with { VenceUtc = Ahora }
        }) Assert.False(Depositar(pin: pin).Permitida);
    }

    [Fact]
    public void WebNoPuedeSustituirLaAutorizacionDeUnaTerminalNiLaDisponibilidad()
    {
        Assert.Equal("SolicitudTerminalNoAutorizada", Depositar(autorizada: false).Codigo);
        Assert.Equal("CasilleroNoDisponible", Depositar(disponible: false).Codigo);
        Assert.Equal("TerminalNoAutorizada", Depositar(terminal: 8).Codigo);
        Assert.Equal("TerminalNoAutorizada", Depositar(activa: false).Codigo);
        Assert.Equal("TerminalNoAutorizada", Depositar(terminal: 0).Codigo);
    }

    [Fact]
    public void EvidenciaNoSePuedeReutilizarEnOtraReservaFinalidadOTerminal()
    {
        foreach (var evidencia in new[] {
            Evidencia with { Consumida = true }, Evidencia with { ReservaId = 18 },
            Evidencia with { TerminalId = 8 }, Evidencia with { Id = Guid.Empty },
            Evidencia with { PersonaId = 202 },
            Evidencia with { Tipo = TipoEvidenciaEncomienda.CapturaDestinatario }
        }) Assert.False(Depositar(evidencia: evidencia).Permitida);
    }

    [Fact]
    public void EvidenciaVencidaFuturaOConDuracionExcesivaNoAutorizaOperacion()
    {
        foreach (var evidencia in new[] {
            Evidencia with { VenceUtc = Ahora }, Evidencia with { EmitidaUtc = Ahora.AddSeconds(1) },
            Evidencia with { VenceUtc = Ahora.AddMinutes(3) }
        }) Assert.Equal("EvidenciaVencida", Depositar(evidencia: evidencia).Codigo);
    }

    [Fact]
    public void CapturaSinConsentimientoBiometricoNoHabilitaDepositoNiRetiro()
    {
        Assert.Equal("ConsentimientoBiometricoPendiente", Depositar(evidencia: Evidencia with { ConsentimientoBiometricoRegistrado = false }).Codigo);
        Assert.Equal("ConsentimientoBiometricoPendiente", Retirar(evidencia: Evidencia with {
            Tipo = TipoEvidenciaEncomienda.CapturaDestinatario, PersonaId = 202, ConsentimientoBiometricoRegistrado = false
        }).Codigo);
    }

    [Fact]
    public void RemitenteRequiereComparacionAprobadaPorServidor()
    {
        Assert.Equal("IdentidadRemitenteNoVerificada", Depositar(evidencia: Evidencia with { ComparacionRemitenteAprobada = false }).Codigo);
    }

    [Fact]
    public void DestinatarioSeCapturaComoEvidenciaSinExigirCoincidenciaBiometrica()
    {
        Assert.True(Retirar().Permitida);
    }

    [Fact]
    public void LosParticipantesSeIdentificanPorSeparadoYLaCapturaNoSeAtribuyeAOtraPersona()
    {
        Assert.Equal("ParticipantesNoValidos", Depositar(Encomienda with { DestinatarioId = 101 }).Codigo);
        Assert.Equal("ParticipantesNoValidos", Depositar(Encomienda with { RemitenteId = 0 }).Codigo);
        Assert.Equal("EvidenciaNoDisponible", Retirar(evidencia: Evidencia with {
            Tipo = TipoEvidenciaEncomienda.CapturaDestinatario, PersonaId = 101
        }).Codigo);
    }

    [Fact]
    public void RetiroExigeDepositoPrevioYLaTerminalCorrespondiente()
    {
        Assert.Equal("EncomiendaNoDepositada", Retirar(Encomienda).Codigo);
        Assert.Equal("TerminalNoCorresponde", Retirar(terminalCorresponde: false).Codigo);
        Assert.Equal("EncomiendaNoDepositada", Retirar(Encomienda with { Estado = EstadoEncomiendaOperativa.Retirada }).Codigo);
    }

    [Fact]
    public void RetiroUtilizaElPagoAnteriorSinCobrarNuevamenteNiExigirPreReservaVigente()
    {
        var yaDepositada = Encomienda with { Estado = EstadoEncomiendaOperativa.Depositada,
            ReservaVenceUtc = Ahora.AddDays(-5), CuentaRemitenteActiva = false };
        Assert.True(Retirar(yaDepositada).Permitida);
        Assert.Equal("PagoNoConfirmado", Retirar(yaDepositada with { Pago = EstadoPagoEncomienda.Pendiente }).Codigo);
    }
}
