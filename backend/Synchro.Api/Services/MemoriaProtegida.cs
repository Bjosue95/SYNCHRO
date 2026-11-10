using System.Security.Cryptography;
namespace Synchro.Api.Services;
internal sealed class MemoriaProtegida : MemoryStream
{
    protected override void Dispose(bool disposing)
    {
        if(disposing&&TryGetBuffer(out var bytes))CryptographicOperations.ZeroMemory(bytes.AsSpan());
        base.Dispose(disposing);
    }
}
