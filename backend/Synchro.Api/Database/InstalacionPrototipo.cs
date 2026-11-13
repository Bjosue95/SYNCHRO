using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;

namespace Synchro.Api.Database;

// Utilidad local, sin endpoint HTTP. No crea ni reinicia bases y no instala sobre
// una base historica sin marca. Los archivos son del ensamblado/revision revisados.
public static class InstalacionPrototipo
{
    public static IReadOnlyList<string> Archivos { get; } = Array.AsReadOnly(new[]
    {
        "Fixtures/EsquemaPrototipo.sql",
        "Migrations/20261110_VerificacionTelefono_Pruebas.sql",
        "Migrations/20261110_VerificacionTelefonoProcedimientos_Pruebas.sql",
        "Migrations/20261110_ValidarCodigoTelefono_Pruebas.sql",
        "Migrations/20261110_ConsumirVerificacionSesion_Pruebas.sql",
        "Migrations/20261112_OutboxVerificacionTelefono_Pruebas.sql",
        "Migrations/20261110_OrigenPreReservaEncomienda_Pruebas.sql",
        "Migrations/20261110_ClientesWeb_Pruebas.sql",
        "Migrations/20261109_ConsentimientoReservaWhatsApp_Pruebas.sql",
        "Migrations/20261109_EvidenciasFacialesRemitente_Pruebas.sql",
        "Migrations/20261110_DepositoEncomiendaAtomico_Pruebas.sql",
        "Migrations/20261110_PinEncomienda_Pruebas.sql",
        "Migrations/20261110_OutboxWhatsApp_Pruebas.sql",
        "Migrations/20261110_RetiroEncomiendaTerminal_Pruebas.sql",
        "Migrations/20261110_ReemisionPinEncomienda_Pruebas.sql",
        "Migrations/20261110_ServiciosTemporales_Pruebas.sql",
        "Migrations/20261110_UsuariosRoles_Pruebas.sql",
        "Migrations/20261110_RostroSimulado_Pruebas.sql",
        "Migrations/20261108_WhatsAppWebhook_Pruebas.sql"
    });
    public static string ExigirConexion(string? cadena) => ConexionSqlAislada.Exigir(cadena, "SynchroDB_Pruebas");
    public static async Task AplicarAsync(string? cadena, string directorio, Action<string>? avance = null, CancellationToken token = default)
    {
        string conexion = ExigirConexion(cadena);
        // Leer todo antes de escribir. Una publicacion incompleta no instala a medias.
        var scripts = new List<(string Nombre, string Sql)>();
        foreach (string archivo in Archivos)
            scripts.Add((archivo, await File.ReadAllTextAsync(Path.Combine(directorio, archivo), token)));
        await using var c = new SqlConnection(conexion); await c.OpenAsync(token);
        await using (var q = new SqlCommand("""
            IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 56600,'Base no autorizada.',1;
            DECLARE @r INT;
            EXEC @r=sys.sp_getapplock @Resource=N'Synchro.InstalacionPrototipo',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=0;
            IF @r<0 THROW 56601,'Hay otra instalacion en curso.',1;
            IF NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
                AND EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
                THROW 56602,'No se instala sobre una base existente sin marca.',1;
            """, c)) await q.ExecuteNonQueryAsync(token);
        foreach (var (nombre, sql) in scripts)
        {
            foreach (string lote in Lotes(sql))
            { await using var q = new SqlCommand(lote, c) { CommandTimeout = 60 }; await q.ExecuteNonQueryAsync(token); }
            // Solo nombres y checksum del codigo; nunca cadena ni datos de usuarios.
            avance?.Invoke(nombre + " SHA256=" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql))));
        }
        // Cerrar la sesion libera el applock incluso si falla un lote. Cada script
        // idempotente conserva su propia transaccion y permite reanudar sin reset.
    }
    internal static IEnumerable<string> Lotes(string sql) => Regex.Split(sql, @"^\s*GO\s*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x));
}
