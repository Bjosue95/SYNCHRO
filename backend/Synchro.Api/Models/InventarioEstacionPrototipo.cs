using System.Text.Json;

namespace Synchro.Api.Models;

/// <summary>
/// Compartimentos de la estación física SYNCHRO de pruebas.
/// No incluye otros registros de SQL, fixtures de ensayos ni estaciones futuras.
/// </summary>
internal static class InventarioEstacionPrototipo
{
    internal static readonly string[] Codigos =
    [
        "S01", "S02", "M03", "M04", "L05",
        "M06", "M07", "M08", "M09", "L10"
    ];

    internal static string CodigosJson => JsonSerializer.Serialize(Codigos);

    internal static string TipoReal(string codigo, string tipo)
        => codigo switch
        {
            "M04" => "Frio",
            "M09" => "Calor",
            _ => tipo
        };
}
