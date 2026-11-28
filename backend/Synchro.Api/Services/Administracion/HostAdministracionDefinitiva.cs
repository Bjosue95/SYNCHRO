using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Synchro.Api.Endpoints;
using Synchro.Api.Models;
using Synchro.Api.Services.Clientes;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Synchro.Api.Services.Administracion;

// Primer host local de integracion. No registra servicios de operaciones, cobros ni workers.
internal static class HostAdministracionDefinitiva
{
    private const string Conexion = @"Server=.\SQLEXPRESS;Database=SynchroDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15";

    internal static async Task IniciarAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Se requiere Windows para DPAPI.");
        await using (var c=new SqlConnection(Conexion))
        {
            await c.OpenAsync();
            await using var q=new SqlCommand("""
                IF DB_NAME()<>N'SynchroDB'
                    OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS'
                    OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1)<>0
                    OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
                    THROW 59070,'Destino no autorizado.',1;
                """,c);
            await q.ExecuteNonQueryAsync();
        }
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=[],EnvironmentName="Production"});
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["ConnectionStrings:SynchroDB"]=Conexion,
            ["AllowedHosts"]="localhost;127.0.0.1;[::1]",
            ["Administracion:UsuariosHabilitados"]="true"
        });
        builder.WebHost.ConfigureKestrel(o=>o.ListenLocalhost(7278,l=>l.UseHttps()));
        string directorio=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SYNCHRO","Definitivo","DataProtectionKeys");
        Directory.CreateDirectory(directorio);
        builder.Services.AddDataProtection().SetApplicationName("SYNCHRO-Administracion-Definitiva").PersistKeysToFileSystem(new DirectoryInfo(directorio)).ProtectKeysWithDpapi();
        builder.Services.AddScoped<IPasswordHasher<Administrador>,PasswordHasher<Administrador>>();
        builder.Services.AddScoped<IRepositorioUsuariosAdministrativos>(sp=>new RepositorioUsuariosAdministrativosSql(sp.GetRequiredService<IConfiguration>(),definitiva:true));
        builder.Services.AddScoped<GestionUsuariosAdministrativos>();
        builder.Services.AddScoped<IRepositorioClientesWeb>(sp=>new RepositorioClientesWebSql(sp.GetRequiredService<IConfiguration>(),definitiva:true));
        builder.Services.AddScoped<CuentasClientesWeb>();
        builder.Services.AddScoped<IPasswordHasher<CuentaClienteWeb>,PasswordHasher<CuentaClienteWeb>>();
        builder.Services.AddSingleton<LimitesAccesoClientes>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
            o.Cookie.Name="Synchro.Admin.Definitivo";
            o.Cookie.HttpOnly=true;
            o.Cookie.SecurePolicy=CookieSecurePolicy.Always;
            o.Cookie.SameSite=SameSiteMode.Strict;
            o.ExpireTimeSpan=TimeSpan.FromMinutes(30);
            o.SlidingExpiration=false;
            o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
            o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
            o.Events.OnValidatePrincipal=async contexto=>{
                if (!int.TryParse(contexto.Principal?.FindFirstValue(ClaimTypes.NameIdentifier),out int id)||id<=0)
                {contexto.RejectPrincipal();return;}
                try
                {
                    await using var c=new SqlConnection(Conexion);
                    await c.OpenAsync(contexto.HttpContext.RequestAborted);
                    await using var q=new SqlCommand("SELECT Activo FROM dbo.Administradores WHERE Id=@id",c);
                    q.Parameters.Add("@id",System.Data.SqlDbType.Int).Value=id;
                    if(await q.ExecuteScalarAsync(contexto.HttpContext.RequestAborted) is not true)
                    {contexto.RejectPrincipal();return;}
                    var acceso=await AccesoAdministrativoSql.ObtenerAsync(c,id,contexto.HttpContext.RequestAborted);
                    if(acceso.VersionSesion<1||!ValidadorSesionAdministrativa.Aplicar(contexto.Principal!,acceso))
                        contexto.RejectPrincipal();
                }
                catch(Exception e)when(e is SqlException or InvalidOperationException){contexto.RejectPrincipal();}
            };
        });
        builder.Services.AddAuthentication().AddCookie("Clientes",o=>{
            o.Cookie.Name="Synchro.ClienteWeb.Definitivo";
            o.Cookie.HttpOnly=true;
            o.Cookie.SecurePolicy=CookieSecurePolicy.Always;
            o.Cookie.SameSite=SameSiteMode.Strict;
            o.ExpireTimeSpan=TimeSpan.FromMinutes(30);
            o.SlidingExpiration=false;
            o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
            o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
            o.Events.OnValidatePrincipal=async c=>{
                if(!int.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier),out int id)||id<=0)
                {c.RejectPrincipal();return;}
                try
                {
                    var cuenta=await c.HttpContext.RequestServices.GetRequiredService<CuentasClientesWeb>().ObtenerAsync(id,c.HttpContext.RequestAborted);
                    if(cuenta is null||!cuenta.Activa||c.Principal?.FindFirstValue("cliente_version")!=cuenta.VersionSesion.ToString())
                        c.RejectPrincipal();
                }
                catch(Exception e)when(e is SqlException or InvalidOperationException){c.RejectPrincipal();}
            };
        });
        builder.Services.AddAuthorization(o=>{
            o.AddPolicy("SoloClientes",p=>p.AddAuthenticationSchemes("Clientes").RequireAuthenticatedUser().RequireRole("Cliente"));
            o.AddPolicy("SesionAdministrativa",p=>p.RequireAuthenticatedUser().RequireRole("Administrador","Operador","Auditor"));
            o.AddPolicy("SoloAdministradores",p=>p.RequireAuthenticatedUser().RequireRole("Administrador"));
            o.AddPolicy("OperarServicios",p=>p.RequireAuthenticatedUser().RequireRole("Administrador","Operador"));
            o.AddPolicy("VerReportes",p=>p.RequireAuthenticatedUser().RequireRole("Administrador","Auditor"));
        });
        builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="Synchro.Csrf.Definitivo";o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Strict;});
        builder.Services.AddRateLimiter(o=>{
            o.RejectionStatusCode=429;
            o.AddFixedWindowLimiter("LoginLimitado",l=>{l.PermitLimit=5;l.Window=TimeSpan.FromMinutes(1);l.QueueLimit=0;});
            foreach(string politica in new[]{"OperacionesTerminal","EncomiendasOperativas"})
                o.AddFixedWindowLimiter(politica,l=>{l.PermitLimit=240;l.Window=TimeSpan.FromMinutes(1);l.QueueLimit=0;});
        });
        var app=builder.Build();
        app.Use(async (ctx,next)=>{
            if(!ctx.Request.IsHttps||ctx.Connection.RemoteIpAddress is not IPAddress ip||!IPAddress.IsLoopback(ip))
            {ctx.Response.StatusCode=403;return;}
            ctx.Response.Headers.CacheControl="no-store";
            await next();
        });
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.MapAdministracionEndpoints();
        app.MapClientesWebEndpoints();
        app.MapCasillerosPrototipoEndpoints(definitiva:true);
        app.MapAdminEspaciosCargaEndpoints(definitiva:true);
        app.MapAdminMovimientosEndpoints(definitiva:true);
        app.MapAdminReportesEndpoints(definitiva:true);
        app.MapUsuariosAdministrativosEndpoints(soloLectura:true);
        app.MapGet("/api/estado",()=>new{proyecto="SYNCHRO",estado="integracion administrativa local",operaciones=false});
        app.MapGet("/api/admin/verificacion-base",async (CancellationToken token)=>{
            await using var c=new SqlConnection(Conexion);await c.OpenAsync(token);
            await using var q=new SqlCommand("SELECT (SELECT COUNT(*) FROM dbo.Casilleros),(SELECT COUNT(*) FROM dbo.EspaciosCarga),(SELECT COUNT(*) FROM dbo.TarifasCargaSynchro)",c);
            await using var r=await q.ExecuteReaderAsync(token);
            await r.ReadAsync(token);
            return Results.Ok(new{baseDatos="SynchroDB",casilleros=r.GetInt32(0),espaciosCarga=r.GetInt32(1),tarifasCarga=r.GetInt32(2)});
        }).RequireAuthorization("SoloAdministradores");
        Console.WriteLine("SYNCHRO | Integracion administrativa local: https://localhost:7278");
        Console.WriteLine("Operaciones, pagos, WhatsApp y rutas de terminal no habilitados.");
        await app.RunAsync();
    }
}
