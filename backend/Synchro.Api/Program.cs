using Synchro.Api.Models;
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
            mensaje = "El casillero no esta Disponible"
        });
    }

    casillero.Estado = "Reservado";

    return Results.Ok(casillero);
});

app.Run();
