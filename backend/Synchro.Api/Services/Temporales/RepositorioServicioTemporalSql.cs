using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Synchro.Api.Services.Temporales;

internal sealed class RepositorioServicioTemporalSql(IConfiguration config) : IRepositorioServicioTemporal
{
    private string Cadena=>ConexionSqlAislada.Exigir(config.GetConnectionString("SynchroDB"),"SynchroDB_Pruebas");
    private static string Inventario(ConfiguracionServicioTemporal c)
        =>c.Clase is ClaseServicioTemporal.CargaCelular or ClaseServicioTemporal.CargaLaptop?"dbo.EspaciosCarga":"dbo.Casilleros";
    private static async Task<string> EstadoInventarioAsync(SqlConnection c,SqlTransaction t,ConfiguracionServicioTemporal config,CancellationToken token)
    {
        // Nombre de tabla de una lista fija; codigo siempre parametrizado.
        await using var q=new SqlCommand($"SELECT Estado FROM {Inventario(config)} WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@codigo",c,t);
        q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=config.Codigo;
        return await q.ExecuteScalarAsync(token) as string??throw new ServicioTemporalNoDisponibleException();
    }
    public async Task<T> TransaccionAsync<T>(string codigo,Guid? operacion,int? terminal,Func<DatosServicioTemporal,T> cambio,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(codigo)||codigo.Length>10)throw new ArgumentException("Codigo invalido.");
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var t=(SqlTransaction)await c.BeginTransactionAsync(token);
        if(terminal is not null)
        {
            await using var q=new SqlCommand("SELECT Activa FROM dbo.Terminales WITH(UPDLOCK,HOLDLOCK) WHERE Id=@terminal",c,t);
            q.Parameters.Add("@terminal",SqlDbType.Int).Value=terminal;
            if(await q.ExecuteScalarAsync(token) is not true)throw new ServicioTemporalNoDisponibleException();
        }
        ConfiguracionServicioTemporal configuracion;
        await using(var q=new SqlCommand("SELECT ConfiguracionJson FROM dbo.CompartimentosServiciosTemporales WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@codigo",c,t))
        {
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=codigo;
            var json=await q.ExecuteScalarAsync(token) as string??throw new ServicioTemporalNoDisponibleException();
            configuracion=JsonSerializer.Deserialize<ConfiguracionServicioTemporal>(json)??throw new InvalidOperationException("Configuracion no disponible.");
            configuracion.Validar();
        }
        var datos=new DatosServicioTemporal{Configuracion=configuracion,EstadoInventario=await EstadoInventarioAsync(c,t,configuracion,token)};
        var existentes=new HashSet<Guid>();
        await using(var q=new SqlCommand("""
            SELECT DatosJson FROM dbo.OperacionesServiciosTemporales WITH(UPDLOCK,HOLDLOCK)
            WHERE Codigo=@codigo AND (Estado IN(N'Reservada',N'Pagada',N'Activa',N'Agotada') OR Id=@id);
            """,c,t))
        {
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=codigo;q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=(object?)operacion??DBNull.Value;
            await using var r=await q.ExecuteReaderAsync(token);
            while(await r.ReadAsync(token))
            {
                var o=JsonSerializer.Deserialize<OperacionServicioTemporal>(r.GetString(0))??throw new InvalidOperationException("Operacion no disponible.");
                datos.Operaciones.Add(o.Id,o);existentes.Add(o.Id);
            }
        }
        T resultado=cambio(datos);
        if(datos.Operaciones.Values.Count(o=>o.Ocupa)>1)throw new InvalidOperationException("Ocupacion incompatible.");
        foreach(var o in datos.Operaciones.Values.OrderBy(o=>o.Ocupa?1:0))
        {
            string sql=existentes.Contains(o.Id)?"""
                UPDATE dbo.OperacionesServiciosTemporales SET TerminalId=@terminal,Estado=@estado,Monto=@monto,
                    Pagado=@pagado,PagoAnulado=@anulado,PagoId=@pago,PagoUtc=@pagoUtc,InicioUtc=@inicio,RetiroUtc=@retiro,DatosJson=@json WHERE Id=@id AND Codigo=@codigo;
                """:"""
                INSERT dbo.OperacionesServiciosTemporales(Id,Codigo,TerminalId,Estado,Monto,Pagado,PagoAnulado,PagoId,PagoUtc,CreadaUtc,InicioUtc,RetiroUtc,DatosJson)
                VALUES(@id,@codigo,@terminal,@estado,@monto,@pagado,@anulado,@pago,@pagoUtc,@creada,@inicio,@retiro,@json);
                """;
            await using var q=new SqlCommand(sql+" IF @@ROWCOUNT<>1 THROW 56202,'Operacion no confirmada.',1;",c,t);
            q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=o.Id;q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=codigo;
            q.Parameters.Add("@terminal",SqlDbType.Int).Value=(object?)o.TerminalId??DBNull.Value;
            q.Parameters.Add("@estado",SqlDbType.NVarChar,20).Value=o.Estado.ToString();
            var monto=q.Parameters.Add("@monto",SqlDbType.Decimal);monto.Precision=18;monto.Scale=2;monto.Value=o.Monto;
            q.Parameters.Add("@pagado",SqlDbType.Bit).Value=o.PagoId is not null;q.Parameters.Add("@anulado",SqlDbType.Bit).Value=o.PagoAnulado;
            q.Parameters.Add("@pago",SqlDbType.UniqueIdentifier).Value=(object?)o.PagoId??DBNull.Value;
            q.Parameters.Add("@pagoUtc",SqlDbType.DateTime2).Value=(object?)o.PagoUtc?.UtcDateTime??DBNull.Value;
            q.Parameters.Add("@creada",SqlDbType.DateTime2).Value=o.CreadaUtc.UtcDateTime;
            q.Parameters.Add("@inicio",SqlDbType.DateTime2).Value=(object?)o.InicioUtc?.UtcDateTime??DBNull.Value;
            q.Parameters.Add("@retiro",SqlDbType.DateTime2).Value=(object?)o.RetiroUtc?.UtcDateTime??DBNull.Value;
            q.Parameters.Add("@json",SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(o);
            await q.ExecuteNonQueryAsync(token);
        }
        await using(var q=new SqlCommand($"UPDATE {Inventario(configuracion)} SET Estado=@estado WHERE Codigo=@codigo; UPDATE dbo.CompartimentosServiciosTemporales SET ConfiguracionJson=@config WHERE Codigo=@codigo;",c,t))
        {
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=codigo;
            q.Parameters.Add("@estado",SqlDbType.NVarChar,20).Value=datos.EstadoInventario;
            q.Parameters.Add("@config",SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(datos.Configuracion);
            await q.ExecuteNonQueryAsync(token);
        }
        foreach(var e in datos.Eventos)
        {
            await using var q=new SqlCommand("INSERT dbo.AuditoriaServiciosTemporales(Codigo,OperacionId,Accion,Actor,FechaUtc)VALUES(@codigo,@id,@accion,@actor,@fecha)",c,t);
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=codigo;q.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=(object?)e.OperacionId??DBNull.Value;
            q.Parameters.Add("@accion",SqlDbType.NVarChar,80).Value=e.Accion;q.Parameters.Add("@actor",SqlDbType.NVarChar,60).Value=e.Actor;
            q.Parameters.Add("@fecha",SqlDbType.DateTime2).Value=e.FechaUtc.UtcDateTime;await q.ExecuteNonQueryAsync(token);
        }
        foreach(var incidencia in datos.Incidencias)
        {
            await using var q=new SqlCommand("INSERT dbo.IncidenciasServiciosTemporales(Codigo,Descripcion)VALUES(@codigo,@descripcion)",c,t);
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=codigo;q.Parameters.Add("@descripcion",SqlDbType.NVarChar,1000).Value=incidencia;
            await q.ExecuteNonQueryAsync(token);
        }
        await t.CommitAsync(token);return resultado;
    }

    public async Task ConfigurarAsync(ConfiguracionServicioTemporal datos,int administrador,CancellationToken token)
    {
        datos.Validar();await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);await using var t=(SqlTransaction)await c.BeginTransactionAsync(token);
        string? anterior;
        await using(var q=new SqlCommand("SELECT ConfiguracionJson FROM dbo.CompartimentosServiciosTemporales WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@codigo",c,t))
        {q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=datos.Codigo;anterior=await q.ExecuteScalarAsync(token) as string;}
        if(anterior is not null)
        {
            var previa=JsonSerializer.Deserialize<ConfiguracionServicioTemporal>(anterior)!;
            if(previa.Clase!=datos.Clase||previa.Conector!=datos.Conector)throw new ServicioTemporalNoDisponibleException();
        }
        string estado;
        try{estado=await EstadoInventarioAsync(c,t,datos,token);}
        catch(ServicioTemporalNoDisponibleException)when(anterior is null)
        {
            string crear=Inventario(datos)=="dbo.EspaciosCarga"?
                "INSERT dbo.EspaciosCarga(Codigo,Estado)VALUES(@codigo,N'Disponible')":
                "INSERT dbo.Casilleros(Codigo,Tamano,Tipo,Estado)VALUES(@codigo,@tamano,@tipo,N'Disponible')";
            await using var q=new SqlCommand(crear,c,t);q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=datos.Codigo;
            q.Parameters.Add("@tamano",SqlDbType.NVarChar,12).Value=datos.Tamano;
            q.Parameters.Add("@tipo",SqlDbType.NVarChar,20).Value=datos.Clase switch{ClaseServicioTemporal.Frio=>"Refrigerado",ClaseServicioTemporal.Calor=>"Caliente",_=>"Normal"};
            await q.ExecuteNonQueryAsync(token);estado="Disponible";
        }
        if(Inventario(datos)=="dbo.Casilleros")
        {
            await using var q=new SqlCommand("SELECT Tipo FROM dbo.Casilleros WHERE Codigo=@codigo",c,t);q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=datos.Codigo;
            string? tipo=await q.ExecuteScalarAsync(token) as string;
            bool compatible=datos.Clase switch{ClaseServicioTemporal.Personal=>tipo=="Normal",ClaseServicioTemporal.Frio=>tipo is "Frio" or "Refrigerado",ClaseServicioTemporal.Calor=>tipo is "Calor" or "Caliente",_=>false};
            if(!compatible)throw new ArgumentException("Tipo de casillero incompatible.");
        }
        string sql=anterior is null?"INSERT dbo.CompartimentosServiciosTemporales(Codigo,ConfiguracionJson)VALUES(@codigo,@json)":"UPDATE dbo.CompartimentosServiciosTemporales SET ConfiguracionJson=@json WHERE Codigo=@codigo";
        await using(var q=new SqlCommand(sql,c,t))
        {q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=datos.Codigo;q.Parameters.Add("@json",SqlDbType.NVarChar,-1).Value=JsonSerializer.Serialize(datos);await q.ExecuteNonQueryAsync(token);}
        if(estado is "Disponible" or "Bloqueado")
        {
            await using var q=new SqlCommand($"UPDATE {Inventario(datos)} SET Estado=@estado WHERE Codigo=@codigo",c,t);
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=datos.Codigo;
            q.Parameters.Add("@estado",SqlDbType.NVarChar,20).Value=datos.Habilitado&&datos.TemperaturaApta?"Disponible":"Bloqueado";await q.ExecuteNonQueryAsync(token);
        }
        await using(var q=new SqlCommand("INSERT dbo.AuditoriaServiciosTemporales(Codigo,Accion,Actor,FechaUtc)VALUES(@codigo,N'Configuracion actualizada',@actor,SYSUTCDATETIME());",c,t))
        {q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=datos.Codigo;q.Parameters.Add("@actor",SqlDbType.NVarChar,60).Value="Administrador:"+administrador;await q.ExecuteNonQueryAsync(token);}
        if(datos.Habilitado&&datos.TemperaturaApta)
        {
            await using var q=new SqlCommand("UPDATE dbo.IncidenciasServiciosTemporales SET ResueltaUtc=SYSUTCDATETIME() WHERE Codigo=@codigo AND ResueltaUtc IS NULL",c,t);
            q.Parameters.Add("@codigo",SqlDbType.NVarChar,10).Value=datos.Codigo;await q.ExecuteNonQueryAsync(token);
        }
        await t.CommitAsync(token);
    }
    public async Task<IReadOnlyList<string>> CodigosAsync(CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);await using var q=new SqlCommand("SELECT Codigo FROM dbo.CompartimentosServiciosTemporales ORDER BY Codigo",c);
        var resultado=new List<string>();await using var r=await q.ExecuteReaderAsync(token);while(await r.ReadAsync(token))resultado.Add(r.GetString(0));return resultado;
    }
    public async Task<ReporteServiciosTemporales> ReportarAsync(DateTimeOffset desde,DateTimeOffset hasta,CancellationToken token)
    {
        await using var c=new SqlConnection(Cadena);await c.OpenAsync(token);
        await using var q=new SqlCommand("""
            SELECT COALESCE(SUM(CASE WHEN CreadaUtc>=@desde AND CreadaUtc<@hasta THEN 1 ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN Estado IN(N'Activa',N'Agotada')THEN 1 ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN RetiroUtc>=@desde AND RetiroUtc<@hasta THEN 1 ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN Pagado=1 AND PagoAnulado=0 AND PagoUtc>=@desde AND PagoUtc<@hasta THEN Monto ELSE 0 END),0),
                (SELECT COUNT(*) FROM dbo.IncidenciasServiciosTemporales WHERE CreadaUtc>=@desde AND CreadaUtc<@hasta),
                COALESCE(SUM(CASE WHEN InicioUtc<@hasta AND (RetiroUtc IS NULL OR RetiroUtc>@desde)
                    THEN DATEDIFF_BIG(MINUTE,CASE WHEN InicioUtc<@desde THEN @desde ELSE InicioUtc END,
                         CASE WHEN RetiroUtc IS NULL OR RetiroUtc>@hasta THEN @hasta ELSE RetiroUtc END) ELSE 0 END),0)
            FROM dbo.OperacionesServiciosTemporales;
            """,c);
        q.Parameters.Add("@desde",SqlDbType.DateTime2).Value=desde.UtcDateTime;q.Parameters.Add("@hasta",SqlDbType.DateTime2).Value=hasta.UtcDateTime;
        await using var r=await q.ExecuteReaderAsync(token);await r.ReadAsync(token);return new(r.GetInt32(0),r.GetInt32(1),r.GetInt32(2),r.GetDecimal(3),r.GetInt32(4),r.GetInt64(5));
    }
}
