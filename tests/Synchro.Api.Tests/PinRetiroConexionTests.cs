using Microsoft.Extensions.Configuration;
using Xunit;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public class PinRetiroConexionTests : PruebaHistoricaAislada
{
    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task PuedeConsultarSaltEnBaseDePruebas()
    {
        // Configuracion efimera propia, sin User Secrets.
        IConfiguration configuracion =
            PruebaHistoricaAislada.Configuracion();

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

    [Theory]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=SynchroDB_Pruebas;Integrated Security=true")]
    [InlineData("Server=(localdb)\\SYNCHRO_Aislado;Database=SynchroDB;Integrated Security=true")]
    [InlineData("Server=(localdb)\\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;AttachDbFilename=C:\\no-abrir.mdf;Integrated Security=true")]
    public async Task RechazaOtroDestinoOArchivoAntesDeAbrirConexion(string cadena)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["ConnectionStrings:SynchroDBPruebas"]=cadena }).Build();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new PinRetiroService(config).ObtenerSaltAsync(1));
    }

    [Fact]
    [Trait("Category", "LocalDb")]
    public async Task ValidarPinDeEncomiendaInexistente()
    {
        IConfiguration configuracion =
            PruebaHistoricaAislada.Configuracion();

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
