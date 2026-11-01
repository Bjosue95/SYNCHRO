using Synchro.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http.HttpResults;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/estado", () => new
{
    proyecto = "SYNCHRO",
    estado = "en desarrollo"
});


var casilleros = new[]
{
    new Casillero
    {
        Codigo = "S01",
        Tamano = "Pequeño",
        Tipo = "Normal",
        Estado ="Disponible"
    },
    new Casillero
    {
        Codigo = "M02",
        Tamano = "Mediano",
        Tipo ="Normal",
        Estado = "Disponible"
    }
};

app.MapGet("/api/casilleros", () => casilleros);

app.MapGet("/api/casilleros/{codigo}", (string codigo) =>
{
    var casillero = casilleros.FirstOrDefault(c =>
        c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

    if (casillero == null)
    {
        return Results.NotFound(new
        {
            mensaje = "Casillero no encontrado"
        });
    }
    return Results.Ok(casillero);
});

app.MapPost("/api/casilleros/{codigo}/reservar", (string codigo) =>
{
    var casillero = casilleros.FirstOrDefault(c =>
        c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

    if (casillero == null)
    {
        return Results.NotFound(new
        {
            mensaje = "Casillero no encontrado"
        });
    }

    if (casillero.Estado != "Disponible")
    {
        return Results.Conflict(new
        {
            mensaje = "El casillero no está Disponible"
        });
    }

    casillero.Estado = "Reservado";

    return Results.Ok(casillero);
});

app.MapPost("/api/casilleros/{codigo}/depositar", (string codigo) =>
{
    var casillero = casilleros.FirstOrDefault(c =>
        c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

    if (casillero == null)
    {
        return Results.NotFound(new
        {
            mensaje = "Casillero no encontrado"
        });
    }

    if (casillero.Estado != "Reservado")
    {
        return Results.Conflict(new
        {
            mensaje = "El casillero no está reservado"
        });
    }

    casillero.Estado = "Ocupado";

    return Results.Ok(new
    {
        mensaje = "Encomienda Depositada correctamente",
        codigo = casillero.Codigo,
        estado = casillero.Estado
    });
});

app.MapPost("/api/casilleros/{codigo}/retirar", (String codigo) =>
{
    var casillero = casilleros.FirstOrDefault(c =>
        c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));

    if (casillero == null)
    {
        return Results.NotFound(new
        {
            mensaje = "Casillero no encontrado"
        });
    }

    if (casillero.Estado != "Ocupado")
    {
        return Results.Conflict(new
        {
            mensaje = "El casillero no tiene encomiendas para retirar"
        });
    }

    casillero.Estado = "Disponible";

    return Results.Ok(new
    {
        mensaje = "Encomienda retirada correctamente",
        codigo = casillero.Codigo,
        estado = casillero.Estado
    });
});

app.MapGet("/api/prueba-bd", async () =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);

    await conexion.OpenAsync();

    using var comando = new SqlCommand("SELECT COUNT(*) FROM Casilleros", conexion);
    var total = Convert.ToInt32(await comando.ExecuteScalarAsync());
    return Results.Ok(new
    {
        mensaje = "Conexión correcta a SychroDB",
        totalCasilleros = total
    });
});

app.MapGet("/api/casilleros-bd", async () =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    var lista = new List<Casillero>();

    using var comando = new SqlCommand("SELECT Codigo , Tamano, Tipo, Estado FROM Casilleros ORDER BY Codigo", conexion);

    using var lector = await comando.ExecuteReaderAsync();

    while (await lector.ReadAsync())
    {
        lista.Add(new Casillero
        {
            Codigo = lector.GetString(0),
            Tamano = lector.GetString(1),
            Tipo = lector.GetString(2),
            Estado = lector.GetString(3)
        });
    }

    return Results.Ok(lista);
});

app.MapPost("/api/casilleros-bd/{codigo}/reservar", async (string codigo) =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    using var comando = new SqlCommand(@"UPDATE Casilleros Set Estado = N'Reservado' WHERE Codigo = @codigo AND Estado = N'Disponible'", conexion);

    comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

    var filasActualizadas = await comando.ExecuteNonQueryAsync();

    if (filasActualizadas == 0)
    {
        return Results.Conflict(new
        {
            mensaje = "Casillero no encontrado o no disponible"
        });
    }

    return Results.Ok(new
    {
        mensaje = "Resrva Guardada en SQL Server",
        codigo = codigo,
        estado = "Reservado"
    });
});

app.Run();
