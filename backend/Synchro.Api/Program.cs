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

builder.Services
    .AddAuthentication()
    .AddCookie("Destinatarios", options =>
    {
        options.Cookie.Name = "SYNCHRO.Destinatarios";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.SlidingExpiration = false;


        options.Events.OnValidatePrincipal = async contexto =>
        {
            var personaTexto = contexto.Principal?
                .FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier
                )?.Value;

            var tipoUsuario = contexto.Principal?
                .FindFirst("tipo_usuario")?.Value;

            if (!int.TryParse(personaTexto, out int personaId)
                || personaId <= 0
                || tipoUsuario != "Destinatario")
            {
                contexto.RejectPrincipal();

                await contexto.HttpContext.SignOutAsync(
                    "Destinatarios");

                return;
            }

            string? cadena = builder.Configuration
                .GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
            {
                contexto.RejectPrincipal();
                return;
            }

            try
            {
                await using var conexion = new SqlConnection(cadena);

                await conexion.OpenAsync(
                    contexto.HttpContext.RequestAborted);

                await using var comando = new SqlCommand(
                    @"SELECT COUNT(1)
              FROM dbo.Personas
              WHERE Id = @PersonaId",
                    conexion);

                comando.Parameters.Add(
                    "@PersonaId",
                    System.Data.SqlDbType.Int
                ).Value = personaId;

                int encontrados = Convert.ToInt32(
                    await comando.ExecuteScalarAsync(
                        contexto.HttpContext.RequestAborted));

                if (encontrados != 1)
                {
                    contexto.RejectPrincipal();

                    await contexto.HttpContext.SignOutAsync(
                        "Destinatarios");
                }
            }
            catch (SqlException)
            {
                contexto.RejectPrincipal();
            }
        };

        // Las API deben devolver errores HTTP,
        // no redireccionar a una pagina de inicio de sesion.
        options.Events.OnRedirectToLogin = contexto =>
        {
            contexto.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = contexto =>
        {
            contexto.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloDestinatarios", policy =>
    {
        policy.AddAuthenticationSchemes("Destinatarios");

        policy.RequireAuthenticatedUser();

        policy.RequireClaim(
            "tipo_usuario",
            "Destinatario"
        );
    });
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

#if false

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

#endif

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

#if false

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

#endif

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

app.MapGet("/api/casilleros/{codigo}/cotizar",
    async (string codigo, string tipoServicio, int minutos) =>
    {
        if (minutos <= 0)
        {
            return Results.BadRequest(new
            {
                mensaje = "La duracion debe ser mayor que cero."
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
                "dbo.CotizarAlmacenamiento", conexion);

            comando.CommandType =
                System.Data.CommandType.StoredProcedure;

            comando.Parameters.Add(
                "@CodigoCasillero",
                System.Data.SqlDbType.NVarChar, 10
            ).Value = codigo;

            comando.Parameters.Add(
                "@TipoServicio",
                System.Data.SqlDbType.NVarChar, 20
            ).Value = tipoServicio;

            comando.Parameters.Add(
                "@MinutosSolicitados",
                System.Data.SqlDbType.Int
            ).Value = minutos;

            await using var lector =
                await comando.ExecuteReaderAsync();

            if (!await lector.ReadAsync())
            {
                return Results.Problem(
                    "No se obtuvo la cotizacion.");
            }

            return Results.Ok(new
            {
                codigoCasillero = lector["CodigoCasillero"],
                tipoServicio = lector["TipoServicio"],
                tipoCasillero = lector["TipoCasillero"],
                tamano = lector["Tamano"],
                minutosSolicitados = lector["MinutosSolicitados"],
                precioBase = lector["PrecioBase"],
                bloquesAdicionales = lector["BloquesAdicionales"],
                precioAdicional = lector["PrecioAdicional"],
                montoTotal = lector["MontoTotal"]
            });
        }
        catch (SqlException ex) when (ex.Number == 50012)
        {
            return Results.NotFound(new
            {
                mensaje = "Casillero no encontrado."
            });
        }
        catch (SqlException ex) when (
            ex.Number == 50010 ||
            ex.Number == 50011 ||
            ex.Number == 50013 ||
            ex.Number == 50015)
        {
            return Results.BadRequest(new
            {
                mensaje = ex.Message
            });
        }
        catch (SqlException ex) when (ex.Number == 50014)
        {
            return Results.Conflict(new
            {
                mensaje = "No existe una tarifa activa unica."
            });
        }
        catch (SqlException)
        {
            return Results.Problem(
                "Error al consultar la tarifa.");
        }
    });

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/demo/reservas/casilleros",
        async (
            ReservarCasilleroSolicitud solicitud,
            HttpContext contexto,
            IAntiforgery antiforgery) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(new
                {
                    mensaje = "Se requiere HTTPS."
                });

            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest(new
                {
                    mensaje = "Token CSRF invalido o ausente."
                });

            if (string.IsNullOrWhiteSpace(solicitud.CodigoCasillero)
                || string.IsNullOrWhiteSpace(solicitud.TipoServicio))
                return Results.BadRequest(new
                {
                    mensaje = "Codigo y servicio son obligatorios."
                });

            var cadena = app.Configuration
                .GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem(
                    "No se encontro la conexion a SynchroDB.");

            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync();

                await using var comando = new SqlCommand(
                    "dbo.ReservarCasilleroSynchro", conexion);

                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@CodigoCasillero",
                    System.Data.SqlDbType.NVarChar, 10
                ).Value = solicitud.CodigoCasillero;

                comando.Parameters.Add(
                    "@TipoServicio",
                    System.Data.SqlDbType.NVarChar, 20
                ).Value = solicitud.TipoServicio;

                await using var lector =
                    await comando.ExecuteReaderAsync();

                if (!await lector.ReadAsync())
                    return Results.Problem(
                        "No se obtuvo la reserva.");

                return Results.Ok(new
                {
                    idUso = lector["IdUso"],
                    codigoCasillero = lector["CodigoCasillero"],
                    tipoServicio = lector["TipoServicio"],
                    tipoCasillero = lector["TipoCasillero"],
                    tamano = lector["Tamano"],
                    estado = lector["Estado"],
                    fechaReserva = lector["FechaReserva"]
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50020 ||
                ex.Number == 50021 ||
                ex.Number == 50024)
            {
                return Results.BadRequest(new
                {
                    mensaje = ex.Message
                });
            }
            catch (SqlException ex) when (ex.Number == 50022)
            {
                return Results.NotFound(new
                {
                    mensaje = "Casillero no encontrado."
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50023 ||
                ex.Number == 50025)
            {
                return Results.Conflict(new
                {
                    mensaje = "Casillero no disponible."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "Error al registrar la reserva.");
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/demo/reservas/{id:int}/cancelar",
        async (
            int id,
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

            if (id <= 0)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Identificador de reserva invalido."
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
                    "dbo.CancelarReservaCasilleroSynchro",
                    conexion);

                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@UsoId",
                    System.Data.SqlDbType.Int
                ).Value = id;

                await using var lector =
                    await comando.ExecuteReaderAsync();

                if (!await lector.ReadAsync())
                {
                    return Results.Problem(
                        "No se obtuvo respuesta de la cancelacion.");
                }

                return Results.Ok(new
                {
                    idUso = lector["IdUso"],
                    codigoCasillero = lector["CodigoCasillero"],
                    tipoServicio = lector["TipoServicio"],
                    estadoOperacion = lector["EstadoOperacion"],
                    estadoCasillero = lector["EstadoCasillero"]
                });
            }
            catch (SqlException ex) when (ex.Number == 50031)
            {
                return Results.NotFound(new
                {
                    mensaje = "Reserva no encontrada."
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50033 ||
                ex.Number == 50034 ||
                ex.Number == 50035)
            {
                return Results.Conflict(new
                {
                    mensaje = "La reserva no puede cancelarse en su estado actual."
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50030)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Identificador invalido."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "Error al cancelar la reserva.");
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/demo/reservas/{id:int}/depositar",
        async (
            int id,
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

            if (id <= 0)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Identificador de operacion invalido."
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
                    "dbo.RegistrarDepositoEncomiendaSynchro",
                    conexion);

                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@UsoId",
                    System.Data.SqlDbType.Int
                ).Value = id;

                await using var lector =
                    await comando.ExecuteReaderAsync();

                if (!await lector.ReadAsync())
                {
                    return Results.Problem(
                        "No se obtuvo respuesta del deposito.");
                }

                return Results.Ok(new
                {
                    idUso = lector["IdUso"],
                    codigoCasillero = lector["CodigoCasillero"],
                    tipoServicio = lector["TipoServicio"],
                    tipoCasillero = lector["TipoCasillero"],
                    estadoOperacion = lector["EstadoOperacion"],
                    estadoCasillero = lector["EstadoCasillero"],
                    fechaIngreso = lector["FechaIngreso"]
                });
            }

            catch (SqlException ex) when (ex.Number == 50140)
            {
                return Results.Conflict(new
                {
                    mensaje = "No se puede depositar la encomienda porque faltan participantes."
                });
            }
            catch (SqlException ex) when (ex.Number == 50141)
            {
                return Results.Conflict(new
                {
                    mensaje = "No se puede depositar la encomienda porque el consentimiento del destinatario no es valido."
                });
            }
            catch (SqlException ex) when (ex.Number == 50142)
            {
                return Results.Conflict(new
                {
                    mensaje = "No se puede depositar la encomienda porque falta verificar el telefono del destinatario."
                });
            }

            catch (SqlException ex) when (ex.Number == 50041)
            {
                return Results.NotFound(new
                {
                    mensaje = "Reserva no encontrada."
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50043 ||
                ex.Number == 50044 ||
                ex.Number == 50045 ||
                ex.Number == 50046 ||
                ex.Number == 50047 ||
                ex.Number == 50048)
            {
                return Results.Conflict(new
                {
                    mensaje = "No se puede registrar el deposito en el estado actual."
                });
            }
            catch (SqlException ex) when (ex.Number == 50040)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Identificador invalido."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "Error al registrar el deposito.");
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/demo/reservas/{id:int}/retirar",
        async (
            int id,
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

            if (id <= 0)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Identificador de operacion invalido."
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
                    "dbo.RegistrarRetiroEncomiendaSynchro",
                    conexion);

                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@UsoId",
                    System.Data.SqlDbType.Int
                ).Value = id;

                await using var lector =
                    await comando.ExecuteReaderAsync();

                if (!await lector.ReadAsync())
                {
                    return Results.Problem(
                        "No se obtuvo respuesta del retiro.");
                }

                return Results.Ok(new
                {
                    idUso = lector["IdUso"],
                    codigoCasillero = lector["CodigoCasillero"],
                    tipoServicio = lector["TipoServicio"],
                    tipoCasillero = lector["TipoCasillero"],
                    estadoOperacion = lector["EstadoOperacion"],
                    estadoCasillero = lector["EstadoCasillero"],
                    fechaIngreso = lector["FechaIngreso"],
                    fechaRetiro = lector["FechaRetiro"]
                });
            }
            catch (SqlException ex) when (ex.Number == 50051)
            {
                return Results.NotFound(new
                {
                    mensaje = "Operacion no encontrada."
                });
            }
            catch (SqlException ex) when (
                ex.Number >= 50053 &&
                ex.Number <= 50058)
            {
                return Results.Conflict(new
                {
                    mensaje = "No se puede retirar la encomienda en su estado actual."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "Error al registrar el retiro.");
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/demo/reservas/{id:int}/participantes",
        async (
            int id,
            AsignarParticipantesPeticion datos,
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

            if (id <= 0 ||
                datos.RemitenteId <= 0 ||
                datos.DestinatarioId <= 0)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Identificadores invalidos."
                });
            }

            if (datos.RemitenteId == datos.DestinatarioId)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Remitente y destinatario deben ser distintos."
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
                    "dbo.AsignarParticipantesEncomiendaSynchro",
                    conexion);

                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@UsoId",
                    System.Data.SqlDbType.Int
                ).Value = id;

                comando.Parameters.Add(
                    "@RemitenteId",
                    System.Data.SqlDbType.Int
                ).Value = datos.RemitenteId;

                comando.Parameters.Add(
                    "@DestinatarioId",
                    System.Data.SqlDbType.Int
                ).Value = datos.DestinatarioId;

                var participantes = new List<object>();

                await using var lector =
                    await comando.ExecuteReaderAsync();

                while (await lector.ReadAsync())
                {
                    participantes.Add(new
                    {
                        usoId = Convert.ToInt32(lector["UsoId"]),
                        personaId = Convert.ToInt32(lector["PersonaId"]),
                        rol = lector["Rol"].ToString()
                    });
                }

                return Results.Ok(new
                {
                    mensaje = "Participantes asignados correctamente.",
                    idUso = id,
                    participantes
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50060 ||
                ex.Number == 50061)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Datos de participantes invalidos."
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50062 ||
                ex.Number == 50066 ||
                ex.Number == 50067)
            {
                return Results.NotFound(new
                {
                    mensaje = "Operacion o persona no encontrada."
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50063 ||
                ex.Number == 50064 ||
                ex.Number == 50065 ||
                ex.Number == 50068 ||
                ex.Number == 2601 ||
                ex.Number == 2627)
            {
                return Results.Conflict(new
                {
                    mensaje = "No se pueden asignar participantes a esta reserva."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "Error al asignar participantes.");
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/demo/verificaciones-telefono",
        async (
            SolicitarVerificacionTelefonoPeticion datos,
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
                    mensaje = "Token CSRF invalido."
                });
            }

            string? telefono = datos.Telefono?.Trim();

            if (datos.PersonaId <= 0 ||
                string.IsNullOrWhiteSpace(telefono) ||
                telefono.Length > 20)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Datos de verificacion invalidos."
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
                string codigo =
                    CodigoVerificacionTelefono.Generar();

                byte[] hash =
                    CodigoVerificacionTelefono.CalcularHash(
                        codigo,
                        datos.PersonaId,
                        telefono,
                        app.Configuration);

                try
                {
                    await using var conexion =
                        new SqlConnection(cadena);

                    await conexion.OpenAsync();

                    await using var comando = new SqlCommand(
                        "dbo.CrearVerificacionTelefonoPendienteSynchro",
                        conexion);

                    comando.CommandType =
                        System.Data.CommandType.StoredProcedure;

                    comando.Parameters.Add(
                        "@PersonaId",
                        System.Data.SqlDbType.Int
                    ).Value = datos.PersonaId;

                    comando.Parameters.Add(
                        "@Telefono",
                        System.Data.SqlDbType.NVarChar,
                        20
                    ).Value = telefono;

                    comando.Parameters.Add(
                        "@CodigoHash",
                        System.Data.SqlDbType.VarBinary,
                        32
                    ).Value = hash;

                    await using var lector =
                        await comando.ExecuteReaderAsync();

                    if (!await lector.ReadAsync())
                    {
                        return Results.Problem(
                            "No se registro la verificacion.");
                    }

                    return Results.Ok(new
                    {
                        id = Convert.ToInt32(lector["Id"]),
                        personaId = Convert.ToInt32(
                            lector["PersonaId"]),
                        estado = lector["Estado"].ToString(),
                        fechaSolicitud = lector["FechaSolicitud"],
                        fechaExpiracion = lector["FechaExpiracion"],
                        mensaje = "Verificacion pendiente registrada."
                    });
                }
                finally
                {
                    System.Security.Cryptography
                        .CryptographicOperations.ZeroMemory(hash);
                }
            }
            catch (SqlException ex) when (
                ex.Number >= 50100 &&
                ex.Number <= 50104)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Persona o telefono invalido."
                });
            }
            catch (SqlException ex) when (ex.Number == 50105)
            {
                return Results.Conflict(new
                {
                    mensaje = "Ya existe una verificacion vigente."
                });
            }

            catch (SqlException ex) when (
                ex.Number == 50106 ||
                ex.Number == 50107 ||
                ex.Number == 50108)
            {
                return Results.Json(
                    new
                    {
                        mensaje = "Demasiadas solicitudes de verificacion. Intenta mas tarde."
                    },
                    statusCode: StatusCodes.Status429TooManyRequests
                );
            }

            catch (SqlException)
            {
                return Results.Problem(
                    "Error al registrar la verificacion.");
            }
            catch (FormatException)
            {
                return Results.Problem(
                    "Configuracion de seguridad invalida.");
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(
                    "No se pudo cargar la clave de seguridad.");
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

if (app.Environment.IsDevelopment())
{
    app.MapPost(
        "/api/demo/verificaciones-telefono/{id:int}/validar",
        async (
            int id,
            ValidarTelefonoPeticion datos,
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
                    mensaje = "Token CSRF invalido."
                });
            }

            if (id <= 0 || datos.PersonaId <= 0 ||
                string.IsNullOrWhiteSpace(datos.Codigo) ||
                datos.Codigo.Length != 6 ||
                !datos.Codigo.All(c => c >= '0' && c <= '9'))
            {
                return Results.BadRequest(new
                {
                    mensaje = "Datos de verificacion invalidos."
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

                // Obtener el telefono registrado para esta verificacion.
                await using var buscar = new SqlCommand(
                    @"SELECT Telefono
                      FROM dbo.VerificacionesTelefono
                      WHERE Id = @Id AND PersonaId = @PersonaId",
                    conexion);

                buscar.Parameters.Add(
                    "@Id", System.Data.SqlDbType.Int
                ).Value = id;

                buscar.Parameters.Add(
                    "@PersonaId", System.Data.SqlDbType.Int
                ).Value = datos.PersonaId;

                string? telefono =
                    await buscar.ExecuteScalarAsync() as string;

                if (telefono is null)
                {
                    return Results.NotFound(new
                    {
                        mensaje = "Verificacion no encontrada."
                    });
                }

                byte[] hash =
                    CodigoVerificacionTelefono.CalcularHash(
                        datos.Codigo,
                        datos.PersonaId,
                        telefono,
                        app.Configuration);

                try
                {
                    await using var comando = new SqlCommand(
                        "dbo.ValidarCodigoTelefonoSynchro",
                        conexion);

                    comando.CommandType =
                        System.Data.CommandType.StoredProcedure;

                    comando.Parameters.Add(
                        "@VerificacionId",
                        System.Data.SqlDbType.Int
                    ).Value = id;

                    comando.Parameters.Add(
                        "@PersonaId",
                        System.Data.SqlDbType.Int
                    ).Value = datos.PersonaId;

                    comando.Parameters.Add(
                        "@CodigoHash",
                        System.Data.SqlDbType.VarBinary,
                        32
                    ).Value = hash;

                    await using var lector =
                        await comando.ExecuteReaderAsync();

                    if (!await lector.ReadAsync())
                    {
                        return Results.Problem(
                            "No se obtuvo respuesta de SQL Server.");
                    }

                    string resultado =
                        lector["Resultado"].ToString() ?? "";

                    var respuesta = new
                    {
                        verificacionId = id,
                        estado = lector["Estado"].ToString(),
                        intentosFallidos = Convert.ToInt32(
                            lector["IntentosFallidos"]),
                        resultado
                    };

                    return resultado switch
                    {
                        "Verificado" => Results.Ok(respuesta),
                        "Incorrecto" => Results.BadRequest(respuesta),
                        "Bloqueado" => Results.Json(
                            respuesta, statusCode: 423),
                        "Vencido" => Results.Json(
                            respuesta, statusCode: 410),
                        "NoDisponible" => Results.Conflict(respuesta),
                        _ => Results.Problem(
                            "Resultado de verificacion desconocido.")
                    };
                }
                finally
                {
                    System.Security.Cryptography
                        .CryptographicOperations.ZeroMemory(hash);
                }
            }
            catch (SqlException ex) when (ex.Number == 50122)
            {
                return Results.NotFound(new
                {
                    mensaje = "Verificacion no encontrada."
                });
            }
            catch (SqlException ex) when (
                ex.Number == 50120 || ex.Number == 50121)
            {
                return Results.BadRequest(new
                {
                    mensaje = "Parametros invalidos."
                });
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "Error al validar el codigo.");
            }
            catch (FormatException)
            {
                return Results.Problem(
                    "Configuracion de seguridad invalida.");
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(
                    "No se pudo cargar la configuracion de seguridad.");
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

if (app.Environment.IsDevelopment())
{
    app.MapPost(
        "/api/demo/verificaciones-telefono/prueba-integracion",
        async (
            HttpContext contexto,
            IAntiforgery antiforgery) =>
        {
            if (!contexto.Request.IsHttps)
                return Results.BadRequest(
                    new { mensaje = "Se requiere HTTPS." });

            if (!await antiforgery.IsRequestValidAsync(contexto))
                return Results.BadRequest(
                    new { mensaje = "Token CSRF invalido." });

            const int personaId = 2;
            const string telefono = "+12025550142";

            var cadena = app.Configuration
                .GetConnectionString("SynchroDB");

            if (string.IsNullOrWhiteSpace(cadena))
                return Results.Problem(
                    "Conexion SQL no configurada.");

            string codigo = CodigoVerificacionTelefono.Generar();

            byte[] hash;

            try
            {
                hash = CodigoVerificacionTelefono.CalcularHash(
                    codigo, personaId, telefono, app.Configuration);
            }
            catch (InvalidOperationException)
            {
                return Results.Problem(
                    "Clave HMAC no configurada correctamente.");
            }
            catch (FormatException)
            {
                return Results.Problem(
                    "Formato de clave HMAC invalido.");
            }

            try
            {
                await using var conexion = new SqlConnection(cadena);
                await conexion.OpenAsync();

                await using var transaccion =
                    (SqlTransaction)await conexion.BeginTransactionAsync();

                try
                {
                    int verificacionId;

                    await using (var crear = new SqlCommand(
                        "dbo.CrearVerificacionTelefonoPendienteSynchro",
                        conexion,
                        transaccion))
                    {
                        crear.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        crear.Parameters.Add("@PersonaId",
                            System.Data.SqlDbType.Int).Value = personaId;

                        crear.Parameters.Add("@Telefono",
                            System.Data.SqlDbType.NVarChar, 20)
                            .Value = telefono;

                        crear.Parameters.Add("@CodigoHash",
                            System.Data.SqlDbType.VarBinary, 32)
                            .Value = hash;

                        await using var lector =
                            await crear.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                            throw new InvalidOperationException(
                                "No se creo la verificacion.");

                        verificacionId =
                            Convert.ToInt32(lector["Id"]);
                    }

                    string resultado;

                    await using (var validar = new SqlCommand(
                        "dbo.ValidarCodigoTelefonoSynchro",
                        conexion,
                        transaccion))
                    {
                        validar.CommandType =
                            System.Data.CommandType.StoredProcedure;

                        validar.Parameters.Add("@VerificacionId",
                            System.Data.SqlDbType.Int)
                            .Value = verificacionId;

                        validar.Parameters.Add("@PersonaId",
                            System.Data.SqlDbType.Int)
                            .Value = personaId;

                        validar.Parameters.Add("@CodigoHash",
                            System.Data.SqlDbType.VarBinary, 32)
                            .Value = hash;

                        await using var lector =
                            await validar.ExecuteReaderAsync();

                        if (!await lector.ReadAsync())
                            throw new InvalidOperationException(
                                "No se obtuvo resultado de validacion.");

                        resultado =
                            lector["Resultado"].ToString() ?? "";
                    }

                    // La prueba no debe dejar datos verificados.
                    await transaccion.RollbackAsync();

                    return Results.Ok(new
                    {
                        prueba = "Integracion C# y SQL Server",
                        resultado,
                        cambiosGuardados = false
                    });
                }
                catch
                {
                    await transaccion.RollbackAsync();
                    throw;
                }
            }
            catch (SqlException)
            {
                return Results.Problem(
                    "Error SQL durante la prueba.");
            }
            finally
            {
                System.Security.Cryptography
                    .CryptographicOperations.ZeroMemory(hash);
            }
        })
        .RequireAuthorization("SoloAdministradores");
}

app.MapGet(
    "/api/destinatarios/mi-sesion",
    (HttpContext contexto) =>
    {
        return Results.Ok(new
        {
            autenticado = true,
            tipoUsuario = contexto.User
                .FindFirst("tipo_usuario")?.Value
        });
    }
)
.RequireAuthorization("SoloDestinatarios");

app.MapGet(
    "/api/destinatarios/csrf-token",
    async (HttpContext contexto, IAntiforgery antiforgery) =>
    {
        if (!contexto.Request.IsHttps)
        {
            return Results.BadRequest(new
            {
                mensaje = "Se requiere HTTPS."
            });
        }

        // Reconocer expresamente la sesion del destinatario.
        var autenticacion =
            await contexto.AuthenticateAsync("Destinatarios");

        if (autenticacion.Succeeded &&
            autenticacion.Principal is not null)
        {
            contexto.User = autenticacion.Principal;
        }
        else
        {
            // Antes del login, el token es anonimo.
            contexto.User =
                new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity());
        }

        contexto.Response.Headers.CacheControl = "no-store";

        var tokens =
            antiforgery.GetAndStoreTokens(contexto);

        return Results.Ok(new
        {
            csrfToken = tokens.RequestToken
        });
    }
)
.AllowAnonymous();

app.MapPost("/api/destinatarios/login",
    async (
        InicioSesionDestinatarioPeticion datos,
        HttpContext contexto,
        IAntiforgery antiforgery) =>
{
    // Mantener deshabilitado el acceso en produccion
    // hasta implementar la entrega real de codigos.
    if (!app.Environment.IsDevelopment())
        return Results.StatusCode(503);

    if (!contexto.Request.IsHttps)
        return Results.BadRequest(new
        {
            mensaje = "Se requiere HTTPS."
        });

    contexto.Response.Headers.CacheControl = "no-store";

    if (!await antiforgery.IsRequestValidAsync(contexto))
        return Results.BadRequest(new
        {
            mensaje = "Token CSRF invalido."
        });

    // Comprobar que el codigo contiene seis digitos.
    if (datos.VerificacionId <= 0 ||
        datos.Codigo is null ||
        datos.Codigo.Length != 6 ||
        datos.Codigo.Any(c => c < '0' || c > '9'))
        return Results.Unauthorized();

    string? cadena = app.Configuration
        .GetConnectionString("SynchroDB");

    if (string.IsNullOrWhiteSpace(cadena))
        return Results.Problem(
            "Conexion SQL no configurada.");

    try
    {
        await using var conexion = new SqlConnection(cadena);
        await conexion.OpenAsync();

        int personaId;
        string telefono;

        // La identidad procede de SQL Server, no del usuario.
        await using (var buscar = new SqlCommand(
            @"SELECT PersonaId, Telefono
              FROM dbo.VerificacionesTelefono
              WHERE Id = @Id
                AND Finalidad = N'InicioSesionDestinatario'",
            conexion))
        {
            buscar.Parameters.Add(
                "@Id", System.Data.SqlDbType.Int
            ).Value = datos.VerificacionId;

            await using var lector =
                await buscar.ExecuteReaderAsync();

            if (!await lector.ReadAsync())
                return Results.Unauthorized();

            personaId = lector.GetInt32(0);
            telefono = lector.GetString(1);
        }

        byte[] hash =
            CodigoVerificacionTelefono.CalcularHash(
                datos.Codigo,
                personaId,
                telefono,
                app.Configuration);

        try
        {
            string resultado;

            // Validar codigo y finalidad.
            await using (var validar = new SqlCommand(
                "dbo.ValidarCodigoTelefonoConFinalidadSynchro",
                conexion))
            {
                validar.CommandType =
                    System.Data.CommandType.StoredProcedure;

                validar.Parameters.Add(
                    "@VerificacionId",
                    System.Data.SqlDbType.Int
                ).Value = datos.VerificacionId;

                validar.Parameters.Add(
                    "@PersonaId",
                    System.Data.SqlDbType.Int
                ).Value = personaId;

                validar.Parameters.Add(
                    "@CodigoHash",
                    System.Data.SqlDbType.VarBinary, 32
                ).Value = hash;

                validar.Parameters.Add(
                    "@FinalidadEsperada",
                    System.Data.SqlDbType.NVarChar, 40
                ).Value = "InicioSesionDestinatario";

                await using var lector =
                    await validar.ExecuteReaderAsync();

                if (!await lector.ReadAsync())
                    return Results.Unauthorized();

                resultado = lector["Resultado"].ToString() ?? "";
            }

            if (resultado != "Verificado")
                return Results.Unauthorized();
        }
        finally
        {
            System.Security.Cryptography
                .CryptographicOperations.ZeroMemory(hash);
        }

        // Impedir emitir dos sesiones con la misma verificacion.
        await using (var consumir = new SqlCommand(
            "dbo.ConsumirVerificacionInicioSesionSynchro",
            conexion))
        {
            consumir.CommandType =
                System.Data.CommandType.StoredProcedure;

            consumir.Parameters.Add(
                "@VerificacionId",
                System.Data.SqlDbType.Int
            ).Value = datos.VerificacionId;

            consumir.Parameters.Add(
                "@PersonaId",
                System.Data.SqlDbType.Int
            ).Value = personaId;

            await using var lector =
                await consumir.ExecuteReaderAsync();

            if (!await lector.ReadAsync())
                return Results.Unauthorized();
        }

        // Solo despues de validar y consumir el codigo
        // creamos la identidad del destinatario.
        var identidad = new System.Security.Claims.ClaimsIdentity(
            new[]
            {
                new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier,
                    personaId.ToString()),

                new System.Security.Claims.Claim(
                    "tipo_usuario",
                    "Destinatario")
            },
            "Destinatarios");

        var usuario =
            new System.Security.Claims.ClaimsPrincipal(identidad);

        await contexto.SignInAsync(
            "Destinatarios",
            usuario,
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10)
            });

        return Results.Ok(new
        {
            mensaje = "Sesion de destinatario iniciada."
        });
    }
    catch (SqlException ex) when (
        ex.Number == 50222 ||
        ex.Number == 50274 ||
        ex.Number == 50275 ||
        ex.Number == 50122)
    {
        return Results.Unauthorized();
    }
    catch (SqlException)
    {
        return Results.Problem(
            "No se pudo completar el inicio de sesion.");
    }
    catch (InvalidOperationException)
    {
        return Results.Problem(
            "Configuracion de seguridad no disponible.");
    }
    catch (FormatException)
    {
        return Results.Problem(
            "Configuracion de seguridad invalida.");
    }
})
.AllowAnonymous();

app.MapPost(
    "/api/destinatarios/logout",
    async (
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
                mensaje = "Token CSRF invalido."
            });
        }

        await contexto.SignOutAsync("Destinatarios");

        contexto.Response.Headers.CacheControl = "no-store";

        return Results.Ok(new
        {
            mensaje = "Sesion de destinatario cerrada."
        });
    }
)
.RequireAuthorization("SoloDestinatarios");

app.MapGet(
    "/api/destinatarios/mis-consentimientos-whatsapp",
    async (HttpContext contexto) =>
    {
        if (!contexto.Request.IsHttps)
        {
            return Results.BadRequest(new
            {
                mensaje = "Se requiere HTTPS."
            });
        }

        // Identificar al destinatario que inicio sesion.
        string? idTexto = contexto.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(idTexto, out int personaId))
        {
            return Results.Unauthorized();
        }

        string? cadena = app.Configuration
            .GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            return Results.Problem(
                "Conexion SQL no configurada.");
        }

        try
        {
            await using var conexion =
                new SqlConnection(cadena);

            await conexion.OpenAsync();

            await using var comando = new SqlCommand(
                @"SELECT TOP (20)
                    Id,
                    Estado,
                    Finalidad,
                    FechaRegistro
                  FROM dbo.ConsentimientosWhatsApp
                  WHERE PersonaId = @PersonaId
                  ORDER BY FechaRegistro DESC, Id DESC",
                conexion);

            comando.Parameters.Add(
                "@PersonaId",
                System.Data.SqlDbType.Int
            ).Value = personaId;

            await using var lector =
                await comando.ExecuteReaderAsync();


            var consentimientos = new List<object>();

            string estadoActual = "Sin consentimiento";

            while (await lector.ReadAsync())
            {
                string estado = lector["Estado"].ToString() ?? "";
                string finalidad = lector["Finalidad"].ToString() ?? "";

                // Identificar el consentimiento mas reciente.
                if (estadoActual == "Sin consentimiento" &&
                    finalidad == "NotificacionesEncomienda")
                {
                    estadoActual = estado;
                }

                consentimientos.Add(new
                {
                    id = Convert.ToInt32(lector["Id"]),
                    estado,
                    finalidad,
                    fechaRegistro = lector["FechaRegistro"]
                });
            }

            contexto.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new
            {
                estadoActual,
                consentimientos
            });

        }
        catch (SqlException)
        {
            return Results.Problem(
                "No se pudieron consultar los consentimientos.");
        }
    }
)
.RequireAuthorization("SoloDestinatarios");

app.MapPost(
    "/api/destinatarios/consentimientos-whatsapp/{id:int}/aceptar",
    async (
        int id,
        AceptarConsentimientoWhatsAppPeticion datos,
        HttpContext contexto,
        IAntiforgery antiforgery) =>
    {
        // Mantener esta funcion en pruebas hasta
        // integrar la verificacion real por WhatsApp.
        if (!app.Environment.IsDevelopment())
        {
            return Results.StatusCode(503);
        }

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
                mensaje = "Token CSRF invalido."
            });
        }

        if (id <= 0 ||
            datos.VerificacionTelefonoId <= 0 ||
            !datos.ConfirmacionExpresa)
        {
            return Results.BadRequest(new
            {
                mensaje = "Se requiere una aceptacion expresa y una verificacion valida."
            });
        }

        // Obtener la persona desde su sesion.
        string? personaTexto = contexto.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(personaTexto, out int personaId))
        {
            return Results.Unauthorized();
        }

        string? cadena = app.Configuration
            .GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            return Results.Problem(
                "Conexion SQL no configurada.");
        }

        try
        {
            await using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            // Comprobar que el consentimiento
            // pertenece al destinatario autenticado.
            await using (var comprobar = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.ConsentimientosWhatsApp
                  WHERE Id = @ConsentimientoId
                    AND PersonaId = @PersonaId",
                conexion))
            {
                comprobar.Parameters.Add(
                    "@ConsentimientoId",
                    System.Data.SqlDbType.Int
                ).Value = id;

                comprobar.Parameters.Add(
                    "@PersonaId",
                    System.Data.SqlDbType.Int
                ).Value = personaId;

                int encontrados = Convert.ToInt32(
                    await comprobar.ExecuteScalarAsync());

                if (encontrados != 1)
                {
                    return Results.NotFound(new
                    {
                        mensaje = "Consentimiento no encontrado."
                    });
                }
            }

            // Registrar la aceptacion mediante SQL Server.
            await using (var comando = new SqlCommand(
                "dbo.RegistrarAceptacionConsentimientoWhatsAppSynchro",
                conexion))
            {
                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@ConsentimientoId",
                    System.Data.SqlDbType.Int
                ).Value = id;

                comando.Parameters.Add(
                    "@VerificacionTelefonoId",
                    System.Data.SqlDbType.Int
                ).Value = datos.VerificacionTelefonoId;

                comando.Parameters.Add(
                    "@ConfirmacionExpresa",
                    System.Data.SqlDbType.Bit
                ).Value = true;

                await comando.ExecuteNonQueryAsync();
            }

            // Confirmar que la aceptacion quedo registrada.
            await using var verificar = new SqlCommand(
                @"SELECT COUNT(1)
                  FROM dbo.ConsentimientosWhatsApp
                  WHERE Id = @ConsentimientoId
                    AND PersonaId = @PersonaId
                    AND VerificacionTelefonoId = @VerificacionId
                    AND Estado = N'Aceptado'",
                conexion);

            verificar.Parameters.Add(
                "@ConsentimientoId",
                System.Data.SqlDbType.Int
            ).Value = id;

            verificar.Parameters.Add(
                "@PersonaId",
                System.Data.SqlDbType.Int
            ).Value = personaId;

            verificar.Parameters.Add(
                "@VerificacionId",
                System.Data.SqlDbType.Int
            ).Value = datos.VerificacionTelefonoId;

            int aceptados = Convert.ToInt32(
                await verificar.ExecuteScalarAsync());

            if (aceptados != 1)
            {
                return Results.Conflict(new
                {
                    mensaje = "No se confirmo la aceptacion."
                });
            }

            contexto.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new
            {
                mensaje = "Consentimiento aceptado correctamente."
            });
        }
        catch (SqlException ex) when (
            ex.Number >= 50180 && ex.Number <= 50199)
        {
            return Results.Conflict(new
            {
                mensaje = "El consentimiento o la verificacion no cumplen los requisitos."
            });
        }
        catch (SqlException)
        {
            return Results.Problem(
                "No se pudo registrar la aceptacion.");
        }
    }
)
.RequireAuthorization("SoloDestinatarios");

app.MapPost(
    "/api/destinatarios/consentimientos-whatsapp/{id:int}/rechazar",
    async (
        int id,
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
                mensaje = "Token CSRF invalido."
            });
        }

        string? personaTexto = contexto.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(personaTexto, out int personaId))
        {
            return Results.Unauthorized();
        }

        if (id <= 0)
        {
            return Results.BadRequest(new
            {
                mensaje = "Consentimiento invalido."
            });
        }

        string? cadena = app.Configuration
            .GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            return Results.Problem(
                "Conexion SQL no configurada.");
        }

        try
        {
            await using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            // Comprobar que el consentimiento
            // pertenece al destinatario.
            await using (var comprobar = new SqlCommand(
                @"SELECT Estado
                  FROM dbo.ConsentimientosWhatsApp
                  WHERE Id = @Id
                    AND PersonaId = @PersonaId",
                conexion))
            {
                comprobar.Parameters.Add(
                    "@Id", System.Data.SqlDbType.Int
                ).Value = id;

                comprobar.Parameters.Add(
                    "@PersonaId", System.Data.SqlDbType.Int
                ).Value = personaId;

                string? estado =
                    await comprobar.ExecuteScalarAsync() as string;

                if (estado is null)
                {
                    return Results.NotFound(new
                    {
                        mensaje = "Consentimiento no encontrado."
                    });
                }

                if (estado != "Pendiente")
                {
                    return Results.Conflict(new
                    {
                        mensaje = "Solo se puede rechazar un consentimiento pendiente."
                    });
                }
            }

            // Registrar el rechazo en SQL Server.
            await using var comando = new SqlCommand(
                "dbo.RechazarConsentimientoWhatsAppSynchro",
                conexion);

            comando.CommandType =
                System.Data.CommandType.StoredProcedure;


            comando.Parameters.Add(
                "@ConsentimientoId",
                System.Data.SqlDbType.Int
            ).Value = id;

            // Identificar al destinatario propietario.
            comando.Parameters.Add(
                "@PersonaId",
                System.Data.SqlDbType.Int
            ).Value = personaId;

            await comando.ExecuteNonQueryAsync();


            contexto.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new
            {
                mensaje = "Consentimiento rechazado correctamente."
            });
        }
        catch (SqlException ex) when (
            ex.Number >= 50000 && ex.Number < 51000)
        {
            return Results.Conflict(new
            {
                mensaje = "No se pudo rechazar el consentimiento en su estado actual."
            });
        }
        catch (SqlException)
        {
            return Results.Problem(
                "Error al registrar el rechazo.");
        }
    }
)
.RequireAuthorization("SoloDestinatarios");

app.MapPost(
    "/api/destinatarios/consentimientos-whatsapp/{id:int}/revocar",
    async (
        int id,
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
                mensaje = "Token CSRF invalido."
            });
        }

        if (id <= 0)
        {
            return Results.BadRequest(new
            {
                mensaje = "Consentimiento invalido."
            });
        }

        // Obtener la identidad del destinatario.
        string? personaTexto = contexto.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(personaTexto, out int personaId)
            || personaId <= 0)
        {
            return Results.Unauthorized();
        }

        string? cadena = app.Configuration
            .GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            return Results.Problem(
                "Conexion SQL no configurada.");
        }

        try
        {
            await using var conexion =
                new SqlConnection(cadena);

            await conexion.OpenAsync();

            await using var comando = new SqlCommand(
                "dbo.RevocarConsentimientoWhatsAppSynchro",
                conexion);

            comando.CommandType =
                System.Data.CommandType.StoredProcedure;

            // Identificador del consentimiento.
            comando.Parameters.Add(
                "@ConsentimientoId",
                System.Data.SqlDbType.Int
            ).Value = id;

            // Identificador del destinatario autenticado.
            comando.Parameters.Add(
                "@PersonaId",
                System.Data.SqlDbType.Int
            ).Value = personaId;

            await comando.ExecuteNonQueryAsync();

            contexto.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new
            {
                mensaje = "Consentimiento revocado correctamente."
            });
        }
        catch (SqlException ex) when (ex.Number == 50302)
        {
            return Results.NotFound(new
            {
                mensaje = "Consentimiento no encontrado."
            });
        }
        catch (SqlException ex) when (
            ex.Number == 50303 ||
            ex.Number == 50304 ||
            ex.Number == 50305)
        {
            return Results.Conflict(new
            {
                mensaje = "El consentimiento no puede revocarse en su estado actual."
            });
        }
        catch (SqlException)
        {
            return Results.Problem(
                "No se pudo revocar el consentimiento.");
        }
    }
)
.RequireAuthorization("SoloDestinatarios");

app.MapPost(
    "/api/destinatarios/reservas/{usoId:int}/solicitar-consentimiento",
    async (
        int usoId,
        HttpContext contexto,
        IAntiforgery antiforgery) =>
    {
        // Funcion habilitada solo para el prototipo.
        if (!app.Environment.IsDevelopment())
            return Results.StatusCode(503);

        if (!contexto.Request.IsHttps)
            return Results.BadRequest(new
            {
                mensaje = "Se requiere HTTPS."
            });

        if (!await antiforgery.IsRequestValidAsync(contexto))
            return Results.BadRequest(new
            {
                mensaje = "Token CSRF invalido."
            });

        string? personaTexto = contexto.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(personaTexto, out int personaId)
            || personaId <= 0)
            return Results.Unauthorized();

        if (usoId <= 0)
            return Results.BadRequest(new
            {
                mensaje = "Reserva invalida."
            });

        string? cadena = app.Configuration
            .GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
            return Results.Problem(
                "Conexion SQL no configurada.");

        try
        {
            await using var conexion =
                new SqlConnection(cadena);

            await conexion.OpenAsync();

            // Comprobar que la reserva pertenece al destinatario.
            await using var buscar = new SqlCommand(
                @"SELECT pe.TelefonoWhatsapp
                  FROM dbo.ParticipantesUso p
                  INNER JOIN dbo.Personas pe
                      ON pe.Id = p.PersonaId
                  WHERE p.UsoId = @UsoId
                    AND p.PersonaId = @PersonaId
                    AND p.Rol = N'Destinatario'",
                conexion);

            buscar.Parameters.Add(
                "@UsoId", System.Data.SqlDbType.Int
            ).Value = usoId;

            buscar.Parameters.Add(
                "@PersonaId", System.Data.SqlDbType.Int
            ).Value = personaId;

            string? telefono =
                await buscar.ExecuteScalarAsync() as string;

            if (string.IsNullOrWhiteSpace(telefono))
                return Results.NotFound(new
                {
                    mensaje = "Reserva no encontrada para este destinatario."
                });


            // Buscar un consentimiento existente
            // del mismo destinatario y telefono.
            int? consentimientoExistenteId = null;
            string? estadoExistente = null;

            await using (var buscarExistente = new SqlCommand(
                @"SELECT TOP (1) Id, Estado
                  FROM dbo.ConsentimientosWhatsApp
                  WHERE PersonaId = @PersonaId
                    AND Telefono = @Telefono
                    AND Finalidad = N'NotificacionesEncomienda'
                    AND VersionAviso = N'v1-demo'
                    AND Estado IN (N'Pendiente', N'Aceptado')
                    AND FechaRevocacion IS NULL
                  ORDER BY FechaRegistro DESC, Id DESC",
                conexion))
            {
                buscarExistente.Parameters.Add(
                    "@PersonaId",
                    System.Data.SqlDbType.Int
                ).Value = personaId;

                buscarExistente.Parameters.Add(
                    "@Telefono",
                    System.Data.SqlDbType.NVarChar, 20
                ).Value = telefono;

                await using var lector =
                    await buscarExistente.ExecuteReaderAsync();

                if (await lector.ReadAsync())
                {
                    consentimientoExistenteId = lector.GetInt32(0);
                    estadoExistente = lector.GetString(1);
                }
            }

            // Si ya existe, asociarlo a la reserva.
            if (consentimientoExistenteId.HasValue)
            {
                await using var asociarExistente = new SqlCommand(
                    "dbo.AsociarConsentimientoEncomiendaSynchro",
                    conexion);

                asociarExistente.CommandType =
                    System.Data.CommandType.StoredProcedure;

                asociarExistente.Parameters.Add(
                    "@UsoId",
                    System.Data.SqlDbType.Int
                ).Value = usoId;

                asociarExistente.Parameters.Add(
                    "@ConsentimientoId",
                    System.Data.SqlDbType.Int
                ).Value = consentimientoExistenteId.Value;

                await asociarExistente.ExecuteNonQueryAsync();

                contexto.Response.Headers.CacheControl = "no-store";

                return Results.Ok(new
                {
                    id = consentimientoExistenteId.Value,
                    usoId,
                    estado = estadoExistente,
                    reutilizado = true,
                    mensaje = "Consentimiento existente asociado a la reserva."
                });
            }

            // Crear y asociar el consentimiento
            // dentro de una misma transaccion.
            await using var transaccion =
                (SqlTransaction)await conexion.BeginTransactionAsync();

            int consentimientoId;

            // PASO 1: Crear consentimiento pendiente.
            await using (var comando = new SqlCommand(
                "dbo.CrearConsentimientoPendienteSynchro",
                conexion,
                transaccion))
            {
                comando.CommandType =
                    System.Data.CommandType.StoredProcedure;

                comando.Parameters.Add(
                    "@UsoId",
                    System.Data.SqlDbType.Int
                ).Value = usoId;

                comando.Parameters.Add(
                    "@Telefono",
                    System.Data.SqlDbType.NVarChar, 20
                ).Value = telefono;

                comando.Parameters.Add(
                    "@Origen",
                    System.Data.SqlDbType.NVarChar, 20
                ).Value = "Web";

                comando.Parameters.Add(
                    "@VersionAviso",
                    System.Data.SqlDbType.NVarChar, 20
                ).Value = "v1-demo";

                await using (var lector =
                    await comando.ExecuteReaderAsync())
                {
                    if (!await lector.ReadAsync())
                    {
                        return Results.Problem(
                            "No se pudo crear el consentimiento.");
                    }

                    consentimientoId =
                        Convert.ToInt32(lector["Id"]);
                }
            }

            // PASO 2: Asociar consentimiento a la reserva.
            await using (var asociar = new SqlCommand(
                "dbo.AsociarConsentimientoEncomiendaSynchro",
                conexion,
                transaccion))
            {
                asociar.CommandType =
                    System.Data.CommandType.StoredProcedure;

                asociar.Parameters.Add(
                    "@UsoId",
                    System.Data.SqlDbType.Int
                ).Value = usoId;

                asociar.Parameters.Add(
                    "@ConsentimientoId",
                    System.Data.SqlDbType.Int
                ).Value = consentimientoId;

                await asociar.ExecuteNonQueryAsync();
            }

            // PASO 3: Confirmar ambas operaciones.
            await transaccion.CommitAsync();

            contexto.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new
            {
                id = consentimientoId,
                usoId,
                estado = "Pendiente",
                mensaje = "Consentimiento creado y asociado a la reserva."
            });

        }
        catch (SqlException ex) when (ex.Number == 50077)
        {
            return Results.Conflict(new
            {
                mensaje = "Ya existe un consentimiento pendiente o aceptado."
            });
        }
        catch (SqlException ex) when (
            ex.Number >= 50070 && ex.Number <= 50078)
        {
            return Results.Conflict(new
            {
                mensaje = "La reserva no cumple los requisitos."
            });
        }

        catch (SqlException ex) when (
            ex.Number >= 50080 && ex.Number <= 50088)
        {
            return Results.Conflict(new
            {
                mensaje = "No se pudo asociar el consentimiento a la reserva."
            });
        }

        catch (SqlException)
        {
            return Results.Problem(
                "Error al solicitar el consentimiento.");
        }
    }
)
.RequireAuthorization("SoloDestinatarios");

app.MapGet(
    "/api/destinatarios/mis-encomiendas",
    async (HttpContext contexto) =>
    {
        if (!contexto.Request.IsHttps)
        {
            return Results.BadRequest(new
            {
                mensaje = "Se requiere HTTPS."
            });
        }

        // Obtener el destinatario desde su sesion.
        string? personaTexto = contexto.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(personaTexto, out int personaId)
            || personaId <= 0)
        {
            return Results.Unauthorized();
        }

        string? cadena = app.Configuration
            .GetConnectionString("SynchroDB");

        if (string.IsNullOrWhiteSpace(cadena))
        {
            return Results.Problem(
                "Conexion SQL no configurada.");
        }

        try
        {
            await using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            await using var comando = new SqlCommand(
                @"SELECT TOP (20)
                    u.Id,
                    u.CodigoCasillero,
                    u.TipoServicio,
                    u.Estado,
                    COALESCE(
                        cw.Estado,
                        N'Sin asociar'
                    ) AS EstadoConsentimiento
                  FROM dbo.UsosCasillero u
                  INNER JOIN dbo.ParticipantesUso p
                      ON p.UsoId = u.Id
                     AND p.Rol = N'Destinatario'
                  LEFT JOIN dbo.ConsentimientosUso cu
                      ON cu.UsoId = u.Id
                  LEFT JOIN dbo.ConsentimientosWhatsApp cw
                      ON cw.Id = cu.ConsentimientoId
                  WHERE p.PersonaId = @PersonaId
                    AND u.TipoServicio = N'Encomienda'
                  ORDER BY u.Id DESC",
                conexion);

            comando.Parameters.Add(
                "@PersonaId",
                System.Data.SqlDbType.Int
            ).Value = personaId;

            await using var lector =
                await comando.ExecuteReaderAsync();

            var encomiendas = new List<object>();

            while (await lector.ReadAsync())
            {
                encomiendas.Add(new
                {
                    usoId = lector.GetInt32(0),
                    codigoCasillero = lector.GetString(1),
                    tipoServicio = lector.GetString(2),
                    estado = lector.GetString(3),
                    estadoConsentimiento = lector.GetString(4)
                });
            }

            contexto.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new
            {
                encomiendas
            });
        }
        catch (SqlException)
        {
            return Results.Problem(
                "No se pudieron consultar las encomiendas.");
        }
    }
)
.RequireAuthorization("SoloDestinatarios");

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

public record ReservarCasilleroSolicitud(
    string? CodigoCasillero,
    string? TipoServicio
);

record AsignarParticipantesPeticion(
    int RemitenteId,
    int DestinatarioId
);

record SolicitarVerificacionTelefonoPeticion(
    int PersonaId,
    string? Telefono
);

record ValidarTelefonoPeticion(
    int PersonaId,
    string Codigo
);

record InicioSesionDestinatarioPeticion(
    int VerificacionId,
    string? Codigo
);

record AceptarConsentimientoWhatsAppPeticion(
    int VerificacionTelefonoId,
    bool ConfirmacionExpresa
);
