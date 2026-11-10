using System.Security.Cryptography;
using System.Text.Json;

namespace Synchro.Api.Services.WhatsApp;

internal enum EstadoEnvioWhatsApp { Pendiente, Enviando, Reintento, Aceptado, Entregado, Leido, Fallido, Indeterminado, Caducado, Cancelado }
internal sealed record EnvioPendienteWhatsApp(Guid Id, Guid Arrendamiento, int ReservaId,
    byte[] Cifrado, int Intentos, DateTimeOffset ExpiraUtc);
internal sealed record CierreEnvioWhatsApp(EstadoEnvioWhatsApp Estado, string? IdMensaje = null,
    DateTimeOffset? ReintentarUtc = null, string? CodigoError = null);

internal interface IAlmacenOutboxWhatsApp
{
    Task<EnvioPendienteWhatsApp?> TomarAsync(CancellationToken cancelacion);
    Task CerrarAsync(EnvioPendienteWhatsApp envio, CierreEnvioWhatsApp cierre, CancellationToken cancelacion);
}

internal static class PoliticaEnvioWhatsApp
{
    public static CierreEnvioWhatsApp Decidir(EnvioPendienteWhatsApp envio, WhatsAppResultado resultado, DateTimeOffset ahora)
    {
        if (resultado.AceptadoPorMeta && !string.IsNullOrWhiteSpace(resultado.IdMensaje))
            return new(EstadoEnvioWhatsApp.Aceptado,resultado.IdMensaje);
        if (resultado.Fallo == FalloWhatsApp.Indeterminado || resultado.AceptadoPorMeta)
            return new(EstadoEnvioWhatsApp.Indeterminado,CodigoError:"ResultadoNoConfirmado");
        if (resultado.Fallo != FalloWhatsApp.Reintentable || envio.Intentos >= 5)
            return new(EstadoEnvioWhatsApp.Fallido,CodigoError:resultado.Fallo.ToString());
        var espera = TimeSpan.FromSeconds(30 * Math.Pow(2,Math.Clamp(envio.Intentos - 1,0,4)));
        if (resultado.ReintentarDespues is { } sugerida && sugerida > espera) espera = sugerida;
        if (espera >= envio.ExpiraUtc - ahora)
            return new(EstadoEnvioWhatsApp.Caducado,CodigoError:"SinTiempoParaReintentar");
        return new(EstadoEnvioWhatsApp.Reintento,ReintentarUtc:ahora + espera,CodigoError:"LimiteDeMeta");
    }
}

internal sealed class ProcesadorOutboxWhatsApp(IAlmacenOutboxWhatsApp almacen,
    ProtectorMensajeWhatsApp protector, IWhatsAppCloudService cliente, TimeProvider reloj)
{
    // Una llamada procesa a lo sumo un mensaje. Ningun PIN/telefono se registra.
    public async Task<bool> ProcesarUnoAsync(CancellationToken cancelacion = default)
    {
        cancelacion.ThrowIfCancellationRequested();
        var envio = await almacen.TomarAsync(cancelacion);
        if (envio is null) return false;
        CierreEnvioWhatsApp cierre;
        if (envio.ExpiraUtc <= reloj.GetUtcNow())
            cierre = new(EstadoEnvioWhatsApp.Caducado,CodigoError:"MensajeVencido");
        else
        {
            MensajeWhatsApp mensaje;
            try { mensaje = protector.Abrir(envio.Id,envio.ReservaId,envio.Cifrado); }
            catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
            {
                await almacen.CerrarAsync(envio,new(EstadoEnvioWhatsApp.Fallido,CodigoError:"ProteccionNoValida"),cancelacion);
                return true;
            }
            var resultado = await cliente.EnviarPlantillaAsync(mensaje.Telefono,mensaje.Plantilla,mensaje.Variables,cancelacion);
            cierre = PoliticaEnvioWhatsApp.Decidir(envio,resultado,reloj.GetUtcNow());
        }
        // Si el proceso muere o SQL falla despues de enviar, el arrendamiento
        // vencido pasa a Indeterminado. No se vuelve a enviar automaticamente.
        await almacen.CerrarAsync(envio,cierre,cancelacion);
        return true;
    }
}
