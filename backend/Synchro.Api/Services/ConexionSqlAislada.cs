using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services;

internal static class ConexionSqlAislada
{
    public static string Exigir(string? cadena, string baseEsperada)
    {
        if (baseEsperada is not ("SynchroDB_Pruebas" or "SynchroDB_Detalle_Pruebas"))
            throw new InvalidOperationException("Base de pruebas no autorizada.");
        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException("Conexion de pruebas no configurada.");

        var datos = new SqlConnectionStringBuilder(cadena);
        if (!string.Equals(datos.InitialCatalog, baseEsperada, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(datos.AttachDBFilename))
            throw new InvalidOperationException("La conexion debe apuntar exclusivamente a la base de pruebas.");
        return cadena;
    }
}
