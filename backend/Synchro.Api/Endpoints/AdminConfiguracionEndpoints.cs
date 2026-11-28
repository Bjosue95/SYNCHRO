using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services;
using Synchro.Api.Services.Temporales;

namespace Synchro.Api.Endpoints;

public sealed record CambiarTarifaSolicitud(Dictionary<string,decimal> Valores);
public sealed record LimpiarDatosSolicitud(string[] Categorias,DateTimeOffset CorteUtc,Dictionary<string,int> Cantidades,string Confirmacion);

public static class AdminConfiguracionEndpoints
{
    private static readonly SemaphoreSlim Puerta=new(1,1);
    private static readonly string[] Tamanos=["Pequeno","Mediano","Grande"];
    private static readonly string[] Modalidades=["Cable","Inalambrica","Laptop"];
    private static readonly int[] Duraciones=[60,120,180,240];
    private static string Carpeta=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SYNCHRO","Configuracion");
    private static string Archivo=>Path.Combine(Carpeta,"tarifas.json");

    internal static void CargarTarifasLocales(WebApplicationBuilder builder)
    {
        if(!builder.Environment.IsDevelopment()||!builder.Configuration.GetValue<bool>("Administracion:ConfiguracionHabilitada"))return;
        var cadena=builder.Configuration.GetConnectionString("SynchroDB");
        if(string.IsNullOrWhiteSpace(cadena)||!ConexionSqlAislada.EsWhatsAppPermitido(new SqlConnectionStringBuilder(cadena)))return;
        Directory.CreateDirectory(Carpeta);
        builder.Configuration.AddJsonFile(Archivo,optional:true,reloadOnChange:false);
    }
    private static string[] Claves(string grupo)=>grupo switch {
        "personal"=>Tamanos.SelectMany(t=>new[]{"CobroRetiroPersonal:Tarifas:"+t,"CobroRetiroPersonal:Adicionales:"+t}).ToArray(),
        "web"=>Tamanos.SelectMany(t=>Duraciones.Select(m=>$"ReservasPrototipo:TarifasNormales:{t}:{m}")).ToArray(),
        _=>[]
    };
    private static bool Precio(decimal n)=>n>0&&n<=100000&&decimal.Round(n,2)==n;
    private static decimal? Leer(IConfiguration c,string k)=>decimal.TryParse(c[k],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n)&&Precio(n)?n:null;
    private static int Actor(HttpContext h)=>int.Parse(h.User.FindFirstValue(ClaimTypes.NameIdentifier)!,CultureInfo.InvariantCulture);
    private static async Task<SqlConnection> Abrir(IConfiguration cfg,CancellationToken ct)
    {
        var cadena=cfg.GetConnectionString("SynchroDB");
        if(string.IsNullOrWhiteSpace(cadena)||!ConexionSqlAislada.EsWhatsAppPermitido(new SqlConnectionStringBuilder(cadena)))throw new InvalidOperationException();
        var db=new SqlConnection(cadena);
        try{
            await db.OpenAsync(ct);
            await using var q=new SqlCommand("""
                SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas' AND CONVERT(int,SERVERPROPERTY('IsLocalDB'))=1 AND EXISTS(
                  SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito' AND CONVERT(nvarchar(10),value)=N'1') THEN 1 ELSE 0 END;
                """,db);
            if(Convert.ToInt32(await q.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException();
            return db;
        }catch{await db.DisposeAsync();throw;}
    }
    private static async Task Auditar(SqlConnection db,SqlTransaction? tx,int actor,string accion,CancellationToken ct)
    {
        await using var q=new SqlCommand("INSERT dbo.AuditoriaUsuariosAdministrativos(AdministradorId,ActorId,Accion,FechaUtc) VALUES(@actor,@actor,@accion,SYSUTCDATETIME())",db,tx);
        q.Parameters.Add("@actor",SqlDbType.Int).Value=actor;q.Parameters.Add("@accion",SqlDbType.NVarChar,80).Value=accion;
        await q.ExecuteNonQueryAsync(ct);
    }

    // Lista cerrada, jamás se acepta SQL, nombres de tablas o rutas proporcionados por el cliente.
    private static readonly Dictionary<string,(string From,string Where)> Limpiezas=new(StringComparer.Ordinal){
        ["whatsapp"] = ("dbo.AutorizacionesWhatsAppManuales a","a.UsoId IS NULL AND a.Estado IN(N'Pendiente',N'Cancelado') AND a.VenceUtc<=@corte"),
        ["rostros"] = ("dbo.RostrosRetiroPersonalV3 a JOIN dbo.UsosCasillero u ON u.Id=a.UsoId","u.FechaRetiro<=@corte AND u.Estado NOT IN(N'Ocupado',N'Reservado')"),
        ["fotografias"] = ("dbo.FotografiasEncomiendaOperativa a JOIN dbo.EvidenciasEncomiendaOperativa e ON e.Id=a.EvidenciaId JOIN dbo.EncomiendasOperativas o ON o.ReservaId=e.ReservaId","o.Estado IN(N'Retirada',N'Cancelada',N'Rechazada') AND a.EliminarUtc<=@corte AND (a.ExtensionHastaUtc IS NULL OR a.ExtensionHastaUtc<=@corte)")
    };
    private static async Task<int> Contar(SqlConnection db,SqlTransaction? tx,string categoria,DateTime corte,bool borrar,CancellationToken ct)
    {
        var (fuente,filtro)=Limpiezas[categoria];
        var sql="SET NOCOUNT ON; "+(borrar?"DELETE a FROM ":"SELECT COUNT(*) FROM ")+fuente+" WHERE "+filtro+(borrar?"; SELECT @@ROWCOUNT;":";");
        await using var q=new SqlCommand(sql,db,tx);
        q.Parameters.Add("@corte",SqlDbType.DateTime2).Value=corte;
        return Convert.ToInt32(await q.ExecuteScalarAsync(ct));
    }
    private static async Task<Dictionary<string,int>> VistaLimpieza(SqlConnection db,SqlTransaction? tx,DateTime corte,CancellationToken ct)
    {
        var d=new Dictionary<string,int>();
        foreach(var k in Limpiezas.Keys)d[k]=await Contar(db,tx,k,corte,false,ct);
        return d;
    }
    public static void MapAdminConfiguracionEndpoints(this WebApplication app)
    {
        var g=app.MapGroup("/api/admin/configuracion").RequireAuthorization("SoloAdministradores")
          .AddEndpointFilter(new ModuloPrototipoHabilitadoFilter(app.Configuration,"Administracion:ConfiguracionHabilitada"))
          .AddEndpointFilter<OperacionTerminalSeguraFilter>().RequireRateLimiting("OperacionesTerminal");
        g.MapGet("/tarifas",async(HttpContext h,IConfiguration cfg)=>{
            try{
                await using var db=await Abrir(cfg,h.RequestAborted);
                var carga=new List<object>();var especiales=new List<object>();
                await using(var q=new SqlCommand("SELECT Modalidad,MinutosBase,PrecioBase,MinutosAdicionales,PrecioAdicional FROM dbo.TarifasCargaSynchro WHERE Modalidad IN(N'Cable',N'Inalambrica',N'Laptop') ORDER BY Modalidad",db))
                await using(var r=await q.ExecuteReaderAsync(h.RequestAborted))
                    while(await r.ReadAsync(h.RequestAborted))carga.Add(new{modalidad=r.GetString(0),minutosBase=r.GetInt32(1),precioBase=r.GetDecimal(2),minutosAdicionales=r.GetInt32(3),precioAdicional=r.GetDecimal(4)});
                await using(var q=new SqlCommand("SELECT Codigo,ConfiguracionJson FROM dbo.CompartimentosServiciosTemporales WHERE Codigo IN(N'M04',N'M09')",db))
                await using(var r=await q.ExecuteReaderAsync(h.RequestAborted))
                    while(await r.ReadAsync(h.RequestAborted)){
                        var c=JsonSerializer.Deserialize<ConfiguracionServicioTemporal>(r.GetString(1));
                        if(c is not null)especiales.Add(new{codigo=r.GetString(0),tarifas=c.Tarifas});
                    }
                return Results.Ok(new{personal=Claves("personal").ToDictionary(k=>k,k=>Leer(cfg,k)),web=Claves("web").ToDictionary(k=>k,k=>Leer(cfg,k)),carga,especiales,ubicacion=cfg["ReservasPrototipo:Ubicacion"]});
            }catch(Exception e)when(e is SqlException or InvalidOperationException or ArgumentException or JsonException){return Results.Problem(statusCode:503,title:"No se pudo consultar la configuración.");}
        });
        g.MapPut("/tarifas/{grupo}",async(string grupo,CambiarTarifaSolicitud datos,HttpContext h,IConfiguration cfg)=>{
            var ct=h.RequestAborted;
            var claves=Claves(grupo);
            if(grupo=="carga")claves=Modalidades.SelectMany(m=>new[]{m+":MinutosBase",m+":PrecioBase",m+":MinutosAdicionales",m+":PrecioAdicional"}).ToArray();
            if(grupo is "M04" or "M09")claves=Duraciones.Select(m=>m.ToString(CultureInfo.InvariantCulture)).ToArray();
            if(claves.Length==0||datos.Valores is null||datos.Valores.Count!=claves.Length||!claves.All(datos.Valores.ContainsKey)||datos.Valores.Any(p=>!Precio(p.Value)||p.Key.Contains(":Minutos")&&(p.Value!=decimal.Truncate(p.Value)||p.Value>1440)))
                return Results.BadRequest(new{mensaje="Completa todos los precios y duraciones válidos."});
            await Puerta.WaitAsync(ct);
            try{
                await using var db=await Abrir(cfg,ct);
                if(grupo is "personal" or "web"){
                    // Solo se persisten las claves de tarifas conocidas. La configuración se recarga inmediatamente.
                    var valores=Claves("personal").Concat(Claves("web")).Where(k=>Leer(cfg,k).HasValue).ToDictionary(k=>k,k=>Leer(cfg,k)!.Value);
                    foreach(var p in datos.Valores)valores[p.Key]=p.Value;
                    Directory.CreateDirectory(Carpeta);
                    string tmp=Archivo+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try{
                        await File.WriteAllTextAsync(tmp,JsonSerializer.Serialize(valores),ct);
                        File.Move(tmp,Archivo,true);
                    }finally{if(File.Exists(tmp))File.Delete(tmp);}
                    ((IConfigurationRoot)cfg).Reload();
                    await Auditar(db,null,Actor(h),"Tarifas "+grupo+" actualizadas",ct);
                }else{
                    await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                    if(grupo=="carga"){
                        foreach(var m in Modalidades){
                            await using var q=new SqlCommand("UPDATE dbo.TarifasCargaSynchro SET MinutosBase=@mb,PrecioBase=@pb,MinutosAdicionales=@ma,PrecioAdicional=@pa WHERE Modalidad=@m",db,tx);
                            q.Parameters.Add("@m",SqlDbType.NVarChar,20).Value=m;
                            q.Parameters.Add("@mb",SqlDbType.Int).Value=(int)datos.Valores[m+":MinutosBase"];
                            q.Parameters.Add("@ma",SqlDbType.Int).Value=(int)datos.Valores[m+":MinutosAdicionales"];
                            q.Parameters.AddWithValue("@pb",datos.Valores[m+":PrecioBase"]);q.Parameters.AddWithValue("@pa",datos.Valores[m+":PrecioAdicional"]);
                            if(await q.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException();
                        }
                    }else{
                        await using var leer=new SqlCommand("SELECT ConfiguracionJson FROM dbo.CompartimentosServiciosTemporales WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@codigo",db,tx);
                        leer.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=grupo;
                        var json=await leer.ExecuteScalarAsync(ct) as string;
                        if(json is null)return Results.Conflict(new{mensaje="El compartimento no tiene configuración."});
                        var anterior=JsonSerializer.Deserialize<ConfiguracionServicioTemporal>(json)!;
                        if(anterior.Clase!=(grupo=="M04"?ClaseServicioTemporal.Frio:ClaseServicioTemporal.Calor))return Results.Conflict(new{mensaje="Tipo de compartimento incompatible."});
                        var nueva=anterior with{Tarifas=datos.Valores.ToDictionary(p=>int.Parse(p.Key,CultureInfo.InvariantCulture),p=>p.Value)};
                        nueva.Validar();
                        await using var q=new SqlCommand("UPDATE dbo.CompartimentosServiciosTemporales SET ConfiguracionJson=@json WHERE Codigo=@codigo",db,tx);
                        q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=grupo;
                        q.Parameters.Add("@json",SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(nueva);
                        if(await q.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException();
                    }
                    await Auditar(db,tx,Actor(h),"Tarifas "+grupo+" actualizadas",ct);await tx.CommitAsync(ct);
                }
                return Results.Ok(new{actualizado=true});
            }catch(Exception e)when(e is SqlException or InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or JsonException){return Results.Problem(statusCode:503,title:"No se pudo completar la actualización. Consulta nuevamente los precios.");}
            finally{Puerta.Release();}
        });
        g.MapGet("/limpieza",async(HttpContext h,IConfiguration cfg)=>{
            try{
                await using var db=await Abrir(cfg,h.RequestAborted);
                var corte=DateTimeOffset.UtcNow;
                return Results.Ok(new{corteUtc=corte,cantidades=await VistaLimpieza(db,null,corte.UtcDateTime,h.RequestAborted)});
            }catch(Exception e)when(e is SqlException or InvalidOperationException or ArgumentException){return Results.Problem(statusCode:503,title:"No se pudo revisar la limpieza.");}
        });
        g.MapPost("/limpieza",async(LimpiarDatosSolicitud datos,HttpContext h,IConfiguration cfg)=>{
            var ct=h.RequestAborted;
            if(datos.Confirmacion!="ELIMINAR"||datos.Categorias is not{Length:>0 and <=3}||datos.Categorias.Distinct().Count()!=datos.Categorias.Length||datos.Categorias.Any(k=>!Limpiezas.ContainsKey(k))||datos.Cantidades is null||datos.CorteUtc>DateTimeOffset.UtcNow||datos.CorteUtc<DateTimeOffset.UtcNow.AddMinutes(-5))
                return Results.BadRequest(new{mensaje="Revisa nuevamente los registros y escribe ELIMINAR."});
            await Puerta.WaitAsync(ct);
            try{
                await using var db=await Abrir(cfg,ct);
                await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                var corte=datos.CorteUtc.UtcDateTime;
                var eliminados=new Dictionary<string,int>();
                foreach(var k in datos.Categorias){
                    var n=await Contar(db,tx,k,corte,false,ct);
                    if(!datos.Cantidades.TryGetValue(k,out var esperado)||esperado!=n)return Results.Conflict(new{mensaje="La cantidad cambió. Revisa nuevamente antes de eliminar."});
                    var borrados=await Contar(db,tx,k,corte,true,ct);
                    if(borrados!=n)throw new InvalidOperationException();
                    eliminados[k]=borrados;
                    await Auditar(db,tx,Actor(h),$"Limpieza {k}: {borrados}",ct);
                }
                await tx.CommitAsync(ct);
                return Results.Ok(new{eliminados});
            }catch(Exception e)when(e is SqlException or InvalidOperationException or ArgumentException){return Results.Problem(statusCode:503,title:"No se completó la limpieza. No se liberaron casilleros.");}
            finally{Puerta.Release();}
        });
        g.MapGet("/respaldos",(HttpContext h)=>{
            var dir=Path.Combine(Carpeta,"Respaldos");
            try{
                var archivos=(Directory.Exists(dir)?new DirectoryInfo(dir).GetFiles("SYNCHRO-*.bak"):Array.Empty<FileInfo>()).OrderByDescending(x=>x.CreationTimeUtc).Select(x=>new{nombre=x.Name,bytes=x.Length,creadoUtc=x.CreationTimeUtc}).ToArray();
                return Results.Ok(new{archivos});
            }catch(IOException){return Results.Problem(statusCode:503,title:"No se pudieron consultar los respaldos.");}
        });
        g.MapPost("/respaldos",async(HttpContext h,IConfiguration cfg)=>{
            var ct=h.RequestAborted;
            await Puerta.WaitAsync(ct);
            string? destino=null;bool verificado=false;
            try{
                await using var db=await Abrir(cfg,ct);
                string dir=Path.Combine(Carpeta,"Respaldos");Directory.CreateDirectory(dir);
                var nombre="SYNCHRO-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N")[..8]+".bak";
                destino=Path.Combine(dir,nombre);
                await using(var q=new SqlCommand("BACKUP DATABASE [SynchroDB_Pruebas] TO DISK=@ruta WITH COPY_ONLY,CHECKSUM",db){CommandTimeout=120}){
                    q.Parameters.Add("@ruta",SqlDbType.NVarChar,4000).Value=destino;await q.ExecuteNonQueryAsync(ct);}
                await using(var q=new SqlCommand("RESTORE VERIFYONLY FROM DISK=@ruta WITH CHECKSUM",db){CommandTimeout=120}){
                    q.Parameters.Add("@ruta",SqlDbType.NVarChar,4000).Value=destino;await q.ExecuteNonQueryAsync(ct);}
                verificado=true;
                await Auditar(db,null,Actor(h),"Respaldo SQL verificado",ct);
                return Results.Ok(new{nombre,bytes=new FileInfo(destino).Length,verificado=true});
            }catch(Exception e)when(e is SqlException or InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException){return Results.Problem(statusCode:503,title:"No se pudo completar el respaldo verificado.");}
            finally{if(!verificado&&destino is not null)try{File.Delete(destino);}catch(IOException){}catch(UnauthorizedAccessException){} Puerta.Release();}
        });
    }
}
