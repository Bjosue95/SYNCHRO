using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Encomiendas;

internal sealed class OperacionEncomiendaException(string codigo, string mensaje, int estado = 409) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
    public int Estado { get; } = estado;
}

internal sealed record ConsentimientoOperativoSolicitud(string Telefono, string Codigo, string MensajeReferencia,
    bool MensajeComprobado, bool AceptaEncomienda, string? TextoMensaje = null);
internal sealed record PagoEncomiendaSolicitud(string Metodo, decimal ImporteHnl, string Referencia,
    bool PagoComprobado, string? JustificacionImporte);
internal sealed record EntregaPinEncomiendaSolicitud(Guid PinId, string Telefono, string MensajeReferencia, bool EnvioComprobado);
internal sealed record CapturaEncomiendaSolicitud(string ImagenPng, double[]? Descriptor, string Modelo,
    string VersionConsentimiento, string? Contrasena = null)
{ public override string ToString() => "Captura de encomienda protegida"; }
internal sealed record AutorizarDepositoEncomiendaSolicitud(Guid SolicitudId, string Pin, CapturaEncomiendaSolicitud Captura)
{ public override string ToString() => "Autorización de depósito protegida"; }
internal sealed record RetiroOperativoSolicitud(Guid EvidenciaId, string Pin, bool EntregaComprobada)
{ public override string ToString() => "Retiro de encomienda protegido"; }

internal sealed partial class EncomiendasOperativasSql(IConfiguration config, IDataProtectionProvider proteccion, TimeProvider reloj, bool definitiva = false)
{
    public const string VersionConsentimiento = "SYNCHRO-ENCOMIENDAS-FACIAL-V3-2026-11";
    private DateTimeOffset Ahora => reloj.GetUtcNow();
    private sealed record Operacion(int Id, int RemitenteId, int DestinatarioId, string Tamano, string Estado,
        DateTimeOffset Vence, string Pago, decimal? Importe, int? UsoId, int? TerminalId, long Version);

    private async Task<SqlConnection> Abrir(CancellationToken ct,bool bandeja=false)
    {
        if (!config.GetValue<bool>("EncomiendasOperacion:Habilitado")&&!(bandeja&&config.GetValue<bool>("WhatsAppManual:Habilitado")))
            throw new OperacionEncomiendaException("NoHabilitado", "La gestión operativa de encomiendas no está habilitada.", 503);
        var cadena = config.GetConnectionString("SynchroDB");
        if(definitiva)
        {
            var autorizada=Synchro.Api.Services.Administracion.DestinoAdministrativoDefinitivo.Exigir(cadena);
            var definitivaSql=new SqlConnection(autorizada);
            try
            {
                await definitivaSql.OpenAsync(ct);
                await using var verificar=new SqlCommand(Synchro.Api.Services.Administracion.DestinoAdministrativoDefinitivo.ValidacionSql,definitivaSql);
                if(Convert.ToInt32(await verificar.ExecuteScalarAsync(ct))!=1)
                    throw new InvalidOperationException("Base definitiva no autorizada.");
                return definitivaSql;
            }
            catch{await definitivaSql.DisposeAsync();throw;}
        }
        var destino = new SqlConnectionStringBuilder(cadena);
        if (!destino.IntegratedSecurity || destino.InitialCatalog != "SynchroDB_Pruebas" ||
            !string.Equals(destino.DataSource, @"(localdb)\SYNCHRO_Aislado", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(destino.AttachDBFilename))
            throw new InvalidOperationException("Encomiendas operativas requiere SYNCHRO_Aislado/SynchroDB_Pruebas.");
        var c = new SqlConnection(cadena);
        try { await c.OpenAsync(ct); return c; } catch { await c.DisposeAsync(); throw; }
    }

    private static SqlCommand Comando(SqlConnection c, SqlTransaction? tx, string sql, params (string Nombre, object? Valor)[] parametros)
    {
        var q = new SqlCommand(sql, c, tx) { CommandTimeout = 20 };
        foreach (var (nombre, valor) in parametros) q.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        return q;
    }
    private static DateTimeOffset Fecha(SqlDataReader r, int i) => new(DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc));
    private static string Texto(string? valor, int maximo, string campo)
    {
        var s = valor?.Trim();
        if (string.IsNullOrEmpty(s) || s.Length > maximo || s.Any(char.IsControl))
            throw new OperacionEncomiendaException("DatosInvalidos", $"Revisa {campo}.", 400);
        return s;
    }
    private static string Rol(string entrada) => entrada.ToLowerInvariant() switch {
        "remitente" => "Remitente", "destinatario" => "Destinatario",
        _ => throw new OperacionEncomiendaException("RolInvalido", "Participante no válido.", 400)
    };
    private static FinalidadPinEncomienda Finalidad(string entrada) => entrada.ToLowerInvariant() switch {
        "deposito" => FinalidadPinEncomienda.Deposito, "retiro" => FinalidadPinEncomienda.Retiro,
        _ => throw new OperacionEncomiendaException("FinalidadInvalida", "Finalidad de PIN no válida.", 400)
    };
    private static void Exigir(bool condicion, string codigo, string mensaje, int estado = 409)
    { if (!condicion) throw new OperacionEncomiendaException(codigo, mensaje, estado); }
    private static void Preparacion(Operacion o, DateTimeOffset ahora) => Exigir(
        o.Estado is "Preparacion" or "ListaParaDeposito" && o.Vence > ahora,
        "EstadoNoDisponible", "La encomienda está vencida o no permite esta operación.");

    private async Task<Operacion> Leer(SqlConnection c, SqlTransaction? tx, int id, int? propietario, CancellationToken ct)
    {
        // Serializar primero la fila canónica. Un JOIN puede leer la pre-reserva antes
        // de esperar este bloqueo y provocar un ciclo al actualizarla al depositar.
        if(tx is not null) {
            await using var bloqueo=Comando(c,tx,"SELECT ReservaId FROM dbo.EncomiendasOperativas WITH(UPDLOCK,HOLDLOCK) WHERE ReservaId=@id",("@id",id));
            await bloqueo.ExecuteScalarAsync(ct);
        }
        await using var q = Comando(c, tx, """
            SELECT o.ReservaId,r.ClientePersonaId,o.DestinatarioId,r.TamanoSolicitado,o.Estado,r.FechaExpiracion,
              o.PagoEstado,o.ImporteHnl,o.UsoId,o.TerminalId,o.Version
            FROM dbo.EncomiendasOperativas o WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=o.ReservaId
            WHERE o.ReservaId=@id AND (@persona IS NULL OR r.ClientePersonaId=@persona);
            """, ("@id", id), ("@persona", propietario));
        await using var r = await q.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new OperacionEncomiendaException("NoEncontrada", "Encomienda no encontrada.", 404);
        return new(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3), r.GetString(4), Fecha(r,5),
            r.GetString(6), r.IsDBNull(7)?null:r.GetDecimal(7), r.IsDBNull(8)?null:r.GetInt32(8), r.IsDBNull(9)?null:r.GetInt32(9), r.GetInt64(10));
    }
    private static async Task Auditar(SqlConnection c, SqlTransaction? tx, int? reserva, int? persona,
        string actor, int actorId, string accion, string estado, string detalle, CancellationToken ct, Guid? pin = null, Guid? evidencia = null)
    {
        await using var q = Comando(c, tx, """
            INSERT dbo.AuditoriaEncomiendaOperativa(ReservaId,PersonaId,ActorTipo,ActorId,Accion,Estado,Detalle,PinId,EvidenciaId)
            VALUES(@reserva,@persona,@actor,@actorId,@accion,@estado,@detalle,@pin,@evidencia);
            """, ("@reserva",reserva),("@persona",persona),("@actor",actor),("@actorId",actorId),
            ("@accion",accion),("@estado",estado),("@detalle",detalle),("@pin",pin),("@evidencia",evidencia));
        await q.ExecuteNonQueryAsync(ct);
    }
    private static async Task Versionar(SqlConnection c, SqlTransaction tx, int id, CancellationToken ct)
    {
        await using var q = Comando(c, tx,"UPDATE dbo.EncomiendasOperativas SET Version=Version+1 WHERE ReservaId=@id",("@id",id));
        await q.ExecuteNonQueryAsync(ct);
    }

    public decimal? Tarifa(string tamano)
    {
        var valor = config[$"EncomiendasOperacion:Tarifas:{tamano}"];
        return decimal.TryParse(valor, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var importe) &&
            importe > 0 && importe <= 100000 && decimal.Round(importe,2)==importe ? importe : null;
    }
    public object Configuracion() => new {
        habilitado=config.GetValue<bool>("EncomiendasOperacion:Habilitado"), moneda="HNL", pago="Manual",
        tarifas=new { pequeno=Tarifa("Pequeno"), mediano=Tarifa("Mediano"), grande=Tarifa("Grande") },
        modelo=RostroSimulado.DescriptorFacialV3.Modelo, versionConsentimiento=VersionConsentimiento,
        versionConsentimientoRetiro=VersionConsentimientoRetiro,
        versionCapturaDemostrativa=VersionCapturaDemostrativa,verificacionFacialRequerida=false,
        revalidacionReferencia="Anual", conservacionFotografiaDias=30, hardwareSimulado=true, tabletIntegrada=false
    };

    public async Task<object> Crear(int persona, string telefono, string tamano, CancellationToken ct,CrearReservaPrototipoSolicitud? seleccion=null)
    {
        await using var c = await Abrir(ct);
        if(seleccion is not null)await LiberarReservasVencidas(c,ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        string remitente;
        await using (var q = Comando(c,tx,"SELECT Telefono FROM dbo.ClientesWebPrototipo WITH(UPDLOCK,HOLDLOCK) WHERE PersonaId=@id AND Activa=1",("@id",persona)))
            remitente = (string?)await q.ExecuteScalarAsync(ct) ?? throw new OperacionEncomiendaException("CuentaNoDisponible","La cuenta no está disponible.",403);
        var personal=seleccion?.Servicio=="Personal";
        var datos = PreReservaEncomiendaService.Validar(persona,personal?remitente:telefono,tamano);
        Exigir(personal||remitente!=datos.Destinatario,"ParticipantesNoValidos","El destinatario debe tener un número distinto al del remitente.",400);
        var reserva=seleccion is null?null:await SeleccionarReserva(c,tx,seleccion,ct);
        int destinatario, id; byte[] aleatorio=RandomNumberGenerator.GetBytes(32), hash=SHA256.HashData(aleatorio);
        CryptographicOperations.ZeroMemory(aleatorio);
        try {
            if(personal)destinatario=persona;
            else {
                await using var q=Comando(c,tx,"INSERT dbo.Personas(TelefonoWhatsapp,TieneCuenta) OUTPUT INSERTED.Id VALUES(@telefono,0)",("@telefono",datos.Destinatario));
                destinatario=Convert.ToInt32(await q.ExecuteScalarAsync(ct));
            }
            await using(var q=Comando(c,tx,"""
                INSERT dbo.PreReservasEncomiendasWeb(TokenGestionHash,TelefonoRemitente,TelefonoDestinatario,TamanoSolicitado,
                  Estado,FechaExpiracion,OrigenPreReserva,TerminalCreacionId,ClientePersonaId)
                OUTPUT INSERTED.Id VALUES(@hash,@remitente,@destinatario,@tamano,N'GestionManual',@vence,N'Web',NULL,@persona);
                """,("@hash",hash),("@remitente",remitente),("@destinatario",datos.Destinatario),("@tamano",datos.Tamano),("@vence",Ahora.AddHours(24).UtcDateTime),("@persona",persona)))
                id=Convert.ToInt32(await q.ExecuteScalarAsync(ct));
            await using(var q=Comando(c,tx,"INSERT dbo.EncomiendasOperativas(ReservaId,DestinatarioId,Estado) VALUES(@id,@destinatario,N'Preparacion')",("@id",id),("@destinatario",destinatario))) await q.ExecuteNonQueryAsync(ct);
            foreach(var p in new[]{(Rol:"Remitente",Persona:persona,Telefono:remitente),(Rol:"Destinatario",Persona:destinatario,Telefono:datos.Destinatario)}) {
                await using var q=Comando(c,tx,"""
                    INSERT dbo.ParticipantesEncomiendaOperativa(ReservaId,Rol,PersonaId,Telefono,AceptaEncomienda,CodigoSolicitud,VenceUtc)
                    VALUES(@id,@rol,@persona,@telefono,@acepta,@codigo,@vence)
                    """,("@id",id),("@rol",p.Rol),("@persona",p.Persona),("@telefono",p.Telefono),("@acepta",personal||p.Rol=="Remitente"),("@codigo",Guid.NewGuid()),("@vence",Ahora.AddHours(24).UtcDateTime));
                await q.ExecuteNonQueryAsync(ct);
            }
            if(reserva is not null) {
                var json=System.Text.Json.JsonSerializer.Serialize(reserva,new System.Text.Json.JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
                Exigir(json.Length<=500,"ConfiguracionInvalida","La descripción de la ubicación es demasiado larga.",503);
                await Auditar(c,tx,id,persona,"Cliente",persona,AccionReservaPrototipo,"Preparacion",json,ct);
                await using var q=Comando(c,tx,"UPDATE dbo.Casilleros SET Estado=N'Reservado' WHERE Codigo=@codigo AND Estado=N'Disponible'",("@codigo",reserva.CodigoCasillero));
                Exigir(await q.ExecuteNonQueryAsync(ct)==1,"SinDisponibilidad","El casillero acaba de reservarse.");
            }
            await Auditar(c,tx,id,persona,"Cliente",persona,"Creada","Preparacion",personal?"Reserva web de pertenencias personales creada.":"Encomienda web creada. Consentimientos y pago pendientes.",ct);
            await tx.CommitAsync(ct); return new { id, estado="Preparacion" };
        } finally { CryptographicOperations.ZeroMemory(hash); }
    }

    public async Task<object> Detalle(int id,int? persona,CancellationToken ct)
    {
        await using var c=await Abrir(ct);
        var fila=await LeerHistorial(c,id,persona,ct);
        var seguimiento=await Seguimiento(c,fila,ct);
        if(fila.Historica)return new{id=fila.Id,estado=fila.Estado,tamano=fila.Tamano,creadaUtc=fila.CreadaUtc,venceUtc=fila.VenceUtc,
            historica=true,version=1,telefonoDestinatario=fila.TelefonoDestinatario,depositoUtc=fila.DepositoUtc,entregaUtc=fila.EntregaUtc,
            retiradaConfirmada=fila.RetiradaConfirmada,cancelacion=fila.Cancelacion,pagoRequiereRevision=fila.PagoRequiereRevision,seguimiento};
        var o=await Leer(c,null,id,persona,ct);
        var cuenta = await LeerConsentimientoCuenta(c,null,o.RemitenteId,ct);
        var reserva = await LeerReserva(c,null,id,ct);
        var partes=new Dictionary<string,object>();bool consentimientos=true;
        string? invitacion = null; // La invitación de destinatario se crea después del depósito.
        string? telefonoDestinatario = null;
        await using(var q=Comando(c,null,"""
            SELECT p.Rol,p.PersonaId,p.Telefono,p.Consentimiento,p.AceptaEncomienda,p.CodigoSolicitud,p.VenceUtc,p.VerificadoUtc,a.NombreUsuario,p.MensajeReferencia
            FROM dbo.ParticipantesEncomiendaOperativa p LEFT JOIN dbo.Administradores a ON a.Id=p.VerificadoAdminId WHERE p.ReservaId=@id ORDER BY p.Rol
            """,("@id",id))) {
            await using var r=await q.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct)) {
                var rol=r.GetString(0);var esRemitente=rol=="Remitente";
                var estado=esRemitente?EstadoRemitente(cuenta,r.GetString(2),r.GetString(3)):reserva?.Servicio=="Personal"?cuenta.Estado:r.GetString(3);
                consentimientos&=estado=="Confirmado"&&(esRemitente||reserva?.Servicio=="Personal"||r.GetBoolean(4));
                var codigoDestinatario = r.GetGuid(5);
                var texto=esRemitente?WhatsAppEncomienda.Aceptacion():WhatsAppEncomienda.AceptacionDestinatario(codigoDestinatario);
                // No invitar ni autorizar al destinatario durante la preparación.
                if(!esRemitente && o.Estado=="Depositada" && reserva?.Servicio!="Personal")
                    invitacion=WhatsAppEncomienda.Invitacion(codigoDestinatario, config["WhatsAppManual:NumeroE164"]);
                if(!esRemitente)telefonoDestinatario=r.GetString(2);
                var deCuenta=esRemitente&&cuenta.Registrado;
                partes[rol]=new { personaId=r.GetInt32(1),telefono=esRemitente?cuenta.Telefono:r.GetString(2),consentimiento=estado,aceptaEncomienda=esRemitente?estado=="Confirmado":r.GetBoolean(4),
                    alcance=esRemitente?(deCuenta?"CuentaTelefono":r.GetString(3)=="Confirmado"?"EncomiendaAnterior":"CuentaTelefono"):"Encomienda",
                    codigo=esRemitente?null:codigoDestinatario.ToString("N"),venceUtc=esRemitente?(DateTimeOffset?)null:Fecha(r,6),
                    codigoAnterior=persona is null?r.GetGuid(5).ToString("N"):null,
                    textoAnterior=persona is null?$"SYNCHRO ACEPTO ENCOMIENDA {id} {rol.ToUpperInvariant()} {r.GetGuid(5):N} RECIBIR MENSAJES Y PIN POR WHATSAPP"+(esRemitente?"":" Y ACEPTO RECIBIR LA ENCOMIENDA"):null,
                    verificadoUtc=deCuenta?cuenta.Fecha:r.IsDBNull(7)?(DateTimeOffset?)null:Fecha(r,7),
                    referencia=deCuenta?cuenta.Referencia:r.IsDBNull(9)?null:r.GetString(9),
                    telefonoVerificado=deCuenta?cuenta.TelefonoVerificado:r.IsDBNull(7)?null:r.GetString(2),
                    administrador=persona is not null?null:deCuenta?cuenta.Administrador:r.IsDBNull(8)?null:r.GetString(8),texto,
                    enlaceWhatsApp=!esRemitente&&o.Estado!="Depositada"?null:WhatsAppEncomienda.Enlace(config["WhatsAppManual:NumeroE164"],texto) };
            }
        }
        var pines=new List<object>(); await using(var q=Comando(c,null,"SELECT Id,Finalidad,Activo,IntentosFallidos,VenceUtc,ConsumidoUtc,EntregadoUtc FROM dbo.PinesEncomiendaOperativa WHERE ReservaId=@id ORDER BY CreadoUtc DESC",("@id",id))) {
            await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))pines.Add(new{id=r.GetGuid(0),finalidad=r.GetString(1),
                estado=!r.IsDBNull(5)?"Consumido":!r.GetBoolean(2)?"Revocado":r.GetInt32(3)>=5?"Bloqueado":Fecha(r,4)<=Ahora&&!(reserva?.Servicio=="Personal"&&o.Estado=="Depositada"&&r.GetString(1)=="Deposito")?"Vencido":!r.IsDBNull(6)?"Entregado":"Emitido",venceUtc=Fecha(r,4)});
        }
        var historial=new List<object>(); await using(var q=Comando(c,null,"""
            SELECT TOP(300) h.Id,h.Accion,h.Estado,h.FechaUtc,h.Detalle,h.ActorTipo,h.ActorId,a.NombreUsuario
            FROM dbo.AuditoriaEncomiendaOperativa h LEFT JOIN dbo.Administradores a ON h.ActorTipo=N'Administrador' AND a.Id=h.ActorId
            WHERE h.ReservaId=@id OR (h.PersonaId=@persona AND h.Accion=@accion) ORDER BY h.Id DESC
            """,("@id",id),("@persona",o.RemitenteId),("@accion",AccionConsentimientoCuenta))) {
            await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)) {
                var descripcion=r.GetString(4);
                if(r.GetString(1)==AccionConsentimientoCuenta) {
                    var mensaje=System.Text.Json.JsonSerializer.Deserialize<MensajeCuenta>(descripcion)!;
                    descripcion=$"WhatsApp de cuenta · {mensaje.Telefono}. Referencia: {mensaje.Referencia}";
                }
                historial.Add(new{id=r.GetInt64(0),accion=r.GetString(1),estado=r.GetString(2),fechaUtc=Fecha(r,3),detalle=descripcion,
                    actor=r.GetString(5),administrador=persona is null&&!r.IsDBNull(7)?r.GetString(7):null});
            }
        }
        var tarifa=reserva?.TarifaHnl??Tarifa(o.Tamano);
        object pago;await using(var q=Comando(c,null,"SELECT PagoEstado,ImporteHnl,MetodoPago,PagoReferencia,PagoConfirmadoUtc,a.NombreUsuario FROM dbo.EncomiendasOperativas o LEFT JOIN dbo.Administradores a ON a.Id=o.PagoAdminId WHERE ReservaId=@id",("@id",id))) {
            await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);pago=new {estado=r.GetString(0),importeHnl=r.IsDBNull(1)?tarifa:r.GetDecimal(1),tarifaConfigurada=tarifa,
                metodo=r.IsDBNull(2)?null:r.GetString(2),referencia=r.IsDBNull(3)?null:r.GetString(3),fechaUtc=r.IsDBNull(4)?(DateTimeOffset?)null:Fecha(r,4),
                administrador=persona is null&&!r.IsDBNull(5)?r.GetString(5):null,moneda="HNL"};
        }
        var evidencias=new List<object>();await using(var q=Comando(c,null,"""
            SELECT e.Id,e.Tipo,e.EmitidaUtc,e.ConsumidaUtc,e.ComparacionRemitenteAprobada,f.EliminarUtc,f.ExtensionHastaUtc,e.PinValidadoId,e.VersionConsentimiento
            FROM dbo.EvidenciasEncomiendaOperativa e LEFT JOIN dbo.FotografiasEncomiendaOperativa f ON f.EvidenciaId=e.Id WHERE e.ReservaId=@id ORDER BY e.EmitidaUtc DESC
            """,("@id",id))) {
            await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))evidencias.Add(new{id=r.GetGuid(0),tipo=r.GetString(1),capturadaUtc=Fecha(r,2),entregaUtc=r.IsDBNull(3)?(DateTimeOffset?)null:Fecha(r,3),
                demostrativa=r.GetString(8)==VersionCapturaDemostrativa,
                coincidenciaBiometrica=r.GetString(8)!=VersionCapturaDemostrativa&&r.GetString(1)=="VerificacionRemitente"?(bool?)r.GetBoolean(4):null,
                pinValidadoId=persona is null&&!r.IsDBNull(7)?(Guid?)r.GetGuid(7):null,
                conservacionHastaUtc=r.IsDBNull(5)?(DateTimeOffset?)null:!r.IsDBNull(6)?Fecha(r,6):Fecha(r,5),
                fotografiaDisponible=persona is null&&!r.IsDBNull(3)&&!r.IsDBNull(5)&&(Fecha(r,5)>Ahora||!r.IsDBNull(6)&&Fecha(r,6)>Ahora)});
        }
        string? casillero=null;
        if(o.UsoId.HasValue) {
            await using var q=Comando(c,null,"SELECT CodigoCasillero FROM dbo.UsosCasillero WHERE Id=@uso",("@uso",o.UsoId));
            casillero=(string?)await q.ExecuteScalarAsync(ct);
        }
        var facial=await EstadoReferencia(c,o.RemitenteId,ct);
        return new {id=o.Id,estado=o.Vence<=Ahora&&o.Estado is "Preparacion" or "ListaParaDeposito"?"Vencida":o.Estado,tamano=o.Tamano,venceUtc=o.Vence,usoId=o.UsoId,terminalId=o.TerminalId,
            casillero,remitente=partes.GetValueOrDefault("Remitente"),destinatario=partes.GetValueOrDefault("Destinatario"),pago,facial,pines,evidencias,historial,
            reserva,historica=false,creadaUtc=fila.CreadaUtc,depositoUtc=fila.DepositoUtc,entregaUtc=fila.EntregaUtc,retiradaConfirmada=fila.RetiradaConfirmada,
            cancelacion=fila.Cancelacion,pagoRequiereRevision=fila.PagoRequiereRevision,seguimiento,
            compartir=new{mensaje=invitacion,enlaceWhatsApp=invitacion is null?null:WhatsAppEncomienda.Enlace(telefonoDestinatario,invitacion)},
            condiciones=new{consentimientosConfirmados=consentimientos,remitenteAutorizado=cuenta.Activa&&partes.ContainsKey("Remitente")&&(cuenta.Estado=="Confirmado" || !cuenta.Registrado && cuenta.Activa && await ConsentimientoRemitenteRegistrado(c,o.Id,ct)),pagoConfirmado=o.Pago=="ConfirmadoManual",pagoDiferidoDestinatario=PagoDiferidoDestinatario(reserva),tabletIntegrada=false,hardwareSimulado=true},version=o.Version};
    }

    private static async Task<bool> ConsentimientoRemitenteRegistrado(SqlConnection c,int id,CancellationToken ct)
    {
        await using var q=Comando(c,null,"SELECT COUNT(*) FROM dbo.ParticipantesEncomiendaOperativa WHERE ReservaId=@id AND Rol=N'Remitente' AND Consentimiento=N'Confirmado'",("@id",id));
        return Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1;
    }

    public async Task<object> ConfirmarConsentimiento(int id,string rolEntrada,ConsentimientoOperativoSolicitud datos,int admin,bool rechazar,CancellationToken ct)
    {
        var rol=Rol(rolEntrada);var referencia=Texto(datos.MensajeReferencia,160,"la referencia del mensaje");
        Exigir(datos.MensajeComprobado,"MensajeNoComprobado","Debes comprobar explícitamente el mensaje recibido.",400);
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,null,ct);
        Exigir(o.Estado!="Cancelada","EncomiendaCancelada","La encomienda está cancelada y no admite nuevas autorizaciones.");
        var cuenta=await LeerConsentimientoCuenta(c,tx,o.RemitenteId,ct);
        var reserva=await LeerReserva(c,tx,id,ct);
        Exigir(reserva?.Servicio!="Personal"||rol=="Remitente","ConsentimientoNoAplicable","Las pertenencias personales usan la autorización general de WhatsApp del titular.",400);
        if(rol=="Remitente" && string.Equals(datos.TextoMensaje?.Trim(),WhatsAppEncomienda.Aceptacion(),StringComparison.Ordinal)) {
            Exigir(cuenta.Activa&&WhatsAppEncomienda.Telefono(datos.Telefono)==cuenta.Telefono,"MensajeNoCoincide","El número recibido no coincide con el teléfono actual de la cuenta.",400);
            Exigir(rechazar||cuenta.Estado!="Confirmado","ConsentimientoNoDisponible","La cuenta ya tiene autorización vigente.");
            await using(var q=Comando(c,tx,"""
                SELECT COUNT(*) FROM dbo.AuditoriaEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK)
                WHERE PersonaId=@persona AND Accion=@accion AND Estado=N'Confirmado'
                  AND JSON_VALUE(CASE WHEN ISJSON(Detalle)=1 THEN Detalle ELSE N'{}' END,'$.Referencia')=@referencia
                """,("@persona",o.RemitenteId),("@accion",AccionConsentimientoCuenta),("@referencia",referencia)))
                Exigir(rechazar||Convert.ToInt32(await q.ExecuteScalarAsync(ct))==0,"MensajeYaUtilizado","Comprueba un nuevo mensaje recibido después de la revocación o del cambio de teléfono.");
            await RegistrarConsentimientoCuenta(c,tx,o.RemitenteId,cuenta.Telefono,referencia,rechazar?"Revocado":"Confirmado","Administrador",admin,ct);
            await Versionar(c,tx,id,ct);await tx.CommitAsync(ct);return new{estado=rechazar?"Revocado":"Confirmado",alcance="CuentaTelefono"};
        }
        if(rol=="Destinatario")
            Exigir(o.Estado=="Depositada"&&o.UsoId.HasValue,"DepositoPendiente","El destinatario solo se autoriza después del depósito confirmado.");
        else Preparacion(o,Ahora);
        await using(var q=Comando(c,tx,"SELECT Telefono,CodigoSolicitud,Consentimiento,VenceUtc FROM dbo.ParticipantesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE ReservaId=@id AND Rol=@rol",("@id",id),("@rol",rol))) {
            await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"ParticipanteNoDisponible","Participante no disponible.");
            var textoAnterior=$"SYNCHRO ACEPTO ENCOMIENDA {id} {rol.ToUpperInvariant()} {r.GetGuid(1):N} RECIBIR MENSAJES Y PIN POR WHATSAPP"+(rol=="Destinatario"?" Y ACEPTO RECIBIR LA ENCOMIENDA":"");
            var nuevo=rol=="Destinatario"&&Guid.TryParseExact(datos.Codigo,"N",out var codigoNuevo)
                &&codigoNuevo==r.GetGuid(1)&&datos.TextoMensaje?.Trim()==WhatsAppEncomienda.AceptacionDestinatario(codigoNuevo);
            // Formatos históricos admitidos únicamente para solicitudes antiguas.
            var anterior=Guid.TryParseExact(datos.Codigo,"N",out var codigo)&&codigo==r.GetGuid(1)&&datos.TextoMensaje?.Trim()==textoAnterior;
            var anteriorConNumero=rol=="Destinatario"&&datos.Codigo==id.ToString(CultureInfo.InvariantCulture)
                &&datos.TextoMensaje?.Trim()==WhatsAppEncomienda.Aceptacion(id);
            Exigir(WhatsAppEncomienda.Telefono(datos.Telefono)==r.GetString(0)&&(nuevo||anterior||anteriorConNumero),"MensajeNoCoincide","El teléfono, texto o referencia de encomienda no coincide con el participante.",400);
            if(rol=="Remitente")Exigir(cuenta.Activa&&cuenta.Telefono==r.GetString(0)&&!cuenta.Registrado,"MensajeNoCoincide","Utiliza la autorización de cuenta desde su teléfono actual.",400);
            Exigir((rol=="Destinatario"&&o.Estado=="Depositada" || Fecha(r,3)>Ahora)&&r.GetString(2)=="Pendiente",
                "ConsentimientoNoDisponible","La solicitud ya fue atendida o está vencida.");
            Exigir(rechazar||rol!="Destinatario"||datos.AceptaEncomienda,"AceptacionPendiente","Confirma también la aceptación de la encomienda.",400);
        }
        var estado=rechazar?"Rechazado":"Confirmado";
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.ParticipantesEncomiendaOperativa SET Consentimiento=@estado,AceptaEncomienda=@acepta,
              MensajeReferencia=@mensaje,VerificadoUtc=@ahora,VerificadoAdminId=@admin WHERE ReservaId=@id AND Rol=@rol;
            """,("@estado",estado),("@acepta",!rechazar),("@mensaje",referencia),("@ahora",Ahora.UtcDateTime),("@admin",admin),("@id",id),("@rol",rol)))await q.ExecuteNonQueryAsync(ct);
        if(rechazar) {await using var q=Comando(c,tx,"UPDATE dbo.EncomiendasOperativas SET Estado=N'Rechazada' WHERE ReservaId=@id; UPDATE dbo.PreReservasEncomiendasWeb SET Estado=N'Rechazada' WHERE Id=@id",("@id",id));await q.ExecuteNonQueryAsync(ct);}
        await Auditar(c,tx,id,null,"Administrador",admin,"Consentimiento"+rol,estado,"Mensaje revisado manualmente. Referencia: "+referencia,ct);
        await Versionar(c,tx,id,ct);await tx.CommitAsync(ct);return new{estado};
    }

    private async Task<bool> Consentimientos(SqlConnection c,SqlTransaction tx,int id,CancellationToken ct)
    {
        var o=await Leer(c,tx,id,null,ct);
        var condiciones=await Condiciones(c,tx,o,ct);
        // Antes del depósito solo se exige el consentimiento de remitente.
        // El del destinatario se comprobará después, antes de emitir su PIN.
        return condiciones.CuentaRemitenteActiva&&
            condiciones.ConsentimientoRemitente==EstadoConsentimientoEncomienda.Confirmado;
    }

    public async Task<object> ConfirmarPago(int id,PagoEncomiendaSolicitud datos,int admin,CancellationToken ct)
    {
        Exigir(datos.PagoComprobado,"PagoNoComprobado","Debes verificar expresamente el dinero recibido.",400);
        Exigir(datos.ImporteHnl>0&&datos.ImporteHnl<=100000&&decimal.Round(datos.ImporteHnl,2)==datos.ImporteHnl,"ImporteInvalido","Indica un importe positivo válido con hasta dos decimales.",400);
        Exigir(datos.Metodo is "Efectivo" or "Transferencia" or "Tarjeta","MetodoInvalido","Método de pago manual no válido.",400);
        var referencia=Texto(datos.Referencia,160,"la referencia del pago");
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,null,ct);
        var reservaPago=await LeerReserva(c,tx,id,ct);
        var esDiferido=PagoDiferidoDestinatario(reservaPago);
        var pagoPrueba=PagoWebDemo(reservaPago)||esDiferido;
        if(esDiferido)
        {
            Exigir(o.Estado=="Depositada"&&o.UsoId.HasValue,
                "PagoTabletPendiente","El destinatario debe realizar el depósito previo y solicitar el pago en la Tablet.");
            string? metodoTablet;
            await using(var q=Comando(c,tx,"""
                SELECT TOP(1) Detalle FROM dbo.AuditoriaEncomiendaOperativa
                WHERE ReservaId=@id AND Accion=N'PagoTabletPendiente' ORDER BY Id DESC;
                """,("@id",id)))
                metodoTablet=(string?)await q.ExecuteScalarAsync(ct);
            Exigir(metodoTablet is "Efectivo" or "Tarjeta" or "NFC",
                "PagoTabletPendiente","El destinatario todavía no ha solicitado el pago en la Tablet.");
            Exigir(datos.Metodo==(metodoTablet=="NFC"?"Tarjeta":metodoTablet),
                "MetodoNoCoincide","El método debe coincidir con el elegido en la Tablet.",400);
        }
        else {
            Preparacion(o,Ahora);
            if(PagoWebDemo(reservaPago))
                Exigir(datos.Metodo=="Tarjeta","MetodoNoCoincide",
                    "Esta solicitud de prueba se realizó con tarjeta desde la web.",400);
        }
        Exigir(o.Pago!="ConfirmadoManual","PagoYaConfirmado","El pago ya está confirmado.");
        // Tras el depósito, la baja del remitente no puede impedir que el
        // destinatario pague y retire una encomienda ya aceptada.
        if(esDiferido) {
            await using var q=Comando(c,tx,"""
                SELECT COUNT(*) FROM dbo.ParticipantesEncomiendaOperativa
                WHERE ReservaId=@id AND Rol=N'Destinatario'
                  AND Consentimiento=N'Confirmado' AND AceptaEncomienda=1;
                """,("@id",id));
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,
                "ConsentimientosPendientes","Falta la aceptación comprobada del destinatario.");
        }else
            Exigir(await Consentimientos(c,tx,id,ct),
                "ConsentimientosPendientes","Comprueba primero la autorización de WhatsApp del remitente.");
        var tarifa=reservaPago?.TarifaHnl??Tarifa(o.Tamano);
        if(tarifa.HasValue)Exigir(tarifa.Value==datos.ImporteHnl,"ImporteNoCoincide","El importe debe coincidir con la tarifa configurada.",400);
        var motivo=tarifa.HasValue?"Tarifa configurada":Texto(datos.JustificacionImporte,500,"la justificación del importe sin tarifa configurada");
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.EncomiendasOperativas SET PagoEstado=N'ConfirmadoManual',ImporteHnl=@importe,MetodoPago=@metodo,
              PagoReferencia=@referencia,JustificacionImporte=@motivo,PagoAdminId=@admin,PagoConfirmadoUtc=@ahora,Version=Version+1 WHERE ReservaId=@id;
            """,("@importe",datos.ImporteHnl),("@metodo",datos.Metodo),("@referencia",referencia),("@motivo",motivo),("@admin",admin),("@ahora",Ahora.UtcDateTime),("@id",id)))await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,id,null,"Administrador",admin,"PagoConfirmadoManual",o.Estado,
            pagoPrueba?"Operación de pago de prueba confirmada manualmente; no se procesó dinero real. Referencia: "+referencia
                      :"Pago manual verificado; no procede de un cobro simulado. Referencia: "+referencia,ct);
        await tx.CommitAsync(ct);return new{estado="ConfirmadoManual",importeHnl=datos.ImporteHnl};
    }
}
