
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Synchro.Api.Tests;

public class DetalleEncomiendaAutorizacionTests
{
    // Obtener identificadores de prueba sin modificar SQL Server.
    private static async Task<(int UsoId, int PropietarioId, int OtroId)>
        ObtenerDatosAsync(FabricaSynchroPruebas fabrica)
    {
        var configuracion = fabrica.Services
            .GetRequiredService<IConfiguration>();

        var cadena = configuracion
            .GetConnectionString("SynchroDB");

        var datosConexion = new SqlConnectionStringBuilder(cadena);

        // Impedir consultar accidentalmente la base original.
        if (datosConexion.InitialCatalog != "SynchroDB_Detalle_Pruebas"
            || datosConexion.DataSource != @"(localdb)\MSSQLLocalDB")
        {
            throw new InvalidOperationException(
                "SEGURIDAD: conexión de pruebas incorrecta.");
        }

        await using var conexion =
            new SqlConnection(datosConexion.ConnectionString);

        await conexion.OpenAsync();

        const string sql = """
            SELECT TOP (1)
                u.Id,
                p.PersonaId,
                (
                    SELECT TOP (1) pe.Id
                    FROM dbo.Personas pe
                    WHERE pe.Id <> p.PersonaId
                    ORDER BY pe.Id
                ) AS OtroId
            FROM dbo.UsosCasillero u
            INNER JOIN dbo.ParticipantesUso p
                ON p.UsoId = u.Id
                AND p.Rol = N'Destinatario'
            WHERE u.TipoServicio = N'Encomienda'
            ORDER BY u.Id DESC;
            """;

        await using var comando = new SqlCommand(sql, conexion);
        await using var lector = await comando.ExecuteReaderAsync();

        if (!await lector.ReadAsync())
        {
            throw new InvalidOperationException(
                "No hay encomiendas para realizar las pruebas.");
        }

        int usoId = lector.GetInt32(0);
        int propietarioId = lector.GetInt32(1);

        // Si no existe otra persona en la copia,
        // utilizar una identidad ficticia diferente.
        int otroId = lector.IsDBNull(2)
            ? (propietarioId == int.MaxValue ? 1 : int.MaxValue)
            : lector.GetInt32(2);

        return (usoId, propietarioId, otroId);
    }

    private static HttpClient CrearCliente(
        FabricaSynchroPruebas fabrica)
    {
        return fabrica.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }

    private static async Task<HttpResponseMessage> ConsultarAsync(
        HttpClient cliente, int usoId, int personaId)
    {
        using var peticion = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/destinatarios/mis-encomiendas/{usoId}");

        peticion.Headers.Add(
            "X-Synchro-Test-Persona",
            personaId.ToString());

        return await cliente.SendAsync(peticion);
    }

    [Fact]
    public async Task DestinatarioPuedeConsultarSuEncomienda()
    {
        using var fabrica = new FabricaSynchroPruebas();
        var datos = await ObtenerDatosAsync(fabrica);
        using var cliente = CrearCliente(fabrica);

        using var respuesta = await ConsultarAsync(
            cliente, datos.UsoId, datos.PropietarioId);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task DestinatarioNoPuedeConsultarEncomiendaAjena()
    {
        using var fabrica = new FabricaSynchroPruebas();
        var datos = await ObtenerDatosAsync(fabrica);
        using var cliente = CrearCliente(fabrica);

        using var respuesta = await ConsultarAsync(
            cliente, datos.UsoId, datos.OtroId);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task EncomiendaInexistenteDevuelve404()
    {
        using var fabrica = new FabricaSynchroPruebas();
        var datos = await ObtenerDatosAsync(fabrica);
        using var cliente = CrearCliente(fabrica);

        using var respuesta = await ConsultarAsync(
            cliente, int.MaxValue, datos.PropietarioId);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}