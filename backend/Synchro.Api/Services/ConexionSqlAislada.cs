using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services;

internal static class ConexionSqlAislada
{
    public static bool EsWhatsAppPermitido(
        SqlConnectionStringBuilder datos)
    {
        if (!string.Equals(
                datos.InitialCatalog,
                "SynchroDB_Pruebas",
                StringComparison.OrdinalIgnoreCase) ||
            !datos.IntegratedSecurity ||
            !string.IsNullOrWhiteSpace(datos.AttachDBFilename))
        {
            return false;
        }

        // Instancia original del prototipo.
        if (string.Equals(
                datos.DataSource,
                @"(localdb)\SYNCHRO_Aislado",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Cualquier otra instancia queda rechazada,
        // excepto la copia expresamente autorizada.
        if (!string.Equals(
                datos.DataSource,
                @"(localdb)\SYNCHRO_Suite_Test",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? configurada =
            Environment.GetEnvironmentVariable(
                "SYNCHRO_SQL_DEPOSITO_TEST");

        if (string.IsNullOrWhiteSpace(configurada))
        {
            return false;
        }

        try
        {
            var declarada =
                new SqlConnectionStringBuilder(configurada);

            return
                string.Equals(
                    declarada.DataSource,
                    @"(localdb)\SYNCHRO_Suite_Test",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    declarada.InitialCatalog,
                    "SynchroDB_Pruebas",
                    StringComparison.OrdinalIgnoreCase) &&
                declarada.IntegratedSecurity &&
                string.IsNullOrWhiteSpace(
                    declarada.AttachDBFilename);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
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
