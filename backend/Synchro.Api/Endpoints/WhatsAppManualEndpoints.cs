using Synchro.Api.Services.Pagos;
using System.Globalization;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Synchro.Api.Services.RostroSimulado;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Endpoints;

// Solo prototipo: confirmacion humana de WhatsApp Business. No integra la API oficial.
public static class WhatsAppManualEndpoints
{
    private const int MaxRostroBytes = 1024 * 1024;

    public sealed record ConfirmarAutorizacionPeticion(
        Guid IntentoId, string? CodigoAutorizacion, string? TelefonoCliente,
        bool MensajeComprobado);
    public sealed record OperacionPeticion(Guid IntentoId);
    public sealed record EmitirPinPeticion(Guid IntentoId, bool Reemplazar);
    public sealed record MarcarEnviadoPeticion(Guid IntentoId, bool EnvioComprobado);
    public sealed record FinalizarRetiroPagadoPeticion(
        Guid CobroId, string? AutorizacionPago)
    {
        public override string ToString() =>
            "Solicitud protegida de finalizacion pagada";
    }
    public sealed record ConfirmarCobroRetiroPeticion(
        Guid CobroId, string? MetodoPago, bool PagoComprobado)
    {
        public override string ToString() =>
            "Confirmacion protegida de pago simulado";
    }
    public sealed record PrepararCobroPersonalPeticion(string? AutorizacionPago)
    {
        public override string ToString() => "Solicitud protegida de cobro";
    }

    public static void MapWhatsAppManualEndpoints(this WebApplication app)
    {
        // El modulo NO existe fuera de Development con BD aislada.
        if (!app.Environment.IsDevelopment() ||
            !app.Configuration.GetValue<bool>("WhatsAppManual:Habilitado")) return;

        string? cadena = app.Configuration.GetConnectionString("SynchroDB");
        var cs = string.IsNullOrWhiteSpace(cadena) ? null : new SqlConnectionStringBuilder(cadena);
        if (cs is null ||
            !string.Equals(cs.InitialCatalog, "SynchroDB_Pruebas", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(cs.DataSource, @"(localdb)\SYNCHRO_Aislado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WhatsApp manual solo se permite en el entorno SQL aislado.");

        var numero = app.Configuration["WhatsAppManual:NumeroE164"]?.Trim().TrimStart('+');
        if (numero is null || numero.Length != 11 || !numero.StartsWith("504", StringComparison.Ordinal) ||
            !numero.All(char.IsAsciiDigit))
            throw new InvalidOperationException("Configurar WhatsAppManual:NumeroE164 como 504XXXXXXXX.");

        // Autoservicio publico solo en Development y SQL aislado.
        // Las acciones del operador conservan su autenticacion.
        var terminal = app.MapGroup("/api/terminales/guardar/whatsapp")
            .RequireRateLimiting("OperacionesTerminal");

        // Verificacion de rostro simulada + PIN; solo en el modulo manual aislado.
        app.MapRetiroFacialManualEndpoints(cadena ?? throw new InvalidOperationException("Conexion SynchroDB no configurada."));

        // SYNCHRO-COBRO-PERSONAL-PREPARAR-V1
        // Solo crea una orden pendiente. No confirma pagos.
        // SYNCHRO-COBRO-ATOMICO-V1
        // Las tarifas vienen solo de la configuracion del servidor.
        // SQL calcula minutos e importe bajo bloqueo.
        terminal.MapPost("/retiro/cobro/preparar", async (
            PrepararCobroPersonalPeticion datos,
            HttpContext http,
            IAntiforgery csrf,
            IDataProtectionProvider proteccion,
            IConfiguration configuracion) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);

            string? token = datos.AutorizacionPago;

            if (token is null || token.Length is < 32 or > 4096)
                return Results.BadRequest(new
                {
                    mensaje = "Autorizacion de pago invalida."
                });

            int usoId;

            try
            {
                string valor = proteccion
                    .CreateProtector(
                        "SYNCHRO.RostroManual.PagoPendiente.v1")
                    .ToTimeLimitedDataProtector()
                    .Unprotect(token);

                if (!int.TryParse(
                    valor,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out usoId) || usoId <= 0)
                    return Results.BadRequest(new
                    {
                        mensaje = "Autorizacion de pago invalida."
                    });
            }
            catch (CryptographicException)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Autorizacion vencida. Verifica nuevamente tu PIN."
                });
            }

            if (!int.TryParse(
                configuracion["CobroRetiroPersonal:MinutosPorBloque"],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int minutosPorBloque) ||
                minutosPorBloque is < 1 or > 1440)
                return Results.Problem(
                    statusCode: 503,
                    title: "Duracion de tarifa no configurada.");

            // No existen precios predeterminados.
            // Cada tamano necesita una tarifa configurada.
            decimal? LeerTarifa(string tamano)
            {
                string? valor = configuracion[
                    $"CobroRetiroPersonal:Tarifas:{tamano}"];

                if (!decimal.TryParse(
                    valor,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal precio))
                    return null;

                if (precio <= 0m ||
                    precio > 100000m ||
                    decimal.Round(precio, 2) != precio)
                    return null;

                return precio;
            }

            decimal? pequeno = LeerTarifa("Pequeno");
            decimal? mediano = LeerTarifa("Mediano");
            decimal? grande = LeerTarifa("Grande");

            // Ninguna tarifa -> no se pueden generar cobros.
            if (pequeno is null &&
                mediano is null &&
                grande is null)
                return Results.Problem(
                    statusCode: 503,
                    title: "Tarifas de resguardo no configuradas.");

            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);

                await using var preparar = new SqlCommand(
                    "dbo.PrepararCobroRetiroPersonalSynchro",
                    db)
                {
                    CommandType = CommandType.StoredProcedure
                };

                preparar.Parameters.Add(
                    "@UsoId", SqlDbType.Int).Value = usoId;

                preparar.Parameters.Add(
                    "@MinutosPorBloque", SqlDbType.Int)
                    .Value = minutosPorBloque;

                void AgregarTarifa(string nombre, decimal? valor)
                {
                    var parametro = preparar.Parameters.Add(
                        nombre, SqlDbType.Decimal);

                    parametro.Precision = 12;
                    parametro.Scale = 2;
                    parametro.Value = valor.HasValue
                        ? valor.Value
                        : DBNull.Value;
                }

                AgregarTarifa("@TarifaPequenoHnl", pequeno);
                AgregarTarifa("@TarifaMedianoHnl", mediano);
                AgregarTarifa("@TarifaGrandeHnl", grande);

                await using var resultado =
                    await preparar.ExecuteReaderAsync(
                        http.RequestAborted);

                if (!await resultado.ReadAsync(
                    http.RequestAborted))
                    return Results.Problem(
                        statusCode: 503,
                        title: "No se genero la orden de cobro.");

                Guid cobroId = resultado.GetGuid(
                    resultado.GetOrdinal("CobroId"));

                int minutos = resultado.GetInt32(
                    resultado.GetOrdinal("MinutosUtilizados"));

                decimal importe = resultado.GetDecimal(
                    resultado.GetOrdinal("ImporteHnl"));

                string estado = resultado.GetString(
                    resultado.GetOrdinal("Estado"));

                string casillero = resultado.GetString(
                    resultado.GetOrdinal("CodigoCasillero"));

                string tamano = resultado.GetString(
                    resultado.GetOrdinal("TamanoCasillero"));

                string descripcion = resultado.GetString(
                    resultado.GetOrdinal("DescripcionTarifa"));

                if (estado != "Pendiente")
                    return Results.Conflict(new
                    {
                        mensaje = "La orden no esta pendiente."
                    });

                return Results.Ok(new
                {
                    cobroId,
                    codigoCasillero = casillero,
                    tamanoCasillero = tamano,
                    minutosUtilizados = minutos,
                    importeHnl = importe,
                    descripcionTarifa = descripcion,
                    moneda = "HNL",
                    estadoPago = "Pendiente",
                    pagoConfirmado = false,
                    retiroConfirmado = false
                });
            }
            catch (SqlException ex) when (ex.Number == 57810)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "Tarifa no configurada para este casillero.");
            }
            catch (SqlException ex) when (
                ex.Number is >= 57803 and <= 57811)
            {
                return Results.Conflict(new
                {
                    mensaje = "No fue posible preparar el cobro."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "Servicio de cobros no disponible.");
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("LoginLimitado");
        // Token CSRF sin exigir vinculacion de tablets.
        // SYNCHRO-RETIRO-PAGADO-HTTP-V1
        // Solo permite finalizar despues de confirmar el pago.
        terminal.MapPost("/retiro/cobro/finalizar", async (
            FinalizarRetiroPagadoPeticion datos,
            HttpContext http,
            IAntiforgery csrf,
            IDataProtectionProvider proteccion,
            IConfiguration configuracion) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!http.Request.IsHttps ||
                !await csrf.IsRequestValidAsync(http))
                return Results.StatusCode(403);

            // No habilitar este flujo antes de completar la
            // configuracion y sus pruebas.
            if (!configuracion.GetValue<bool>(
                "CobroRetiroPersonal:Obligatorio"))
                return Results.Conflict(new
                {
                    mensaje = "El retiro pagado aun no esta habilitado.",
                    retiroConfirmado = false
                });

            if (datos.CobroId == Guid.Empty ||
                datos.AutorizacionPago is null ||
                datos.AutorizacionPago.Length is < 32 or > 4096)
                return Results.BadRequest(new
                {
                    mensaje = "Autorizacion de retiro invalida."
                });

            int usoId;

            try
            {
                // El uso se obtiene del token protegido generado
                // despues de validar el rostro y el PIN.
                string protegido = proteccion
                    .CreateProtector(
                        "SYNCHRO.RostroManual.PagoPendiente.v1")
                    .ToTimeLimitedDataProtector()
                    .Unprotect(datos.AutorizacionPago);

                if (!int.TryParse(
                    protegido,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out usoId) || usoId <= 0)
                    return Results.BadRequest(new
                    {
                        mensaje = "Autorizacion de retiro invalida."
                    });
            }
            catch (CryptographicException)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Autorizacion vencida. Verifica nuevamente tu identidad."
                });
            }
            catch (FormatException)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Autorizacion de retiro invalida."
                });
            }

            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);

                await using var finalizar = new SqlCommand(
                    "dbo.FinalizarRetiroPersonalPagadoSynchro",
                    db)
                {
                    CommandType = CommandType.StoredProcedure
                };

                finalizar.Parameters.Add(
                    "@UsoId", SqlDbType.Int).Value = usoId;

                finalizar.Parameters.Add(
                    "@CobroId", SqlDbType.UniqueIdentifier)
                    .Value = datos.CobroId;

                // El procedimiento comprueba dentro de su
                // transaccion SQL el pago confirmado, el estado
                // del casillero y el PIN no consumido.
                await using var resultado =
                    await finalizar.ExecuteReaderAsync(
                        http.RequestAborted);

                if (!await resultado.ReadAsync(
                    http.RequestAborted))
                    return Results.Problem(
                        statusCode: 503,
                        title: "No se obtuvo confirmacion del retiro.");

                bool confirmado = resultado.GetBoolean(
                    resultado.GetOrdinal("RetiroConfirmado"));

                if (!confirmado)
                    return Results.Conflict(new
                    {
                        mensaje = "El retiro no fue confirmado."
                    });

                string codigo = resultado.GetString(
                    resultado.GetOrdinal("CodigoCasillero"));

                string estado = resultado.GetString(
                    resultado.GetOrdinal("EstadoCasillero"));

                decimal importe = resultado.GetDecimal(
                    resultado.GetOrdinal("ImporteHnl"));

                DateTime fecha = DateTime.SpecifyKind(
                    resultado.GetDateTime(
                        resultado.GetOrdinal("FechaRetiro")),
                    DateTimeKind.Utc);

                return Results.Ok(new
                {
                    retiroConfirmado = true,
                    pagoSimulado = true,
                    pagoAplicado = true,
                    codigoCasillero = codigo,
                    estadoCasillero = estado,
                    importeHnl = importe,
                    moneda = "HNL",
                    fechaRetiro = fecha,
                    hardwareSimulado = true
                });
            }
            catch (SqlException ex) when (
                ex.Number is >= 57703 and <= 57714)
            {
                return Results.Conflict(new
                {
                    mensaje = "El pago o la autorizacion ya no permite finalizar el retiro.",
                    retiroConfirmado = false
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudo completar el retiro pagado.");
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("OperacionesTerminal");
        terminal.MapGet("/csrf-token", (HttpContext http, IAntiforgery antiforgery) =>
        {
            if (!http.Request.IsHttps)
                return Results.StatusCode(403);

            http.Response.Headers.CacheControl = "no-store";

            var tokens = antiforgery.GetAndStoreTokens(http);

            return Results.Ok(new {
                csrfToken = tokens.RequestToken
            });
        })
        .AllowAnonymous()
        .RequireRateLimiting("LoginLimitado");

        terminal.MapPost("/iniciar", async (HttpContext http, IAntiforgery antiforgery, IDataProtectionProvider proteccion) =>
        {
            if (!await TerminalSegura(http, antiforgery)) return Results.StatusCode(403);
            http.Response.Headers.CacheControl = "no-store";
            string codigo = http.Request.Headers["X-SYNCHRO-CASILLERO"].ToString().Trim();
            if (!CodigoValido(codigo) || http.Request.ContentType != "application/octet-stream" ||
                http.Request.Headers["X-SYNCHRO-CONSENTIMIENTO"].ToString() != "ACEPTO")
                return Results.BadRequest(new { mensaje = "Solicitud de autorizacion invalida." });

            // Verificar la captura PNG; el modo manual no retiene biometria.
            byte[] rostro;
            try { rostro = await LeerLimitado(http.Request.Body, MaxRostroBytes, http.RequestAborted); }
            catch (InvalidDataException) { return Results.BadRequest(new { mensaje = "Captura demasiado grande." }); }
            if (rostro.Length < 16 || !rostro.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
            {
                CryptographicOperations.ZeroMemory(rostro);
                return Results.BadRequest(new { mensaje = "Captura PNG no valida." });
            }

            // Human.js ya detecto el rostro: el servidor valida el PNG y crea
            // un patron visual SIMULADO, NO un descriptor biometrico real.
            double[]? patron = PatronVisualSimulado.Extraer(rostro);
            if (patron is null)
            {
                CryptographicOperations.ZeroMemory(rostro);
                return Results.BadRequest(new { mensaje = "Imagen facial PNG no valida para la demostracion." });
            }
            byte[]? patronProtegido = null;
            var id = Guid.NewGuid();
            string codigoAutorizacion = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            byte[] tokenHash = SHA256.HashData(Convert.FromHexString(token));

            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using var tx = (SqlTransaction)await db.BeginTransactionAsync(http.RequestAborted);
                int usoId = await CrearReservaAsync(db, tx, codigo, http.RequestAborted);
                // Asociacion interna por UsoId. No se muestra al cliente.
                patronProtegido = RetiroFacialManualEndpoints.ProtegerPatron(proteccion, usoId, patron);
                await using (var guardarRostro = new SqlCommand("""
                    INSERT dbo.RostrosRetiroPersonalManual(UsoId, PatronProtegido, Modelo)
                    VALUES (@Uso, @Patron, @Modelo);
                    """, db, tx))
                {
                    guardarRostro.Parameters.Add("@Uso", SqlDbType.Int).Value = usoId;
                    guardarRostro.Parameters.Add("@Patron", SqlDbType.VarBinary, -1).Value = patronProtegido;
                    guardarRostro.Parameters.Add("@Modelo", SqlDbType.NVarChar, 60).Value = PatronVisualSimulado.Modelo;
                    await guardarRostro.ExecuteNonQueryAsync(http.RequestAborted);
                }
                await using var comando = new SqlCommand("""
                    IF NOT EXISTS (SELECT 1 FROM dbo.Casilleros WHERE Codigo=@Codigo AND Estado=N'Reservado' AND Tipo=N'Normal')
                        THROW 57101, 'Casillero no disponible.', 1;
                    INSERT INTO dbo.AutorizacionesWhatsAppManuales
                    (Id,CodigoAutorizacion,CodigoCasillero,TokenConsultaHash,Estado,VenceUtc,UsoId)
                    SELECT @Id,@CodigoAutorizacion,@Codigo,@Hash,N'Pendiente',FechaVencimiento,@Uso FROM dbo.UsosCasillero WHERE Id=@Uso;
                    """, db, tx);
                comando.Parameters.Add("@Uso", SqlDbType.Int).Value = usoId;
                comando.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
                comando.Parameters.Add("@CodigoAutorizacion", SqlDbType.VarChar,16).Value = codigoAutorizacion;
                comando.Parameters.Add("@Codigo", SqlDbType.NVarChar,10).Value = codigo;
                comando.Parameters.Add("@Hash", SqlDbType.VarBinary,32).Value = tokenHash;
                await comando.ExecuteNonQueryAsync(http.RequestAborted);
                await tx.CommitAsync(http.RequestAborted);
                string texto = $"SYNCHRO ACEPTO {codigoAutorizacion} RECIBIR PIN DE RETIRO POR WHATSAPP";
                string url = $"https://wa.me/{numero}?text={Uri.EscapeDataString(texto)}";
                return Results.Ok(new { intentoId = id, tokenConsulta = token, urlWhatsapp = url });
            }
            catch (SqlException ex) when (ex.Number is 57101 or >=50020 and <=50027 or 2601 or 2627)
            { return Results.Conflict(new { mensaje = "Casillero no disponible para la autorizacion." }); }
            catch (SqlException)
            { return Results.Problem(title: "No se pudo preparar la autorizacion.", statusCode:503); }
            finally
            {
                CryptographicOperations.ZeroMemory(rostro);
                CryptographicOperations.ZeroMemory(tokenHash);
                Array.Clear(patron);
                if (patronProtegido is not null) CryptographicOperations.ZeroMemory(patronProtegido);
            }
        });

        terminal.MapGet("/estado/{id:guid}", async (Guid id, HttpContext http) =>
        {
            if (!http.Request.IsHttps) return Results.StatusCode(403);
            http.Response.Headers.CacheControl = "no-store";
            if (!LeerToken(http.Request.Headers["X-SYNCHRO-INTENTO"].ToString(), out var hash))
                return Results.Unauthorized();
            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using var cmd = new SqlCommand("""
                    SELECT a.Estado, a.VenceUtc, u.FechaVencimiento, u.Estado
                    FROM dbo.AutorizacionesWhatsAppManuales AS a
                    LEFT JOIN dbo.UsosCasillero AS u ON u.Id=a.UsoId
                    WHERE a.Id=@Id AND a.TokenConsultaHash=@Hash
                    """,db);
                cmd.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value=id;
                cmd.Parameters.Add("@Hash", SqlDbType.VarBinary,32).Value=hash;
                await using var reader = await cmd.ExecuteReaderAsync(http.RequestAborted);
                if (!await reader.ReadAsync(http.RequestAborted)) return Results.NotFound();
                string estado=reader.GetString(0);
                bool vencido=reader.GetDateTime(1)<=DateTime.UtcNow ||
                   (!reader.IsDBNull(2) && reader.GetDateTime(2)<=DateTime.UtcNow && estado!="Confirmado");
                if (vencido && estado!="Confirmado") estado="Vencido";
                bool reservaConfirmada=estado is "Reservado" or "PinEmitido" or "Enviado" or "Confirmado";
                // Solo confirmacion DECLARADA por el operador. No equivale a delivery receipt de Meta.
                bool pinEntregado=estado is "Enviado" or "Confirmado";
                return Results.Ok(new { estado, reservaConfirmada, pinEntregado });
            }
            catch (SqlException) { return Results.Problem(statusCode:503,title:"Estado no disponible."); }
            finally { CryptographicOperations.ZeroMemory(hash); }
        });

        terminal.MapPost("/deposito/confirmar", async (OperacionPeticion datos, HttpContext http, IAntiforgery antiforgery) =>
        {
            if (!await TerminalSegura(http,antiforgery)) return Results.StatusCode(403);
            http.Response.Headers.CacheControl="no-store";
            if (datos.IntentoId==Guid.Empty || !LeerToken(http.Request.Headers["X-SYNCHRO-INTENTO"].ToString(), out var hash))
                return Results.BadRequest(new { mensaje="Operacion invalida." });
            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using var cmd = new SqlCommand("dbo.ConfirmarDepositoPersonalManualSynchro",db)
                { CommandType=CommandType.StoredProcedure };
                cmd.Parameters.Add("@IntentoId",SqlDbType.UniqueIdentifier).Value=datos.IntentoId;
                cmd.Parameters.Add("@TokenConsultaHash",SqlDbType.VarBinary,32).Value=hash;
                await using var r=await cmd.ExecuteReaderAsync(http.RequestAborted);
                if (!await r.ReadAsync(http.RequestAborted)) return Results.Problem(statusCode:503);
                return Results.Ok(new { depositoConfirmado=true,
                    idUso=r.GetInt32(0), codigoCasillero=r.GetString(1), estadoCasillero=r.GetString(2) });
            }
            catch (SqlException ex) when (ex.Number is >=57002 and <=57009)
            { return Results.Conflict(new { mensaje="El deposito ya no puede confirmarse. Consulta al operador." }); }
            catch (SqlException)
            { return Results.Problem(statusCode:503,title:"No se pudo confirmar el deposito."); }
            finally { CryptographicOperations.ZeroMemory(hash); }
        });

        var admin=app.MapGroup("/api/admin/whatsapp-manual")
            .RequireAuthorization("OperarServicios")
            .RequireRateLimiting("OperacionesTerminal");

        // SYNCHRO-CONFIRMAR-COBRO-RETIRO-V1
        // Solo un operador autorizado puede confirmar una simulacion.
                // SYNCHRO-AUDITORIA-COBRO-RETIRO-V1
        // SYNCHRO-ADMIN-LISTADO-COBROS-V1
        // Solo usuarios con permiso OperarServicios.
        admin.MapGet("/cobros/retiro/pendientes", async (
            HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!http.Request.IsHttps)
                return Results.StatusCode(403);

            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);

                await using var comando = new SqlCommand("""
                    SELECT TOP (50)
                        c.CobroId,
                        u.CodigoCasillero,
                        c.MinutosUtilizados,
                        c.ImporteHnl,
                        c.DescripcionTarifa,
                        c.CreadoUtc
                    FROM dbo.CobrosRetiroPersonal c
                    INNER JOIN dbo.UsosCasillero u
                        ON u.Id = c.UsoId
                    INNER JOIN dbo.Casilleros cas
                        ON cas.Codigo = u.CodigoCasillero
                    WHERE c.Estado = N'Pendiente'
                      AND u.TipoServicio = N'ResguardoPersonal'
                      AND u.OrigenReserva = N'Terminal'
                      AND u.Estado = N'Ocupado'
                      AND u.FechaRetiro IS NULL
                      AND cas.Estado = N'Ocupado'
                    ORDER BY c.CreadoUtc DESC
                    """, db);

                var cobros = new List<object>();

                await using var lector =
                    await comando.ExecuteReaderAsync(
                        http.RequestAborted);

                while (await lector.ReadAsync(http.RequestAborted))
                {
                    cobros.Add(new
                    {
                        cobroId = lector.GetGuid(0),
                        codigoCasillero = lector.GetString(1),
                        minutosUtilizados = lector.GetInt32(2),
                        importeHnl = lector.GetDecimal(3),
                        descripcionTarifa = lector.GetString(4),
                        creadoUtc = DateTime.SpecifyKind(
                            lector.GetDateTime(5),
                            DateTimeKind.Utc)
                    });
                }

                return Results.Ok(new { cobros });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudieron consultar los cobros.");
            }
        });
admin.MapPost("/cobros/retiro/confirmar", async (
            ConfirmarCobroRetiroPeticion datos,
            HttpContext http,
            IAntiforgery csrf) =>
        {
            http.Response.Headers.CacheControl = "no-store";

            if (!await TerminalSegura(http, csrf))
                return Results.StatusCode(403);

            if (datos.CobroId == Guid.Empty ||
                !datos.PagoComprobado)
                return Results.BadRequest(new
                {
                    mensaje = "Debes comprobar el pago simulado."
                });

            if (!int.TryParse(
                http.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int operadorId) || operadorId <= 0)
                return Results.Unauthorized();

            string metodo;

            try
            {
                metodo = ModalidadesPagoSimulado
                    .ParaRetiroPersonal(datos.MetodoPago);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Metodo de pago no permitido."
                });
            }

            try
            {
                await using var db = new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);

                // Una sola modificacion atomica:
                // no se aceptan pagos de usos finalizados.
                await using var comando = new SqlCommand(
                    "dbo.ConfirmarCobroRetiroPersonalAuditableSynchro",
                    db)
                {
                    CommandType = CommandType.StoredProcedure
                };

                comando.Parameters.Add(
                    "@CobroId", SqlDbType.UniqueIdentifier)
                    .Value = datos.CobroId;

                comando.Parameters.Add(
                    "@Metodo", SqlDbType.NVarChar, 24)
                    .Value = metodo;

                comando.Parameters.Add(
                    "@OperadorId", SqlDbType.Int).Value = operadorId;

                comando.Parameters.Add(
                    "@PagoComprobado", SqlDbType.Bit)
                    .Value = datos.PagoComprobado;
                await using var lector =
                    await comando.ExecuteReaderAsync(
                        http.RequestAborted);

                if (!await lector.ReadAsync(http.RequestAborted))
                    return Results.Conflict(new
                    {
                        mensaje = "Cobro inexistente, confirmado o no disponible."
                    });

                Guid cobroId = lector.GetGuid(0);
                decimal importe = lector.GetDecimal(1);
                string estado = lector.GetString(2);
                string casillero = lector.GetString(3);

                return Results.Ok(new
                {
                    cobroId,
                    codigoCasillero = casillero,
                    importeHnl = importe,
                    moneda = "HNL",
                    metodoPago = metodo,
                    estadoPago = estado,
                    pagoConfirmado = true,
                    pagoSimulado = true,
                    retiroConfirmado = false
                });
            }
            catch (SqlException ex) when (
                ex.Number is 57910 or 57911)
            {
                return Results.Conflict(new
                {
                    mensaje = "El cobro ya no permite confirmacion."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "No se pudo confirmar el pago simulado.");
            }
        });
        admin.MapGet("/pendientes",async (HttpContext http) =>
        {
            if (!http.Request.IsHttps) return Results.StatusCode(403);
            http.Response.Headers.CacheControl="no-store";
            try
            {
                await using var db=new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using var cmd=new SqlCommand("""
                    SELECT TOP (30) a.Id,a.CodigoAutorizacion,a.CodigoCasillero,a.Estado,
                      a.CreadoUtc,a.VenceUtc,a.TelefonoCliente,a.UsoId,
                      u.FechaVencimiento
                    FROM dbo.AutorizacionesWhatsAppManuales a
                    LEFT JOIN dbo.UsosCasillero u ON a.UsoId=u.Id
                    WHERE a.Estado NOT IN(N'Confirmado',N'Cancelado')
                    ORDER BY a.CreadoUtc DESC
                    """, db);
                var items=new List<object>();
                await using var reader=await cmd.ExecuteReaderAsync(http.RequestAborted);
                while(await reader.ReadAsync(http.RequestAborted))
                {
                    string estado=reader.GetString(3);
                    bool vencida=reader.GetDateTime(5)<=DateTime.UtcNow || (!reader.IsDBNull(8) && reader.GetDateTime(8)<=DateTime.UtcNow);
                    items.Add(new {
                        intentoId=reader.GetGuid(0), codigoAutorizacion=reader.GetString(1),
                        codigoCasillero=reader.GetString(2), estado=vencida ? "Vencido" : estado,
                        creadoUtc=DateTime.SpecifyKind(reader.GetDateTime(4),DateTimeKind.Utc),
                        venceUtc=DateTime.SpecifyKind(reader.GetDateTime(5),DateTimeKind.Utc),
                        telefonoCliente=reader.IsDBNull(6)?null:reader.GetString(6),
                        usoId=reader.IsDBNull(7)?(int?)null:reader.GetInt32(7)
                    });
                }
                return Results.Ok(new { operaciones=items });
            }
            catch(SqlException) { return Results.Problem(statusCode:503,title:"Operaciones no disponibles."); }
        });

        admin.MapPost("/confirmar",async (ConfirmarAutorizacionPeticion datos, HttpContext http, IAntiforgery csrf) =>
        {
            if (!await TerminalSegura(http,csrf)) return Results.StatusCode(403);
            http.Response.Headers.CacheControl="no-store";
            string codigo=(datos.CodigoAutorizacion??"").Trim().ToUpperInvariant();
            string telefono=(datos.TelefonoCliente??"").Trim().TrimStart('+');
            if (!datos.MensajeComprobado || datos.IntentoId==Guid.Empty || codigo.Length!=16 ||
                !codigo.All(char.IsAsciiHexDigit) || telefono.Length!=11 ||
                !telefono.StartsWith("504",StringComparison.Ordinal) || !telefono.All(char.IsAsciiDigit))
                return Results.BadRequest(new { mensaje="Debes cotejar el codigo del mensaje y el telefono con WhatsApp." });
            if (!int.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier),out int operadorId)||operadorId<=0)
                return Results.Unauthorized();
            string pin=CodigoPinRetiroPersonal.Generar();
            byte[] salt=CodigoPinRetiroPersonal.GenerarSalt();
            byte[]? pinHash=null;
            try
            {
                await using var db=new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using var tx=(SqlTransaction)await db.BeginTransactionAsync(http.RequestAborted);
                string? casillero=null;
                int? usoExistente=null;
                await using(var check=new SqlCommand("""
                    SELECT CodigoCasillero,UsoId
                    FROM dbo.AutorizacionesWhatsAppManuales WITH (UPDLOCK,HOLDLOCK)
                    WHERE Id=@Id AND CodigoAutorizacion=@Codigo AND Estado=N'Pendiente'
                      AND VenceUtc>SYSUTCDATETIME()
                    """,db,tx))
                {
                    check.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=datos.IntentoId;
                    check.Parameters.Add("@Codigo",SqlDbType.VarChar,16).Value=codigo;
                    await using var lector=await check.ExecuteReaderAsync(http.RequestAborted);
                    if(await lector.ReadAsync(http.RequestAborted)) {
                        casillero=lector.GetString(0);
                        usoExistente=lector.IsDBNull(1)?null:lector.GetInt32(1);
                    }
                }
                if (casillero is null) return Results.Conflict(new { mensaje="Autorizacion vencida o no coincide el codigo." });

                // Utilizar el procedimiento existente; la transaccion exterior mantiene atomica la asociacion.
                int usoId=usoExistente ?? await CrearReservaAsync(db,tx,casillero,http.RequestAborted);
                // No se expone el token de deposito: este flujo verifica su propio token de intento.
                await using(var upd=new SqlCommand("""
                    UPDATE dbo.AutorizacionesWhatsAppManuales
                    SET Estado=N'Reservado', UsoId=@Uso, TelefonoCliente=@Telefono, OperadorId=@Operador
                    WHERE Id=@Id AND Estado=N'Pendiente';
                    """,db,tx))
                {
                    upd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=datos.IntentoId;
                    upd.Parameters.Add("@Uso",SqlDbType.Int).Value=usoId;
                    upd.Parameters.Add("@Telefono",SqlDbType.VarChar,15).Value=telefono;
                    upd.Parameters.Add("@Operador",SqlDbType.Int).Value=operadorId;
                    if(await upd.ExecuteNonQueryAsync(http.RequestAborted)!=1) return Results.Conflict();
                }
                // SYNCHRO-WA-PLAZO-RESERVA-30MIN-V1
                // Solo para esta autorizacion manual ya verificada. La transaccion
                // mantiene alineados los plazos de la reserva SQL y la autorizacion.
                // La ventana pendiente inicial sigue siendo de 20 minutos.
                await using (var plazo = new SqlCommand("""
                    IF NOT EXISTS(SELECT 1 FROM dbo.Casilleros WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@Codigo AND Estado=N'Reservado' AND Tipo=N'Normal')
                        THROW 57130,'Casillero no disponible.',1;
                    DECLARE @NuevoVencimiento DATETIME2(7) = DATEADD(MINUTE, 30, SYSUTCDATETIME());
                    UPDATE dbo.UsosCasillero
                    SET FechaVencimiento = @NuevoVencimiento
                    WHERE Id = @Uso AND CodigoCasillero = @Codigo
                      AND Estado = N'Reservado'
                      AND TipoServicio=N'ResguardoPersonal' AND OrigenReserva=N'Terminal'
                      AND FechaIngreso IS NULL AND FechaRetiro IS NULL
                      AND FechaVencimiento > SYSUTCDATETIME();
                    IF @@ROWCOUNT <> 1
                        THROW 57130, 'No se pudo establecer la vigencia de la reserva.', 1;

                    UPDATE dbo.AutorizacionesWhatsAppManuales
                    SET VenceUtc = @NuevoVencimiento
                    WHERE Id = @Id AND UsoId = @Uso AND Estado = N'Reservado'
                      AND VenceUtc > SYSUTCDATETIME();
                    IF @@ROWCOUNT <> 1
                        THROW 57131, 'No se pudo alinear la autorizacion manual.', 1;
                    """, db, tx))
                {
                    plazo.Parameters.Add("@Uso", SqlDbType.Int).Value = usoId;
                    plazo.Parameters.Add("@Codigo", SqlDbType.NVarChar, 10).Value = casillero;
                    plazo.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = datos.IntentoId;
                    await plazo.ExecuteNonQueryAsync(http.RequestAborted);
                }
                pinHash=CodigoPinRetiroPersonal.CalcularHash(pin,usoId,salt,app.Configuration);
                await using(var emitir=new SqlCommand("""
                    INSERT dbo.CodigosRetiroPersonal(UsoId,PinHash,PinSalt,FechaVencimiento)
                    VALUES(@Uso,@Hash,@Salt,NULL);
                    UPDATE dbo.AutorizacionesWhatsAppManuales SET Estado=N'PinEmitido' WHERE Id=@Id AND Estado=N'Reservado';
                    IF @@ROWCOUNT<>1 THROW 57132,'No se pudo emitir el PIN.',1;
                    """,db,tx)) {
                    emitir.Parameters.Add("@Uso",SqlDbType.Int).Value=usoId;
                    emitir.Parameters.Add("@Hash",SqlDbType.VarBinary,32).Value=pinHash;
                    emitir.Parameters.Add("@Salt",SqlDbType.VarBinary,32).Value=salt;
                    emitir.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=datos.IntentoId;
                    await emitir.ExecuteNonQueryAsync(http.RequestAborted);
                }
                await tx.CommitAsync(http.RequestAborted);
                return Results.Ok(new { reservado=true, usoId, codigoCasillero=casillero, pinRetiro=pin, telefonoCliente=telefono });
            }
            catch(SqlException ex) when (ex.Number is >=50020 and <=50027 or >=57130 and <=57132 or 2601 or 2627)
            { return Results.Conflict(new {mensaje="Casillero ya no disponible o reserva invalida."}); }
            catch(SqlException)
            { return Results.Problem(statusCode:503,title:"No fue posible reservar el casillero."); }
            catch(Exception ex) when(ex is InvalidOperationException or FormatException)
            { return Results.Problem(statusCode:503,title:"Configuracion de PIN no disponible."); }
            finally { CryptographicOperations.ZeroMemory(salt); if(pinHash is not null) CryptographicOperations.ZeroMemory(pinHash); }
        });

        admin.MapPost("/cancelar",async(OperacionPeticion datos,HttpContext http,IAntiforgery csrf)=> {
            if(!await TerminalSegura(http,csrf))return Results.StatusCode(403);
            http.Response.Headers.CacheControl="no-store";
            try {
                await using var db=new SqlConnection(cadena);await db.OpenAsync(http.RequestAborted);
                await using var cmd=new SqlCommand("dbo.CancelarReservaPersonalManualSynchro",db){CommandType=CommandType.StoredProcedure};
                cmd.Parameters.Add("@IntentoId",SqlDbType.UniqueIdentifier).Value=datos.IntentoId;
                await cmd.ExecuteNonQueryAsync(http.RequestAborted);
                return Results.Ok(new{cancelada=true});
            }
            catch(SqlException ex) when(ex.Number is >=57300 and <=57309) {return Results.Conflict(new{mensaje="Solo se pueden cancelar reservas sin deposito ni otra ocupacion activa."});}
            catch(SqlException){return Results.Problem(statusCode:503,title:"No se pudo cancelar la reserva.");}
        });

        admin.MapPost("/emitir-pin",async (EmitirPinPeticion datos, HttpContext http, IAntiforgery csrf) =>
        {
            if (!await TerminalSegura(http,csrf)) return Results.StatusCode(403);
            http.Response.Headers.CacheControl="no-store";
            if (datos.IntentoId==Guid.Empty) return Results.BadRequest();
            string pin=CodigoPinRetiroPersonal.Generar();
            byte[] salt=CodigoPinRetiroPersonal.GenerarSalt();
            byte[]? hash=null;
            try
            {
                await using var db=new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using var tx=(SqlTransaction)await db.BeginTransactionAsync(http.RequestAborted);
                int usoId=0;
                string? casillero=null;
                string? telefono=null;
                await using(var cmd=new SqlCommand("""
                    SELECT a.UsoId,a.CodigoCasillero,a.TelefonoCliente
                    FROM dbo.AutorizacionesWhatsAppManuales a WITH (UPDLOCK,HOLDLOCK)
                    INNER JOIN dbo.UsosCasillero u WITH (UPDLOCK,HOLDLOCK) ON u.Id=a.UsoId
                    WHERE a.Id=@Id AND a.VenceUtc>SYSUTCDATETIME()
                      AND ((a.Estado=N'Reservado' AND @Reemplazar=0)
                        OR (a.Estado=N'PinEmitido' AND @Reemplazar=1))
                      AND u.Estado=N'Reservado' AND u.FechaVencimiento>SYSUTCDATETIME()
                      AND u.FechaIngreso IS NULL AND u.FechaRetiro IS NULL
                    """,db,tx))
                {
                    cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=datos.IntentoId;
                    cmd.Parameters.Add("@Reemplazar",SqlDbType.Bit).Value=datos.Reemplazar;
                    await using var r=await cmd.ExecuteReaderAsync(http.RequestAborted);
                    if (!await r.ReadAsync(http.RequestAborted)) return Results.Conflict(new {mensaje="La reserva no permite emitir el PIN."});
                    usoId=r.GetInt32(0); casillero=r.GetString(1); telefono=r.GetString(2);
                }
                hash=CodigoPinRetiroPersonal.CalcularHash(pin,usoId,salt,app.Configuration);
                await using(var cmd=new SqlCommand(datos.Reemplazar ? """
                    UPDATE dbo.CodigosRetiroPersonal
                    SET PinHash=@Hash, PinSalt=@Salt, FechaCreacion=SYSUTCDATETIME(),
                        FechaVencimiento=NULL, IntentosFallidos=0
                    WHERE UsoId=@Uso AND FechaUso IS NULL;
                    """ : """
                    INSERT INTO dbo.CodigosRetiroPersonal (UsoId,PinHash,PinSalt,FechaVencimiento)
                    VALUES (@Uso,@Hash,@Salt,NULL);
                    """,db,tx))
                {
                    cmd.Parameters.Add("@Uso",SqlDbType.Int).Value=usoId;
                    cmd.Parameters.Add("@Hash",SqlDbType.VarBinary,32).Value=hash;
                    cmd.Parameters.Add("@Salt",SqlDbType.VarBinary,32).Value=salt;
                    if (await cmd.ExecuteNonQueryAsync(http.RequestAborted)!=1)
                        return Results.Conflict(new {mensaje="No se pudo proteger el PIN de retiro."});
                }
                if (!datos.Reemplazar)
                {
                    await using var cmd=new SqlCommand("""
                        UPDATE dbo.AutorizacionesWhatsAppManuales
                        SET Estado=N'PinEmitido' WHERE Id=@Id AND Estado=N'Reservado';
                        """,db,tx);
                    cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=datos.IntentoId;
                    if(await cmd.ExecuteNonQueryAsync(http.RequestAborted)!=1)
                        return Results.Conflict(new {mensaje="No se pudo finalizar la emision del PIN."});
                }
                await tx.CommitAsync(http.RequestAborted);
                // UNICA vez que el PIN en texto claro abandona el servidor: solo operador autorizado.
                return Results.Ok(new { pinRetiro=pin, usoId, codigoCasillero=casillero, telefonoCliente=telefono });
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            { return Results.Conflict(new { mensaje="Ya se emitio un PIN para esta reserva." }); }
            catch (SqlException)
            { return Results.Problem(statusCode:503,title:"No se pudo emitir el PIN."); }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            { return Results.Problem(statusCode:503,title:"Configuracion de PIN no disponible."); }
            finally
            {
                CryptographicOperations.ZeroMemory(salt);
                if (hash is not null) CryptographicOperations.ZeroMemory(hash);
            }
        });

        admin.MapPost("/marcar-enviado",async (MarcarEnviadoPeticion datos, HttpContext http, IAntiforgery csrf) =>
        {
            if (!await TerminalSegura(http,csrf)) return Results.StatusCode(403);
            http.Response.Headers.CacheControl="no-store";
            if (!datos.EnvioComprobado || datos.IntentoId==Guid.Empty) return Results.BadRequest(new { mensaje="Confirma el envio manual." });
            try
            {
                await using var db=new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using var cmd=new SqlCommand("""
                    UPDATE a SET Estado=N'Enviado',FechaEnvioDeclaradaUtc=SYSUTCDATETIME()
                    FROM dbo.AutorizacionesWhatsAppManuales a
                    INNER JOIN dbo.UsosCasillero u ON u.Id=a.UsoId
                    WHERE a.Id=@Id AND a.Estado=N'PinEmitido' AND a.VenceUtc>SYSUTCDATETIME()
                      AND u.Estado=N'Reservado' AND u.FechaVencimiento>SYSUTCDATETIME();
                    SELECT @@ROWCOUNT;
                    """,db);
                cmd.Parameters.Add("@Id",SqlDbType.UniqueIdentifier).Value=datos.IntentoId;
                int cambiados=Convert.ToInt32(await cmd.ExecuteScalarAsync(http.RequestAborted));
                if(cambiados!=1) return Results.Conflict(new {mensaje="La operacion no permite confirmar el envio."});
                return Results.Ok(new {envioDeclarado=true});
            }
            catch(SqlException) { return Results.Problem(statusCode:503,title:"No se pudo confirmar el envio."); }
        });
    }

    private static async Task<int> CrearReservaAsync(SqlConnection db,SqlTransaction tx,string codigo,CancellationToken token)
    {
        var (_,hash)=Synchro.Api.Seguridad.TokenDeposito.Generar();
        try {
            await using var cmd=new SqlCommand("dbo.ReservarCasilleroSynchro",db,tx){CommandType=CommandType.StoredProcedure};
            cmd.Parameters.Add("@CodigoCasillero",SqlDbType.NVarChar,10).Value=codigo;
            cmd.Parameters.Add("@TipoServicio",SqlDbType.NVarChar,20).Value="ResguardoPersonal";
            cmd.Parameters.Add("@OrigenReserva",SqlDbType.NVarChar,20).Value="Terminal";
            cmd.Parameters.Add("@TokenDepositoHash",SqlDbType.VarBinary,32).Value=hash;
            await using var r=await cmd.ExecuteReaderAsync(token);
            if(!await r.ReadAsync(token))throw new InvalidOperationException("Reserva no confirmada.");
            return Convert.ToInt32(r["IdUso"]);
        } finally {CryptographicOperations.ZeroMemory(hash);}
    }

    private static bool CodigoValido(string codigo) =>
        codigo.Length is >= 1 and <= 10 && codigo.All(c=>char.IsAsciiLetterOrDigit(c)||c=='-');

    private static async Task<bool> TerminalSegura(HttpContext http,IAntiforgery antiforgery)
        => http.Request.IsHttps && await antiforgery.IsRequestValidAsync(http);

    private static bool LeerToken(string token, out byte[] hash)
    {
        hash=Array.Empty<byte>();
        if(token.Length!=64 || !token.All(char.IsAsciiHexDigit)) return false;
        hash=SHA256.HashData(Convert.FromHexString(token));
        return true;
    }

    private static async Task<byte[]> LeerLimitado(Stream body,int limite,CancellationToken ct)
    {
        using var memoria=new MemoryStream();
        byte[] buffer=new byte[8192];
        int bytes;
        while((bytes=await body.ReadAsync(buffer.AsMemory(),ct))>0)
        {
            if (memoria.Length+bytes>limite) throw new InvalidDataException();
            memoria.Write(buffer,0,bytes);
        }
        return memoria.ToArray();
    }
}
