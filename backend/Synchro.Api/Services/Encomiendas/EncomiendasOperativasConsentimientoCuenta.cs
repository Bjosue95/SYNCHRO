using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

internal sealed partial class EncomiendasOperativasSql
{
    // El historial existente admite eventos de cuenta (ReservaId NULL). El último
    // evento es la fuente de verdad; nunca se infiere autorización al compartir.
    private const string AccionConsentimientoCuenta = "ConsentimientoCuentaRemitente";
    private sealed record MensajeCuenta(string Telefono, string Referencia);
    private sealed record ConsentimientoCuenta(string Telefono, string Estado, bool Registrado,
        DateTimeOffset? Fecha, string? Referencia, string? Administrador, bool Activa, string? TelefonoVerificado = null);

    private async Task<ConsentimientoCuenta> LeerConsentimientoCuenta(SqlConnection c, SqlTransaction? tx, int persona, CancellationToken ct)
    {
        string telefono; bool activa;
        await using (var q = Comando(c, tx, "SELECT Telefono,Activa FROM dbo.ClientesWebPrototipo WITH(UPDLOCK,HOLDLOCK) WHERE PersonaId=@persona", ("@persona", persona))) {
            await using var r = await q.ExecuteReaderAsync(ct);
            Exigir(await r.ReadAsync(ct), "CuentaNoDisponible", "Cuenta no disponible.", 404);
            telefono = r.GetString(0); activa = r.GetBoolean(1);
        }
        await using var evento = Comando(c, tx, """
            SELECT TOP(1) h.Estado,h.Detalle,h.FechaUtc,a.NombreUsuario
            FROM dbo.AuditoriaEncomiendaOperativa h
            LEFT JOIN dbo.Administradores a ON h.ActorTipo=N'Administrador' AND a.Id=h.ActorId
            WHERE h.ReservaId IS NULL AND h.PersonaId=@persona AND h.Accion=@accion ORDER BY h.Id DESC
            """, ("@persona", persona), ("@accion", AccionConsentimientoCuenta));
        await using var lector = await evento.ExecuteReaderAsync(ct);
        if (!await lector.ReadAsync(ct)) return new(telefono, "Pendiente", false, null, null, null, activa);
        var mensaje = JsonSerializer.Deserialize<MensajeCuenta>(lector.GetString(1))!;
        var estado = mensaje.Telefono == telefono && activa ? lector.GetString(0) : "Pendiente";
        var comprobado = lector.GetString(0) == "Confirmado";
        return new(telefono, estado, true, comprobado ? Fecha(lector, 2) : null, mensaje.Referencia,
            comprobado && !lector.IsDBNull(3) ? lector.GetString(3) : null, activa, comprobado ? mensaje.Telefono : null);
    }

    public async Task<object> ConsultarConsentimientoCuenta(int persona, CancellationToken ct)
    {
        await using var c = await Abrir(ct);
        var cuenta = await LeerConsentimientoCuenta(c, null, persona, ct);
        return new { telefono = cuenta.Telefono, consentimiento = cuenta.Estado, alcance = "CuentaTelefono",
            texto = WhatsAppEncomienda.Aceptacion(), enlaceWhatsApp = WhatsAppEncomienda.Enlace(config["WhatsAppManual:NumeroE164"], WhatsAppEncomienda.Aceptacion()),
            verificadoUtc = cuenta.Fecha, referencia = cuenta.Referencia, telefonoVerificado=cuenta.TelefonoVerificado };
    }

    public async Task<object> RevocarConsentimientoCuenta(int persona, CancellationToken ct)
    {
        await using var c = await Abrir(ct); await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        var cuenta = await LeerConsentimientoCuenta(c, tx, persona, ct);
        await RegistrarConsentimientoCuenta(c, tx, persona, cuenta.Telefono, "Revocación desde la cuenta", "Revocado", "Cliente", persona, ct);
        await tx.CommitAsync(ct);
        return new { consentimiento = "Revocado" };
    }

    private static Task RegistrarConsentimientoCuenta(SqlConnection c, SqlTransaction tx, int persona, string telefono,
        string referencia, string estado, string actor, int actorId, CancellationToken ct)
    {
        var detalle=JsonSerializer.Serialize(new MensajeCuenta(telefono, referencia), new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Exigir(detalle.Length<=500,"ReferenciaInvalida","Utiliza una referencia de mensaje más breve.",400);
        return Auditar(c, tx, null, persona, actor, actorId, AccionConsentimientoCuenta, estado,detalle,ct);
    }

    private static string EstadoRemitente(ConsentimientoCuenta cuenta, string telefonoAnterior, string consentimientoAnterior) =>
        cuenta.Registrado ? cuenta.Estado : cuenta.Activa && cuenta.Telefono == telefonoAnterior ? consentimientoAnterior : "Pendiente";
}
