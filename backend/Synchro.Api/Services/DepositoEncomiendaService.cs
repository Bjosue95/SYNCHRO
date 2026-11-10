using System.Data;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services.WhatsApp;

namespace Synchro.Api.Services;

// Contrato interno: no acepta booleanos de aprobacion, telefonos ni identidad
// de terminal enviados por el navegador. SQL vuelve a comprobar los vinculos.
internal sealed record DepositoEncomiendaComando(
    int ReservaId, int TerminalId, Guid EvidenciaId, string CodigoCasillero);

internal sealed record DepositoEncomiendaResultado(int UsoId, string CodigoCasillero);

internal sealed class DepositoEncomiendaService(IConfiguration configuracion,
    PreparadorPinEncomienda? pines = null, SqlOutboxWhatsApp? outbox = null)
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
        if (pines is null || outbox is null)
            throw new InvalidOperationException("No se configuraron los servicios de PIN y notificaciones.");
        string? plantilla = configuracion["WhatsApp:PlantillaPinRetiro"];
        if (!MensajeWhatsApp.PlantillaEsValida(plantilla))
            throw new InvalidOperationException("No se configuro la plantilla de PIN de retiro.");

        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancelacion);
        int usoId;
        string codigo;
        await using (var consulta = new SqlCommand("dbo.DepositarEncomiendaReservaSynchro", conexion, transaccion)
        {
            CommandType = CommandType.StoredProcedure
        })
        {
            consulta.Parameters.Add("@ReservaId", SqlDbType.Int).Value = comando.ReservaId;
            consulta.Parameters.Add("@TerminalId", SqlDbType.Int).Value = comando.TerminalId;
            consulta.Parameters.Add("@EvidenciaId", SqlDbType.UniqueIdentifier).Value = comando.EvidenciaId;
            consulta.Parameters.Add("@CodigoCasillero", SqlDbType.NVarChar, 10).Value = comando.CodigoCasillero;
            await using var lector = await consulta.ExecuteReaderAsync(cancelacion);
            if (!await lector.ReadAsync(cancelacion))
                throw new InvalidOperationException("SQL no confirmo el deposito.");
            usoId = lector.GetInt32(0);
            codigo = lector.GetString(1);
        }
        // SQL conserva los bloqueos de reserva y consentimiento hasta el commit.
        // La identidad y telefono se obtienen del servidor, nunca de la solicitud.
        int destinatario;
        string telefono;
        await using (var q = new SqlCommand("""
            SELECT p.PersonaId,CASE WHEN LEFT(r.TelefonoDestinatario,1)=N'+'
                THEN r.TelefonoDestinatario ELSE N'+'+r.TelefonoDestinatario END
            FROM dbo.ParticipantesUso p
            JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=@reserva
            WHERE p.UsoId=@uso AND p.Rol=N'Destinatario';
            """,conexion,transaccion))
        {
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=comando.ReservaId;
            q.Parameters.Add("@uso",SqlDbType.Int).Value=usoId;
            await using var r=await q.ExecuteReaderAsync(cancelacion);
            if (!await r.ReadAsync(cancelacion))throw new InvalidOperationException("Destinatario no encontrado.");
            destinatario=r.GetInt32(0);telefono=r.GetString(1);
        }
        using var pin=pines.Crear(usoId,destinatario);
        await using (var q=new SqlCommand("""
            INSERT dbo.CodigosRetiroEncomiendaPrototipo(UsoId,DestinatarioId,PinHash,PinSalt,VenceUtc)
            VALUES (@uso,@destinatario,@hash,@salt,@vence);
            """,conexion,transaccion))
        {
            q.Parameters.Add("@uso",SqlDbType.Int).Value=usoId;
            q.Parameters.Add("@destinatario",SqlDbType.Int).Value=destinatario;
            q.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=pin.Hash;
            q.Parameters.Add("@salt",SqlDbType.VarBinary,32).Value=pin.Salt;
            q.Parameters.Add("@vence",SqlDbType.DateTime2).Value=pin.VenceUtc.UtcDateTime;
            await q.ExecuteNonQueryAsync(cancelacion);
        }
        await outbox.EncolarAsync(conexion,transaccion,comando.EvidenciaId,comando.ReservaId,
            new MensajeWhatsApp(telefono,plantilla!,[pin.Pin,codigo]),pin.VenceUtc,cancelacion);
        await transaccion.CommitAsync(cancelacion);
        // El PIN solo queda en el mensaje cifrado; no se devuelve a la terminal.
        return new(usoId,codigo);
    }
}
