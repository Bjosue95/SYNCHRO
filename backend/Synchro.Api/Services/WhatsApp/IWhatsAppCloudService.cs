
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Synchro.Api.Services.WhatsApp;

public sealed record WhatsAppResultado(
    bool AceptadoPorMeta,
    string? IdMensaje,
    string Mensaje
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
