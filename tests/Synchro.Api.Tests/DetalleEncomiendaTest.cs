
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

public class DetalleEncomiendaTests
{
    [Fact]
    public async Task SinSesion_NoPuedeConsultarDetalle()
    {
        using var servidor =
            new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");

                    builder.ConfigureAppConfiguration(
                        (_, configuracion) =>
                        {
                            configuracion.AddInMemoryCollection(
                                new Dictionary<string, string?>
                                {
                                    ["ConnectionStrings:SynchroDB"] = ""
                                });
                        });
                });

        using var cliente = servidor.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var respuesta = await cliente.GetAsync(
            "/api/destinatarios/mis-encomiendas/1");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            respuesta.StatusCode);
    }
}
