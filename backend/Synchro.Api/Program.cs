using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Synchro.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)

    .AddCookie(options =>
    {
        options.Cookie.Name = "Synchro.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);


        options.Events.OnValidatePrincipal = async context =>
        {
            var idTexto = context.Principal?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(idTexto, out int id))
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return;
            }

            var configuracion = context.HttpContext
                .RequestServices.GetRequiredService<IConfiguration>();

            var cadena = configuracion.GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                context.RejectPrincipal();
                return;
            }

            using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            using var comando = new SqlCommand(@"
        SELECT Activo
        FROM dbo.Administradores
        WHERE Id = @id", conexion);

            comando.Parameters.Add(
                "@id", System.Data.SqlDbType.Int).Value = id;

            var resultado = await comando.ExecuteScalarAsync();

            if (resultado is not bool activo || !activo)
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };


        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloAdministradores", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("Administrador"));
});

builder.Services.AddScoped<
    IPasswordHasher<Administrador>,
    PasswordHasher<Administrador>>();


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("LoginLimitado", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                contexto.Connection.RemoteIpAddress?.ToString()
                ?? "sin-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var app = builder.Build();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

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

app.MapPost("/api/espacios-carga/{codigo}/iniciar", async (string codigo, IniciarCargaSolicitud solicitud) =>
{
    if (solicitud.TipoDispositivo is not ("Celular" or "Laptop"))
    {
        return Results.BadRequest(new
        {
            mensaje = "El dispositivo debe ser Celular o Laptop"
        });
    }

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

    using var insertar = new SqlCommand(@"
    INSERT INTO dbo.SesionesCarga (CodigoEspacio, TipoDispositivo)
    OUTPUT INSERTED.Id
    VALUES (@codigo, @tipoDispositivo)",
     conexion, transaccion);

    insertar.Parameters.Add("@codigo", System.Data.SqlDbType.NVarChar, 10).Value = codigo;

    insertar.Parameters.Add("@tipoDispositivo", System.Data.SqlDbType.NVarChar, 20).Value = solicitud.TipoDispositivo!;

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


    using var registrarCobro = new SqlCommand(@"
    INSERT INTO dbo.CobrosCarga (
        SesionCargaId,
        TipoDispositivo,
        MinutosFacturados,
        MinutosBaseAplicados,
        PrecioBaseAplicado,
        MinutosAdicionalesAplicados,
        PrecioAdicionalAplicado,
        MontoTotal
    )
    OUTPUT INSERTED.MontoTotal
    SELECT
        S.Id,
        S.TipoDispositivo,
        T.MinutosBase + B.Bloques * T.MinutosAdicionales,
        T.MinutosBase,
        T.PrecioBase,
        T.MinutosAdicionales,
        T.PrecioAdicional,
        T.PrecioBase + B.Bloques * T.PrecioAdicional
    FROM dbo.SesionesCarga AS S
    INNER JOIN dbo.TarifasCarga AS T
        ON S.TipoDispositivo = T.TipoDispositivo
    CROSS APPLY (
        SELECT DATEDIFF_BIG(SECOND, S.Inicio, S.Fin)
            AS Segundos
    ) AS D
    CROSS APPLY (
        SELECT CASE
            WHEN D.Segundos <= T.MinutosBase * 60
                THEN 0
            ELSE (
                D.Segundos - T.MinutosBase * 60
                + T.MinutosAdicionales * 60 - 1
            ) / (T.MinutosAdicionales * 60)
        END AS Bloques
    ) AS B
    WHERE S.Id = @id AND S.Fin IS NOT NULL",
        conexion, transaccion);

    registrarCobro.Parameters.Add(
        "@id", System.Data.SqlDbType.Int).Value = id;

    var monto = await registrarCobro.ExecuteScalarAsync();

    if (monto == null)
    {
        transaccion.Rollback();

        return Results.Conflict(new
        {
            mensaje = "No se pudo calcular el cobro. Verifique la tarifa."
        });
    }


    using var registrarPago = new SqlCommand(@"
    INSERT INTO dbo.Pagos (
        SesionCargaId,
        Monto,
        Estado
    )
    SELECT
        SesionCargaId,
        MontoTotal,
        N'Pendiente'
    FROM dbo.CobrosCarga
    WHERE SesionCargaId = @id",
        conexion, transaccion);

    registrarPago.Parameters.Add(
        "@id", System.Data.SqlDbType.Int).Value = id;

    var filasPago = await registrarPago.ExecuteNonQueryAsync();

    if (filasPago != 1)
    {
        transaccion.Rollback();

        return Results.Conflict(new
        {
            mensaje = "No se pudo registrar el pago pendiente."
        });
    }


    transaccion.Commit();

    return Results.Ok(new
    {
        mensaje = "Sesion finalizada correctamente",
        idSesion = id,
        estado = "Disponible",
        montoTotal = Convert.ToDecimal(monto)
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


app.MapPost("/api/admin/login", async (
    LoginAdministradorSolicitud solicitud,
    HttpContext contexto,
    IPasswordHasher<Administrador> hasher) =>
{
    if (!contexto.Request.IsHttps)
    {
        return Results.BadRequest(new
        {
            mensaje = "El inicio de sesion requiere HTTPS."
        });
    }

    if (string.IsNullOrWhiteSpace(solicitud.NombreUsuario) ||
        string.IsNullOrWhiteSpace(solicitud.Contrasena) ||
        solicitud.NombreUsuario.Length > 80)
    {
        return Results.BadRequest(new
        {
            mensaje = "Datos de inicio de sesion invalidos."
        });
    }

    var cadena = app.Configuration
        .GetConnectionString("SynchroDB");

    using var conexion = new SqlConnection(cadena);
    await conexion.OpenAsync();

    Administrador? administrador = null;

    using (var comando = new SqlCommand(@"
        SELECT Id, NombreUsuario, PasswordHash, Activo
        FROM dbo.Administradores
        WHERE NombreUsuario = @usuario",
        conexion))
    {
        comando.Parameters.Add(
            "@usuario",
            System.Data.SqlDbType.NVarChar,
            80).Value = solicitud.NombreUsuario;

        using var lector = await comando.ExecuteReaderAsync();

        if (await lector.ReadAsync())
        {
            administrador = new Administrador
            {
                Id = lector.GetInt32(0),
                NombreUsuario = lector.GetString(1),
                PasswordHash = lector.GetString(2),
                Activo = lector.GetBoolean(3)
            };
        }
    }

    if (administrador == null || !administrador.Activo)
    {
        return Results.Unauthorized();
    }

    var resultado = hasher.VerifyHashedPassword(
        administrador,
        administrador.PasswordHash,
        solicitud.Contrasena);

    if (resultado == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    var claims = new List<Claim>
    {
        new Claim(
            ClaimTypes.NameIdentifier,
            administrador.Id.ToString()),

        new Claim(
            ClaimTypes.Name,
            administrador.NombreUsuario),

        new Claim(
            ClaimTypes.Role,
            "Administrador")
    };

    var identidad = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme);

    var principal = new ClaimsPrincipal(identidad);

    await contexto.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal);

    return Results.Ok(new
    {
        mensaje = "Inicio de sesion correcto",
        usuario = administrador.NombreUsuario,
        rol = "Administrador"
    });
}).RequireRateLimiting("LoginLimitado");


app.MapGet("/api/admin/mi-sesion", (ClaimsPrincipal usuario) =>
{
    return Results.Ok(new
    {
        mensaje = "Acceso administrativo autorizado",
        usuario = usuario.Identity?.Name,
        rol = "Administrador"
    });
})
.RequireAuthorization("SoloAdministradores");


app.MapPost("/api/admin/logout", async (
    HttpContext contexto,
    IAntiforgery antiforgery) =>
{
    if (!contexto.Request.IsHttps)
    {
        return Results.BadRequest(new
        {
            mensaje = "Se requiere HTTPS."
        });
    }

    if (!await antiforgery.IsRequestValidAsync(contexto))
    {
        return Results.BadRequest(new
        {
            mensaje = "Token CSRF invalido o ausente."
        });
    }

    await contexto.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    return Results.Ok(new
    {
        mensaje = "Sesion administrativa cerrada correctamente"
    });
})
.RequireAuthorization("SoloAdministradores");



app.MapGet("/api/admin/csrf-token",
    (HttpContext contexto, IAntiforgery antiforgery) =>
{
    if (!contexto.Request.IsHttps)
    {
        return Results.BadRequest(new
        {
            mensaje = "Se requiere HTTPS."
        });
    }

    contexto.Response.Headers.CacheControl = "no-store";

    var tokens = antiforgery.GetAndStoreTokens(contexto);

    return Results.Ok(new
    {
        csrfToken = tokens.RequestToken
    });
})
.RequireAuthorization("SoloAdministradores");

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/demo/pagos/{id:int}/confirmar",
        async (
            int id,
            ConfirmarPagoSimuladoSolicitud solicitud,
            HttpContext contexto,
            IAntiforgery antiforgery) =>
        {
            if (!contexto.Request.IsHttps)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Se requiere HTTPS."
                });
            }

            if (!await antiforgery.IsRequestValidAsync(contexto))
            {
                return Results.BadRequest(new
                {
                    mensaje = "Token CSRF invalido o ausente."
                });
            }

            string? metodo = solicitud.CanalPago switch
            {
                "Tablet" => "NFC",
                "Web" => "TarjetaWeb",
                _ => null
            };

            if (metodo is null)
            {
                return Results.BadRequest(new
                {
                    mensaje = "El canal debe ser Tablet o Web."
                });
            }

            var cadena = app.Configuration
                .GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                return Results.Problem(
                    "No se encontro la conexion a SynchroDB.");
            }

            try
            {
                await using var conexion =
                    new SqlConnection(cadena);

                await conexion.OpenAsync();

                await using var comando =
                    new SqlCommand(
                        "dbo.ConfirmarPagoSimulado",
                        conexion);

                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@PagoId",
                    System.Data.SqlDbType.Int
                ).Value = id;

                comando.Parameters.Add(
                    "@CanalPago",
                    System.Data.SqlDbType.NVarChar,
                    20
                ).Value = solicitud.CanalPago;

                comando.Parameters.Add(
                    "@MetodoPago",
                    System.Data.SqlDbType.NVarChar,
                    30
                ).Value = metodo;

                await using var lector =
                    await comando.ExecuteReaderAsync();

                if (!await lector.ReadAsync())
                {
                    return Results.Problem(
                        "No se obtuvo respuesta del pago.");
                }

                return Results.Ok(new
                {
                    id = lector["Id"],
                    monto = lector["Monto"],
                    estado = lector["Estado"],
                    metodoPago = lector["MetodoPago"],
                    canalPago = lector["CanalPago"],
                    referenciaPago = lector["ReferenciaPago"],
                    esSimulado = lector["EsSimulado"],
                    fechaPago = lector["FechaPago"]
                });
            }
            catch (SqlException ex) when (ex.Number == 50002)
            {
                return Results.Conflict(new
                {
                    mensaje = "El pago no existe, ya fue confirmado o no es simulado."
                });
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/demo/pagos/{id:int}/rechazar",
        async (
            int id,
            RegistrarRechazoPagoSimuladoSolicitud solicitud,
            HttpContext contexto,
            IAntiforgery antiforgery) =>
        {
            if (!contexto.Request.IsHttps)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Se requiere HTTPS."
                });
            }

            if (!await antiforgery.IsRequestValidAsync(contexto))
            {
                return Results.BadRequest(new
                {
                    mensaje = "Token CSRF invalido o ausente."
                });
            }

            if (solicitud.CanalPago is not ("Tablet" or "Web"))
            {
                return Results.BadRequest(new
                {
                    mensaje = "El canal debe ser Tablet o Web."
                });
            }

            var cadena = app.Configuration
                .GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                return Results.Problem(
                    "No se encontro la conexion a SynchroDB.");
            }

            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync();

                await using var comando = new SqlCommand(
                    "dbo.RegistrarIntentoPagoRechazado",
                    conexion);

                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@PagoId",
                    System.Data.SqlDbType.Int
                ).Value = id;

                comando.Parameters.Add(
                    "@CanalPago",
                    System.Data.SqlDbType.NVarChar,
                    20
                ).Value = solicitud.CanalPago;

                await using var lector =
                    await comando.ExecuteReaderAsync();

                if (!await lector.ReadAsync())
                {
                    return Results.Problem(
                        "No se obtuvo respuesta del intento de pago.");
                }

                return Results.Ok(new
                {
                    idIntento = lector["IdIntento"],
                    pagoId = lector["PagoId"],
                    canalPago = lector["CanalPago"],
                    metodoPago = lector["MetodoPago"],
                    resultado = lector["Resultado"],
                    referenciaIntento = lector["ReferenciaIntento"],
                    fechaIntento = lector["FechaIntento"],
                    estadoPago = lector["EstadoPago"]
                });
            }
            catch (SqlException ex) when (ex.Number == 50004)
            {
                return Results.Conflict(new
                {
                    mensaje = "El pago no existe, no esta pendiente o no es simulado."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "Ocurrio un error al registrar el intento de pago.");
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

app.Run();

public record IniciarCargaSolicitud(string? TipoDispositivo);

public record LoginAdministradorSolicitud(
    string? NombreUsuario,
    string? Contrasena
);

public record ConfirmarPagoSolicitud(
    string? MetodoPago
);

public record ConfirmarPagoSimuladoSolicitud(
    string CanalPago
);

public record RegistrarRechazoPagoSimuladoSolicitud(
    string? CanalPago
);
