using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Synchro.Api.Services.WhatsApp;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Services;

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
    private static DepositoEncomiendaService Servicio()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SynchroDB"] = Cadena,
            ["RetiroPin:ClaveHmac"] = ClavePrueba,
            ["WhatsApp:PlantillaPinRetiro"] = "pin_retiro"
        }).Build();
        return new(config,new PreparadorPinEncomienda(config,TimeProvider.System),
            new SqlOutboxWhatsApp(config,new ProtectorMensajeWhatsApp(Proteccion)));
    }

    private static async Task PrepararAsync()
    {
        await using var c = new SqlConnection(Cadena);
        await c.OpenAsync();
        foreach (string archivo in new[] {"EsquemaDeposito.sql", "Consentimiento.sql", "Evidencias.sql", "Deposito.sql", "PinEncomienda.sql", "Outbox.sql"})
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
            INSERT dbo.Casilleros VALUES (@codigo,N'Normal',N'Mediano',N'Disponible');
            INSERT dbo.PreReservasEncomiendasWeb(TokenGestionHash,TelefonoRemitente,TelefonoDestinatario,TamanoSolicitado,FechaExpiracion)
            VALUES (CRYPT_GEN_RANDOM(32),N'+50499990001',N'+50499990002',N'Mediano',DATEADD(HOUR,1,SYSUTCDATETIME()));
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
        q.Parameters.Add("@codigo", SqlDbType.NVarChar,10).Value=codigo;
        q.Parameters.Add("@evidencia", SqlDbType.UniqueIdentifier).Value=evidencia;
        await using var r = await q.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        return new(r.GetInt32(0),r.GetInt32(1),r.GetInt32(2),r.GetInt32(3),evidencia,codigo);
    }

    private static async Task EjecutarAsync(SqlConnection c, Datos d, string sql)
    {
        await using var q = new SqlCommand(sql,c);
        q.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;
        q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=d.Codigo;
        q.Parameters.Add("@terminal",SqlDbType.Int).Value=d.Terminal;
        await q.ExecuteNonQueryAsync();
    }

    private static async Task LimpiarAsync(SqlConnection c, Datos d)
    {
        await EjecutarAsync(c,d,"""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DELETE dbo.OutboxWhatsApp WHERE ReservaId=@reserva;
            DELETE dbo.CodigosRetiroEncomiendaPrototipo WHERE UsoId IN (SELECT UsoId FROM dbo.DepositosPreReservaEncomienda WHERE ReservaId=@reserva);
            DELETE dbo.ParticipantesUso WHERE UsoId IN (SELECT UsoId FROM dbo.DepositosPreReservaEncomienda WHERE ReservaId=@reserva);
            DECLARE @usos TABLE (Id INT);
            DELETE dbo.DepositosPreReservaEncomienda OUTPUT DELETED.UsoId INTO @usos WHERE ReservaId=@reserva;
            DELETE dbo.UsosCasillero WHERE Id IN (SELECT Id FROM @usos);
            DELETE dbo.EvidenciasFacialesRemitente WHERE ReservaId=@reserva;
            DELETE dbo.ParticipantesPreReservaVerificados WHERE ReservaId=@reserva;
            DELETE dbo.ConsentimientosPreReservaWhatsApp WHERE ReservaId=@reserva;
            DELETE dbo.PreReservasEncomiendasWeb WHERE Id=@reserva;
            DELETE dbo.Casilleros WHERE Codigo=@codigo;
            DELETE dbo.Terminales WHERE Id=@terminal AND Nombre=N'TEST_DEPOSITO';
            COMMIT TRANSACTION;
            """);
        await using var personas = new SqlCommand("DELETE dbo.Personas WHERE Id IN (@remitente,@destinatario)",c);
        personas.Parameters.Add("@remitente",SqlDbType.Int).Value=d.Remitente;
        personas.Parameters.Add("@destinatario",SqlDbType.Int).Value=d.Destinatario;
        await personas.ExecuteNonQueryAsync();
    }

    private static DepositoEncomiendaComando Comando(Datos d) => new(d.Reserva,d.Terminal,d.Evidencia,d.Codigo);

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
            Assert.True(resultado.UsoId > 0); Assert.Equal(d.Codigo,resultado.CodigoCasillero);
            var error = await Assert.ThrowsAsync<SqlException>(() => Servicio().DepositarAsync(Comando(d)));
            Assert.Contains(error.Number,new[]{55605,55606,55610});
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
                """,c);
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;
            Assert.Equal(1,Convert.ToInt32(await q.ExecuteScalarAsync()));
        }
        finally { await LimpiarAsync(c,d); }
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
            var resultados = await Task.WhenAll(Intentar(),Intentar());
            Assert.Single(resultados,x=>x);
        }
        finally { await LimpiarAsync(c,d); }
    }

    [SqlDepositoTheory]
    [Trait("Category", "SqlAislado")]
    [InlineData("TerminalRevocada",55604)]
    [InlineData("ReservaVencida",55606)]
    [InlineData("TamanoDistinto",55607)]
    [InlineData("ParticipantesNoVerificados",55608)]
    [InlineData("ConsentimientoRevocado",55609)]
    [InlineData("TelefonoAjeno",55609)]
    [InlineData("ConfirmacionFutura",55609)]
    [InlineData("EvidenciaVencida",55610)]
    [InlineData("EvidenciaAjena",55610)]
    [InlineData("SinPruebaDeVida",55610)]
    public async Task RechazaCondicionesInvalidasSinConsumir(string escenario,int esperado)
    {
        await PrepararAsync();
        await using var c = new SqlConnection(Cadena); await c.OpenAsync();
        Datos d = await CrearAsync(c);
        try
        {
            string sql=escenario switch
            {
                "TerminalRevocada"=>"UPDATE dbo.Terminales SET Activa=0 WHERE Id=@terminal;",
                "ReservaVencida"=>"UPDATE dbo.PreReservasEncomiendasWeb SET FechaExpiracion=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE Id=@reserva;",
                "TamanoDistinto"=>"UPDATE dbo.Casilleros SET Tamano=N'Grande' WHERE Codigo=@codigo;",
                "ParticipantesNoVerificados"=>"DELETE dbo.ParticipantesPreReservaVerificados WHERE ReservaId=@reserva;",
                "ConsentimientoRevocado"=>"UPDATE dbo.ConsentimientosPreReservaWhatsApp SET Estado=N'Revocado' WHERE ReservaId=@reserva;",
                "TelefonoAjeno"=>"UPDATE dbo.ConsentimientosPreReservaWhatsApp SET TelefonoWhatsApp=N'+50499990003' WHERE ReservaId=@reserva;",
                "ConfirmacionFutura"=>"UPDATE dbo.ConsentimientosPreReservaWhatsApp SET FechaConfirmacionUtc=DATEADD(MINUTE,1,SYSUTCDATETIME()) WHERE ReservaId=@reserva;",
                "EvidenciaVencida"=>"UPDATE dbo.EvidenciasFacialesRemitente SET FechaEmisionUtc=DATEADD(SECOND,-100,SYSUTCDATETIME()),FechaExpiracionUtc=DATEADD(SECOND,-10,SYSUTCDATETIME()) WHERE ReservaId=@reserva;",
                "EvidenciaAjena"=>"UPDATE dbo.EvidenciasFacialesRemitente SET RemitenteId=2147483647 WHERE ReservaId=@reserva;",
                "SinPruebaDeVida"=>"UPDATE dbo.EvidenciasFacialesRemitente SET PruebaDeVidaSuperada=0 WHERE ReservaId=@reserva;",
                _=>throw new ArgumentException("Escenario no previsto.")
            };
            await EjecutarAsync(c,d,sql);
            var error=await Assert.ThrowsAsync<SqlException>(()=>Servicio().DepositarAsync(Comando(d)));
            Assert.Equal(esperado,error.Number);
            await using var q=new SqlCommand("SELECT Consumida FROM dbo.EvidenciasFacialesRemitente WHERE ReservaId=@reserva;",c);
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;
            Assert.False((bool)(await q.ExecuteScalarAsync())!);
        }
        finally { await LimpiarAsync(c,d); }
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
            await EjecutarAsync(c,d,$"""
                CREATE OR ALTER TRIGGER dbo.FalloDepositoFixture ON dbo.OutboxWhatsApp
                AFTER INSERT AS
                BEGIN
                    IF EXISTS (SELECT 1 FROM inserted WHERE ReservaId={d.Reserva})
                        THROW 55710, 'Fallo sintetico posterior al consumo.', 1;
                END;
                """);
            var error = await Assert.ThrowsAsync<SqlException>(()=>Servicio().DepositarAsync(Comando(d)));
            Assert.Equal(55710,error.Number);
            await using var q = new SqlCommand("""
                SELECT (SELECT COUNT(*) FROM dbo.EvidenciasFacialesRemitente WHERE ReservaId=@reserva AND Consumida=0 AND FechaConsumoUtc IS NULL),
                       (SELECT COUNT(*) FROM dbo.UsosCasillero WHERE CodigoCasillero=@codigo),
                       (SELECT Estado FROM dbo.Casilleros WHERE Codigo=@codigo);
                """,c);
            q.Parameters.Add("@reserva",SqlDbType.Int).Value=d.Reserva;
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=d.Codigo;
            await using var r = await q.ExecuteReaderAsync(); Assert.True(await r.ReadAsync());
            Assert.Equal(1,r.GetInt32(0)); Assert.Equal(0,r.GetInt32(1)); Assert.Equal("Disponible",r.GetString(2));
        }
        finally
        {
            await EjecutarAsync(c,d,"DROP TRIGGER IF EXISTS dbo.FalloDepositoFixture;");
            await LimpiarAsync(c,d);
        }
    }
}

[CollectionDefinition("SQL Deposito Aislado", DisableParallelization = true)]
public sealed class ColeccionSqlDeposito;
