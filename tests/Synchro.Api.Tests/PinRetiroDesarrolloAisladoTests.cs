using Microsoft.AspNetCore.Builder;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class PinRetiroDesarrolloAisladoTests : PruebaHistoricaAislada
{
    [Fact]
    public async Task PermiteConexionALaBaseAislada()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Testing"
            });

        builder.Configuration.AddConfiguration(PruebaHistoricaAislada.Configuracion());

        Assert.False(string.IsNullOrWhiteSpace(
            builder.Configuration["RetiroPin:ClaveHmac"]));

        builder.Configuration["ConnectionStrings:SynchroDB"] =
            @"Server=(localdb)\SYNCHRO_Aislado;" +
            "Database=SynchroDB_Pruebas;" +
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
            "Database=SynchroDB_Pruebas;" +
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
