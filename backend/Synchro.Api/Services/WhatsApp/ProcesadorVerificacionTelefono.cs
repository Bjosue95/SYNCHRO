using System.Security.Cryptography;
using System.Text.Json;

namespace Synchro.Api.Services.WhatsApp;

internal sealed record EnvioVerificacionTelefono(Guid Id, Guid Arrendamiento, int VerificacionId,
    byte[] Cifrado, int Intentos, DateTimeOffset ExpiraUtc);

internal interface IOutboxVerificacionTelefono
{
    Task<EnvioVerificacionTelefono?> TomarAsync(CancellationToken token);
    Task<bool> AutorizarAsync(EnvioVerificacionTelefono envio, CancellationToken token);
    Task CerrarAsync(EnvioVerificacionTelefono envio, CierreEnvioWhatsApp cierre, CancellationToken token);
}

internal sealed class ProcesadorVerificacionTelefono(IOutboxVerificacionTelefono almacen,
    ProtectorVerificacionTelefono protector, IWhatsAppCloudService cliente, TimeProvider reloj)
{
    public async Task<bool> ProcesarUnoAsync(CancellationToken token = default)
    {
        var envio = await almacen.TomarAsync(token);
        if (envio is null) return false;
        try
        {
            CierreEnvioWhatsApp cierre;
            if (envio.ExpiraUtc <= reloj.GetUtcNow())
                cierre = new(EstadoEnvioWhatsApp.Caducado);
            else
            {
                MensajeWhatsApp mensaje;
                try { mensaje = protector.Abrir(envio.Id, envio.VerificacionId, envio.Cifrado); }
                catch (Exception e) when (e is CryptographicException or JsonException or ArgumentException)
                {
                    await almacen.CerrarAsync(envio, new(EstadoEnvioWhatsApp.Fallido, CodigoError: "ProteccionNoValida"), token);
                    return true;
                }
                // Revalidar inmediatamente antes de la llamada externa. Una revocacion
                // posterior al inicio de la llamada no puede recuperar un mensaje enviado.
                if (!await almacen.AutorizarAsync(envio, token))
                    cierre = new(EstadoEnvioWhatsApp.Cancelado);
                else
                {
                    WhatsAppResultado resultado;
                    try { resultado = await cliente.EnviarPlantillaAsync(mensaje.Telefono, mensaje.Plantilla, mensaje.Variables, token); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        await almacen.CerrarAsync(envio, new(EstadoEnvioWhatsApp.Indeterminado, CodigoError: "ResultadoNoConfirmado"), token);
                        return true;
                    }
                    // Solo comparte la politica de resultados; nunca la cola de encomiendas.
                    cierre = PoliticaEnvioWhatsApp.Decidir(new(envio.Id, envio.Arrendamiento,
                        envio.VerificacionId, [], envio.Intentos, envio.ExpiraUtc), resultado, reloj.GetUtcNow());
                }
            }
            await almacen.CerrarAsync(envio, cierre, token);
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(envio.Cifrado); }
    }
}
