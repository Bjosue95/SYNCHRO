internal readonly record struct CondicionesDepositoEncomienda(
    bool ReservaVigente,
    bool ConsentimientoConfirmado,
    bool TerminalAutorizada,
    bool RemitenteVinculadoAReserva,
    bool CasilleroDisponible,
    ResultadoFacial? VerificacionFacialServidor
);

internal static class PoliticaDepositoEncomienda
{
    // Todas las condiciones deben proceder de verificaciones
    // del servidor. Nunca del contenido enviado por el navegador.
    public static bool PuedeAutorizar(
        CondicionesDepositoEncomienda condiciones)
    {
        return condiciones.ReservaVigente
            && condiciones.ConsentimientoConfirmado
            && condiciones.TerminalAutorizada
            && condiciones.RemitenteVinculadoAReserva
            && condiciones.CasilleroDisponible
            && condiciones.VerificacionFacialServidor
                is { Aprobado: true };
    }
}
