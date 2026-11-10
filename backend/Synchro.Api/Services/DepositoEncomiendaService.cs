using System.Data;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services;

// Contrato interno: no acepta booleanos de aprobacion, telefonos ni identidad
// de terminal enviados por el navegador. SQL vuelve a comprobar los vinculos.
internal sealed record DepositoEncomiendaComando(
    int ReservaId, int TerminalId, Guid EvidenciaId, string CodigoCasillero);

internal sealed record DepositoEncomiendaResultado(int UsoId, string CodigoCasillero);

internal sealed class DepositoEncomiendaService(IConfiguration configuracion)
{
    internal static void Validar(DepositoEncomiendaComando comando)
    {
        ArgumentNullException.ThrowIfNull(comando);
        if (comando.ReservaId <= 0 || comando.TerminalId <= 0 ||
            comando.EvidenciaId == Guid.Empty ||
            string.IsNullOrWhiteSpace(comando.CodigoCasillero) ||
            comando.CodigoCasillero.Length > 10 ||
            comando.CodigoCasillero.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Datos de deposito invalidos.");
    }

    public async Task<DepositoEncomiendaResultado> DepositarAsync(
        DepositoEncomiendaComando comando, CancellationToken cancelacion = default)
    {
        Validar(comando);
        cancelacion.ThrowIfCancellationRequested();
        string cadena = ConexionSqlAislada.Exigir(
            configuracion.GetConnectionString("SynchroDB"), "SynchroDB_Pruebas");
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);
        await using var consulta = new SqlCommand("dbo.DepositarEncomiendaReservaSynchro", conexion)
        {
            CommandType = CommandType.StoredProcedure
        };
        consulta.Parameters.Add("@ReservaId", SqlDbType.Int).Value = comando.ReservaId;
        consulta.Parameters.Add("@TerminalId", SqlDbType.Int).Value = comando.TerminalId;
        consulta.Parameters.Add("@EvidenciaId", SqlDbType.UniqueIdentifier).Value = comando.EvidenciaId;
        consulta.Parameters.Add("@CodigoCasillero", SqlDbType.NVarChar, 10).Value = comando.CodigoCasillero;
        await using var lector = await consulta.ExecuteReaderAsync(cancelacion);
        if (!await lector.ReadAsync(cancelacion))
            throw new InvalidOperationException("SQL no confirmo el deposito.");
        return new(lector.GetInt32(0), lector.GetString(1));
    }
}
