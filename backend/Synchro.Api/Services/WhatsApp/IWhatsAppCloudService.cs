
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Synchro.Api.Services.WhatsApp;

public enum FalloWhatsApp
{
    Ninguno, DatosInvalidos, Configuracion, Rechazado, Reintentable, Indeterminado
}

public sealed record WhatsAppResultado(
    bool AceptadoPorMeta,
    string? IdMensaje,
    string Mensaje,
    FalloWhatsApp Fallo = FalloWhatsApp.Ninguno,
    TimeSpan? ReintentarDespues = null
);

public interface IWhatsAppCloudService
{
    Task<WhatsAppResultado> EnviarPlantillaAsync(
        string telefonoE164,
        string nombrePlantilla,
        IReadOnlyList<string> variables,
        CancellationToken cancelacion = default
    );
}
