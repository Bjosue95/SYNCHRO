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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataProtection();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Synchro.Api.Services.IPreReservasEncomienda,Synchro.Api.Services.PreReservaEncomiendaService>();
builder.Services.AddScoped<Synchro.Api.Services.PreparadorPinEncomienda>();
builder.Services.AddScoped<Synchro.Api.Services.DepositoEncomiendaService>();
builder.Services.AddScoped<Synchro.Api.Services.IFlujoEncomiendasTerminal,Synchro.Api.Services.FlujoEncomiendasTerminalSql>();
builder.Services.AddScoped<ProtectorMensajeWhatsApp>();
builder.Services.AddScoped<SqlOutboxWhatsApp>();
builder.Services.AddScoped<IAlmacenOutboxWhatsApp>(s => s.GetRequiredService<SqlOutboxWhatsApp>());
builder.Services.AddScoped<ProcesadorOutboxWhatsApp>();
if (builder.Configuration.GetValue<bool>("WhatsApp:Outbox:Habilitado"))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("La outbox del prototipo solo puede activarse en Development.");
    Synchro.Api.Services.ConexionSqlAislada.Exigir(
        builder.Configuration.GetConnectionString("SynchroDB"), "SynchroDB_Pruebas");
    builder.Services.AddHostedService<OutboxWhatsAppWorker>();
}

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

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            using var comando = new SqlCommand(@"
        SELECT Activo
        FROM dbo.Administradores
        WHERE Id = @id", conexion);

            comando.Parameters.Add(
                "@id", System.Data.SqlDbType.Int).Value = id;

            var resultado = await comando.ExecuteScalarAsync();

            if (resultado is not bool activo || !activo)
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
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

    options.AddPolicy("SoloTerminal", policy =>
    {
        policy.AddAuthenticationSchemes("Terminal");
        policy.RequireAuthenticatedUser();
    });

});

/* SYNCHRO | Autenticacion de terminal */

builder.Services
    .AddAuthentication()
    .AddCookie("Terminal", options =>
    {
        options.Cookie.Name = "Synchro.Terminal";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;


        options.ExpireTimeSpan = builder.Environment.IsDevelopment()
            ? TimeSpan.FromHours(2)
            : TimeSpan.FromMinutes(15);

        options.SlidingExpiration = false;

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
    });

builder.Services.AddScoped<
    IPasswordHasher<Administrador>,
    PasswordHasher<Administrador>>();


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

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

builder.Services.AddSingleton<
    IVerificadorFacial,
    VerificadorFacialNoDisponible>();
// El servicio de vencimiento se registra solo bajo la condicion de pruebas indicada abajo.

// SYNCHRO - Vencimiento automatico de reservas
// Activado solamente en la base de pruebas

var cadenaVencimiento = builder.Configuration
    .GetConnectionString("SynchroDB");

if (builder.Environment.IsDevelopment() &&
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

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapEstadoEndpoints();
app.MapWhatsAppWebhookEndpoints();

// Modulos HTTP separados por funcionalidad.
app.MapCasillerosDemoEndpoints();
app.MapCasillerosBaseDatosEndpoints();
app.MapCargaDispositivosEndpoints();
app.MapAdministracionEndpoints();
app.MapPagosDemoEndpoints();
app.MapCotizacionesEndpoints();
app.MapReservasDemoEndpoints();
app.MapParticipantesDemoEndpoints();
app.MapVerificacionesDemoEndpoints();
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
    app.MapDepositosTerminalEndpoints();
    app.MapRetirosPersonalesTerminalEndpoints();
    app.MapRecuperacionPinPersonalEndpoints();
    // Pre-reservas web de encomiendas: prototipo aislado y sin deposito.
    app.MapPreReservasEncomiendaWebEndpoints();
    app.MapConsentimientosPreReservaWhatsApp();
    // Emision y consulta preliminar exclusivas de la terminal (sin deposito).
    app.MapEncomiendasTerminalEndpoints();
    app.MapFlujoEncomiendasTerminalEndpoints();
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

public record ConfirmarPagoSimuladoSolicitud(
    string CanalPago
);

public record RegistrarRechazoPagoSimuladoSolicitud(
    string? CanalPago
);

public record ReservarCasilleroSolicitud(
    string? CodigoCasillero,
    string? TipoServicio
);

record AsignarParticipantesPeticion(
    int RemitenteId,
    int DestinatarioId
);

record SolicitarVerificacionTelefonoPeticion(
    int PersonaId,
    string? Telefono
);

record ValidarTelefonoPeticion(
    int PersonaId,
    string Codigo
);

record InicioSesionDestinatarioPeticion(
    int VerificacionId,
    string? Codigo
);

record AceptarConsentimientoWhatsAppPeticion(
    int VerificacionTelefonoId,
    bool ConfirmacionExpresa
);

public partial class Program
{
}
