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

app.MapPost("/api/casilleros-bd/{codigo}/depositar", async (string codigo) =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    using var comando = new SqlCommand(@"UPDATE Casilleros SET Estado = N'Ocupado' WHERE Codigo = @codigo And Estado = N'Reservado'", conexion);
    comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

    var filasActualizadas = await comando.ExecuteNonQueryAsync();

    if (filasActualizadas == 0)
    {
        return Results.Conflict(new
        {
            mensaje = "Casillero no encontrado o no reservado"
        });
    }

    return Results.Ok(new
    {
        mensaje = "Deposito Guardador en SQL Server",
        codigo = codigo,
        estado = "Ocupado"
    });
});

app.MapPost("/api/casilleros-bd/{codigo}/retirar", async (string codigo) =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    using var comando = new SqlCommand(@"UPDATE Casilleros SET Estado =N'Disponible' WHERE Codigo = @codigo AND EStado = N'Ocupado'", conexion);

    comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

    var filasActualizadas = await comando.ExecuteNonQueryAsync();

    if (filasActualizadas == 0)
    {
        return Results.Conflict(new
        {
            mensaje = "Casillero no encontrado o no ocupado"
        });
    }

    return Results.Ok(new
    {
        mensaje = "Retiro guardado en SQL Server",
        codigo = codigo,
        estado = "Disponible"
    });
});

app.MapGet("/api/espacios-carga", async () =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    var lista = new List<EspacioCarga>();

    using var comando = new SqlCommand("SELECT Codigo, EStado FROM EspaciosCarga ORDER BY Codigo", conexion);

    using var lector = await comando.ExecuteReaderAsync();

    while (await lector.ReadAsync())
    {
        lista.Add(new EspacioCarga
        {
            Codigo = lector.GetString(0),
            Estado = lector.GetString(1)
        });
    }

    return Results.Ok(lista);
});

app.MapPost("/api/espacios-carga/{codigo}/reservar", async (string codigo) =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    using var comando = new SqlCommand(@"UPDATE EspaciosCarga SET Estado = N'Reservado' WHERE Codigo = @codigo AND Estado = N'Disponible'", conexion);

    comando.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

    var filasActualizadas = await comando.ExecuteNonQueryAsync();

    if (filasActualizadas == 0)
    {
        return Results.Conflict(new
        {
            mensaje = "Espacio de carga no encontrado o no disponible"
        });
    }

    return Results.Ok(new
    {
        mensaje = "Espacio de carga reservado correctamente",
        codigo = codigo,
        estado = "Reservado"
    });
});

app.MapPost("/api/espacios-carga/{codigo}/iniciar", async (string codigo) =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    using var transaccion = conexion.BeginTransaction();

    using var actualizar = new SqlCommand(@"UPDATE ESpaciosCarga SET Estado = N'Ocupado' WHERE Codigo = @codigo ANd Estado = N'Reservado'", conexion, transaccion);

    actualizar.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

    var filas = await actualizar.ExecuteNonQueryAsync();

    if (filas == 0)
    {
        transaccion.Rollback();

        return Results.Conflict(new
        {
            mensaje = "el espacio no esta reservado"
        });
    }

    using var insertar = new SqlCommand(@"INSERT INTO SesionesCarga (CodigoEspacio) OUTPUT INSERTED.Id Values (@Codigo)", conexion, transaccion);

    insertar.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

    var idSesion = Convert.ToInt32(await insertar.ExecuteScalarAsync());

    transaccion.Commit();

    return Results.Ok(new
    {
        mensaje = " Sesion de carga iniciada correctamente",
        codigo = codigo,
        estado = "Ocupado",
        idSesion = idSesion
    });
});

app.MapPost("/api/sesiones-carga/{id}/finalizar", async (int id) =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    using var transaccion = conexion.BeginTransaction();

    using var finalizar = new SqlCommand(@"UPDATE SesionesCarga SET Fin = SYSDATETIME() OUTPUT INSERTED.CodigoEspacio WHERE Id = @id AND Fin IS NULL", conexion, transaccion);

    finalizar.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;

    var codigo = await finalizar.ExecuteScalarAsync() as string;

    if (codigo == null)
    {
        transaccion.Rollback();

        return Results.Conflict(new
        {
            mensaje = "sesion no encontrada o ya finalizada"
        });
    }

    using var liberar = new SqlCommand(@"UPDATE EspaciosCarga SET Estado = N'Disponible' WHERE Codigo = @codigo AND Estado = N'Ocupado'", conexion, transaccion);

    liberar.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

    var filas = await liberar.ExecuteNonQueryAsync();

    if (filas != 1)
    {
        transaccion.Rollback();

        return Results.Conflict(new
        {
            mensaje = "No pudo liberar el espacio de carga"
        });
    }

    transaccion.Commit();

    return Results.Ok(new
    {
        mensaje = " Sesion finalizada correctamente",
        idSesion = id,
        estado = "Disponible"
    });
});

app.MapGet("/api/tarifas-carga", async () =>
{
    var cadena = app.Configuration.GetConnectionString("SynchroDb");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    var tarifas = new List<TarifaCarga>();

    using var comando = new SqlCommand(@"SELECT TipoDispositivo, MinutosBase, PrecioBase, MinutosAdicionales, PrecioAdicional FROM TarifasCarga", conexion);


    using var lector = await comando.ExecuteReaderAsync();

    while (await lector.ReadAsync())
    {
        tarifas.Add(new TarifaCarga
        {
            TipoDispositivo = lector.GetString(0),
            MinutosBase = lector.GetInt32(1),
            PrecioBase = lector.GetDecimal(2),
            MinutosAdicionales = lector.GetInt32(3),
            PrecioAdicional = lector.GetDecimal(4)
        });
    }

    return Results.Ok(tarifas);

});

app.Run();
