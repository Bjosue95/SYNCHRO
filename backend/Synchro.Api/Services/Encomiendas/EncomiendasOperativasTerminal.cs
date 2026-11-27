using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Synchro.Api.Services.Encomiendas;

internal sealed partial class EncomiendasOperativasSql
{
    public async Task<object> IdentificarParaRetiro(string pin,int terminal,CancellationToken ct)
    {
        Exigir(pin is {Length:6}&&pin.All(char.IsAsciiDigit),"PinNoValido","No se pudo validar el PIN de retiro.",403);
        await using var c=await Abrir(ct);
        int? identificada=null;
        // La terminal obtiene el identificador interno con el PIN. El destinatario
        // no introduce la referencia pública de la encomienda como otro factor.
        await using(var q=Comando(c,null,"""
            SELECT o.ReservaId,o.DestinatarioId,p.PinHash,p.PinSalt
            FROM dbo.EncomiendasOperativas o JOIN dbo.PinesEncomiendaOperativa p ON p.ReservaId=o.ReservaId
            JOIN dbo.ParticipantesEncomiendaOperativa d ON d.ReservaId=o.ReservaId AND d.Rol=N'Destinatario'
            WHERE o.TerminalId=@terminal AND o.Estado=N'Depositada'
              AND (o.PagoEstado=N'ConfirmadoManual'
                   OR EXISTS (SELECT 1 FROM dbo.AuditoriaEncomiendaOperativa a
                       WHERE a.ReservaId=o.ReservaId AND a.Accion=N'ReservaPrototipoCreada'
                         AND JSON_VALUE(a.Detalle,'$.Pagador')=N'Destinatario'
                         AND JSON_VALUE(a.Detalle,'$.ModalidadPago')=N'Tablet'))
              AND p.Finalidad=N'Retiro' AND p.Activo=1 AND p.ConsumidoUtc IS NULL AND p.EntregadoUtc IS NOT NULL
              AND p.VenceUtc>@ahora AND p.IntentosFallidos<5 AND d.AceptaEncomienda=1;
            """,("@terminal",terminal),("@ahora",Ahora.UtcDateTime))) {
            await using var r=await q.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct)) {
                byte[] hash=(byte[])r[2],salt=(byte[])r[3];
                byte[] calculado=CodigoPinRetiro.CalcularHashEncomienda(pin,r.GetInt32(0),r.GetInt32(1),salt,config,FinalidadPinEncomienda.Retiro);
                try {
                    if(!CodigoPinRetiro.VerificarHash(calculado,hash))continue;
                    Exigir(identificada is null,"PinNoValido","No se pudo validar el PIN de retiro.",403);
                    identificada=r.GetInt32(0);
                } finally {CryptographicOperations.ZeroMemory(hash);CryptographicOperations.ZeroMemory(salt);CryptographicOperations.ZeroMemory(calculado);}
            }
        }
        Exigir(identificada.HasValue,"PinNoValido","No se pudo validar el PIN de retiro.",403);
        return await ConsultarParaRetiro(identificada!.Value,terminal,ct);
    }

    // Acceso libre EXCLUSIVO del prototipo de pruebas: el servidor conserva
    // un identificador interno estable para relacionar depósitos, PIN y retiros.
    // No identifica ni autoriza el navegador, y jamás acepta IDs del cliente.
    public async Task<int> TerminalInternaReservasPruebas(CancellationToken ct)
    {
        if (!config.GetValue<bool>("ReservasPrototipo:AccesoLibrePruebas"))
            throw new OperacionEncomiendaException(
                "AccesoLibreNoHabilitado", "Modo de pruebas no habilitado.", 403);

        await using var conexion = await Abrir(ct);
        await using var transaccion = (SqlTransaction)await conexion
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        int id;
        await using (var buscar = Comando(conexion, transaccion, """
            SELECT Id FROM dbo.Terminales WITH(UPDLOCK,HOLDLOCK)
            WHERE Codigo=N'SYNCHRO-RESERVAS-DEMO';
            """))
        {
            var existente = await buscar.ExecuteScalarAsync(ct);
            id = existente is int actual ? actual : 0;
        }

        if (id == 0)
        {
            await using var crear = Comando(conexion, transaccion, """
                INSERT dbo.Terminales(Codigo,Nombre,Activa,FechaAutorizacion)
                OUTPUT INSERTED.Id
                VALUES(N'SYNCHRO-RESERVAS-DEMO',N'Estación interna de pruebas',1,SYSUTCDATETIME());
                """);
            id = Convert.ToInt32(await crear.ExecuteScalarAsync(ct));
        }
        else
        {
            // No reactivar una estación de pruebas expresamente desactivada.
            await using var comprobar = Comando(conexion, transaccion, """
                SELECT COUNT(*) FROM dbo.Terminales
                WHERE Id=@id AND Activa=1;
                """, ("@id", id));
            Exigir(Convert.ToInt32(await comprobar.ExecuteScalarAsync(ct)) == 1,
                "TerminalDemoDesactivada",
                "La estación interna de pruebas está desactivada.", 503);
        }

        await transaccion.CommitAsync(ct);
        return id;
    }

    // La vinculación (cookie Terminal) identifica la estación. Esta validación
    // adicional determina cuál estación puede atender las reservas web.
    private async Task ExigirTerminalDeReservas(int terminal, CancellationToken ct)
    {
        // El prototipo acepta cualquier tablet; el backend asigna una estación
        // interna para conservar la trazabilidad sin vincular el dispositivo.
        if (config.GetValue<bool>("ReservasPrototipo:AccesoLibrePruebas"))
            return;

        Exigir(terminal > 0, "TerminalNoAutorizada",
            "No se encontró una terminal autorizada.", 403);

        await using var conexion = await Abrir(ct);
        await using var comando = Comando(conexion, null, """
            SELECT COUNT(*),
                   COALESCE(SUM(CASE WHEN Id=@terminal THEN 1 ELSE 0 END), 0)
            FROM dbo.Terminales
            WHERE Activa=1;
            """, ("@terminal", terminal));
        await using var lector = await comando.ExecuteReaderAsync(ct);
        Exigir(await lector.ReadAsync(ct), "TerminalNoAutorizada",
            "No se pudo comprobar la terminal.", 403);
        var activas = lector.GetInt32(0);
        var coincide = lector.GetInt32(1);

        Exigir(coincide == 1, "TerminalNoAutorizada",
            "Esta estación no está activa en el servidor.", 403);

        var configurada = config.GetValue<int?>("ReservasPrototipo:TerminalId");
        if (configurada is > 0)
        {
            Exigir(configurada.Value == terminal, "TerminalNoAutorizada",
                "Esta estación no es la asignada a las reservas web.", 403);
            return;
        }

        // En una instalación de una sola estación, su ID se toma de SQL
        // sin exigir configurar un número que cambia al reinstalar la DB.
        Exigir(activas == 1, "TerminalReservasSinAsignar",
            "Hay varias estaciones activas. El administrador debe definir " +
            "ReservasPrototipo:TerminalId con el ID de la estación de reservas.", 403);
    }

    // La pantalla valida la asignación antes de solicitar fotografía o PIN.
    public async Task<object> EstadoTerminalDeReservas(int terminal, CancellationToken ct)
    {
        await ExigirTerminalDeReservas(terminal, ct);
        return new { autorizada = true };
    }

    // Identifica una reserva web usando exclusivamente su PIN de depósito.
    // No consume el PIN ni confirma el depósito.
    public async Task<object> IdentificarParaDeposito(
        string pin, int terminal, CancellationToken ct)
    {
        Exigir(
            pin is { Length: 6 } && pin.All(char.IsAsciiDigit),
            "PinNoValido",
            "No se pudo validar el PIN de depósito.",
            403
        );

        // Validar que esta estación esté asignada a reservas web. Cuando
        // solo existe una terminal activa, es la estación de reservas.
        // Si existen varias, exigir una asignación explícita (sin permitir
        // que cualquier estación activa use el PIN de una reserva ajena).
        await ExigirTerminalDeReservas(terminal, ct);
        await using var c = await Abrir(ct);
        int? identificada = null;

        await using (var q = Comando(c, null, """
            SELECT o.ReservaId, r.ClientePersonaId,
                   p.PinHash, p.PinSalt
            FROM dbo.EncomiendasOperativas o
            JOIN dbo.PreReservasEncomiendasWeb r
                ON r.Id = o.ReservaId
            JOIN dbo.PinesEncomiendaOperativa p
                ON p.ReservaId = o.ReservaId
            WHERE o.TerminalId IS NULL AND o.Estado IN (N'Preparacion', N'ListaParaDeposito')
              AND (o.PagoEstado = N'ConfirmadoManual'
                OR EXISTS (SELECT 1 FROM dbo.AuditoriaEncomiendaOperativa a
                    WHERE a.ReservaId=o.ReservaId AND a.Accion=N'ReservaPrototipoCreada'
                      AND JSON_VALUE(a.Detalle,'$.Pagador')=N'Destinatario'
                      AND JSON_VALUE(a.Detalle,'$.ModalidadPago')=N'Tablet'))
              AND r.FechaExpiracion > @ahora
              AND p.Finalidad = N'Deposito'
              AND p.Activo = 1
              AND p.ConsumidoUtc IS NULL
              AND p.EntregadoUtc IS NOT NULL
              AND p.VenceUtc > @ahora
              AND p.IntentosFallidos < 5
              AND EXISTS (
                  SELECT 1
                  FROM dbo.AuditoriaEncomiendaOperativa a
                  WHERE a.ReservaId = o.ReservaId
                    AND a.Accion = N'ReservaPrototipoCreada'
              );
            """, ("@ahora", Ahora.UtcDateTime)))
        {
            await using var r = await q.ExecuteReaderAsync(ct);

            while (await r.ReadAsync(ct))
            {
                int reservaId = r.GetInt32(0);
                int remitenteId = r.GetInt32(1);

                byte[] hash = (byte[])r[2];
                byte[] salt = (byte[])r[3];
                byte[]? calculado = null;

                try
                {
                    calculado = CodigoPinRetiro.CalcularHashEncomienda(
                        pin,
                        reservaId,
                        remitenteId,
                        salt,
                        config,
                        FinalidadPinEncomienda.Deposito
                    );

                    if (!CodigoPinRetiro.VerificarHash(calculado, hash))
                        continue;

                    Exigir(
                        identificada is null,
                        "PinNoValido",
                        "No se pudo identificar una reserva única.",
                        403
                    );

                    identificada = reservaId;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(hash);
                    CryptographicOperations.ZeroMemory(salt);

                    if (calculado is not null)
                        CryptographicOperations.ZeroMemory(calculado);
                }
            }
        }

        Exigir(
            identificada.HasValue,
            "PinNoValido",
            "No se pudo validar el PIN de depósito.",
            403
        );

        var reserva = await LeerReserva(c, null, identificada.GetValueOrDefault(), ct);

        Exigir(
            reserva is not null,
            "ReservaNoDisponible",
            "No se encontró una reserva web disponible.",
            404
        );

        return new
        {
            reservaId = identificada.GetValueOrDefault(),
            casillero = reserva!.CodigoCasillero,
            servicio = reserva.Servicio,
            tipoCasillero = reserva.TipoCasillero
        };
    }
    public async Task<object> ConsultarParaRetiro(int id,int terminal,CancellationToken ct)
    {
        await using var c=await Abrir(ct);var o=await Leer(c,null,id,null,ct);
        Exigir(o.TerminalId==terminal&&o.Estado is "Depositada" or "Retirada","NoEncontrada","Encomienda no disponible en esta terminal.",404);
        var reserva=await LeerReserva(c,null,id,ct);
        var diferido=PagoDiferidoDestinatario(reserva);
        Exigir(o.Pago=="ConfirmadoManual"||diferido,
            "PagoPendiente","La encomienda no tiene un pago confirmado.");
        await using var q=Comando(c,null,"SELECT CodigoCasillero FROM dbo.UsosCasillero WHERE Id=@uso",("@uso",o.UsoId));
        return new{encomiendaId=id,estado=o.Estado,tamano=o.Tamano,casillero=(string?)await q.ExecuteScalarAsync(ct),
            pagoConfirmado=o.Pago=="ConfirmadoManual",
            pagoAdicionalRequerido=diferido&&o.Pago!="ConfirmadoManual",
            importeHnl=reserva?.TarifaHnl??Tarifa(o.Tamano),
            pagador=reserva?.Pagador??"Remitente",capturaComoEvidencia=true,
            reserva,versionConsentimientoRetiro=reserva is null?VersionConsentimientoRetiro:VersionCapturaDemostrativa,
            versionCapturaDemostrativa=VersionCapturaDemostrativa,comparacionFacialRequerida=false,conservacionFotografiaDias=30,hardwareSimulado=true};
    }

    public async Task ValidarTerminal(int id,CancellationToken ct)
    {
        await using var c=await Abrir(ct);await using var q=Comando(c,null,"SELECT COUNT(*) FROM dbo.Terminales WHERE Id=@id AND Activa=1",("@id",id));
        Exigir(id>0&&Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"TerminalNoAutorizada","La terminal no está activa.",403);
    }
    public async Task<object> CrearSolicitudDeposito(int terminal,string codigo,CancellationToken ct)
    {
        codigo=Texto(codigo,10,"el casillero").ToUpperInvariant();
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.Terminales WHERE Id=@id AND Activa=1",("@id",terminal)))
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"TerminalNoAutorizada","Terminal no disponible.",403);
        await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.Casilleros WHERE Codigo=@codigo AND Tipo=N'Normal' AND Estado=N'Disponible'",("@codigo",codigo)))
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"CasilleroNoDisponible","Casillero no disponible.");
        var id=Guid.NewGuid();var vence=Ahora.AddMinutes(2);
        await using(var q=Comando(c,tx,"INSERT dbo.SolicitudesDepositoEncomiendaOperativa(Id,TerminalId,CodigoCasillero,CreadaUtc,VenceUtc) VALUES(@id,@terminal,@codigo,@ahora,@vence)",("@id",id),("@terminal",terminal),("@codigo",codigo),("@ahora",Ahora.UtcDateTime),("@vence",vence.UtcDateTime)))await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,null,null,"Terminal",terminal,"SolicitudDepositoCreada","Pendiente","Solicitud temporal para autorización desde la cuenta del remitente.",ct);
        await tx.CommitAsync(ct);return new{solicitudId=id,venceUtc=vence,casillero=codigo,aperturaRealizada=false};
    }

    public async Task<object> ConsultarSolicitudDeposito(Guid id,int terminal,CancellationToken ct)
    {
        await using var c=await Abrir(ct);await using var q=Comando(c,null,"""
            SELECT s.Estado,s.VenceUtc,s.CodigoCasillero,s.ReservaId FROM dbo.SolicitudesDepositoEncomiendaOperativa s
            JOIN dbo.Terminales t ON t.Id=s.TerminalId AND t.Activa=1 WHERE s.Id=@id AND s.TerminalId=@terminal
            """,("@id",id),("@terminal",terminal));
        await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"SolicitudNoEncontrada","Solicitud no encontrada.",404);
        return new{solicitudId=id,estado=Fecha(r,1)<=Ahora&&r.GetString(0) is "Pendiente" or "Autorizada"?"Vencida":r.GetString(0),venceUtc=Fecha(r,1),casillero=r.GetString(2),encomiendaId=r.IsDBNull(3)?(int?)null:r.GetInt32(3),hardwareSimulado=true};
    }

    private async Task<CondicionesEncomiendaOperativa> Condiciones(SqlConnection c,SqlTransaction tx,Operacion o,CancellationToken ct)
    {
        var cuenta=await LeerConsentimientoCuenta(c,tx,o.RemitenteId,ct);
        bool activa=cuenta.Activa;
        var remitente=EstadoConsentimientoEncomienda.Pendiente;var destinatario=EstadoConsentimientoEncomienda.Pendiente;bool acepta=false;
        await using(var q=Comando(c,tx,"SELECT Rol,Consentimiento,AceptaEncomienda,Telefono FROM dbo.ParticipantesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE ReservaId=@id",("@id",o.Id))) {
            await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)) {
                var estado=Enum.Parse<EstadoConsentimientoEncomienda>(r.GetString(1));
                if(r.GetString(0)=="Remitente")remitente=Enum.Parse<EstadoConsentimientoEncomienda>(EstadoRemitente(cuenta,r.GetString(3),r.GetString(1)));else{destinatario=estado;acepta=r.GetBoolean(2);}
            }
        }
        var reserva=await LeerReserva(c,tx,o.Id,ct);
        var personal=reserva?.Servicio=="Personal";
        if(personal){destinatario=remitente;acepta=true;}
        return new(o.Id,o.RemitenteId,o.DestinatarioId,Enum.Parse<EstadoEncomiendaOperativa>(o.Estado),activa,remitente,destinatario,acepta,
            o.Pago=="ConfirmadoManual"?EstadoPagoEncomienda.ConfirmadoManual:EstadoPagoEncomienda.Pendiente,
            o.Vence,personal,PagoDiferidoDestinatario(reserva));
    }
    private async Task<CondicionesPinEncomiendaOperativa> CondicionesPin(SqlConnection c,SqlTransaction tx,int reserva,Guid id,FinalidadPinEncomienda finalidad,CancellationToken ct)
    {
        await using var q=Comando(c,tx,"SELECT Finalidad,Activo,IntentosFallidos,VenceUtc,ConsumidoUtc,RevocadoUtc,EntregadoUtc FROM dbo.PinesEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND ReservaId=@reserva",("@id",id),("@reserva",reserva));
        await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"PinNoDisponible","PIN no disponible.");
        return new(Enum.Parse<FinalidadPinEncomienda>(r.GetString(0)),r.GetBoolean(1)&&r.GetString(0)==finalidad.ToString(),!r.IsDBNull(6),!r.IsDBNull(4),!r.IsDBNull(5),r.GetInt32(2),5,Fecha(r,3));
    }
    private async Task<EvidenciaEncomiendaOperativa> Evidencia(SqlConnection c,SqlTransaction tx,Guid id,CancellationToken ct)
    {
        await using var q=Comando(c,tx,"SELECT Id,ReservaId,TerminalId,PersonaId,Tipo,EmitidaUtc,VenceUtc,ConsumidaUtc,VersionConsentimiento,ComparacionRemitenteAprobada FROM dbo.EvidenciasEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",("@id",id));
        await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"EvidenciaNoDisponible","Evidencia no disponible.");var tipo=Enum.Parse<TipoEvidenciaEncomienda>(r.GetString(4));
        return new(r.GetGuid(0),r.GetInt32(1),r.GetInt32(2),r.GetInt32(3),tipo,Fecha(r,5),Fecha(r,6),!r.IsDBNull(7),r.GetString(8)==VersionCapturaDemostrativa||r.GetString(8)==(tipo==TipoEvidenciaEncomienda.VerificacionRemitente?VersionConsentimiento:VersionConsentimientoRetiro),r.GetBoolean(9));
    }

    public async Task<object> ConfirmarDeposito(Guid solicitud,int terminal,bool comprobado,CancellationToken ct,double[]? descriptorPersonal=null)
    {
        Exigir(comprobado,"DepositoNoComprobado","La terminal debe confirmar expresamente el depósito.",400);
        await using var c=await Abrir(ct);
        int id;
        // Descubrir el vínculo antes de la transacción; después bloquear operación -> solicitud,
        // igual que AutorizarDeposito, y comprobar nuevamente el vínculo bajo bloqueo.
        await using(var q=Comando(c,null,"SELECT ReservaId FROM dbo.SolicitudesDepositoEncomiendaOperativa WHERE Id=@id AND TerminalId=@terminal AND ReservaId IS NOT NULL",("@id",solicitud),("@terminal",terminal)))
            id=await q.ExecuteScalarAsync(ct) is int reservaId?reservaId:throw new OperacionEncomiendaException("SolicitudNoDisponible","Solicitud no autorizada para esta terminal.");
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);
        var o=await Leer(c,tx,id,null,ct);
        Guid evidenciaId,pinId;string codigo,estado;DateTimeOffset vence;
        await using(var q=Comando(c,tx,"SELECT ReservaId,EvidenciaId,PinId,CodigoCasillero,Estado,VenceUtc FROM dbo.SolicitudesDepositoEncomiendaOperativa WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND TerminalId=@terminal AND ReservaId=@reserva",("@id",solicitud),("@terminal",terminal),("@reserva",id))) {
            await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"SolicitudNoDisponible","Solicitud no autorizada para esta terminal.");
            id=r.GetInt32(0);evidenciaId=r.GetGuid(1);pinId=r.GetGuid(2);codigo=r.GetString(3);estado=r.GetString(4);vence=Fecha(r,5);
        }
        if(estado=="Depositada"&&o.UsoId.HasValue){await tx.CommitAsync(ct);return new{depositoConfirmado=true,usoId=o.UsoId,casillero=codigo,hardwareSimulado=true};}
        Exigir(estado=="Autorizada"&&vence>Ahora,"SolicitudVencida","La autorización de depósito no está vigente.");
        bool activa,disponible;
        var reserva=await LeerReserva(c,tx,id,ct);
        var personal=reserva?.Servicio=="Personal";
        Exigir(!personal||descriptorPersonal is not null,"RostroV3Requerido","La reserva personal necesita el registro facial V3.",400);
        await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.Terminales WHERE Id=@id AND Activa=1",("@id",terminal)))activa=Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1;
        await ExigirCompartimento(c,tx,reserva,codigo,o.Tamano,ct);
        await using(var q=Comando(c,tx,"""
            SELECT COUNT(*) FROM dbo.UsosCasillero u WITH(UPDLOCK,HOLDLOCK)
            WHERE u.CodigoCasillero=@codigo AND u.FechaRetiro IS NULL AND u.Estado IN(N'Reservado',N'Ocupado');
            """,("@codigo",codigo)))disponible=Convert.ToInt32(await q.ExecuteScalarAsync(ct))==0;
        await using(var q=Comando(c,tx,"""
            SELECT COUNT(*) FROM dbo.EvidenciasEncomiendaOperativa e LEFT JOIN dbo.ReferenciasEncomiendaV3 v
             ON v.ReferenciaId=e.ReferenciaId AND v.PersonaId=e.PersonaId AND v.RevalidarUtc>@ahora
            WHERE e.Id=@id AND e.PinValidadoId=@pin AND (e.VersionConsentimiento=@demo OR v.ReferenciaId IS NOT NULL)
            """,("@ahora",Ahora.UtcDateTime),("@id",evidenciaId),("@pin",pinId),("@demo",VersionCapturaDemostrativa)))
            Exigir(Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1,"EvidenciaNoDisponible","La captura o el PIN vinculados ya no son válidos.");
        var decision=PoliticaEncomiendaOperativa.Depositar(await Condiciones(c,tx,o,ct),await CondicionesPin(c,tx,id,pinId,FinalidadPinEncomienda.Deposito,ct),await Evidencia(c,tx,evidenciaId,ct),terminal,activa,true,disponible,Ahora);
        Exigir(decision.Permitida,decision.Codigo,"No se cumplen las condiciones para depositar.");
        int uso;
        await using(var q=Comando(c,tx,"""
            INSERT dbo.UsosCasillero(CodigoCasillero,TipoServicio,FechaReserva,FechaIngreso,Estado,OrigenReserva)
            OUTPUT INSERTED.Id VALUES(@codigo,@servicio,@ahora,@ahora,N'Ocupado',N'Web')
            """,("@codigo",codigo),("@ahora",Ahora.UtcDateTime),("@servicio",reserva?.Servicio=="Personal"?"ResguardoPersonal":"Encomienda")))uso=Convert.ToInt32(await q.ExecuteScalarAsync(ct));
        await using(var q=Comando(c,tx,"""
            INSERT dbo.ParticipantesUso(UsoId,PersonaId,Rol) VALUES(@uso,@remitente,N'Remitente'),(@uso,@destinatario,N'Destinatario');
            UPDATE dbo.Casilleros SET Estado=N'Ocupado' WHERE Codigo=@codigo;
            UPDATE dbo.EncomiendasOperativas SET UsoId=@uso,TerminalId=@terminal,Estado=N'Depositada',FechaDepositoUtc=@ahora,Version=Version+1 WHERE ReservaId=@id;
            UPDATE dbo.PreReservasEncomiendasWeb SET Estado=N'Convertida' WHERE Id=@id;
            UPDATE dbo.PinesEncomiendaOperativa SET Activo=0,ConsumidoUtc=@ahora WHERE Id=@pin AND @personal=0;
            UPDATE dbo.EvidenciasEncomiendaOperativa SET ConsumidaUtc=@ahora WHERE Id=@ev;
            UPDATE dbo.SolicitudesDepositoEncomiendaOperativa SET Estado=N'Depositada' WHERE Id=@solicitud;
            """,("@uso",uso),("@remitente",o.RemitenteId),("@destinatario",o.DestinatarioId),("@codigo",codigo),("@terminal",terminal),("@ahora",Ahora.UtcDateTime),("@id",id),("@pin",pinId),("@ev",evidenciaId),("@solicitud",solicitud),("@personal",personal)))await q.ExecuteNonQueryAsync(ct);
        if(personal)await RegistrarRostroPersonalWeb(c,tx,o,uso,descriptorPersonal!,terminal,ct);
        await Auditar(c,tx,id,null,"Terminal",terminal,"DepositoConfirmado","Depositada","Depósito confirmado en terminal; apertura física simulada.",ct,pinId,evidenciaId);
        await tx.CommitAsync(ct);return new{depositoConfirmado=true,usoId=uso,casillero=codigo,hardwareSimulado=true};
    }

    private const string ContextoRecogidaEncomienda="SYNCHRO.Encomienda.Recogida.v1";
    private sealed record PermisoRecogidaEncomienda(int Id,int Terminal,Guid Pin,Guid Evidencia,string Casillero);

    public Task<object> AutorizarRecogidaEncomienda(int id,int terminal,RetiroOperativoSolicitud datos,CancellationToken ct)
        => Retirar(id,terminal,datos,ct,soloAutorizar:true);

    public async Task<object> ConfirmarRecogidaEncomienda(int id,int terminal,string? autorizacion,bool recogidaComprobada,CancellationToken ct)
    {
        Exigir(recogidaComprobada,"EntregaNoComprobada","Confirma la recogida de la encomienda.",400);
        Exigir(autorizacion is {Length:>=32 and <=4096},"AutorizacionInvalida","Repite la verificación de retiro.",403);
        PermisoRecogidaEncomienda permiso;
        try { permiso=JsonSerializer.Deserialize<PermisoRecogidaEncomienda>(proteccion.CreateProtector(ContextoRecogidaEncomienda).ToTimeLimitedDataProtector().Unprotect(autorizacion!))??throw new JsonException(); }
        catch(Exception ex) when(ex is CryptographicException or JsonException or FormatException) { throw new OperacionEncomiendaException("AutorizacionVencida","La autorización venció o no es válida. Repite la verificación.",403); }
        Exigir(permiso.Id==id&&permiso.Terminal==terminal,"AutorizacionInvalida","La autorización no corresponde a esta operación.",403);
        return await Retirar(id,terminal,new(permiso.Evidencia,"",true),ct,permiso:permiso);
    }

    public Task<object> Retirar(int id,int terminal,RetiroOperativoSolicitud datos,CancellationToken ct)
        => Retirar(id,terminal,datos,ct,false,null);

    private async Task<object> Retirar(int id,int terminal,RetiroOperativoSolicitud datos,CancellationToken ct,bool soloAutorizar=false,PermisoRecogidaEncomienda? permiso=null)
    {
        Exigir(soloAutorizar||datos.EntregaComprobada,"EntregaNoComprobada","La terminal debe confirmar expresamente la entrega.",400);
        await using var c=await Abrir(ct);await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,null,ct);
        if(permiso is not null&&o.Estado=="Retirada"&&o.TerminalId==terminal) {
            await using var anterior=Comando(c,tx,"SELECT COUNT(*) FROM dbo.AuditoriaEncomiendaOperativa WHERE ReservaId=@id AND Accion=N'EntregaConfirmada' AND PinId=@pin AND EvidenciaId=@ev",("@id",id),("@pin",permiso.Pin),("@ev",permiso.Evidencia));
            Exigir(Convert.ToInt32(await anterior.ExecuteScalarAsync(ct))==1,"RetiroNoDisponible","La operación ya fue cerrada.",409);
            await tx.CommitAsync(ct);return new{retiroConfirmado=true,casillero=permiso.Casillero,hardwareSimulado=true,idempotente=true};
        }
        Exigir(o.Estado=="Depositada"&&o.TerminalId==terminal&&o.UsoId.HasValue,"RetiroNoDisponible","Retiro no disponible para esta terminal.");
        Exigir((await LeerReserva(c,tx,id,ct))?.Servicio!="Personal","RostroV3Requerido","Usa Mis pertenencias y la verificación facial V3.",403);
        var pin=permiso?.Pin??await VerificarPin(c,tx,o,FinalidadPinEncomienda.Retiro,datos.Pin,terminal,ct);
        bool activa;await using(var q=Comando(c,tx,"SELECT COUNT(*) FROM dbo.Terminales WHERE Id=@id AND Activa=1",("@id",terminal)))activa=Convert.ToInt32(await q.ExecuteScalarAsync(ct))==1;
        var condicionesPin=await CondicionesPin(c,tx,id,pin,FinalidadPinEncomienda.Retiro,ct);
        var evidencia=await Evidencia(c,tx,datos.EvidenciaId,ct);
        var decision=PoliticaEncomiendaOperativa.Retirar(await Condiciones(c,tx,o,ct),condicionesPin,evidencia,terminal,activa,true,Ahora);
        Exigir(decision.Permitida,decision.Codigo,"No se cumplen las condiciones para retirar.");
        var reserva=await LeerReserva(c,tx,id,ct);
        string codigo;await using(var q=Comando(c,tx,"SELECT CodigoCasillero FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND TipoServicio=@servicio AND Estado=N'Ocupado' AND FechaRetiro IS NULL",("@id",o.UsoId),("@servicio",reserva?.Servicio=="Personal"?"ResguardoPersonal":"Encomienda")))
            codigo=(string?)await q.ExecuteScalarAsync(ct)??throw new OperacionEncomiendaException("UsoNoDisponible","El casillero no tiene una entrega pendiente.");
        if(permiso is not null)Exigir(permiso.Casillero==codigo,"RetiroNoDisponible","El casillero no coincide con la autorización.",403);
        if(soloAutorizar) {
            var vence=evidencia.VenceUtc<condicionesPin.VenceUtc?evidencia.VenceUtc:condicionesPin.VenceUtc;
            Exigir(vence>Ahora,"AutorizacionVencida","Repite la captura y el PIN.",403);
            var token=proteccion.CreateProtector(ContextoRecogidaEncomienda).ToTimeLimitedDataProtector().Protect(JsonSerializer.Serialize(new PermisoRecogidaEncomienda(id,terminal,pin,datos.EvidenciaId,codigo)),vence);
            await Auditar(c,tx,id,o.DestinatarioId,"Terminal",terminal,"RecogidaEncomiendaAutorizada","Depositada","PIN y evidencia comprobados; pendiente recogida.",ct,pin,datos.EvidenciaId);
            await tx.CommitAsync(ct);
            return new{retiroAutorizado=true,retiroConfirmado=false,casillero=codigo,estadoCasillero="Ocupado",autorizacionRetiro=token,venceUtc=vence,hardwareSimulado=true};
        }
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.FotografiasEncomiendaOperativa SET EntregaUtc=@ahora,EliminarUtc=@eliminar
             WHERE EvidenciaId=@ev AND EliminarUtc>@ahora;
            """,("@ahora",Ahora.UtcDateTime),("@eliminar",Ahora.AddDays(30).UtcDateTime),("@ev",datos.EvidenciaId)))
            Exigir(await q.ExecuteNonQueryAsync(ct)==1,"FotografiaNoDisponible","La evidencia de entrega no está disponible.");
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.Casilleros SET Estado=N'Disponible' WHERE Codigo=@codigo AND Estado=N'Ocupado'
             AND NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK) WHERE CodigoCasillero=@codigo AND Id<>@uso AND FechaRetiro IS NULL AND Estado IN(N'Reservado',N'Ocupado'));
            """,("@codigo",codigo),("@uso",o.UsoId)))Exigir(await q.ExecuteNonQueryAsync(ct)==1,"CasilleroNoDisponible","No fue posible liberar el casillero de forma segura.");
        await using(var q=Comando(c,tx,"""
            UPDATE dbo.UsosCasillero SET Estado=N'Finalizado',FechaRetiro=@ahora WHERE Id=@uso;
            UPDATE dbo.EncomiendasOperativas SET Estado=N'Retirada',FechaEntregaUtc=@ahora,Version=Version+1 WHERE ReservaId=@id;
            UPDATE dbo.PreReservasEncomiendasWeb SET Estado=N'Retirada' WHERE Id=@id;
            UPDATE dbo.PinesEncomiendaOperativa SET Activo=0,ConsumidoUtc=@ahora WHERE Id=@pin;
            UPDATE dbo.EvidenciasEncomiendaOperativa SET ConsumidaUtc=@ahora,PinValidadoId=@pin WHERE Id=@ev;
            """,("@ahora",Ahora.UtcDateTime),("@uso",o.UsoId),("@id",id),("@pin",pin),("@ev",datos.EvidenciaId)))await q.ExecuteNonQueryAsync(ct);
        await Auditar(c,tx,id,o.DestinatarioId,"Terminal",terminal,"EntregaConfirmada","Retirada","PIN de retiro consumido; evidencia ligada a casillero y entrega. Sin cobro adicional; apertura física simulada.",ct,pin,datos.EvidenciaId);
        await tx.CommitAsync(ct);return new{retiroConfirmado=true,pagoAdicionalRequerido=false,casillero=codigo,hardwareSimulado=true};
    }
}




