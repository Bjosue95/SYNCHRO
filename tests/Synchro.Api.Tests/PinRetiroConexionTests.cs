using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

public class PinRetiroConexionTests
{
    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task PuedeConsultarSaltEnBaseDePruebas()
    {
        // Leer los User Secrets del proyecto Synchro.Api.
        IConfiguration configuracion =
            new ConfigurationBuilder()
                .AddUserSecrets(
                    typeof(PinRetiroService).Assembly,
                    optional: false)
                .Build();

        // Comprobar que la conexion fue configurada.
        Assert.False(string.IsNullOrWhiteSpace(
            configuracion.GetConnectionString(
                "SynchroDBPruebas")));

        var servicio = new PinRetiroService(configuracion);

        // Identificador ficticio inexistente.
        // La consulta no debe insertar ni cambiar datos.
        byte[]? salt = await servicio.ObtenerSaltAsync(
            int.MaxValue);

        // Si no existe ese registro, debe devolver null.
        Assert.Null(salt);
    }

    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task ValidarPinDeEncomiendaInexistente()
    {
        IConfiguration configuracion =
            new ConfigurationBuilder()
                .AddUserSecrets(
                    typeof(PinRetiroService).Assembly,
                    optional: false)
                .Build();

        var servicio = new PinRetiroService(configuracion);

        string resultado = await servicio.ValidarPinAsync(
            int.MaxValue,
            int.MaxValue,
            "123456");

        Assert.Equal("NoDisponible", resultado);
    }

    [Fact]
    public async Task RechazarPinConFormatoIncorrecto()
    {
        IConfiguration configuracion =
            new ConfigurationBuilder()
                .Build();

        var servicio = new PinRetiroService(configuracion);

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.ValidarPinAsync(
                10,
                20,
                "ABC123"));
    }

}
