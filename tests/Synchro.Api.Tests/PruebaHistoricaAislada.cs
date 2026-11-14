using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Synchro.Api.Database;

namespace Synchro.Api.Tests;

// Sin secretos privados ni datos preexistentes. Cada caso tiene sus propios IDs.
public abstract class PruebaHistoricaAislada : IAsyncLifetime
{
    internal const string CadenaAislada = @"Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
    protected int TerminalFixture { get; private set; }
    protected int AdministradorFixture { get; private set; }
    protected int UsoFixture { get; private set; }
    protected int RemitenteFixture { get; private set; }
    protected int DestinatarioFixture { get; private set; }
    protected int ReservaFixture { get; private set; }
    private readonly string etiqueta = "H" + Guid.NewGuid().ToString("N")[..8];

    internal static IConfiguration Configuracion() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string,string?>
        {
            ["ConnectionStrings:SynchroDB"]=CadenaAislada,
            ["ConnectionStrings:SynchroDBPruebas"]=CadenaAislada,
            ["RetiroPin:ClaveHmac"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();

    internal static async Task VerificarAsync(SqlConnection c)
    {
        var datos=new SqlConnectionStringBuilder(c.ConnectionString);
        Assert.Equal(@"(localdb)\SYNCHRO_Aislado",datos.DataSource,StringComparer.OrdinalIgnoreCase);
        Assert.Equal("SynchroDB_Pruebas",datos.InitialCatalog);
        Assert.True(string.IsNullOrEmpty(datos.AttachDBFilename));
        await using var q=new SqlCommand("""
            IF DB_NAME()<>N'SynchroDB_Pruebas' OR NOT EXISTS
                (SELECT 1 FROM sys.extended_properties WHERE class=0
                 AND name=N'SynchroFixtureDeposito' AND CONVERT(nvarchar(10),value)=N'1')
                THROW 57300,'Base o marcador no autorizado.',1;
            """,c);
        await q.ExecuteNonQueryAsync();
    }

    private static async Task AplicarAsync(SqlConnection c,string archivo,bool soloProcedimiento=false)
    {
        // Adaptar exclusivamente el catalogo del contrato historico. No se relajan
        // condiciones de PIN, autorizacion, vencimiento, consumo o concurrencia.
        string sql=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"sql","historicos",archivo));
        sql=sql.Replace("N'SynchroDB_Detalle_Pruebas'","N'SynchroDB_Pruebas'");
        if(soloProcedimiento)
        {
            // El archivo original de retiro es un lanzador para desarrollo.
            // Solo se instala su definicion, nunca su preambulo ni su transaccion.
            sql=InstalacionPrototipo.Lotes(sql).Single(x=>x.Contains("CREATE OR ALTER PROCEDURE"));
        }
        foreach(var lote in InstalacionPrototipo.Lotes(sql))
        {
            await VerificarAsync(c);
            await using var q=new SqlCommand(lote,c);
            await q.ExecuteNonQueryAsync();
        }
    }

    public async Task InitializeAsync()
    {
        await using var c=new SqlConnection(CadenaAislada);
        await c.OpenAsync();
        await VerificarAsync(c);
        var declarada=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SYNCHRO_SQL_DEPOSITO_TEST"));
        Assert.Equal(@"(localdb)\SYNCHRO_Aislado",declarada.DataSource,StringComparer.OrdinalIgnoreCase);
        Assert.Equal("SynchroDB_Pruebas",declarada.InitialCatalog);
        Assert.True(string.IsNullOrEmpty(declarada.AttachDBFilename));
        await DepositoEncomiendaSqlTests.PrepararAsync();
        foreach(var archivo in new[]{"Esquema.sql","PrepararPin.sql","EmitirPin.sql","ValidarPin.sql",
            "ConsumirEvidencia.sql","SolicitudesVinculacion.sql","ConfirmarVinculacion.sql"})
            await AplicarAsync(c,archivo);
        await AplicarAsync(c,"RetiroConPinYFacial.sql",soloProcedimiento:true);
        await VerificarAsync(c);
        await using var crear=new SqlCommand("""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            INSERT dbo.Terminales(Codigo,Nombre,Activa) VALUES(@tag,N'HISTORICO_FIXTURE',1);
            DECLARE @terminal int=SCOPE_IDENTITY();
            INSERT dbo.Administradores(NombreUsuario,PasswordHash,Activo) VALUES(@tag,N'HASH_FICTICIO_NO_AUTENTICABLE',1);
            DECLARE @admin int=SCOPE_IDENTITY();
            INSERT dbo.Personas(Nombre) VALUES(N'REMITENTE_FICTICIO');
            DECLARE @remitente int=SCOPE_IDENTITY();
            INSERT dbo.Personas(Nombre) VALUES(N'DESTINATARIO_FICTICIO');
            DECLARE @destinatario int=SCOPE_IDENTITY();
            INSERT dbo.Casilleros(Codigo,Tamano,Tipo,Estado) VALUES(@tag,N'Mediano',N'Normal',N'Ocupado');
            INSERT dbo.UsosCasillero(CodigoCasillero,TipoServicio,FechaReserva,FechaIngreso,Estado)
                VALUES(@tag,N'Encomienda',DATEADD(minute,-5,SYSUTCDATETIME()),SYSUTCDATETIME(),N'Ocupado');
            DECLARE @uso int=SCOPE_IDENTITY();
            INSERT dbo.ParticipantesUso(UsoId,PersonaId,Rol) VALUES(@uso,@remitente,N'Remitente'),(@uso,@destinatario,N'Destinatario');
            INSERT dbo.PreReservasEncomiendasWeb(TokenGestionHash,TelefonoRemitente,TelefonoDestinatario,TamanoSolicitado,FechaExpiracion)
                VALUES(CRYPT_GEN_RANDOM(32),N'+999000000001',N'+999000000002',N'Mediano',DATEADD(hour,1,SYSUTCDATETIME()));
            DECLARE @reserva int=SCOPE_IDENTITY();
            COMMIT;
            SELECT @terminal,@admin,@remitente,@destinatario,@uso,@reserva;
            """,c);
        crear.Parameters.Add("@tag",SqlDbType.NVarChar,10).Value=etiqueta;
        await using var r=await crear.ExecuteReaderAsync();
        Assert.True(await r.ReadAsync());
        TerminalFixture=r.GetInt32(0); AdministradorFixture=r.GetInt32(1);
        RemitenteFixture=r.GetInt32(2); DestinatarioFixture=r.GetInt32(3);
        UsoFixture=r.GetInt32(4); ReservaFixture=r.GetInt32(5);
    }

    public async Task DisposeAsync()
    {
        if(TerminalFixture==0) return;
        await using var c=new SqlConnection(CadenaAislada);
        await c.OpenAsync();
        await VerificarAsync(c);
        await using var q=new SqlCommand("""
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DELETE dbo.ConsentimientosPreReservaWhatsApp WHERE ReservaId=@reserva;
            DELETE dbo.PreReservasEncomiendasWeb WHERE Id=@reserva;
            DELETE dbo.ParticipantesUso WHERE UsoId=@uso;
            DELETE dbo.UsosCasillero WHERE Id=@uso AND CodigoCasillero=@tag;
            DELETE dbo.Casilleros WHERE Codigo=@tag;
            DELETE dbo.Personas WHERE Id IN(@r,@d);
            DELETE dbo.Administradores WHERE Id=@admin AND NombreUsuario=@tag;
            DELETE dbo.Terminales WHERE Id=@terminal AND Codigo=@tag AND Nombre=N'HISTORICO_FIXTURE';
            COMMIT;
            """,c);
        q.Parameters.AddWithValue("@reserva",ReservaFixture);q.Parameters.AddWithValue("@uso",UsoFixture);
        q.Parameters.AddWithValue("@r",RemitenteFixture);q.Parameters.AddWithValue("@d",DestinatarioFixture);
        q.Parameters.AddWithValue("@admin",AdministradorFixture);q.Parameters.AddWithValue("@terminal",TerminalFixture);
        q.Parameters.AddWithValue("@tag",etiqueta);
        await q.ExecuteNonQueryAsync();
    }
}
