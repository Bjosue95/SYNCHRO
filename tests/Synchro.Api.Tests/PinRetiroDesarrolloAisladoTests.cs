using Microsoft.AspNetCore.Builder;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace Synchro.Api.Tests;

public sealed class PinRetiroDesarrolloAisladoTests
{
    [Fact]
    public async Task PermiteConexionALaBaseAislada()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

        builder.Configuration.AddUserSecrets(
            typeof(PinRetiroDesarrolloService).Assembly,
            optional: false);

        Assert.False(string.IsNullOrWhiteSpace(
            builder.Configuration["RetiroPin:ClaveHmac"]));

        builder.Configuration["ConnectionStrings:SynchroDB"] =
            @"Server=(localdb)\SYNCHRO_Aislado;" +
            "Database=SynchroDB;" +
            "Integrated Security=True;" +
            "Encrypt=True;" +
            "TrustServerCertificate=True;";

        var servicio = new PinRetiroDesarrolloService(
            builder.Configuration,
            builder.Environment);

        await servicio.ComprobarConexionAsync();
    }

    [Fact]
    public async Task RechazaInstanciaPrincipalEnTesting()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

        builder.Configuration["ConnectionStrings:SynchroDB"] =
            @"Server=(localdb)\MSSQLLocalDB;" +
            "Database=SynchroDB;" +
            "Integrated Security=True;" +
            "Encrypt=True;" +
            "TrustServerCertificate=True;";

        var servicio = new PinRetiroDesarrolloService(
            builder.Configuration,
            builder.Environment);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ComprobarConexionAsync());
    }
}
