namespace Synchro.Api.Services.Encomiendas;

internal enum EstadoEncomiendaOperativa { Preparacion, ListaParaDeposito, Depositada, Retirada, Rechazada, Cancelada }
internal enum EstadoConsentimientoEncomienda { Pendiente, Confirmado, Rechazado, Revocado, Vencido }
internal enum EstadoPagoEncomienda { NoConfigurado, Pendiente, ConfirmadoManual, ConfirmadoSimulado, Rechazado }
internal enum FinalidadPinEncomienda { Deposito, Retiro }
internal enum TipoEvidenciaEncomienda { VerificacionRemitente, CapturaDestinatario }

// Contextos internos construidos a partir de datos del servidor. No son contratos HTTP.
internal sealed record EvidenciaEncomiendaOperativa(
    Guid Id, int ReservaId, int TerminalId, int PersonaId, TipoEvidenciaEncomienda Tipo,
    DateTimeOffset EmitidaUtc, DateTimeOffset VenceUtc, bool Consumida,
    bool ConsentimientoBiometricoRegistrado, bool ComparacionRemitenteAprobada);

internal sealed record CondicionesEncomiendaOperativa(
    int ReservaId,
    int RemitenteId,
    int DestinatarioId,
    EstadoEncomiendaOperativa Estado,
    bool CuentaRemitenteActiva,
    EstadoConsentimientoEncomienda ConsentimientoRemitente,
    EstadoConsentimientoEncomienda ConsentimientoDestinatario,
    bool DestinatarioAceptoEncomienda,
    EstadoPagoEncomienda Pago,
    DateTimeOffset ReservaVenceUtc,bool UsoPersonal=false,bool PagoDiferidoDestinatario=false);

internal sealed record CondicionesPinEncomiendaOperativa(
    FinalidadPinEncomienda Finalidad,
    bool HashVerificado,
    bool EntregaManualConfirmada,
    bool Consumido,
    bool Revocado,
    int IntentosFallidos,
    int MaximoIntentos,
    DateTimeOffset VenceUtc);

internal sealed record DecisionEncomiendaOperativa(bool Permitida, string Codigo)
{
    public static readonly DecisionEncomiendaOperativa Autorizar = new(true, "Permitida");
    public static DecisionEncomiendaOperativa Rechazar(string codigo) => new(false, codigo);
}

internal static class PoliticaEncomiendaOperativa
{
    public static DecisionEncomiendaOperativa Depositar(
        CondicionesEncomiendaOperativa encomienda,
        CondicionesPinEncomiendaOperativa pin,
        EvidenciaEncomiendaOperativa evidencia,
        int terminalAutenticada,
        bool terminalActiva,
        bool solicitudDepositoAutorizada,
        bool casilleroDisponible,
        DateTimeOffset ahora)
    {
        if (encomienda.ReservaId <= 0 || encomienda.Estado is not
            (EstadoEncomiendaOperativa.Preparacion or EstadoEncomiendaOperativa.ListaParaDeposito))
            return DecisionEncomiendaOperativa.Rechazar("EstadoNoDepositable");
        if (encomienda.ReservaVenceUtc <= ahora)
            return DecisionEncomiendaOperativa.Rechazar("ReservaVencida");
        var participantes = ValidarParticipantes(encomienda);
        if (!participantes.Permitida) return participantes;
        // Solo encomiendas cuyo destinatario pagará en Tablet pueden depositarse
        // antes del cobro. Retirar() continúa exigiendo confirmación administrativa.
        if (encomienda.Pago != EstadoPagoEncomienda.ConfirmadoManual && !encomienda.PagoDiferidoDestinatario)
            return DecisionEncomiendaOperativa.Rechazar("PagoNoConfirmado");
        var resultadoPin = ValidarPin(pin, FinalidadPinEncomienda.Deposito, ahora);
        if (!resultadoPin.Permitida) return resultadoPin;
        var resultadoEvidencia = ValidarEvidencia(evidencia, encomienda.ReservaId,
            encomienda.RemitenteId, terminalAutenticada, terminalActiva, TipoEvidenciaEncomienda.VerificacionRemitente, ahora);
        if (!resultadoEvidencia.Permitida) return resultadoEvidencia;
        if (!solicitudDepositoAutorizada)
            return DecisionEncomiendaOperativa.Rechazar("SolicitudTerminalNoAutorizada");
        return casilleroDisponible ? DecisionEncomiendaOperativa.Autorizar
            : DecisionEncomiendaOperativa.Rechazar("CasilleroNoDisponible");
    }

    public static DecisionEncomiendaOperativa Retirar(
        CondicionesEncomiendaOperativa encomienda,
        CondicionesPinEncomiendaOperativa pin,
        EvidenciaEncomiendaOperativa captura,
        int terminalAutenticada,
        bool terminalActiva,
        bool terminalCorrespondeAlDeposito,
        DateTimeOffset ahora)
    {
        if (encomienda.ReservaId <= 0 || encomienda.Estado != EstadoEncomiendaOperativa.Depositada)
            return DecisionEncomiendaOperativa.Rechazar("EncomiendaNoDepositada");
        // Se verifica el pago anterior, sin crear ni exigir un segundo cobro al destinatario.
        if (encomienda.Pago != EstadoPagoEncomienda.ConfirmadoManual)
            return DecisionEncomiendaOperativa.Rechazar("PagoNoConfirmado");
        if (!terminalCorrespondeAlDeposito)
            return DecisionEncomiendaOperativa.Rechazar("TerminalNoCorresponde");
        // La baja posterior del remitente o el vencimiento de la pre-reserva no
        // deben inmovilizar una encomienda ya depositada. No se envían nuevos mensajes aquí.
        if (encomienda.ConsentimientoDestinatario != EstadoConsentimientoEncomienda.Confirmado ||
            !encomienda.DestinatarioAceptoEncomienda)
            return DecisionEncomiendaOperativa.Rechazar("AceptacionDestinatarioPendiente");
        var resultadoPin = ValidarPin(pin, FinalidadPinEncomienda.Retiro, ahora);
        if (!resultadoPin.Permitida) return resultadoPin;
        // Captura con consentimiento y finalidad de evidencia; no representa coincidencia de identidad.
        return ValidarEvidencia(captura, encomienda.ReservaId, encomienda.DestinatarioId, terminalAutenticada,
            terminalActiva, TipoEvidenciaEncomienda.CapturaDestinatario, ahora);
    }

    private static DecisionEncomiendaOperativa ValidarParticipantes(CondicionesEncomiendaOperativa encomienda)
    {
        if (encomienda.RemitenteId <= 0 || encomienda.DestinatarioId <= 0 || !encomienda.UsoPersonal&&encomienda.RemitenteId == encomienda.DestinatarioId)
            return DecisionEncomiendaOperativa.Rechazar("ParticipantesNoValidos");
        if (!encomienda.CuentaRemitenteActiva)
            return DecisionEncomiendaOperativa.Rechazar("CuentaRemitenteInactiva");
        if (encomienda.ConsentimientoRemitente != EstadoConsentimientoEncomienda.Confirmado)
            return DecisionEncomiendaOperativa.Rechazar("ConsentimientoRemitentePendiente");
        // La aceptación del destinatario se solicita únicamente después del depósito.
        // Este método valida las condiciones del remitente para DEPOSITAR.
        return DecisionEncomiendaOperativa.Autorizar;
    }

    private static DecisionEncomiendaOperativa ValidarPin(
        CondicionesPinEncomiendaOperativa pin, FinalidadPinEncomienda finalidad, DateTimeOffset ahora)
    {
        if (pin.Finalidad != finalidad || !pin.HashVerificado)
            return DecisionEncomiendaOperativa.Rechazar("PinNoValido");
        if (pin.Consumido || pin.Revocado)
            return DecisionEncomiendaOperativa.Rechazar("PinNoDisponible");
        if (!pin.EntregaManualConfirmada)
            return DecisionEncomiendaOperativa.Rechazar("EntregaPinNoConfirmada");
        if (pin.MaximoIntentos is < 1 or > 5 || pin.IntentosFallidos < 0 || pin.IntentosFallidos >= pin.MaximoIntentos)
            return DecisionEncomiendaOperativa.Rechazar("PinBloqueado");
        return pin.VenceUtc > ahora ? DecisionEncomiendaOperativa.Autorizar
            : DecisionEncomiendaOperativa.Rechazar("PinVencido");
    }

    private static DecisionEncomiendaOperativa ValidarEvidencia(
        EvidenciaEncomiendaOperativa evidencia, int reserva, int persona, int terminal, bool terminalActiva,
        TipoEvidenciaEncomienda finalidad, DateTimeOffset ahora)
    {
        if (terminal <= 0 || !terminalActiva || evidencia.TerminalId != terminal)
            return DecisionEncomiendaOperativa.Rechazar("TerminalNoAutorizada");
        if (evidencia.Id == Guid.Empty || evidencia.ReservaId != reserva || persona <= 0 || evidencia.PersonaId != persona || evidencia.Tipo != finalidad || evidencia.Consumida)
            return DecisionEncomiendaOperativa.Rechazar("EvidenciaNoDisponible");
        if (!evidencia.ConsentimientoBiometricoRegistrado)
            return DecisionEncomiendaOperativa.Rechazar("ConsentimientoBiometricoPendiente");
        if (evidencia.EmitidaUtc > ahora || evidencia.VenceUtc <= ahora ||
            evidencia.VenceUtc <= evidencia.EmitidaUtc || evidencia.VenceUtc - evidencia.EmitidaUtc > TimeSpan.FromMinutes(2))
            return DecisionEncomiendaOperativa.Rechazar("EvidenciaVencida");
        return DecisionEncomiendaOperativa.Autorizar;
    }
}
