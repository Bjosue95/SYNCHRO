
using Microsoft.Extensions.Logging;

namespace Synchro.Api.Services.WhatsApp;

internal enum ResultadoEjecucionInicioWhatsApp
{
    SinSolicitudes,
    Preparada,
    NoPreparada,
    Indeterminada,
    CierreNoConfirmado
}

// Coordina una sola solicitud por ejecucion.
// No se ejecuta automaticamente ni envia mensajes.
internal sealed class EjecutorSolicitudesInicioWhatsApp(
    ReclamadorSolicitudesInicioWhatsAppSql reclamador,
    ProcesadorSolicitudesInicioWhatsAppSql procesador,
    CierreFalloSolicitudesInicioWhatsAppSql cierre,
    ILogger<EjecutorSolicitudesInicioWhatsApp> logger)
{
    public async Task<ResultadoEjecucionInicioWhatsApp>
        EjecutarUnaAsync(
            CancellationToken cancelacion = default)
    {
        // Reclamar una sola solicitud.
        // Si no hay pendientes, no modificar nada.
        SolicitudInicioWhatsAppTomada? solicitud =
            await reclamador.TomarAsync(cancelacion);

        if (solicitud is null)
        {
            return ResultadoEjecucionInicioWhatsApp
                .SinSolicitudes;
        }

        try
        {
            // El procesador valida el arrendamiento
            // y prepara la verificacion en SQL.
            bool preparada =
                await procesador.PrepararAsync(
                    solicitud,
                    cancelacion);

            if (!preparada)
            {
                // No intentar cerrar una solicitud
                // cuyo arrendamiento ya no es valido.
                return ResultadoEjecucionInicioWhatsApp
                    .NoPreparada;
            }

            logger.LogInformation(
                "Solicitud WhatsApp {EventoId} preparada.",
                solicitud.EventoId);

            return ResultadoEjecucionInicioWhatsApp
                .Preparada;
        }
        catch (OperationCanceledException)
            when (cancelacion.IsCancellationRequested)
        {
            // Durante el apagado, no afirmar que
            // una operacion fue confirmada o fallida.
            throw;
        }
        catch (Exception error)
        {
            // Una excepcion no demuestra por si sola
            // que la transaccion SQL no se confirmo.
            // Por seguridad, no reintentar aqui.
            logger.LogWarning(
                error,
                "Resultado incierto al preparar " +
                "solicitud WhatsApp {EventoId}.",
                solicitud.EventoId);

            try
            {
                // El cierre comprueba nuevamente
                // el arrendamiento y que no exista
                // una verificacion ya vinculada.
                bool cerrada = await cierre.CerrarAsync(
                    solicitud,
                    ResultadoFalloSolicitudInicio
                        .Indeterminado,
                    cancelacion);

                if (cerrada)
                {
                    return ResultadoEjecucionInicioWhatsApp
                        .Indeterminada;
                }

                // Si ya esta Preparada, vencida o
                // pertenece a otro proceso, no
                // modificar su estado.
                return ResultadoEjecucionInicioWhatsApp
                    .CierreNoConfirmado;
            }
            catch (OperationCanceledException)
                when (cancelacion.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception errorCierre)
            {
                logger.LogError(
                    errorCierre,
                    "No se pudo confirmar el cierre " +
                    "de solicitud WhatsApp {EventoId}.",
                    solicitud.EventoId);

                // El arrendamiento existente permitira
                // revisar posteriormente el resultado.
                return ResultadoEjecucionInicioWhatsApp
                    .CierreNoConfirmado;
            }
        }
    }
}
