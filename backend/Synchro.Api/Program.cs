using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Synchro.Api.Models;
using Synchro.Api.Endpoints;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http.HttpResults;
using Synchro.Api.Services.WhatsApp;
using Synchro.Api.Services.Administracion;
using Synchro.Api.Services.Clientes;
using Synchro.Api.Services.RostroSimulado;
using Microsoft.AspNetCore.DataProtection;
using Synchro.Api.Services.Encomiendas;

var builder = WebApplication.CreateBuilder(args);
AdminConfiguracionEndpoints.CargarTarifasLocales(builder);

if (builder.Configuration.GetValue<bool>("EncomiendasOperacion:Habilitado"))
{
    var destinoEncomiendas = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("SynchroDB"));
    if (!builder.Environment.IsDevelopment() || !destinoEncomiendas.IntegratedSecurity ||
        destinoEncomiendas.InitialCatalog != "SynchroDB_Pruebas" ||
        !string.Equals(destinoEncomiendas.DataSource, @"(localdb)\SYNCHRO_Aislado", StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrEmpty(destinoEncomiendas.AttachDBFilename))
        throw new InvalidOperationException("Encomiendas operativas requiere Development y SYNCHRO_Aislado/SynchroDB_Pruebas.");
}

builder.Services.AddScoped<Synchro.Api.Services.Temporales.IRepositorioServicioTemporal, Synchro.Api.Services.Temporales.RepositorioServicioTemporalSql>();
builder.Services.AddScoped<Synchro.Api.Services.Temporales.ServicioTemporal>();
if (builder.Configuration.GetValue<bool>("ServiciosTemporales:Habilitado") &&
    builder.Configuration.GetValue("ServiciosTemporales:ProcesarVencimientos", true))
    builder.Services.AddHostedService<Synchro.Api.Services.Temporales.VencimientoServiciosTemporalesWorker>();
builder.Services.AddScoped<IRepositorioUsuariosAdministrativos, RepositorioUsuariosAdministrativosSql>();
builder.Services.AddScoped<GestionUsuariosAdministrativos>();
builder.Services.AddScoped<IRepositorioClientesWeb, RepositorioClientesWebSql>();
builder.Services.AddScoped<CuentasClientesWeb>();
builder.Services.AddScoped<IPasswordHasher<CuentaClienteWeb>, PasswordHasher<CuentaClienteWeb>>();
builder.Services.AddOpenApi();
builder.Services.AddDataProtection();
builder.Services.AddScoped<EncomiendasOperativasSql>();
if(builder.Configuration.GetValue<bool>("EncomiendasOperacion:Habilitado")&&builder.Configuration.GetValue("EncomiendasOperacion:LimpiarBiometria",true))
    builder.Services.AddHostedService<LimpiezaBiometriaEncomiendasWorker>();
// Al habilitar el flujo automatico, exigir claves persistentes cifradas por DPAPI.
// El directorio debe ser privado de la cuenta Windows que ejecuta el servicio.
if (builder.Configuration.GetValue<bool>("WhatsApp:SolicitudesInicioWorker:Habilitado") ||
    builder.Configuration.GetValue<bool>("WhatsApp:VerificacionOutbox:Habilitado") ||
    builder.Configuration.GetValue<bool>("CargaDispositivos:RegistroHabilitado") ||
    builder.Configuration.GetValue<bool>("EncomiendasOperacion:Habilitado"))
{
    if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("El flujo requiere proteccion DPAPI de Windows.");
    var directorioClaves = builder.Configuration["ProteccionDatos:DirectorioClaves"];
    if (string.IsNullOrWhiteSpace(directorioClaves) || !Path.IsPathFullyQualified(directorioClaves))
        throw new InvalidOperationException("Configurar un directorio absoluto privado para las claves persistentes.");
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(directorioClaves))
        .ProtectKeysWithDpapi();
}
builder.Services.AddScoped<ProtectorVerificacionTelefono>();
builder.Services.AddScoped<EmisorVerificacionTelefonoSql>();
builder.Services.AddScoped<IdentidadDestinatarioWhatsAppSql>();
builder.Services.AddScoped<ReclamadorSolicitudesInicioWhatsAppSql>();
builder.Services.AddScoped<ProcesadorSolicitudesInicioWhatsAppSql>();
builder.Services.AddScoped<CierreFalloSolicitudesInicioWhatsAppSql>();
builder.Services.AddScoped<EjecutorSolicitudesInicioWhatsApp>();
builder.Services.AddScoped<IOutboxVerificacionTelefono, SqlOutboxVerificacionTelefono>();
builder.Services.AddScoped<ProcesadorVerificacionTelefono>();
if (builder.Configuration.GetValue<bool>("WhatsApp:SolicitudesInicioWorker:Habilitado"))
    builder.Services.AddHostedService<SolicitudesInicioWhatsAppWorker>();
if (builder.Configuration.GetValue<bool>("WhatsApp:VerificacionOutbox:Habilitado"))
    builder.Services.AddHostedService<VerificacionTelefonoWorker>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Synchro.Api.Services.IPreReservasEncomienda, Synchro.Api.Services.PreReservaEncomiendaService>();
builder.Services.AddScoped<Synchro.Api.Services.PreparadorPinEncomienda>();
builder.Services.AddScoped<Synchro.Api.Services.IReemisionPinEncomienda, Synchro.Api.Services.ReemisionPinEncomiendaSql>();
builder.Services.AddScoped<Synchro.Api.Services.DepositoEncomiendaService>();
builder.Services.AddScoped<Synchro.Api.Services.IFlujoEncomiendasTerminal, Synchro.Api.Services.FlujoEncomiendasTerminalSql>();
builder.Services.AddScoped<ProtectorMensajeWhatsApp>();
builder.Services.AddScoped<SqlOutboxWhatsApp>();
builder.Services.AddScoped<IAlmacenOutboxWhatsApp>(s => s.GetRequiredService<SqlOutboxWhatsApp>());
builder.Services.AddScoped<ProcesadorOutboxWhatsApp>();
if (builder.Configuration.GetValue<bool>("WhatsApp:Outbox:Habilitado"))
    builder.Services.AddHostedService<OutboxWhatsAppWorker>();

builder.Services.AddHttpClient<
    IWhatsAppCloudService,
    WhatsAppCloudService>(cliente =>
{
    cliente.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)

    .AddCookie(options =>
    {
        options.Cookie.Name = "Synchro.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);


        options.Events.OnValidatePrincipal = async context =>
        {
            var idTexto = context.Principal?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(idTexto, out int id))
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return;
            }

            var configuracion = context.HttpContext
                .RequestServices.GetRequiredService<IConfiguration>();

            var cadena = configuracion.GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                context.RejectPrincipal();
                return;
            }

            try
            {
                await using var conexion = new SqlConnection(cadena); await conexion.OpenAsync(context.HttpContext.RequestAborted);
                await using var comando = new SqlCommand("SELECT Activo FROM dbo.Administradores WHERE Id=@id", conexion);
                comando.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;
                if (await comando.ExecuteScalarAsync(context.HttpContext.RequestAborted) is not true)
                {
                    context.RejectPrincipal(); await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return;
                }
                var acceso = await AccesoAdministrativoSql.ObtenerAsync(conexion, id, context.HttpContext.RequestAborted);
                if (!ValidadorSesionAdministrativa.Aplicar(context.Principal!, acceso))
                { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); }
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException or PlatformNotSupportedException)
            { context.RejectPrincipal(); }
        };


        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloAdministradores", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Administrador"));

    options.AddPolicy("SesionAdministrativa", p => p.RequireAuthenticatedUser().RequireRole("Administrador", "Operador", "Auditor"));
    options.AddPolicy("OperarServicios", p => p.RequireAuthenticatedUser().RequireRole("Administrador", "Operador"));
    options.AddPolicy("VerReportes", p => p.RequireAuthenticatedUser().RequireRole("Administrador", "Auditor"));

    options.AddPolicy("SoloTerminal", policy =>
    {
        policy.AddAuthenticationSchemes("Terminal");
        policy.RequireAuthenticatedUser();
    });
    options.AddPolicy("SoloTerminalDispositivo", policy =>
    {
        policy.AddAuthenticationSchemes("TerminalDispositivo");
        policy.RequireAuthenticatedUser();
        policy.RequireRole("TerminalDispositivo");
    });

});

builder.Services.AddAuthentication().AddCookie("Clientes", o =>
{
    o.Cookie.Name = "SYNCHRO.ClienteWeb"; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Lax; o.ExpireTimeSpan = TimeSpan.FromMinutes(30); o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async c =>
    {
        if (!int.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out int id) || id <= 0)
        { c.RejectPrincipal(); return; }
        try
        {
            var cuenta = await c.HttpContext.RequestServices.GetRequiredService<CuentasClientesWeb>().ObtenerAsync(id, c.HttpContext.RequestAborted);
            if (cuenta is null || !cuenta.Activa || c.Principal?.FindFirstValue("cliente_version") != cuenta.VersionSesion.ToString())
            { c.RejectPrincipal(); await c.HttpContext.SignOutAsync("Clientes"); }
        }
        catch (Exception e) when (e is SqlException or InvalidOperationException or PlatformNotSupportedException) { c.RejectPrincipal(); }
    };
});
builder.Services.AddAuthorization(o => o.AddPolicy("SoloClientes", p => p.AddAuthenticationSchemes("Clientes").RequireAuthenticatedUser().RequireRole("Cliente")));

/* SYNCHRO | Autenticacion de terminal */

builder.Services
    .AddAuthentication()
    .AddCookie("Terminal", options =>
    {
        options.Cookie.Name = "Synchro.Terminal";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;


        // Sesión de estación: se renueva mientras la terminal esté en uso.
        // Cada petición sigue comprobando que la terminal permanezca activa en SQL.
        options.ExpireTimeSpan = builder.Environment.IsDevelopment()
            ? TimeSpan.FromDays(7)
            : TimeSpan.FromHours(24);

        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = async context =>
        {
            var idTexto = context.Principal?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(idTexto, out int terminalId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync("Terminal");
                return;
            }

            var configuracion = context.HttpContext
                .RequestServices.GetRequiredService<IConfiguration>();

            var cadena = configuracion
                .GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                context.RejectPrincipal();
                return;
            }

            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync();

                await using var comando = new SqlCommand(
                    """
            SELECT Activa
            FROM dbo.Terminales
            WHERE Id = @terminalId;
            """,
                    conexion);

                comando.Parameters.Add(
                    "@terminalId",
                    System.Data.SqlDbType.Int
                ).Value = terminalId;

                var resultado = await comando.ExecuteScalarAsync();

                if (resultado is not bool activa || !activa)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync("Terminal");
                }
            }
            catch (SqlException)
            {
                context.RejectPrincipal();
            }
        };


        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    })
    // Cookie independiente: la vinculación del dispositivo permanece mientras
    // se use la estación. Nunca contiene un PIN de cliente ni un secreto JS.
    .AddCookie("TerminalDispositivo", options =>
    {
        options.Cookie.Name = "Synchro.Terminal.Dispositivo";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(365);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            // Solo la ÚLTIMA vinculación aprobada conserva validez. Revocar
            // la terminal o volver a vincularla invalida el dispositivo anterior.
            var identidad = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var version = context.Principal?.FindFirstValue("VinculoVersion");
            if (!int.TryParse(identidad, out var terminalId) || terminalId <= 0 ||
                string.IsNullOrWhiteSpace(version))
            {
                context.RejectPrincipal();
                return;
            }

            var cadena = context.HttpContext.RequestServices
                .GetRequiredService<IConfiguration>().GetConnectionString("SynchroDB");
            if (string.IsNullOrWhiteSpace(cadena))
            {
                context.RejectPrincipal();
                return;
            }
            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync(context.HttpContext.RequestAborted);
                await using var comando = new SqlCommand(
                    "SELECT Activa, FechaAutorizacion FROM dbo.Terminales WHERE Id=@id", conexion);
                comando.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = terminalId;
                await using var lector = await comando.ExecuteReaderAsync(context.HttpContext.RequestAborted);
                if (!await lector.ReadAsync(context.HttpContext.RequestAborted) ||
                    !lector.GetBoolean(0) || lector.IsDBNull(1) ||
                    lector.GetDateTime(1).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) != version)
                    context.RejectPrincipal();
            }
            catch (SqlException) { context.RejectPrincipal(); }
        };
    });

builder.Services.AddScoped<
    IPasswordHasher<Administrador>,
    PasswordHasher<Administrador>>();


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    // Separar consultas periódicas de las operaciones protegidas y del acceso.
    options.AddPolicy("EncomiendasOperativas", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{contexto.Connection.RemoteIpAddress}|{(contexto.Request.Path.StartsWithSegments("/api/admin") ? "admin" : "web")}|{(HttpMethods.IsGet(contexto.Request.Method) ? "lectura" : "escritura")}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = HttpMethods.IsGet(contexto.Request.Method) ? 120 : 20,
                Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));

    options.AddPolicy("IdentificarRetiroEncomienda", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{contexto.Connection.RemoteIpAddress}|{contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));

    options.AddPolicy("OperacionesTerminal", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Retiro personal: independiente del limite de inicio de sesion.
    options.AddPolicy("RetiroPersonal", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("LoginLimitado", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                contexto.Connection.RemoteIpAddress?.ToString()
                ?? "sin-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services
    .AddAuthentication()
    .AddCookie("Destinatarios", options =>
    {
        options.Cookie.Name = "SYNCHRO.Destinatarios";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.SlidingExpiration = false;


        options.Events.OnValidatePrincipal = async contexto =>
        {
            var personaTexto = contexto.Principal?
                .FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier
                )?.Value;

            var tipoUsuario = contexto.Principal?
                .FindFirst("tipo_usuario")?.Value;

            if (!int.TryParse(personaTexto, out int personaId)
                || personaId <= 0
                || tipoUsuario != "Destinatario")
            {
                contexto.RejectPrincipal();

                await contexto.HttpContext.SignOutAsync(
                    "Destinatarios");

                return;
            }

            string? cadena = builder.Configuration
                .GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                contexto.RejectPrincipal();
                return;
            }

            try
            {
                await using var conexion = new SqlConnection(cadena);

                await conexion.OpenAsync(
                    contexto.HttpContext.RequestAborted);

                await using var comando = new SqlCommand(
                    @"SELECT COUNT(1)
              FROM dbo.Personas
              WHERE Id = @PersonaId",
                    conexion);

                comando.Parameters.Add(
                    "@PersonaId",
                    System.Data.SqlDbType.Int
                ).Value = personaId;

                int encontrados = Convert.ToInt32(
                    await comando.ExecuteScalarAsync(
                        contexto.HttpContext.RequestAborted));

                if (encontrados != 1)
                {
                    contexto.RejectPrincipal();

                    await contexto.HttpContext.SignOutAsync(
                        "Destinatarios");
                }
            }
            catch (SqlException)
            {
                contexto.RejectPrincipal();
            }
        };

        // Las API deben devolver errores HTTP,
        // no redireccionar a una pagina de inicio de sesion.
        options.Events.OnRedirectToLogin = contexto =>
        {
            contexto.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = contexto =>
        {
            contexto.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloDestinatarios", policy =>
    {
        policy.AddAuthenticationSchemes("Destinatarios");

        policy.RequireAuthenticatedUser();

        policy.RequireClaim(
            "tipo_usuario",
            "Destinatario"
        );
    });
});

builder.Services.AddSingleton<IReferenciasRostroSimulado, ReferenciasRostroSimuladoSql>();
builder.Services.AddSingleton<IVerificadorFacial>(s =>
{
    var c = s.GetRequiredService<IConfiguration>(); var entorno = s.GetRequiredService<IHostEnvironment>();
    if (!ConfiguracionRostroSimulado.EntornoPermitido(entorno, c)) return new VerificadorFacialNoDisponible();
    return new VerificadorFacialSimulado(s.GetRequiredService<IReferenciasRostroSimulado>(), c, s.GetRequiredService<TimeProvider>());
});
if (ConfiguracionRostroSimulado.EntornoPermitido(builder.Environment, builder.Configuration) &&
    builder.Configuration.GetValue("RostroSimulado:LimpiarVencidas", true))
    builder.Services.AddHostedService<LimpiezaRostroSimuladoWorker>();

string? conexionLimpiezaV3 =
    builder.Configuration.GetConnectionString("SynchroDB");

if (builder.Environment.IsDevelopment() &&
    builder.Configuration.GetValue<bool>("WhatsAppManual:Habilitado") &&
    !string.IsNullOrWhiteSpace(conexionLimpiezaV3))
{
    var destinoLimpiezaV3 =
        new SqlConnectionStringBuilder(conexionLimpiezaV3);

    if (string.Equals(
            destinoLimpiezaV3.InitialCatalog,
            "SynchroDB_Pruebas",
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            destinoLimpiezaV3.DataSource,
            @"(localdb)\SYNCHRO_Aislado",
            StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddHostedService<LimpiezaRostrosRetiroV3Worker>();
    }
}
// El servicio de vencimiento se registra solo bajo la condicion de pruebas indicada abajo.

// SYNCHRO - Vencimiento automatico de reservas
// Activado solamente en la base de pruebas

var cadenaVencimiento = builder.Configuration
    .GetConnectionString("SynchroDB");

if (builder.Configuration.GetValue<bool>("Reservas:ProcesarVencimientos") &&
    builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(cadenaVencimiento) &&
    string.Equals(
        new SqlConnectionStringBuilder(cadenaVencimiento)
            .InitialCatalog,
        "SynchroDB_Pruebas",
        StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<
        Synchro.Api.Services.VencimientoReservasService>();
}


var app = builder.Build();
// Acceso libre a reservas ÚNICAMENTE en el prototipo aislado.
// El backend rehúsa arrancar si alguien intenta activar este modo en
// producción o conectarlo a una base de datos fuera de SYNCHRO_Aislado.
if (app.Configuration.GetValue<bool>("ReservasPrototipo:AccesoLibrePruebas"))
{
    var cadenaDemo = app.Configuration.GetConnectionString("SynchroDB");
    if (!app.Environment.IsDevelopment() ||
        !app.Configuration.GetValue<bool>("EncomiendasOperacion:Habilitado") ||
        string.IsNullOrWhiteSpace(cadenaDemo) ||
        !Synchro.Api.Services.ConexionSqlAislada.EsWhatsAppPermitido(
            new SqlConnectionStringBuilder(cadenaDemo)))
        throw new InvalidOperationException(
            "Acceso libre de reservas permitido solo en Development y SynchroDB_Pruebas de SYNCHRO_Aislado.");
}
foreach (string indicador in new[] { "WhatsApp:SolicitudesInicioHabilitadas", "WhatsApp:SolicitudesInicioWorker:Habilitado", "WhatsApp:VerificacionOutbox:Habilitado", "EncomiendasOperacion:Habilitado" })
{
    if (!app.Configuration.GetValue<bool>(indicador)) continue;
    var sql = new SqlConnectionStringBuilder(app.Configuration.GetConnectionString("SynchroDB"));
    if (!app.Environment.IsDevelopment() ||
        !Synchro.Api.Services.ConexionSqlAislada.EsWhatsAppPermitido(sql))
        throw new InvalidOperationException("El flujo operativo requiere Development y la instancia SYNCHRO_Aislado.");
    Synchro.Api.Services.ConexionSqlAislada.Exigir(sql.ConnectionString, "SynchroDB_Pruebas");
}
// Validar tambien la configuracion final del host: incluye los proveedores
// agregados al construirlo. Los trabajadores aun no han arrancado.
foreach (string modulo in new[] { "ServiciosTemporales:Habilitado", "WhatsApp:Outbox:Habilitado", "Administracion:UsuariosHabilitados", "RostroSimulado:Habilitado", "RostroSimulado:LimpiarVencidas" })
{
    if (!app.Configuration.GetValue<bool>(modulo)) continue;
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Los modulos de prototipo requieren Development.");
    Synchro.Api.Services.ConexionSqlAislada.Exigir(app.Configuration.GetConnectionString("SynchroDB"), "SynchroDB_Pruebas");
}

if (app.Environment.IsDevelopment())
    app.UseWhen(c => c.Request.Path.StartsWithSegments("/openapi"), rama => rama.Use(async (contexto, siguiente) =>
    {
        if (!contexto.Request.IsHttps) { contexto.Response.StatusCode = 400; return; }
        await siguiente(contexto);
    }));

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapEstadoEndpoints();
app.MapWhatsAppWebhookEndpoints();

// Modulos HTTP separados por funcionalidad.
app.MapCasillerosBaseDatosEndpoints();
app.MapCasillerosPrototipoEndpoints();
app.MapCargaDispositivosEndpoints();
app.MapAdministracionEndpoints();
app.MapCotizacionesEndpoints();
// No registrar asignacion arbitraria de participantes de encomiendas por ID.
app.MapDestinatariosSesionEndpoints();
app.MapDestinatariosConsentimientosEndpoints();
app.MapDestinatariosEncomiendasEndpoints();

// SYNCHRO - Vinculacion de terminales
// Habilitada solo en desarrollo y base de pruebas

var cadenaTerminales = app.Configuration
    .GetConnectionString("SynchroDB");

var nombreBaseTerminales = string.IsNullOrWhiteSpace(cadenaTerminales)
    ? ""
    : new SqlConnectionStringBuilder(cadenaTerminales)
        .InitialCatalog;

if (app.Environment.IsDevelopment() &&
    string.Equals(
        nombreBaseTerminales,
        "SynchroDB_Pruebas",
        StringComparison.OrdinalIgnoreCase))
{
    app.MapVinculacionTerminalEndpoints();
    app.MapAprobacionTerminalEndpoints();
    app.MapConfirmacionTerminalEndpoints();
    app.MapReservasTerminalEndpoints();
    app.MapWhatsAppManualEndpoints();
    app.MapDepositosTerminalEndpoints();
    app.MapRetirosPersonalesTerminalEndpoints();
    app.MapRecuperacionPinPersonalEndpoints();
    // Pre-reservas web de encomiendas: prototipo aislado y sin deposito.
    app.MapClientesWebEndpoints();

    app.MapRostroSimuladoEndpoints();
    app.MapPreReservasEncomiendaWebEndpoints();
    app.MapConsentimientosPreReservaWhatsApp();
    // Emision y consulta preliminar exclusivas de la terminal (sin deposito).
    app.MapEncomiendasTerminalEndpoints();
    app.MapFlujoEncomiendasTerminalEndpoints();
    app.MapEncomiendasOperativasEndpoints();
    app.MapServiciosTemporalesEndpoints();
    app.MapCargaSesionSeguraEndpoints();
    app.MapRetiroCargaIdentidadEndpoints();
    app.MapRetiroCargaAutoIdentificacionEndpoints();
    app.MapRetiroCargaCobroEndpoints();
    app.MapAdminCobrosCargaEndpoints();
    app.MapAdminEspaciosCargaEndpoints();
    app.MapAdminMovimientosEndpoints();
    app.MapAdminReportesEndpoints();
    app.MapUsuariosAdministrativosEndpoints();
    app.MapAdminConfiguracionEndpoints();
    app.MapOpenApi().RequireAuthorization("SoloAdministradores");
}

app.Run();

public record IniciarCargaSolicitud(string? TipoDispositivo);

public record LoginAdministradorSolicitud(
    string? NombreUsuario,
    string? Contrasena
);

public record ConfirmarPagoSolicitud(
    string? MetodoPago
);


record AsignarParticipantesPeticion(
    int RemitenteId,
    int DestinatarioId
);

record InicioSesionDestinatarioPeticion(
    int VerificacionId,
    string? Codigo
)
{
    public override string ToString() => "Solicitud de inicio protegida";
}

record AceptarConsentimientoWhatsAppPeticion(
    int VerificacionTelefonoId,
    bool ConfirmacionExpresa
);

public partial class Program
{
}
