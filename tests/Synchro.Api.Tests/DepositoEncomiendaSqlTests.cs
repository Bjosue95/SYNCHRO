using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Synchro.Api.Services.WhatsApp;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;
using Synchro.Api.Services.Temporales;
using Synchro.Api.Services.Clientes;
using Microsoft.AspNetCore.Identity;

namespace Synchro.Api.Tests;

public sealed class SqlDepositoFactAttribute : FactAttribute
{
    public SqlDepositoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST")))
            Skip = "Requiere SQL Server aislado y SYNCHRO_SQL_DEPOSITO_TEST; no se sustituye por memoria.";
    }
}

public sealed class SqlDepositoTheoryAttribute : TheoryAttribute
{
    public SqlDepositoTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST")))
            Skip = "Requiere SYNCHRO_SQL_DEPOSITO_TEST y SQL Server aislado.";
    }
}

[Collection("SQL Deposito Aislado")]
public sealed class DepositoEncomiendaSqlTests
{
    private static string Cadena => ConexionSqlAislada.Exigir(
        Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST"), "SynchroDB_Pruebas");

    private static readonly IDataProtectionProvider Proteccion = new EphemeralDataProtectionProvider();
    private static readonly string ClavePrueba = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static IConfiguration Configuracion()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SynchroDB"] = Cadena,
            ["RetiroPin:ClaveHmac"] = ClavePrueba,
            ["ServiciosTemporales:ClaveHmac"] = ClavePrueba,
            ["WhatsApp:PlantillaPinRetiro"] = "pin_retiro"
        }).Build();
    private static DepositoEncomiendaService Servicio()
    {
        var config=Configuracion();
        return new(config, new PreparadorPinEncomienda(config, TimeProvider.System),
            new SqlOutboxWhatsApp(config, new ProtectorMensajeWhatsApp(Proteccion)));
    }

    internal static async Task PrepararAsync()
    {
        await using var c = new SqlConnection(Cadena);
        await c.OpenAsync();
        foreach (string archivo in new[] { "EsquemaDeposito.sql", "OrigenPreReserva.sql", "ClientesWeb.sql", "Consentimiento.sql", "Evidencias.sql", "Deposito.sql", "PinEncomienda.sql", "Outbox.sql", "RetiroEncomienda.sql", "ServiciosTemporales.sql", "UsuariosRoles.sql", "RostroSimulado.sql", "ReemisionPin.sql" })
        {
            string sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sql", "aislado", archivo));
            foreach (string lote in Regex.Split(sql, @"^\s*GO\s*\r?$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(lote)) continue;
                await using var q = new SqlCommand(lote, c);
                await q.ExecuteNonQueryAsync(); // Un error detiene todos los lotes siguientes.
            }
        }
    }

    private sealed record Datos(int Reserva, int Terminal, int Remitente, int Destinatario, Guid Evidencia, string Codigo);

    private static async Task<Datos> CrearAsync(SqlConnection c)
    {
        string codigo = "T" + Guid.NewGuid().ToString("N")[..8];
        var evidencia = Guid.NewGuid();
        await using var q = new SqlCommand("""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            INSERT dbo.Terminales(Codigo,Nombre,Activa) VALUES (@codigo,N'TEST_DEPOSITO',1);
            DECLARE @terminal INT = SCOPE_IDENTITY();
            INSERT dbo.Personas DEFAULT VALUES;
            DECLARE @remitente INT = SCOPE_IDENTITY();
            INSERT dbo.Personas DEFAULT VALUES;
            DECLARE @destinatario INT = SCOPE_IDENTITY();
            INSERT dbo.ClientesWebPrototipo(PersonaId,NombreUsuario,Telefono,PasswordHash)VALUES(@remitente,N'SQL-'+@codigo,N'+50499990001',@passwordHash);
            INSERT dbo.Casilleros VALUES (@codigo,N'Normal',N'Mediano',N'Disponible');
            INSERT dbo.PreReservasEncomiendasWeb(TokenGestionHash,TelefonoRemitente,TelefonoDestinatario,TamanoSolicitado,FechaExpiracion,ClientePersonaId)
            VALUES (CRYPT_GEN_RANDOM(32),N'+50499990001',N'+50499990002',N'Mediano',DATEADD(HOUR,1,SYSUTCDATETIME()),@remitente);
            DECLARE @reserva INT = SCOPE_IDENTITY();
            INSERT dbo.ParticipantesPreReservaVerificados VALUES (@reserva,@remitente,@destinatario,DATEADD(MINUTE,-1,SYSUTCDATETIME()),DATEADD(HOUR,1,SYSUTCDATETIME()));
            INSERT dbo.ConsentimientosPreReservaWhatsApp
                (ReservaId,TelefonoWhatsApp,CodigoHash,Estado,FechaCreacionUtc,FechaExpiracionUtc,FechaConfirmacionUtc,MensajeIdConfirmacion)
            VALUES (@reserva,N'+50499990002',CRYPT_GEN_RANDOM(32),N'Confirmado',DATEADD(MINUTE,-1,SYSUTCDATETIME()),DATEADD(HOUR,1,SYSUTCDATETIME()),SYSUTCDATETIME(),@codigo);
            INSERT dbo.EvidenciasFacialesRemitente
                (EvidenciaId,ReservaId,RemitenteId,TerminalId,FechaEmisionUtc,FechaExpiracionUtc,RostroCoincide,PruebaDeVidaSuperada)
            VALUES (@evidencia,@reserva,@remitente,@terminal,DATEADD(SECOND,-1,SYSUTCDATETIME()),DATEADD(SECOND,90,SYSUTCDATETIME()),1,1);
            COMMIT TRANSACTION;
            SELECT @reserva,@terminal,@remitente,@destinatario;
            """, c);
        q.Parameters.Add("@passwordHash",SqlDbType.NVarChar,500).Value=new PasswordHasher<CuentaClienteWeb>().HashPassword(new(){NombreUsuario="SQL-"+codigo},"Clave-cliente-SQL-solo-pruebas-123");
        q.Parameters.Add("@codigo", SqlDbType.NVarChar, 10).Value = codigo;
        q.Parameters.Add("@evidencia", SqlDbType.UniqueIdentifier).Value = evidencia;
        await using var r = await q.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        return new(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), evidencia, codigo);
    }

    private static async Task EjecutarAsync(SqlConnection c, Datos d, string sql)
    {
        await using var q = new SqlCommand(sql, c);
        q.Parameters.Add("@remitente",SqlDbType.Int).Value=d.Remitente;
        q.Parameters.Add("@destinatario",SqlDbType.Int).Value=d.Destinatario;
        q.Parameters.Add("@reserva", SqlDbType.Int).Value = d.Reserva;
        q.Parameters.Add("@codigo", SqlDbType.NVarChar, 10).Value = d.Codigo;
        q.Parameters.Add("@terminal", SqlDbType.Int).Value = d.Terminal;
        await q.ExecuteNonQueryAsync();
    }

    private static async Task LimpiarAsync(SqlConnection c, Datos d)
    {
        await EjecutarAsync(c, d, """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DELETE dbo.OperacionesServiciosTemporales WHERE Codigo=@codigo;
            DELETE dbo.CompartimentosServiciosTemporales WHERE Codigo=@codigo;
            DELETE dbo.AuditoriaServiciosTemporales WHERE Codigo=@codigo;
            DELETE dbo.IncidenciasServiciosTemporales WHERE Codigo=@codigo;
            DELETE dbo.EvidenciasFacialesRetiroEncomienda WHERE UsoId IN (SELECT UsoId FROM dbo.DepositosPreReservaEncomienda WHERE ReservaId=@reserva);
            DELETE dbo.AuditoriaEncomiendasPrototipo WHERE UsoId IN (SELECT UsoId FROM dbo.DepositosPreReservaEncomienda WHERE ReservaId=@reserva);
            DELETE dbo.OutboxWhatsApp WHERE ReservaId=@reserva;
            DELETE dbo.ReemisionesPinEncomienda WHERE UsoId IN(SELECT UsoId FROM dbo.DepositosPreReservaEncomienda WHERE ReservaId=@reserva);
            DELETE dbo.CodigosRetiroEncomiendaPrototipo WHERE UsoId IN (SELECT UsoId FROM dbo.DepositosPreReservaEncomienda WHERE ReservaId=@reserva);
            DELETE dbo.ParticipantesUso WHERE UsoId IN (SELECT UsoId FROM dbo.DepositosPreReservaEncomienda WHERE ReservaId=@reserva);
            DECLARE @usos TABLE (Id INT);
            DELETE dbo.DepositosPreReservaEncomienda OUTPUT DELETED.UsoId INTO @usos WHERE ReservaId=@reserva;
            DELETE dbo.UsosCasillero WHERE Id IN (SELECT Id FROM @usos);
            DELETE dbo.EvidenciasFacialesRemitente WHERE ReservaId=@reserva;
            DELETE dbo.ParticipantesPreReservaVerificados WHERE ReservaId=@reserva;
            DELETE dbo.ConsentimientosPreReservaWhatsApp WHERE ReservaId=@reserva;
            DELETE dbo.PreReservasEncomiendasWeb WHERE Id=@reserva;
            DELETE dbo.ReferenciasRostroSimulado WHERE PersonaId IN(@remitente,@destinatario);
            DELETE dbo.ClientesWebPrototipo WHERE PersonaId=@remitente;
            DELETE dbo.Casilleros WHERE Codigo=@codigo;
            DELETE dbo.Terminales WHERE Id=@terminal AND Nombre=N'TEST_DEPOSITO';
            COMMIT TRANSACTION;
            """);
        await using var personas = new SqlCommand("DELETE dbo.Personas WHERE Id IN (@remitente,@destinatario)", c);
        personas.Parameters.Add("@remitente", SqlDbType.Int).Value = d.Remitente;
        personas.Parameters.Add("@destinatario", SqlDbType.Int).Value = d.Destinatario;
        await personas.ExecuteNonQueryAsync();
    }

    private static DepositoEncomiendaComando Comando(Datos d) => new(d.Reserva, d.Terminal, d.Evidencia, d.Codigo);

    [SqlDepositoFact]
    [Trait("Category", "SqlAislado")]
    public async Task MigracionEsIdempotenteYDepositoConsumeUnaSolaVez()
    {
        await PrepararAsync();
        await PrepararAsync();
        await using var c = new SqlConnection(Cadena); await c.OpenAsync();
        Datos d = await CrearAsync(c);
        try
        {
            var resultado = await Servicio().DepositarAsync(Comando(d));
            Assert.True(resultado.UsoId > 0); Assert.Equal(d.Codigo, resultado.CodigoCasillero);
            var error = await Assert.ThrowsAsync<SqlException>(() => Servicio().DepositarAsync(Comando(d)));
            Assert.Contains(error.Number, new[] { 55605, 55606, 55610 });
            await using var q = new SqlCommand("""
                SELECT COUNT(*) FROM dbo.DepositosPreReservaEncomienda d
                JOIN dbo.EvidenciasFacialesRemitente e ON e.EvidenciaId=d.EvidenciaId
                JOIN dbo.UsosCasillero u ON u.Id=d.UsoId
                JOIN dbo.Casilleros c ON c.Codigo=u.CodigoCasillero
                WHERE d.ReservaId=@reserva AND e.Consumida=1 AND e.FechaConsumoUtc IS NOT NULL
                  AND u.Estado=N'Ocupado' AND c.Estado=N'Ocupado'
                  AND (SELECT COUNT(*) FROM dbo.ParticipantesUso p WHERE p.UsoId=u.Id)=2
                  AND (SELECT COUNT(*) FROM dbo.CodigosRetiroEncomiendaPrototipo p WHERE p.UsoId=u.Id AND p.Consumido=0)=1
                  AND (SELECT COUNT(*) FROM dbo.OutboxWhatsApp o WHERE o.ReservaId=d.ReservaId AND o.Estado=N'Pendiente' AND o.ContenidoProtegido IS NOT NULL)=1;
                """, c);
            q.Parameters.Add("@reserva", SqlDbType.Int).Value = d.Reserva;
            Assert.Equal(1, Convert.ToInt32(await q.ExecuteScalarAsync()));
        }
        finally { await LimpiarAsync(c, d); }
    }

    [SqlDepositoFact]
    [Trait("Category", "SqlAislado")]
    public async Task DosSolicitudesConcurrentesSoloDepositanUnaVez()
    {
        await PrepararAsync();
        await using var c = new SqlConnection(Cadena); await c.OpenAsync();
        Datos d = await CrearAsync(c);
        try
        {
            async Task<bool> Intentar()
            {
                try { await Servicio().DepositarAsync(Comando(d)); return true; }
                catch (SqlException e) when (e.Number is 55605 or 55606 or 55610) { return false; }
            }
            var resultados = await Task.WhenAll(Intentar(), Intentar());
            Assert.Single(resultados, x => x);
        }
        finally { await LimpiarAsync(c, d); }
    }

    [SqlDepositoTheory]
    [Trait("Category", "SqlAislado")]
    [InlineData("TerminalRevocada", 55604)]
    [InlineData("ReservaVencida", 55606)]
    [InlineData("TamanoDistinto", 55607)]
    [InlineData("ParticipantesNoVerificados", 55608)]
    [InlineData("ConsentimientoRevocado", 55609)]
    [InlineData("TelefonoAjeno", 55609)]
    [InlineData("ConfirmacionFutura", 55609)]
    [InlineData("EvidenciaVencida", 55610)]
    [InlineData("EvidenciaAjena", 55610)]
    [InlineData("SinPruebaDeVida", 55610)]
    public async Task RechazaCondicionesInvalidasSinConsumir(string escenario, int esperado)
    {
        await PrepararAsync();
        await using var c = new SqlConnection(Cadena); await c.OpenAsync();
        Datos d = await CrearAsync(c);
        try
        {
            string sql = escenario switch
            {
                "TerminalRevocada" => "UPDATE dbo.Terminales SET Activa=0 WHERE Id=@terminal;",
                "ReservaVencida" => "UPDATE dbo.PreReservasEncomiendasWeb SET FechaExpiracion=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE Id=@reserva;",
                "TamanoDistinto" => "UPDATE dbo.Casilleros SET Tamano=N'Grande' WHERE Codigo=@codigo;",
                "ParticipantesNoVerificados" => "DELETE dbo.ParticipantesPreReservaVerificados WHERE ReservaId=@reserva;",
                "ConsentimientoRevocado" => "UPDATE dbo.ConsentimientosPreReservaWhatsApp SET Estado=N'Revocado' WHERE ReservaId=@reserva;",
                "TelefonoAjeno" => "UPDATE dbo.ConsentimientosPreReservaWhatsApp SET TelefonoWhatsApp=N'+50499990003' WHERE ReservaId=@reserva;",
                "ConfirmacionFutura" => "UPDATE dbo.ConsentimientosPreReservaWhatsApp SET FechaConfirmacionUtc=DATEADD(MINUTE,1,SYSUTCDATETIME()) WHERE ReservaId=@reserva;",
                "EvidenciaVencida" => "UPDATE dbo.EvidenciasFacialesRemitente SET FechaEmisionUtc=DATEADD(SECOND,-100,SYSUTCDATETIME()),FechaExpiracionUtc=DATEADD(SECOND,-10,SYSUTCDATETIME()) WHERE ReservaId=@reserva;",
                "EvidenciaAjena" => "UPDATE dbo.EvidenciasFacialesRemitente SET RemitenteId=2147483647 WHERE ReservaId=@reserva;",
                "SinPruebaDeVida" => "UPDATE dbo.EvidenciasFacialesRemitente SET PruebaDeVidaSuperada=0 WHERE ReservaId=@reserva;",
                _ => throw new ArgumentException("Escenario no previsto.")
            };
            await EjecutarAsync(c, d, sql);
            var error = await Assert.ThrowsAsync<SqlException>(() => Servicio().DepositarAsync(Comando(d)));
            Assert.Equal(esperado, error.Number);
            await using var q = new SqlCommand("SELECT Consumida FROM dbo.EvidenciasFacialesRemitente WHERE ReservaId=@reserva;", c);
            q.Parameters.Add("@reserva", SqlDbType.Int).Value = d.Reserva;
            Assert.False((bool)(await q.ExecuteScalarAsync())!);
        }
        finally { await LimpiarAsync(c, d); }
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task NoDepositaReservasAntiguasSinCuentaInactivasNiCreadasDesdeTerminal()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();
        foreach(string escenario in new[]{"SinCuenta","Inactiva","Terminal"})
        {
            Datos d=await CrearAsync(c);
            try
            {
                await EjecutarAsync(c,d,escenario switch
                {
                    "SinCuenta"=>"UPDATE dbo.PreReservasEncomiendasWeb SET ClientePersonaId=NULL WHERE Id=@reserva;",
                    "Inactiva"=>"UPDATE dbo.ClientesWebPrototipo SET Activa=0 WHERE PersonaId=@remitente;",
                    _=>"UPDATE dbo.PreReservasEncomiendasWeb SET OrigenPreReserva=N'Terminal',TerminalCreacionId=@terminal WHERE Id=@reserva;"
                });
                var error=await Assert.ThrowsAsync<SqlException>(()=>Servicio().DepositarAsync(Comando(d)));
                Assert.Equal(55606,error.Number);
                await using var q=new SqlCommand("SELECT Consumida FROM dbo.EvidenciasFacialesRemitente WHERE EvidenciaId=@id",c);
                q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=d.Evidencia;
                Assert.False((bool)(await q.ExecuteScalarAsync())!);
            }
            finally{await LimpiarAsync(c,d);}
        }
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task ClienteRegistradoCreaReservaSqlSoloWebSinAltaDesdeTerminal()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        int creada=0;
        try
        {
            var resultado=await new PreReservaEncomiendaService(Configuracion()).CrearAsync(d.Remitente,"+50499990002","Mediano",default);
            creada=resultado.Id;
            await using var q=new SqlCommand("SELECT OrigenPreReserva,TerminalCreacionId,Estado,TokenGestionHash,ClientePersonaId FROM dbo.PreReservasEncomiendasWeb WHERE Id=@id",c);
            q.Parameters.Add("@id",SqlDbType.Int).Value=creada;
            await using var r=await q.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
            Assert.Equal("Web",r.GetString(0));Assert.True(r.IsDBNull(1));Assert.Equal("PendienteConsentimiento",r.GetString(2));Assert.Equal(d.Remitente,r.GetInt32(4));
            Assert.Equal(SHA256.HashData(Convert.FromHexString(resultado.TokenGestion)),(byte[])r[3]);
        }
        finally
        {
            await using(var q=new SqlCommand("DELETE dbo.PreReservasEncomiendasWeb WHERE Id=@id AND ClientePersonaId=@cliente",c))
            {q.Parameters.Add("@id",SqlDbType.Int).Value=creada;q.Parameters.Add("@cliente",SqlDbType.Int).Value=d.Remitente;await q.ExecuteNonQueryAsync();}
            await LimpiarAsync(c,d);
        }
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task ReservaTermicaSqlPersisteClienteWebYRechazaCuentaInactivaYTerminal()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            await EjecutarAsync(c,d,"UPDATE dbo.Casilleros SET Tipo=N'Refrigerado' WHERE Codigo=@codigo;");
            var servicio=new ServicioTemporal(new RepositorioServicioTemporalSql(Configuracion()),Configuracion(),TimeProvider.System);
            await servicio.ConfigurarAsync(ServiciosTemporalesTests.Compartimento(ClaseServicioTemporal.Frio)with{Codigo=d.Codigo},7,default);
            await Assert.ThrowsAsync<ReservaSoloWebException>(()=>servicio.ReservarAsync(d.Codigo,30,d.Terminal,default));
            await Assert.ThrowsAsync<ClienteWebRequeridoException>(()=>servicio.ReservarAsync(d.Codigo,30,null,default));
            await EjecutarAsync(c,d,"UPDATE dbo.ClientesWebPrototipo SET Activa=0 WHERE PersonaId=@remitente;");
            await Assert.ThrowsAsync<ClienteWebRequeridoException>(()=>servicio.ReservarAsync(d.Codigo,30,null,default,d.Remitente));
            await EjecutarAsync(c,d,"UPDATE dbo.ClientesWebPrototipo SET Activa=1 WHERE PersonaId=@remitente;");
            var r=await servicio.ReservarAsync(d.Codigo,30,null,default,d.Remitente);
            await using(var q=new SqlCommand("SELECT JSON_VALUE(DatosJson,'$.ClientePersonaId'),JSON_VALUE(DatosJson,'$.OrigenReserva'),TerminalId FROM dbo.OperacionesServiciosTemporales WHERE Id=@id",c))
            {
                q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=r.Operacion.Id;await using var lector=await q.ExecuteReaderAsync();Assert.True(await lector.ReadAsync());
                Assert.Equal(d.Remitente.ToString(),lector.GetString(0));Assert.Equal("Web",lector.GetString(1));Assert.True(lector.IsDBNull(2));
            }
            var p=await servicio.PagarAsync(d.Codigo,r.Operacion.Id,r.Credencial,Guid.NewGuid(),null,default,d.Remitente);
            var otraInstancia=new ServicioTemporal(new RepositorioServicioTemporalSql(Configuracion()),Configuracion(),TimeProvider.System);
            await otraInstancia.IniciarAsync(d.Codigo,r.Operacion.Id,r.Credencial,d.Terminal,true,default);
            Assert.True((await otraInstancia.RetirarAsync(d.Codigo,r.Operacion.Id,r.Credencial,d.Terminal,p.PinRetiro!,default)).Autorizado);
        }
        finally{await LimpiarAsync(c,d);}
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task ServicioTemporalSqlConectaPrepagoVencimientoYRetiro()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            var reloj=new RelojServicioTemporal();
            var repo=new RepositorioServicioTemporalSql(Configuracion());var servicio=new ServicioTemporal(repo,Configuracion(),reloj);
            await servicio.ConfigurarAsync(ServiciosTemporalesTests.Compartimento(ClaseServicioTemporal.Personal)with{Codigo=d.Codigo},7,default);
            var reserva=await servicio.ReservarAsync(d.Codigo,30,null,default);
            var pago=await servicio.PagarAsync(d.Codigo,reserva.Operacion.Id,reserva.Credencial,Guid.NewGuid(),null,default);
            await servicio.IniciarAsync(d.Codigo,reserva.Operacion.Id,reserva.Credencial,d.Terminal,true,default);
            reloj.Avanzar(TimeSpan.FromMinutes(31));
            Assert.Equal(EstadoServicioTemporal.Agotada,(await servicio.ConsultarAsync(d.Codigo,reserva.Operacion.Id,reserva.Credencial,default)).Estado);
            Assert.True((await servicio.RetirarAsync(d.Codigo,reserva.Operacion.Id,reserva.Credencial,d.Terminal,pago.PinRetiro!,default)).Autorizado);
            reloj.Avanzar(TimeSpan.FromMinutes(1));var reporte=await servicio.ReportarAsync(reloj.GetUtcNow().AddHours(-1),reloj.GetUtcNow(),default);
            // La fixture puede conservar historiales ajenos: verificar solo nuestro registro abajo.
            Assert.True(reporte.Finalizadas>=1);
            await using var q=new SqlCommand("""
                SELECT c.Estado,o.Estado,o.Monto,o.Pagado,o.PagoAnulado
                FROM dbo.Casilleros c JOIN dbo.OperacionesServiciosTemporales o ON o.Codigo=c.Codigo WHERE o.Id=@id;
                """,c);
            q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=reserva.Operacion.Id;
            await using var r=await q.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
            Assert.Equal("Disponible",r.GetString(0));Assert.Equal("Finalizada",r.GetString(1));Assert.Equal(10,r.GetDecimal(2));Assert.True(r.GetBoolean(3));Assert.False(r.GetBoolean(4));
        }
        finally{await LimpiarAsync(c,d);}
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task ServicioTemporalSqlNoDuplicaReservaConcurrente()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            var servicio=new ServicioTemporal(new RepositorioServicioTemporalSql(Configuracion()),Configuracion(),TimeProvider.System);
            await servicio.ConfigurarAsync(ServiciosTemporalesTests.Compartimento(ClaseServicioTemporal.Personal)with{Codigo=d.Codigo},7,default);
            async Task<bool> Reservar()
            {try{await servicio.ReservarAsync(d.Codigo,30,d.Terminal,default);return true;}catch(ServicioTemporalNoDisponibleException){return false;}}
            var resultados=await Task.WhenAll(Reservar(),Reservar());Assert.Single(resultados,r=>r);
            await using var q=new SqlCommand("SELECT COUNT(*) FROM dbo.OperacionesServiciosTemporales WHERE Codigo=@codigo",c);
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=d.Codigo;Assert.Equal(1,Convert.ToInt32(await q.ExecuteScalarAsync()));
        }
        finally{await LimpiarAsync(c,d);}
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task ServicioTemporalSqlReviertePagoSiFallaAuditoria()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            var servicio=new ServicioTemporal(new RepositorioServicioTemporalSql(Configuracion()),Configuracion(),TimeProvider.System);
            await servicio.ConfigurarAsync(ServiciosTemporalesTests.Compartimento(ClaseServicioTemporal.Personal)with{Codigo=d.Codigo},7,default);
            var reserva=await servicio.ReservarAsync(d.Codigo,30,d.Terminal,default);
            await using(var q=new SqlCommand($"""
                CREATE TRIGGER dbo.FalloServicioTemporalFixture ON dbo.AuditoriaServiciosTemporales AFTER INSERT AS
                BEGIN
                    IF EXISTS(SELECT 1 FROM inserted WHERE Codigo=N'{d.Codigo}' AND Accion=N'Pago simulado confirmado')
                        THROW 56290,'Fallo sintetico de auditoria.',1;
                END;
                """,c)){await q.ExecuteNonQueryAsync();}
            var error=await Assert.ThrowsAsync<SqlException>(()=>servicio.PagarAsync(d.Codigo,reserva.Operacion.Id,reserva.Credencial,Guid.NewGuid(),d.Terminal,default));
            Assert.Equal(56290,error.Number);
            await using var comprobar=new SqlCommand("SELECT Estado,Pagado FROM dbo.OperacionesServiciosTemporales WHERE Id=@id",c);
            comprobar.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=reserva.Operacion.Id;
            await using var r=await comprobar.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.Equal("Reservada",r.GetString(0));Assert.False(r.GetBoolean(1));
        }
        finally{await EjecutarAsync(c,d,"DROP TRIGGER IF EXISTS dbo.FalloServicioTemporalFixture;");await LimpiarAsync(c,d);}
    }

    // Nuevos casos externos sin Skip: si se ejecutan sin la conexion SQL
    // declarada deben fallar explicitamente, nunca aparentar validacion.
    private static async Task<(int Uso,Guid Rostro,string Pin)> PrepararRetiroAsync(SqlConnection c,Datos d)
    {
        var deposito=await Servicio().DepositarAsync(Comando(d));
        await using var q=new SqlCommand("SELECT ContenidoProtegido FROM dbo.OutboxWhatsApp WHERE ReservaId=@reserva",c);
        q.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;
        var cifrado=Assert.IsType<byte[]>(await q.ExecuteScalarAsync());
        var mensaje=new ProtectorMensajeWhatsApp(Proteccion).Abrir(d.Evidencia,d.Reserva,cifrado);
        var ahora=DateTimeOffset.UtcNow;Guid rostro=Guid.NewGuid();
        await new FlujoEncomiendasTerminalSql(Configuracion(),Servicio(),TimeProvider.System)
            .GuardarEvidenciaRetiroAsync(new(rostro,deposito.UsoId,d.Destinatario,d.Terminal,ahora,ahora.AddMinutes(2)),default);
        return(deposito.UsoId,rostro,mensaje.Variables[0]);
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task ReemisionSqlReviertePinOutboxYRegistroSiFallaAuditoria()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        string trigger="TR_Reemision_"+Guid.NewGuid().ToString("N");
        try
        {
            var(uso,_,pinAnterior)=await PrepararRetiroAsync(c,d);
            await using(var q=new SqlCommand("UPDATE dbo.CodigosRetiroEncomiendaPrototipo SET CreadoUtc=DATEADD(MINUTE,-3,SYSUTCDATETIME()),IntentosFallidos=5 WHERE UsoId=@uso",c))
            {q.Parameters.Add("@uso",SqlDbType.Int).Value=uso;await q.ExecuteNonQueryAsync();}
            // CREATE TRIGGER debe ser la primera instruccion y no recibir los
            // parametros auxiliares del fixture (correccion del propietario 5f6cd39).
            await using(var q=new SqlCommand($"CREATE TRIGGER dbo.[{trigger}] ON dbo.AuditoriaEncomiendasPrototipo AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE UsoId={uso} AND Accion=N'PIN reemitido') THROW 56599,'Fallo sintetico de auditoria.',1; END",c))
                await q.ExecuteNonQueryAsync();
            var config=Configuracion();var servicio=new ReemisionPinEncomiendaSql(config,new(config,TimeProvider.System),new(config,new(Proteccion)));
            var error=await Assert.ThrowsAsync<SqlException>(()=>servicio.ReemitirAsync(new(uso,d.Destinatario,d.Terminal,Guid.NewGuid(),"+50499990002",DateTimeOffset.UtcNow.AddSeconds(-1)),default));
            Assert.Equal(56599,error.Number);
            await using var consulta=new SqlCommand("""
                SELECT PinHash,PinSalt,IntentosFallidos,(SELECT COUNT(*)FROM dbo.ReemisionesPinEncomienda WHERE UsoId=@uso),
                    (SELECT COUNT(*)FROM dbo.OutboxWhatsApp WHERE ReservaId=@reserva AND Estado=N'Pendiente')
                FROM dbo.CodigosRetiroEncomiendaPrototipo WHERE UsoId=@uso
                """,c);
            consulta.Parameters.Add("@uso",SqlDbType.Int).Value=uso;consulta.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;
            await using var r=await consulta.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
            Assert.Equal(CodigoPinRetiro.CalcularHash(pinAnterior,uso,d.Destinatario,(byte[])r[1],config),(byte[])r[0]);
            Assert.Equal(5,r.GetInt32(2));Assert.Equal(0,r.GetInt32(3));Assert.Equal(1,r.GetInt32(4));
        }
        finally
        {
            await using(var q=new SqlCommand($"DROP TRIGGER IF EXISTS dbo.[{trigger}]",c))await q.ExecuteNonQueryAsync();
            await LimpiarAsync(c,d);
        }
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task ReemisionSqlEsAtomicaIdempotenteYCancelaPinPrevio()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            var(uso,_,pinAnterior)=await PrepararRetiroAsync(c,d);
            await using(var q=new SqlCommand("UPDATE dbo.CodigosRetiroEncomiendaPrototipo SET CreadoUtc=DATEADD(MINUTE,-3,SYSUTCDATETIME()),IntentosFallidos=5 WHERE UsoId=@uso",c))
            {q.Parameters.Add("@uso",SqlDbType.Int).Value=uso;await q.ExecuteNonQueryAsync();}
            var config=Configuracion();var protector=new ProtectorMensajeWhatsApp(Proteccion);
            var servicio=new ReemisionPinEncomiendaSql(config,new(config,TimeProvider.System),new(config,protector));
            var solicitud=new SolicitudReemisionPin(uso,d.Destinatario,d.Terminal,Guid.NewGuid(),"+50499990002",DateTimeOffset.UtcNow.AddSeconds(-1));
            Assert.Equal(ResultadoReemisionPin.NoDisponible,await servicio.ReemitirAsync(solicitud with{OperacionId=d.Evidencia},default));
            Assert.Equal(ResultadoReemisionPin.Encolado,await servicio.ReemitirAsync(solicitud,default));
            Assert.Equal(ResultadoReemisionPin.YaEncolado,await servicio.ReemitirAsync(solicitud,default));
            await using var q2=new SqlCommand("""
                SELECT p.PinHash,p.PinSalt,p.IntentosFallidos,o.ContenidoProtegido,
                    (SELECT COUNT(*)FROM dbo.ReemisionesPinEncomienda WHERE UsoId=@uso),
                    (SELECT COUNT(*)FROM dbo.OutboxWhatsApp WHERE ReservaId=@reserva AND Estado=N'Cancelado')
                FROM dbo.CodigosRetiroEncomiendaPrototipo p JOIN dbo.OutboxWhatsApp o ON o.Id=@operacion WHERE p.UsoId=@uso
                """,c);
            q2.Parameters.Add("@uso",SqlDbType.Int).Value=uso;q2.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;q2.Parameters.Add("@operacion",SqlDbType.UniqueIdentifier).Value=solicitud.OperacionId;
            await using var r=await q2.ExecuteReaderAsync();Assert.True(await r.ReadAsync());Assert.Equal(0,r.GetInt32(2));Assert.Equal(1,r.GetInt32(4));Assert.Equal(1,r.GetInt32(5));
            var mensaje=protector.Abrir(solicitud.OperacionId,d.Reserva,(byte[])r[3]);Assert.NotEqual(pinAnterior,mensaje.Variables[0]);
            Assert.NotEqual(CodigoPinRetiro.CalcularHash(pinAnterior,uso,d.Destinatario,(byte[])r[1],config),(byte[])r[0]);
        }
        finally{await LimpiarAsync(c,d);}
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task RetiroSqlConcurrenteConsumePinYRostroUnaSolaVez()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            var (uso,rostro,pin)=await PrepararRetiroAsync(c,d);
            var flujo=new FlujoEncomiendasTerminalSql(Configuracion(),Servicio(),TimeProvider.System);
            var comando=new RetirarEncomiendaComando(uso,d.Destinatario,d.Terminal,rostro,pin);
            var resultados=await Task.WhenAll(flujo.RetirarAsync(comando,default),flujo.RetirarAsync(comando,default));
            Assert.Single(resultados,r=>r==ResultadoRetiroEncomienda.Retirado);
            Assert.Single(resultados,r=>r==ResultadoRetiroEncomienda.NoDisponible);
            await using var q=new SqlCommand("""
                SELECT u.Estado,c.Estado,p.Consumido,e.ConsumidaUtc,
                    (SELECT COUNT(*) FROM dbo.AuditoriaEncomiendasPrototipo WHERE UsoId=@uso AND Accion=N'Retiro confirmado'),
                    (SELECT Estado FROM dbo.OutboxWhatsApp WHERE ReservaId=@reserva)
                FROM dbo.UsosCasillero u JOIN dbo.Casilleros c ON c.Codigo=u.CodigoCasillero
                JOIN dbo.CodigosRetiroEncomiendaPrototipo p ON p.UsoId=u.Id
                JOIN dbo.EvidenciasFacialesRetiroEncomienda e ON e.UsoId=u.Id
                WHERE u.Id=@uso;
                """,c);
            q.Parameters.Add("@uso",SqlDbType.Int).Value=uso;q.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;
            await using var r=await q.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
            Assert.Equal("Finalizado",r.GetString(0));Assert.Equal("Disponible",r.GetString(1));Assert.True(r.GetBoolean(2));
            Assert.False(r.IsDBNull(3));Assert.Equal(1,r.GetInt32(4));Assert.Equal("Cancelado",r.GetString(5));
        }
        finally{await LimpiarAsync(c,d);}
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task RetiroSqlBloqueaPinSinConsumirBiometriaNiLiberarEquipo()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            var(uso,rostro,pin)=await PrepararRetiroAsync(c,d);
            var flujo=new FlujoEncomiendasTerminalSql(Configuracion(),Servicio(),TimeProvider.System);
            string incorrecto=pin=="000000"?"000001":"000000";
            var comando=new RetirarEncomiendaComando(uso,d.Destinatario,d.Terminal,rostro,incorrecto);
            for(int i=1;i<=5;i++)
                Assert.Equal(i<5?ResultadoRetiroEncomienda.PinIncorrecto:ResultadoRetiroEncomienda.PinBloqueado,
                    await flujo.RetirarAsync(comando,default));
            Assert.Equal(ResultadoRetiroEncomienda.PinBloqueado,await flujo.RetirarAsync(comando with{Pin=pin},default));
            await using var q=new SqlCommand("SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo",c);
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=d.Codigo;
            Assert.Equal("Ocupado",await q.ExecuteScalarAsync());
        }
        finally{await LimpiarAsync(c,d);}
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task RetiroSqlRevierteConsumosSiFallaAuditoria()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            var(uso,rostro,pin)=await PrepararRetiroAsync(c,d);
            await using(var q=new SqlCommand($"""
                CREATE TRIGGER dbo.FalloRetiroFixture ON dbo.AuditoriaEncomiendasPrototipo
                AFTER INSERT AS
                BEGIN
                    IF EXISTS(SELECT 1 FROM inserted WHERE UsoId={uso}) THROW 56090,'Fallo sintetico de auditoria.',1;
                END;
                """,c)){await q.ExecuteNonQueryAsync();}
            var flujo=new FlujoEncomiendasTerminalSql(Configuracion(),Servicio(),TimeProvider.System);
            var error=await Assert.ThrowsAsync<SqlException>(()=>flujo.RetirarAsync(new(uso,d.Destinatario,d.Terminal,rostro,pin),default));
            Assert.Equal(56090,error.Number);
            await using var q2=new SqlCommand("""
                SELECT p.Consumido,e.ConsumidaUtc,u.Estado,c.Estado
                FROM dbo.CodigosRetiroEncomiendaPrototipo p
                JOIN dbo.EvidenciasFacialesRetiroEncomienda e ON e.UsoId=p.UsoId
                JOIN dbo.UsosCasillero u ON u.Id=p.UsoId JOIN dbo.Casilleros c ON c.Codigo=u.CodigoCasillero
                WHERE p.UsoId=@uso;
                """,c);
            q2.Parameters.Add("@uso",SqlDbType.Int).Value=uso;
            await using var r=await q2.ExecuteReaderAsync();Assert.True(await r.ReadAsync());
            Assert.False(r.GetBoolean(0));Assert.True(r.IsDBNull(1));Assert.Equal("Ocupado",r.GetString(2));Assert.Equal("Ocupado",r.GetString(3));
        }
        finally{await EjecutarAsync(c,d,"DROP TRIGGER IF EXISTS dbo.FalloRetiroFixture;");await LimpiarAsync(c,d);}
    }

    [Fact]
    [Trait("Category","SqlAislado")]
    public async Task CredencialAjenaEnDepositoNoConsumeEvidencia()
    {
        await PrepararAsync();await using var c=new SqlConnection(Cadena);await c.OpenAsync();Datos d=await CrearAsync(c);
        try
        {
            var comando=Comando(d) with{TokenGestionHash=RandomNumberGenerator.GetBytes(32)};
            var error=await Assert.ThrowsAsync<SqlException>(()=>Servicio().DepositarAsync(comando));Assert.Equal(55606,error.Number);
            await using var q=new SqlCommand("SELECT Consumida FROM dbo.EvidenciasFacialesRemitente WHERE ReservaId=@reserva",c);
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;Assert.False((bool)(await q.ExecuteScalarAsync())!);
        }
        finally{await LimpiarAsync(c,d);}
    }

    [SqlDepositoFact]
    [Trait("Category", "SqlAislado")]
    public async Task ErrorPosteriorAlConsumoRevierteTodo()
    {
        await PrepararAsync();
        await using var c = new SqlConnection(Cadena); await c.OpenAsync();
        Datos d = await CrearAsync(c);
        // Trigger exclusivo de la fixture marcada. No se instala en una base del propietario.
        try
        {

            await using (var cmdTrigger = new SqlCommand($"""
    CREATE TRIGGER dbo.FalloDepositoFixture ON dbo.OutboxWhatsApp
    AFTER INSERT AS
    BEGIN
        IF EXISTS (SELECT 1 FROM inserted WHERE ReservaId={d.Reserva})
            THROW 55710, 'Fallo sintetico posterior al consumo.', 1;
    END;
    """, c))
            {
                await cmdTrigger.ExecuteNonQueryAsync();
            }


            var error = await Assert.ThrowsAsync<SqlException>(() => Servicio().DepositarAsync(Comando(d)));
            Assert.Equal(55710, error.Number);
            await using var q = new SqlCommand("""
                SELECT (SELECT COUNT(*) FROM dbo.EvidenciasFacialesRemitente WHERE ReservaId=@reserva AND Consumida=0 AND FechaConsumoUtc IS NULL),
                       (SELECT COUNT(*) FROM dbo.UsosCasillero WHERE CodigoCasillero=@codigo),
                       (SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo);
                """, c);
            q.Parameters.Add("@reserva", SqlDbType.Int).Value = d.Reserva;
            q.Parameters.Add("@codigo", SqlDbType.NVarChar, 10).Value = d.Codigo;
            await using var r = await q.ExecuteReaderAsync(); Assert.True(await r.ReadAsync());
            Assert.Equal(1, r.GetInt32(0)); Assert.Equal(0, r.GetInt32(1)); Assert.Equal("Disponible", r.GetString(2));
        }
        finally
        {
            await EjecutarAsync(c, d, "DROP TRIGGER IF EXISTS dbo.FalloDepositoFixture;");
            await LimpiarAsync(c, d);
        }
    }
}

[CollectionDefinition("SQL Deposito Aislado", DisableParallelization = true)]
public sealed class ColeccionSqlDeposito;
