
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Synchro.Api.Tests;

public sealed class FabricaSynchroPruebas
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        // Ejecutar únicamente en el entorno de pruebas.
        builder.UseEnvironment("Testing");

        // Utilizar exclusivamente la copia SQL.
        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:SynchroDB"] =
                        @"Server=(localdb)\SYNCHRO_Aislado;" +
                        "Database=SynchroDB_Pruebas;" +
                        "Integrated Security=True;" +
                        "Encrypt=True;" +
                        "TrustServerCertificate=True;"
                });
        });

        // Reemplazar la autenticación de destinatarios
        // solamente dentro del servidor de pruebas.
        builder.ConfigureTestServices(servicios =>
        {
            servicios.AddAuthentication()
                .AddScheme<
                    AuthenticationSchemeOptions,
                    AutenticacionDestinatarioPruebas>(
                    AutenticacionDestinatarioPruebas.Esquema,
                    _ => { });

            servicios.PostConfigure<AuthorizationOptions>(
                opciones =>
                {
                    opciones.AddPolicy(
                        "SoloDestinatarios",
                        new AuthorizationPolicyBuilder(
                            AutenticacionDestinatarioPruebas.Esquema)
                            .RequireAuthenticatedUser()
                            .RequireClaim(
                                "tipo_usuario",
                                "Destinatario")
                            .Build());
                });
        });
    }
}
