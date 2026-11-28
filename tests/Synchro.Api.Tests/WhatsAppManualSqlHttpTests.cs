using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Synchro.Api.Database;
using Synchro.Api.Services;

namespace Synchro.Api.Tests;

[Collection("SQL Deposito Aislado")]
public sealed class WhatsAppManualSqlHttpTests
{
#if SYNCHRO_CONSULTAS_PLATAFORMA
    [SqlDepositoFact]
    public async Task ProyeccionSqlFiltraPropietarioYNoDevuelveSecretos()
    {
        await using var db=new SqlConnection(FabricaTerminalHttpPruebas.CadenaSqlAislada);await db.OpenAsync();await PruebaHistoricaAislada.VerificarAsync(db);
        string codigo="Q"+Guid.NewGuid().ToString("N")[..8];
        try {
            await EscalarAsync(db,"""
                INSERT dbo.CompartimentosServiciosTemporales(Codigo,ConfiguracionJson)VALUES(@codigo,N'{}');
                INSERT dbo.OperacionesServiciosTemporales(Id,Codigo,Estado,Monto,Pagado,PagoAnulado,CreadaUtc,DatosJson)
                VALUES(NEWID(),@codigo,N'Finalizada',15,0,0,SYSUTCDATETIME(),N'{"ClientePersonaId":987654,"Clase":"Frio","Minutos":30,"TokenHash":"SECRETO_DE_PRUEBA"}'),
                      (NEWID(),@codigo,N'Finalizada',15,0,0,SYSUTCDATETIME(),N'{"ClientePersonaId":987655,"Clase":"Frio","Minutos":30,"TokenHash":"SECRETO_AJENO"}');
                """,codigo);
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:SynchroDB"]=FabricaTerminalHttpPruebas.CadenaSqlAislada}).Build();
            var filas=await new ConsultasPlataforma(config).ConsultarAsync("reservas",987654,default);
            var propias=filas.Where(x=>Equals(x["codigo"],codigo)).ToArray();Assert.Single(propias);
            string json=JsonSerializer.Serialize(propias);Assert.DoesNotContain("SECRETO",json);Assert.DoesNotContain("DatosJson",json);Assert.DoesNotContain("TokenHash",json);
            await Assert.ThrowsAsync<ArgumentException>(()=>new ConsultasPlataforma(config).ConsultarAsync("clientes",987654,default));
        } finally {await EscalarAsync(db,"DELETE dbo.OperacionesServiciosTemporales WHERE Codigo=@codigo;DELETE dbo.CompartimentosServiciosTemporales WHERE Codigo=@codigo",codigo);}
    }
#else
    [Xunit.Fact(Skip = "Pendiente: implementar ConsultasPlataforma y comprobar la proyeccion de reservas.")]
    public void ProyeccionSqlFiltraPropietarioYNoDevuelveSecretos()
    {
    }
#endif
    private static async Task PrepararAsync(SqlConnection db)
    {
        await PruebaHistoricaAislada.VerificarAsync(db);
        foreach(string nombre in new[]{"WhatsAppManual.sql","WhatsAppManualCancelacion.sql"})
        foreach(string lote in InstalacionPrototipo.Lotes(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"sql","aislado",nombre))))
        {await using var cmd=new SqlCommand(lote,db);await cmd.ExecuteNonQueryAsync();}
    }
    private static WebApplicationFactory<Program> Fabrica()=>new FabricaTerminalHttpPruebas().WithWebHostBuilder(b=> {
        b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?> {
            ["WhatsAppManual:Habilitado"]="true",["WhatsAppManual:NumeroE164"]="50400000000",["RetiroPersonal:ClaveHmac"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["WhatsApp:Outbox:Habilitado"]="false",["WhatsApp:VerificacionOutbox:Habilitado"]="false",["WhatsApp:SolicitudesInicioWorker:Habilitado"]="false"
        }));
        b.ConfigureTestServices(s=>s.AddAuthentication(o=>{o.DefaultScheme="ManualAdminTest";o.DefaultAuthenticateScheme="ManualAdminTest";o.DefaultChallengeScheme="ManualAdminTest";})
            .AddScheme<AuthenticationSchemeOptions,AdministradorTemporalPruebas>("ManualAdminTest",_=>{}));
    });
    private static async Task<Guid> IniciarAsync(HttpClient c,string codigo)
    {
        await PreReservasWebTabletTests.CsrfAsync(c,"/api/terminales/guardar/whatsapp/csrf-token");
        using var req=new HttpRequestMessage(HttpMethod.Post,"/api/terminales/guardar/whatsapp/iniciar");
        req.Headers.Add("X-SYNCHRO-CASILLERO",codigo);req.Headers.Add("X-SYNCHRO-CONSENTIMIENTO","ACEPTO");
        req.Content=new ByteArrayContent(RostroSimuladoTests.Png());req.Content.Headers.ContentType=new("application/octet-stream");
        using var r=await c.SendAsync(req);r.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        var tokenConsulta = json.RootElement.GetProperty("tokenConsulta").GetString();

        if (string.IsNullOrWhiteSpace(tokenConsulta))
            throw new InvalidOperationException("El servidor no devolvio el token del intento.");

        c.DefaultRequestHeaders.Remove("X-SYNCHRO-INTENTO");
        c.DefaultRequestHeaders.Add("X-SYNCHRO-INTENTO", tokenConsulta);

        return json.RootElement.GetProperty("intentoId").GetGuid();
    }
    private static async Task<object?> EscalarAsync(SqlConnection db,string sql,string codigo)
    {await using var q=new SqlCommand(sql,db);q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=codigo;return await q.ExecuteScalarAsync();}
    [SqlDepositoFact]
    public async Task CobroObligatorio_BloqueaRetiroDirecto()
    {
        using var fabrica = Fabrica().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["CobroRetiroPersonal:Obligatorio"] = "true"
                    })));

        using var terminal = fabrica.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost")
        });

        await PreReservasWebTabletTests.CsrfAsync(
            terminal,
            "/api/terminales/guardar/whatsapp/csrf-token");

        // Sin pago, la ruta antigua no puede confirmar un retiro.
        using var antiguo = await terminal.PostAsJsonAsync(
            "/api/terminales/guardar/whatsapp/retiro/pin",
            new
            {
                pin = "123456",
                tokenFacial = "token-no-valido"
            });

        Assert.Equal(HttpStatusCode.Conflict, antiguo.StatusCode);

        using var respuesta = JsonDocument.Parse(
            await antiguo.Content.ReadAsStringAsync());

        Assert.True(respuesta.RootElement
            .GetProperty("pagoObligatorio").GetBoolean());

        Assert.False(respuesta.RootElement
            .GetProperty("retiroConfirmado").GetBoolean());

        // La prevalidacion sigue disponible, pero rechaza
        // una autorizacion facial incorrecta.
        using var prevalidar = await terminal.PostAsJsonAsync(
            "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
            new
            {
                pin = "123456",
                tokenFacial = "token-no-valido"
            });

        Assert.Equal(HttpStatusCode.BadRequest,
            prevalidar.StatusCode);
    }
    [SqlDepositoFact]
    public async Task RetiroPersonalManual_BloqueaEnQuintoIntento()
    {
        await using var db = new SqlConnection(
            FabricaTerminalHttpPruebas.CadenaSqlAislada);

        await db.OpenAsync();
        await PrepararAsync(db);

        string codigo = "R" + Guid.NewGuid().ToString("N")[..8];

        await EscalarAsync(db, """
            INSERT dbo.Casilleros(Codigo,Tamano,Tipo,Estado)
            VALUES(@codigo,N'Mediano',N'Normal',N'Disponible')
            """, codigo);

        try
        {
            using var fabrica = Fabrica();

            using var terminal = fabrica.CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost")
            });

            // 1. Iniciar reserva con un PNG de prueba valido.
            Guid intento = await IniciarAsync(terminal, codigo);

            string autorizacion = (string)(await EscalarAsync(db, """
                SELECT CodigoAutorizacion
                FROM dbo.AutorizacionesWhatsAppManuales
                WHERE CodigoCasillero=@codigo
                """, codigo))!;

            // 2. Confirmacion manual por un operador de pruebas.
            using var admin = fabrica.CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost")
            });

            admin.DefaultRequestHeaders.Add("X-Test-Admin", "si");

            await PreReservasWebTabletTests.CsrfAsync(
                admin, "/api/admin/csrf-token");

            var solicitud = new
            {
                intentoId = intento,
                codigoAutorizacion = autorizacion,
                telefonoCliente = "50400000001",
                mensajeComprobado = true,
                textoMensaje = $"SYNCHRO ACEPTO {autorizacion} RECIBIR PIN DE RETIRO POR WHATSAPP",
                mensajeReferencia = "WA-PRUEBA-"+Guid.NewGuid().ToString("N")
            };

            using var confirmada = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/confirmar", solicitud);

            Assert.True(confirmada.IsSuccessStatusCode,
                "Confirmacion: " +
                await confirmada.Content.ReadAsStringAsync());

            using var datos = JsonDocument.Parse(
                await confirmada.Content.ReadAsStringAsync());

            string pinCorrecto = datos.RootElement
                .GetProperty("pinRetiro").GetString()!;

            Assert.Matches("^[0-9]{6}$", pinCorrecto);

            // 3. Declarar el PIN enviado en el flujo manual.
            using var enviado = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/marcar-enviado",
                new
                {
                    intentoId = intento,
                    envioComprobado = true,
                    telefonoCliente = "50400000001",mensajeReferencia="WA-ENVIO-PRUEBA-"+Guid.NewGuid().ToString("N")
                });

            Assert.True(enviado.IsSuccessStatusCode,
                "Envio: " + await enviado.Content.ReadAsStringAsync());

            // 4. Confirmar deposito con el endpoint real.
            using var deposito = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/deposito/confirmar",
                new { intentoId = intento });

            Assert.True(deposito.IsSuccessStatusCode,
                "Deposito: " + await deposito.Content.ReadAsStringAsync());

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));

            Assert.Equal(1, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.RostrosRetiroPersonalManual r
                JOIN dbo.UsosCasillero u ON u.Id=r.UsoId
                WHERE u.CodigoCasillero=@codigo
                """, codigo)));

            // 5. Obtener autorizacion facial de demostracion.
            terminal.DefaultRequestHeaders.Add(
                "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

            using var imagen = new ByteArrayContent(
                RostroSimuladoTests.Png());

            imagen.Headers.ContentType =
                new("application/octet-stream");

            using var facial = await terminal.PostAsync(
                "/api/terminales/guardar/whatsapp/retiro/rostro",
                imagen);

            Assert.True(facial.IsSuccessStatusCode,
                "Rostro: " + await facial.Content.ReadAsStringAsync());

            using var resultadoFacial = JsonDocument.Parse(
                await facial.Content.ReadAsStringAsync());

            Assert.True(resultadoFacial.RootElement
                .GetProperty("verificacionSimulada").GetBoolean());

            string token = resultadoFacial.RootElement
                .GetProperty("tokenFacial").GetString()!;

            // Preparar un escenario con cuatro intentos fallidos.
            // Solo afecta a la operacion ficticia de esta prueba.
            await EscalarAsync(db, """
                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 50001, 'Base incorrecta', 1;

                UPDATE p
                SET IntentosFallidos = 4
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id = p.UsoId
                WHERE u.CodigoCasillero = @codigo
                  AND u.Estado = N'Ocupado'
                  AND p.FechaUso IS NULL;
                """, codigo);

            string consultarIntentos = """
                SELECT p.IntentosFallidos
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id = p.UsoId
                WHERE u.CodigoCasillero = @codigo
                """;

            Assert.Equal(4, Convert.ToInt32(
                await EscalarAsync(db, consultarIntentos, codigo)));

            string pinIncorrecto = pinCorrecto == "000000"
                ? "111111" : "000000";

            // El quinto PIN incorrecto debe aumentar el contador.
            using var incorrecto = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
                new { pin = pinIncorrecto, tokenFacial = token });

            Assert.Equal(HttpStatusCode.BadRequest,
                incorrecto.StatusCode);

            Assert.Equal(5, Convert.ToInt32(
                await EscalarAsync(db, consultarIntentos, codigo)));

            // Aunque el siguiente PIN sea correcto,
            // la operacion debe permanecer bloqueada.
            using var bloqueado = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
                new { pin = pinCorrecto, tokenFacial = token });

            Assert.NotEqual(HttpStatusCode.OK, bloqueado.StatusCode);

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.UsosCasillero WHERE CodigoCasillero=@codigo",
                codigo));

            // El PIN no debe quedar marcado como utilizado.
            Assert.Equal(0, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id = p.UsoId
                WHERE u.CodigoCasillero = @codigo
                  AND p.FechaUso IS NOT NULL
                """, codigo)));
        }
        finally
        {
            // Limpiar solamente el casillero ficticio de este caso.
            await EscalarAsync(db, """
                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 50001, 'Base incorrecta', 1;

                DELETE r
                FROM dbo.RostrosRetiroPersonalManual r
                JOIN dbo.UsosCasillero u ON u.Id=r.UsoId
                WHERE u.CodigoCasillero=@codigo;

                DELETE p
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId
                WHERE u.CodigoCasillero=@codigo;

                DELETE dbo.AutorizacionesWhatsAppManuales
                WHERE CodigoCasillero=@codigo;

                DELETE dbo.UsosCasillero
                WHERE CodigoCasillero=@codigo;

                DELETE dbo.Casilleros
                WHERE Codigo=@codigo;
                """, codigo);
        }
    }
    [SqlDepositoFact]
    public async Task RetiroPersonalManual_CompruebaRostroPinYCasillero()
    {
        await using var db = new SqlConnection(
            FabricaTerminalHttpPruebas.CadenaSqlAislada);

        await db.OpenAsync();
        await PrepararAsync(db);

        string codigo = "R" + Guid.NewGuid().ToString("N")[..8];

        await EscalarAsync(db, """
            INSERT dbo.Casilleros(Codigo,Tamano,Tipo,Estado)
            VALUES(@codigo,N'Mediano',N'Normal',N'Disponible')
            """, codigo);

        try
        {
            using var fabrica = Fabrica();

            using var terminal = fabrica.CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost")
            });

            // 1. Iniciar reserva con un PNG de prueba valido.
            Guid intento = await IniciarAsync(terminal, codigo);

            string autorizacion = (string)(await EscalarAsync(db, """
                SELECT CodigoAutorizacion
                FROM dbo.AutorizacionesWhatsAppManuales
                WHERE CodigoCasillero=@codigo
                """, codigo))!;

            // 2. Confirmacion manual por un operador de pruebas.
            using var admin = fabrica.CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost")
            });

            admin.DefaultRequestHeaders.Add("X-Test-Admin", "si");

            await PreReservasWebTabletTests.CsrfAsync(
                admin, "/api/admin/csrf-token");

            var solicitud = new
            {
                intentoId = intento,
                codigoAutorizacion = autorizacion,
                telefonoCliente = "50400000001",
                mensajeComprobado = true,
                textoMensaje = $"SYNCHRO ACEPTO {autorizacion} RECIBIR PIN DE RETIRO POR WHATSAPP",
                mensajeReferencia = "WA-PRUEBA-"+Guid.NewGuid().ToString("N")
            };

            using var confirmada = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/confirmar", solicitud);

            Assert.True(confirmada.IsSuccessStatusCode,
                "Confirmacion: " +
                await confirmada.Content.ReadAsStringAsync());

            using var datos = JsonDocument.Parse(
                await confirmada.Content.ReadAsStringAsync());

            string pinCorrecto = datos.RootElement
                .GetProperty("pinRetiro").GetString()!;

            Assert.Matches("^[0-9]{6}$", pinCorrecto);

            // 3. Declarar el PIN enviado en el flujo manual.
            using var enviado = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/marcar-enviado",
                new
                {
                    intentoId = intento,
                    envioComprobado = true,
                    telefonoCliente = "50400000001",mensajeReferencia="WA-ENVIO-PRUEBA-"+Guid.NewGuid().ToString("N")
                });

            Assert.True(enviado.IsSuccessStatusCode,
                "Envio: " + await enviado.Content.ReadAsStringAsync());

            // 4. Confirmar deposito con el endpoint real.
            using var deposito = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/deposito/confirmar",
                new { intentoId = intento });

            Assert.True(deposito.IsSuccessStatusCode,
                "Deposito: " + await deposito.Content.ReadAsStringAsync());

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));

            Assert.Equal(1, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.RostrosRetiroPersonalManual r
                JOIN dbo.UsosCasillero u ON u.Id=r.UsoId
                WHERE u.CodigoCasillero=@codigo
                """, codigo)));

            // 5. Obtener autorizacion facial de demostracion.
            terminal.DefaultRequestHeaders.Add(
                "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

            using var imagen = new ByteArrayContent(
                RostroSimuladoTests.Png());

            imagen.Headers.ContentType =
                new("application/octet-stream");

            using var facial = await terminal.PostAsync(
                "/api/terminales/guardar/whatsapp/retiro/rostro",
                imagen);

            Assert.True(facial.IsSuccessStatusCode,
                "Rostro: " + await facial.Content.ReadAsStringAsync());

            using var resultadoFacial = JsonDocument.Parse(
                await facial.Content.ReadAsStringAsync());

            Assert.True(resultadoFacial.RootElement
                .GetProperty("verificacionSimulada").GetBoolean());

            string token = resultadoFacial.RootElement
                .GetProperty("tokenFacial").GetString()!;

            // 6. Un PIN incorrecto no debe liberar el casillero.
            string pinIncorrecto = pinCorrecto == "000000"
                ? "111111" : "000000";

            using var incorrecto = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
                new { pin = pinIncorrecto, tokenFacial = token });

            Assert.Equal(HttpStatusCode.BadRequest,
                incorrecto.StatusCode);

            Assert.Equal(1, Convert.ToInt32(await EscalarAsync(db, """
                SELECT p.IntentosFallidos
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId
                WHERE u.CodigoCasillero=@codigo
                """, codigo)));

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));

            // 7. PIN correcto: autorizar cobro, sin liberar el espacio.
            using var retiro = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
                new { pin = pinCorrecto, tokenFacial = token });

            Assert.True(retiro.IsSuccessStatusCode,
                "Retiro: " + await retiro.Content.ReadAsStringAsync());

            using var resultadoRetiro = JsonDocument.Parse(
                await retiro.Content.ReadAsStringAsync());

            Assert.False(resultadoRetiro.RootElement
                .GetProperty("retiroConfirmado").GetBoolean());
            Assert.True(resultadoRetiro.RootElement.GetProperty("pagoPendiente").GetBoolean());
            Assert.False(string.IsNullOrEmpty(resultadoRetiro.RootElement.GetProperty("autorizacionPago").GetString()));

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.UsosCasillero WHERE CodigoCasillero=@codigo",
                codigo));

            Assert.Equal(0, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId
                WHERE u.CodigoCasillero=@codigo
                  AND p.FechaUso IS NOT NULL
                """, codigo)));

            // 8. El mismo retiro no puede confirmarse otra vez.
            using var repetido = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
                new { pin = pinCorrecto, tokenFacial = token });

            Assert.NotEqual(HttpStatusCode.OK, repetido.StatusCode);
        }
        finally
        {
            // Limpiar solamente el casillero ficticio de este caso.
            await EscalarAsync(db, """
                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 50001, 'Base incorrecta', 1;

                DELETE r
                FROM dbo.RostrosRetiroPersonalManual r
                JOIN dbo.UsosCasillero u ON u.Id=r.UsoId
                WHERE u.CodigoCasillero=@codigo;

                DELETE p
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId
                WHERE u.CodigoCasillero=@codigo;

                DELETE dbo.AutorizacionesWhatsAppManuales
                WHERE CodigoCasillero=@codigo;

                DELETE dbo.UsosCasillero
                WHERE CodigoCasillero=@codigo;

                DELETE dbo.Casilleros
                WHERE Codigo=@codigo;
                """, codigo);
        }
    }
    [SqlDepositoFact]
    public async Task RetiroPersonalManual_PrevalidarNoLiberaCasillero()
    {
        await using var db = new SqlConnection(
            FabricaTerminalHttpPruebas.CadenaSqlAislada);

        await db.OpenAsync();
        await PrepararAsync(db);

        string codigo = "P" + Guid.NewGuid().ToString("N")[..8];

        await EscalarAsync(db, """
            INSERT dbo.Casilleros(Codigo,Tamano,Tipo,Estado)
            VALUES(@codigo,N'Mediano',N'Normal',N'Disponible')
            """, codigo);

        try
        {
            using var fabrica = Fabrica().WithWebHostBuilder(b =>
                b.ConfigureAppConfiguration((_, configuracion) =>
                    configuracion.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["CobroRetiroPersonal:MinutosPorBloque"] = "30",
                            ["CobroRetiroPersonal:Tarifas:Pequeno"] = "1.00",
                            ["CobroRetiroPersonal:Tarifas:Mediano"] = "1.00",
                            ["CobroRetiroPersonal:Tarifas:Grande"] = "1.00",
                            ["CobroRetiroPersonal:Adicionales:Pequeno"] = "1.00",
                            ["CobroRetiroPersonal:Adicionales:Mediano"] = "1.00",
                            ["CobroRetiroPersonal:Adicionales:Grande"] = "1.00",
                            ["CobroRetiroPersonal:Obligatorio"] = "true"
                        })));

            using var terminal = fabrica.CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost")
            });

            Guid intento = await IniciarAsync(terminal, codigo);

            string autorizacion = (string)(await EscalarAsync(db, """
                SELECT CodigoAutorizacion
                FROM dbo.AutorizacionesWhatsAppManuales
                WHERE CodigoCasillero=@codigo
                """, codigo))!;

            using var admin = fabrica.CreateClient(new()
            {
                BaseAddress = new Uri("https://localhost")
            });

            admin.DefaultRequestHeaders.Add("X-Test-Admin", "si");

            await PreReservasWebTabletTests.CsrfAsync(
                admin, "/api/admin/csrf-token");

            using var confirmada = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/confirmar",
                new
                {
                    intentoId = intento,
                    codigoAutorizacion = autorizacion,
                    telefonoCliente = "50400000001",
                    mensajeComprobado = true,
                    textoMensaje = $"SYNCHRO ACEPTO {autorizacion} RECIBIR PIN DE RETIRO POR WHATSAPP",
                    mensajeReferencia = "WA-PRUEBA-"+Guid.NewGuid().ToString("N")
                });

            Assert.True(confirmada.IsSuccessStatusCode,
                await confirmada.Content.ReadAsStringAsync());

            using var datos = JsonDocument.Parse(
                await confirmada.Content.ReadAsStringAsync());

            string pinCorrecto = datos.RootElement
                .GetProperty("pinRetiro").GetString()!;

            using var enviado = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/marcar-enviado",
                new
                {
                    intentoId = intento,
                    envioComprobado = true,
                    telefonoCliente = "50400000001",mensajeReferencia="WA-ENVIO-PRUEBA-"+Guid.NewGuid().ToString("N")
                });

            Assert.True(enviado.IsSuccessStatusCode);

            using var deposito = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/deposito/confirmar",
                new { intentoId = intento });

            Assert.True(deposito.IsSuccessStatusCode);

            terminal.DefaultRequestHeaders.Add(
                "X-SYNCHRO-CONSENTIMIENTO", "ACEPTO");

            using var imagen = new ByteArrayContent(
                RostroSimuladoTests.Png());

            imagen.Headers.ContentType =
                new("application/octet-stream");

            using var facial = await terminal.PostAsync(
                "/api/terminales/guardar/whatsapp/retiro/rostro",
                imagen);

            Assert.True(facial.IsSuccessStatusCode,
                await facial.Content.ReadAsStringAsync());

            using var resultadoFacial = JsonDocument.Parse(
                await facial.Content.ReadAsStringAsync());

            string token = resultadoFacial.RootElement
                .GetProperty("tokenFacial").GetString()!;

            string pinIncorrecto = pinCorrecto == "000000"
                ? "111111" : "000000";

            // PIN incorrecto: rechazo y un intento fallido.
            using var incorrecto = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
                new { pin = pinIncorrecto, tokenFacial = token });

            Assert.Equal(HttpStatusCode.BadRequest,
                incorrecto.StatusCode);

            Assert.Equal(1, Convert.ToInt32(await EscalarAsync(db, """
                SELECT p.IntentosFallidos
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId
                WHERE u.CodigoCasillero=@codigo
                """, codigo)));

            // PIN correcto: avanzar al pago sin liberar.
            using var correcto = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
                new { pin = pinCorrecto, tokenFacial = token });

            Assert.True(correcto.IsSuccessStatusCode,
                await correcto.Content.ReadAsStringAsync());

            using var resultado = JsonDocument.Parse(
                await correcto.Content.ReadAsStringAsync());

            var cuerpo = resultado.RootElement;

            Assert.True(cuerpo.GetProperty("pinValidado").GetBoolean());
            Assert.True(cuerpo.GetProperty("pagoPendiente").GetBoolean());
            Assert.False(cuerpo.GetProperty("pagoConfirmado").GetBoolean());
            Assert.False(cuerpo.GetProperty("retiroConfirmado").GetBoolean());

            Assert.Equal(codigo,
                cuerpo.GetProperty("codigoCasillero").GetString());

            Assert.True(
                cuerpo.GetProperty("minutosUtilizados").GetInt32() >= 1);

            Assert.False(string.IsNullOrWhiteSpace(
                cuerpo.GetProperty("autorizacionPago").GetString()));

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.UsosCasillero WHERE CodigoCasillero=@codigo",
                codigo));

            Assert.Equal(0, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.UsosCasillero
                WHERE CodigoCasillero=@codigo
                  AND FechaRetiro IS NOT NULL
                """, codigo)));

            Assert.Equal(0, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId
                WHERE u.CodigoCasillero=@codigo
                  AND p.FechaUso IS NOT NULL
                """, codigo)));

            Assert.Equal(0, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.CobrosRetiroPersonal c
                JOIN dbo.UsosCasillero u ON u.Id=c.UsoId
                WHERE u.CodigoCasillero=@codigo
                """, codigo)));

            // La reutilizacion del token se probara por separado.
            // SYNCHRO-PRUEBA-COBRO-HTTP-V1
            // La tarifa ficticia esta configurada solo en esta prueba.
            string autorizacionPago = cuerpo
                .GetProperty("autorizacionPago")
                .GetString()!;

            using var orden = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/cobro/preparar",
                new { autorizacionPago });

            Assert.True(
                orden.IsSuccessStatusCode,
                $"Preparacion del cobro: HTTP {(int)orden.StatusCode} ({orden.StatusCode}). " +
                await orden.Content.ReadAsStringAsync());

            using var datosOrden = JsonDocument.Parse(
                await orden.Content.ReadAsStringAsync());

            var ordenJson = datosOrden.RootElement;

            Guid cobroId = ordenJson
                .GetProperty("cobroId")
                .GetGuid();

            Assert.NotEqual(Guid.Empty, cobroId);

            Assert.Equal(
                codigo,
                ordenJson.GetProperty("codigoCasillero")
                    .GetString());

            Assert.Equal(
                1.00m,
                ordenJson.GetProperty("importeHnl")
                    .GetDecimal());

            Assert.Equal(
                "HNL",
                ordenJson.GetProperty("moneda")
                    .GetString());

            Assert.Equal(
                "Pendiente",
                ordenJson.GetProperty("estadoPago")
                    .GetString());

            Assert.False(
                ordenJson.GetProperty("pagoConfirmado")
                    .GetBoolean());

            Assert.False(
                ordenJson.GetProperty("retiroConfirmado")
                    .GetBoolean());

            Assert.Equal(
                1,
                Convert.ToInt32(await EscalarAsync(db, """
                    SELECT COUNT(*)
                    FROM dbo.CobrosRetiroPersonal c
                    JOIN dbo.UsosCasillero u ON u.Id=c.UsoId
                    WHERE u.CodigoCasillero=@codigo
                      AND c.Estado=N'Pendiente'
                    """, codigo)));

            Assert.Equal(
                cobroId,
                (Guid)(await EscalarAsync(db, """
                    SELECT c.CobroId
                    FROM dbo.CobrosRetiroPersonal c
                    JOIN dbo.UsosCasillero u ON u.Id=c.UsoId
                    WHERE u.CodigoCasillero=@codigo
                    """, codigo))!);

            Assert.Equal(
                "Ocupado",
                await EscalarAsync(db, """
                    SELECT Estado
                    FROM dbo.Casilleros
                    WHERE Codigo=@codigo
                    """, codigo));

            // Continuar con las pruebas SQL ya existentes:
            // pago pendiente, pago confirmado y doble retiro.
            const string finalizarSql = """
                DECLARE @UsoId INT = (
                    SELECT Id FROM dbo.UsosCasillero
                    WHERE CodigoCasillero=@codigo
                );

                DECLARE @CobroId UNIQUEIDENTIFIER = (
                    SELECT c.CobroId
                    FROM dbo.CobrosRetiroPersonal c
                    WHERE c.UsoId=@UsoId
                );

                EXEC dbo.FinalizarRetiroPersonalPagadoSynchro
                    @UsoId=@UsoId,
                    @CobroId=@CobroId;
                """;

            // 1. Un cobro pendiente NO permite retirar.
            var rechazoPendiente =
                await Assert.ThrowsAsync<SqlException>(
                    () => EscalarAsync(db, finalizarSql, codigo));

            Assert.Equal(57708, rechazoPendiente.Number);

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));

            Assert.Equal("Ocupado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.UsosCasillero WHERE CodigoCasillero=@codigo",
                codigo));

                        // SYNCHRO-ADMIN-LISTADO-COBROS-V1
            // Antes de elegir el metodo la orden no debe aparecer como cobro listo.
            using var antesMetodo = await admin.GetAsync("/api/admin/whatsapp-manual/cobros/retiro/pendientes");
            Assert.True(antesMetodo.IsSuccessStatusCode);
            using var listaAntes = JsonDocument.Parse(await antesMetodo.Content.ReadAsStringAsync());
            Assert.DoesNotContain(listaAntes.RootElement.GetProperty("cobros").EnumerateArray(), x => x.GetProperty("cobroId").GetGuid()==cobroId);
            using var elegirMetodo = await terminal.PostAsJsonAsync("/api/terminales/guardar/whatsapp/retiro/cobro/metodo", new { cobroId, autorizacionPago, metodoPago="Efectivo" });
            Assert.True(elegirMetodo.IsSuccessStatusCode, await elegirMetodo.Content.ReadAsStringAsync());
            // El operador autenticado debe ver la orden pendiente.
            using var listadoCobros = await admin.GetAsync(
                "/api/admin/whatsapp-manual/cobros/retiro/pendientes");

            Assert.True(
                listadoCobros.IsSuccessStatusCode,
                "Listado: HTTP " +
                (int)listadoCobros.StatusCode + ". " +
                await listadoCobros.Content.ReadAsStringAsync());

            using var datosCobros = JsonDocument.Parse(
                await listadoCobros.Content.ReadAsStringAsync());

            Assert.True(
                datosCobros.RootElement
                    .GetProperty("cobros")
                    .EnumerateArray()
                    .Any(x =>
                        x.GetProperty("cobroId").GetGuid() == cobroId && x.GetProperty("metodoPago").GetString()=="Efectivo"),
                "La orden pendiente debe estar visible al operador.");
// Pago sin comprobacion: rechazado.
            using var noComprobado = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/cobros/retiro/confirmar",
                new
                {
                    cobroId,
                    metodoPago = "Efectivo",
                    pagoComprobado = false
                });

            Assert.Equal(
                HttpStatusCode.BadRequest,
                noComprobado.StatusCode);

            using var otroMetodo = await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/cobros/retiro/confirmar", new { cobroId, metodoPago="NFC", pagoComprobado=true });
            Assert.Equal(HttpStatusCode.Conflict,otroMetodo.StatusCode);
            // Confirmacion por el operador autorizado.
            using var pago = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/cobros/retiro/confirmar",
                new
                {
                    cobroId,
                    metodoPago = "Efectivo",
                    pagoComprobado = true
                });

            Assert.True(
                pago.IsSuccessStatusCode,
                $"Confirmacion: HTTP {(int)pago.StatusCode}. " +
                await pago.Content.ReadAsStringAsync());

            using var respuestaPago = JsonDocument.Parse(
                await pago.Content.ReadAsStringAsync());

            Assert.True(respuestaPago.RootElement
                .GetProperty("pagoConfirmado").GetBoolean());

            Assert.True(respuestaPago.RootElement
                .GetProperty("pagoSimulado").GetBoolean());

            Assert.False(respuestaPago.RootElement
                .GetProperty("retiroConfirmado").GetBoolean());

            // SYNCHRO-AUDITORIA-COBRO-RETIRO-V1
            Assert.Equal(1, Convert.ToInt32(
                await EscalarAsync(db, """
                    SELECT COUNT(*)
                    FROM dbo.AuditoriaCobrosRetiroPersonal
                    WHERE CodigoCasillero=@codigo
                      AND OperadorId > 0
                      AND ImporteHnl=1.00
                      AND MetodoPago=N'EfectivoSimulado'
                      AND ConfirmadoUtc IS NOT NULL
                    """, codigo)));
            // Una segunda confirmacion no debe procesarse.
            using var pagoRepetido = await admin.PostAsJsonAsync(
                "/api/admin/whatsapp-manual/cobros/retiro/confirmar",
                new
                {
                    cobroId,
                    metodoPago = "Efectivo",
                    pagoComprobado = true
                });

            Assert.Equal(
                HttpStatusCode.Conflict,
                pagoRepetido.StatusCode);
            Assert.Equal(cobroId, (Guid)(await EscalarAsync(db, """
                SELECT c.CobroId
                FROM dbo.CobrosRetiroPersonal c
                JOIN dbo.UsosCasillero u ON u.Id=c.UsoId
                WHERE u.CodigoCasillero=@codigo
                """, codigo))!);

            // 3. Finalizacion HTTP usando la autorizacion
            // emitida despues del PIN y el cobro confirmado.
            using var retiroHttp = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/cobro/finalizar",
                new { cobroId, autorizacionPago });

            Assert.True(
                retiroHttp.IsSuccessStatusCode,
                $"Retiro HTTP: {(int)retiroHttp.StatusCode}. " +
                await retiroHttp.Content.ReadAsStringAsync());

            using var datosRetiro = JsonDocument.Parse(
                await retiroHttp.Content.ReadAsStringAsync());

            Assert.True(datosRetiro.RootElement
                .GetProperty("retiroConfirmado").GetBoolean());

            Assert.True(datosRetiro.RootElement
                .GetProperty("pagoAplicado").GetBoolean());

            Assert.Equal(
                codigo,
                datosRetiro.RootElement
                    .GetProperty("codigoCasillero").GetString());

            // La evidencia facial debe eliminarse al retirar.
            Assert.Equal(0, Convert.ToInt32(
                await EscalarAsync(db, """
                    SELECT COUNT(*)
                    FROM dbo.RostrosRetiroPersonalManual r
                    JOIN dbo.UsosCasillero u
                        ON u.Id = r.UsoId
                    WHERE u.CodigoCasillero = @codigo
                    """, codigo)));
            // El mismo pago no puede liberar el casillero otra vez.
            using var retiroDuplicado = await terminal.PostAsJsonAsync(
                "/api/terminales/guardar/whatsapp/retiro/cobro/finalizar",
                new { cobroId, autorizacionPago });

            Assert.Equal(
                HttpStatusCode.Conflict,
                retiroDuplicado.StatusCode);

            Assert.Equal("Disponible", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));

            Assert.Equal("Finalizado", await EscalarAsync(db,
                "SELECT Estado FROM dbo.UsosCasillero WHERE CodigoCasillero=@codigo",
                codigo));

            Assert.Equal(1, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId
                WHERE u.CodigoCasillero=@codigo
                  AND p.FechaUso IS NOT NULL
                """, codigo)));

            Assert.Equal(1, Convert.ToInt32(await EscalarAsync(db, """
                SELECT COUNT(*)
                FROM dbo.CobrosRetiroPersonal c
                JOIN dbo.UsosCasillero u ON u.Id=c.UsoId
                WHERE u.CodigoCasillero=@codigo
                  AND c.Estado=N'Aplicado'
                  AND c.FinalizadoUtc IS NOT NULL
                """, codigo)));

            // 4. No se puede finalizar dos veces.
            var segundoRetiro =
                await Assert.ThrowsAsync<SqlException>(
                    () => EscalarAsync(db, finalizarSql, codigo));

            Assert.Equal(57705, segundoRetiro.Number);

            Assert.Equal("Disponible", await EscalarAsync(db,
                "SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",
                codigo));
        }
        finally
        {
            // Eliminar exclusivamente el casillero temporal.
            await EscalarAsync(db, """
                IF DB_NAME() <> N'SynchroDB_Pruebas'
                    THROW 50001, 'Base incorrecta', 1;

                DELETE FROM dbo.AuditoriaCobrosRetiroPersonal
                WHERE CodigoCasillero=@codigo;

                DELETE c
                FROM dbo.CobrosRetiroPersonal c
                JOIN dbo.UsosCasillero u ON u.Id=c.UsoId
                WHERE u.CodigoCasillero=@codigo;

                DELETE r
                FROM dbo.RostrosRetiroPersonalManual r
                JOIN dbo.UsosCasillero u ON u.Id=r.UsoId
                WHERE u.CodigoCasillero=@codigo;

                DELETE p
                FROM dbo.CodigosRetiroPersonal p
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId
                WHERE u.CodigoCasillero=@codigo;

                DELETE dbo.AutorizacionesWhatsAppManuales
                WHERE CodigoCasillero=@codigo;

                DELETE dbo.UsosCasillero
                WHERE CodigoCasillero=@codigo;

                DELETE dbo.Casilleros
                WHERE Codigo=@codigo;
                """, codigo);
        }
    }
    [SqlDepositoTheory]
    [InlineData(false)][InlineData(true)]
    public async Task QrRetieneConfirmacionEmiteUnaSolaVezYCancelacionValidaDeposito(bool depositar)
    {
        await using var db=new SqlConnection(FabricaTerminalHttpPruebas.CadenaSqlAislada);await db.OpenAsync();await PrepararAsync(db);
        string codigo="W"+Guid.NewGuid().ToString("N")[..8];
        await EscalarAsync(db,"INSERT dbo.Casilleros(Codigo,Tamano,Tipo,Estado)VALUES(@codigo,N'Mediano',N'Normal',N'Disponible')",codigo);
        try {
            using var fabrica=Fabrica();using var terminal=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});
            var intento=await IniciarAsync(terminal,codigo);
            Assert.Equal("Reservado",await EscalarAsync(db,"SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",codigo));
            using(var segunda=new HttpRequestMessage(HttpMethod.Post,"/api/terminales/guardar/whatsapp/iniciar")) {
                segunda.Headers.Add("X-SYNCHRO-CASILLERO",codigo);segunda.Headers.Add("X-SYNCHRO-CONSENTIMIENTO","ACEPTO");
                segunda.Content=new ByteArrayContent(RostroSimuladoTests.Png());segunda.Content.Headers.ContentType=new("application/octet-stream");
                using var rechazo=await terminal.SendAsync(segunda);Assert.Equal(HttpStatusCode.Conflict,rechazo.StatusCode);
            }
            string autorizacion=(string)(await EscalarAsync(db,"SELECT CodigoAutorizacion FROM dbo.AutorizacionesWhatsAppManuales WHERE CodigoCasillero=@codigo",codigo))!;
            using var admin=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")});admin.DefaultRequestHeaders.Add("X-Test-Admin","si");
            await PreReservasWebTabletTests.CsrfAsync(admin,"/api/admin/csrf-token");
            var payload=new{intentoId=intento,codigoAutorizacion=autorizacion,telefonoCliente="50400000001",mensajeComprobado=true,textoMensaje=$"SYNCHRO ACEPTO {autorizacion} RECIBIR PIN DE RETIRO POR WHATSAPP",mensajeReferencia="WA-PRUEBA-"+Guid.NewGuid().ToString("N")};
            using(var noCsrf=fabrica.CreateClient(new(){BaseAddress=new Uri("https://localhost")})) {
                noCsrf.DefaultRequestHeaders.Add("X-Test-Admin","si");using var rechazo=await noCsrf.PostAsJsonAsync("/api/admin/whatsapp-manual/confirmar",payload);Assert.Equal(HttpStatusCode.Forbidden,rechazo.StatusCode);
            }
            using var confirmada=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/confirmar",payload);confirmada.EnsureSuccessStatusCode();
            using var body=JsonDocument.Parse(await confirmada.Content.ReadAsStringAsync());Assert.Matches("^[0-9]{6}$",body.RootElement.GetProperty("pinRetiro").GetString()!);
            using var repetida=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/confirmar",payload);Assert.Equal(HttpStatusCode.Conflict,repetida.StatusCode);
            Assert.Equal(1,Convert.ToInt32(await EscalarAsync(db,"SELECT COUNT(*) FROM dbo.CodigosRetiroPersonal p JOIN dbo.UsosCasillero u ON u.Id=p.UsoId WHERE u.CodigoCasillero=@codigo",codigo)));
            if(depositar) {
                await EscalarAsync(db,"UPDATE dbo.UsosCasillero SET Estado=N'Ocupado',FechaIngreso=SYSUTCDATETIME() WHERE CodigoCasillero=@codigo;UPDATE dbo.Casilleros SET Estado=N'Ocupado' WHERE Codigo=@codigo",codigo);
            }
            using var cancelada=await admin.PostAsJsonAsync("/api/admin/whatsapp-manual/cancelar",new{intentoId=intento});
            Assert.Equal(depositar?HttpStatusCode.Conflict:HttpStatusCode.OK,cancelada.StatusCode);
            Assert.Equal(depositar?"Ocupado":"Disponible",await EscalarAsync(db,"SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",codigo));
            if(!depositar)Assert.Equal(0,Convert.ToInt32(await EscalarAsync(db,"SELECT COUNT(*) FROM dbo.CodigosRetiroPersonal p JOIN dbo.UsosCasillero u ON u.Id=p.UsoId WHERE u.CodigoCasillero=@codigo AND p.FechaUso IS NULL",codigo)));
        } finally {
        // SYNCHRO-FIX-CLEANUP-ROSTRO-MANUAL
        // Eliminar primero el registro hijo del uso ficticio.
        await EscalarAsync(
            db,
            "IF DB_NAME() <> N'SynchroDB_Pruebas' THROW 50001, 'Base no autorizada', 1; DELETE FROM dbo.RostrosRetiroPersonalManual WHERE UsoId IN (SELECT Id FROM dbo.UsosCasillero WHERE CodigoCasillero = @codigo);",
            codigo);

            await EscalarAsync(db,"DELETE p FROM dbo.CodigosRetiroPersonal p JOIN dbo.UsosCasillero u ON u.Id=p.UsoId WHERE u.CodigoCasillero=@codigo;DELETE dbo.AutorizacionesWhatsAppManuales WHERE CodigoCasillero=@codigo;DELETE dbo.UsosCasillero WHERE CodigoCasillero=@codigo;DELETE dbo.Casilleros WHERE Codigo=@codigo",codigo);
        }
    }
}
