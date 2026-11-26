using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.Data.SqlClient;
using Synchro.Api.Services.Temporales;
using Synchro.Api.Models;

namespace Synchro.Api.Services.Encomiendas;

internal sealed record CrearReservaPrototipoSolicitud(string Servicio,string TipoCasillero,string TamanoSolicitado,
    string UbicacionId,int Minutos,string? TelefonoDestinatario=null);
internal sealed record ReservaPrototipo(string Servicio,string TipoCasillero,string CodigoCasillero,string UbicacionId,
    string Ubicacion,int Minutos,decimal? TarifaHnl);
internal sealed record OpcionReservaPrototipo(string Codigo,string Tipo,string Tamano,string UbicacionId,string Ubicacion,
    int[] Duraciones,Dictionary<int,decimal>? Tarifas);

internal sealed partial class EncomiendasOperativasSql
{
    private const string AccionReservaPrototipo="ReservaPrototipoCreada";
    private string UbicacionPrototipo=>config["ReservasPrototipo:Ubicacion"]?.Trim() is {Length:>0 and <=100} nombre?nombre:"Estación del prototipo · ubicación por configurar";
    private int[] DuracionesPrototipo=>config.GetSection("ReservasPrototipo:DuracionesMinutos").Get<int[]>() is {Length:>0} minutos
        ?minutos.Where(m=>m is >0 and <=1440).Distinct().Order().ToArray():[30,60,120];
    // La ausencia de una lista configurada NO autoriza a ofrecer todos los fixtures SQL.
    // Por defecto, solo se reservan los 10 compartimentos visibles en la tablet.
    // La lista explícita permite usar una estación de pruebas aislada en los tests.
    private string CodigosPrototipo=>config.GetSection("ReservasPrototipo:Casilleros").Get<string[]>() is {Length:>0} codigos
        ? JsonSerializer.Serialize(codigos.Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
        : InventarioEstacionPrototipo.CodigosJson;
    private static string? TipoReserva(string tipo)=>tipo switch{"Normal"=>"Normal","Refrigerado" or "Frio"=>"Refrigerado","Caliente" or "Calor"=>"Caliente",_=>null};

    private async Task<ReservaPrototipo?> LeerReserva(SqlConnection c,SqlTransaction? tx,int id,CancellationToken ct)
    {
        await using var q=Comando(c,tx,"SELECT TOP(1) Detalle FROM dbo.AuditoriaEncomiendaOperativa WHERE ReservaId=@id AND Accion=@accion ORDER BY Id",("@id",id),("@accion",AccionReservaPrototipo));
        return await q.ExecuteScalarAsync(ct) is string json?JsonSerializer.Deserialize<ReservaPrototipo>(json):null;
    }

    private static ConfiguracionServicioTemporal? ConfiguracionCompartimento(string? json,string tipo)
    {
        if(json is null)return null;
        try {
            var c=JsonSerializer.Deserialize<ConfiguracionServicioTemporal>(json);c?.Validar();
            return c is not null&&c.Habilitado&&c.TemperaturaApta&&
                (tipo=="Normal"&&c.Clase==ClaseServicioTemporal.Personal||tipo=="Refrigerado"&&c.Clase==ClaseServicioTemporal.Frio||tipo=="Caliente"&&c.Clase==ClaseServicioTemporal.Calor)?c:null;
        } catch(Exception e)when(e is JsonException or ArgumentException){return null;}
    }

    public async Task<object> CatalogoReservas(CancellationToken ct)
    {
        await using var c=await Abrir(ct);await LiberarReservasVencidas(c,ct);
        await using var q=Comando(c,null,"""
            SELECT c.Codigo,c.Tipo,c.Tamano,t.ConfiguracionJson FROM dbo.Casilleros c
            LEFT JOIN dbo.CompartimentosServiciosTemporales t ON t.Codigo=c.Codigo
            WHERE c.Estado=N'Disponible'
              AND (@codigos IS NULL OR c.Codigo IN(SELECT value FROM OPENJSON(@codigos)))
              AND NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero u WHERE u.CodigoCasillero=c.Codigo AND u.FechaRetiro IS NULL AND u.Estado IN(N'Reservado',N'Ocupado'))
              AND NOT EXISTS(SELECT 1 FROM dbo.OperacionesServiciosTemporales s WHERE s.Codigo=c.Codigo AND s.Estado IN(N'Reservada',N'Pagada',N'Activa',N'Agotada'))
            ORDER BY c.Codigo;
            """,("@codigos",CodigosPrototipo));
        var opciones=new List<OpcionReservaPrototipo>();await using var r=await q.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)) {
            var tipo=TipoReserva(InventarioEstacionPrototipo.TipoReal(r.GetString(0),r.GetString(1)));if(tipo is null)continue;
            var configuracion=ConfiguracionCompartimento(r.IsDBNull(3)?null:r.GetString(3),tipo);
            if((tipo!="Normal"||!r.IsDBNull(3))&&configuracion is null)continue;
            var duraciones=configuracion?.Tarifas.Where(t=>t.Value>0).Select(t=>t.Key).Order().ToArray()??DuracionesPrototipo;
            if(duraciones.Length==0)continue;
            opciones.Add(new(r.GetString(0),tipo,r.GetString(2)=="Pequeño"?"Pequeno":r.GetString(2),"prototipo",UbicacionPrototipo,duraciones,configuracion?.Tarifas));
        }
        return new{opciones,ubicacionPendiente=string.IsNullOrWhiteSpace(config["ReservasPrototipo:Ubicacion"]),
            capturaEnWebRequerida=false,capturaTablet="DemostrativaSinComparacion",hardwareSimulado=true};
    }

    private async Task<ReservaPrototipo> SeleccionarReserva(SqlConnection c,SqlTransaction tx,CrearReservaPrototipoSolicitud d,CancellationToken ct)
    {
        Exigir(d.Servicio is "Personal" or "Encomienda","ServicioInvalido","Selecciona pertenencias personales o encomienda.",400);
        Exigir(d.UbicacionId=="prototipo"&&d.TipoCasillero is "Normal" or "Refrigerado" or "Caliente","SeleccionInvalida","Selecciona un tipo y una ubicación disponibles.",400);
        // La regla es también del servidor: ocultar tarjetas no impide peticiones directas.
        Exigir(d.Servicio=="Encomienda"||d.TipoCasillero=="Normal",
            "CasilleroExclusivoEncomiendas",
            "Los casilleros refrigerados y calientes son exclusivos para encomiendas.",400);
        await using var q=Comando(c,tx,"""
            SELECT c.Codigo,c.Tipo,t.ConfiguracionJson FROM dbo.Casilleros c WITH(UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.CompartimentosServiciosTemporales t ON t.Codigo=c.Codigo
            WHERE c.Estado=N'Disponible' AND (c.Tamano=@tamano OR @tamano=N'Pequeno' AND c.Tamano=N'Pequeño')
              AND (@codigos IS NULL OR c.Codigo IN(SELECT value FROM OPENJSON(@codigos)))
              AND @tipo = CASE
                    WHEN c.Codigo=N'M04' THEN N'Refrigerado'
                    WHEN c.Codigo=N'M09' THEN N'Caliente'
                    WHEN c.Tipo=N'Frio' THEN N'Refrigerado'
                    WHEN c.Tipo=N'Calor' THEN N'Caliente'
                    ELSE c.Tipo END
              AND NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero u WHERE u.CodigoCasillero=c.Codigo AND u.FechaRetiro IS NULL AND u.Estado IN(N'Reservado',N'Ocupado'))
              AND NOT EXISTS(SELECT 1 FROM dbo.OperacionesServiciosTemporales s WHERE s.Codigo=c.Codigo AND s.Estado IN(N'Reservada',N'Pagada',N'Activa',N'Agotada'))
            ORDER BY c.Codigo;
            """,("@tipo",d.TipoCasillero),("@tamano",d.TamanoSolicitado),("@codigos",CodigosPrototipo));
        await using var r=await q.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)) {
            var configuracion=ConfiguracionCompartimento(r.IsDBNull(2)?null:r.GetString(2),d.TipoCasillero);
            if((d.TipoCasillero!="Normal"||!r.IsDBNull(2))&&configuracion is null)continue;
            if(!(configuracion is null?DuracionesPrototipo.Contains(d.Minutos):configuracion.Tarifas.TryGetValue(d.Minutos,out var importe)&&importe>0))continue;
            return new(d.Servicio,d.TipoCasillero,r.GetString(0),"prototipo",UbicacionPrototipo,d.Minutos,
                configuracion?.Tarifas.GetValueOrDefault(d.Minutos) is >0?configuracion.Tarifas[d.Minutos]:null);
        }
        throw new OperacionEncomiendaException("SinDisponibilidad","Ya no hay un casillero compatible con esa selección. Actualiza la disponibilidad.");
    }

    private async Task ExigirCompartimento(SqlConnection c,SqlTransaction tx,ReservaPrototipo? reserva,string codigo,string tamano,CancellationToken ct)
    {
        await using var q=Comando(c,tx,"""
            SELECT CASE WHEN c.Codigo=N'M04' THEN N'Frio' WHEN c.Codigo=N'M09' THEN N'Calor' ELSE c.Tipo END AS Tipo,
                   c.Estado,t.ConfiguracionJson FROM dbo.Casilleros c WITH(UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.CompartimentosServiciosTemporales t ON t.Codigo=c.Codigo WHERE c.Codigo=@codigo AND (c.Tamano=@tamano OR @tamano=N'Pequeno' AND c.Tamano=N'Pequeño');
            """,("@codigo",codigo),("@tamano",tamano));
        await using var r=await q.ExecuteReaderAsync(ct);Exigir(await r.ReadAsync(ct),"CasilleroNoDisponible","Casillero no disponible.");
        var tipo=TipoReserva(r.GetString(0));
        Exigir(reserva is null?r.GetString(1)=="Disponible"&&tipo=="Normal":reserva.CodigoCasillero==codigo&&reserva.TipoCasillero==tipo&&r.GetString(1)=="Reservado",
            "CasilleroNoDisponible","El casillero no corresponde a esta reserva o no está disponible.");
        if(tipo!="Normal"||!r.IsDBNull(2))Exigir(ConfiguracionCompartimento(r.IsDBNull(2)?null:r.GetString(2),tipo!) is not null,"CompartimentoNoCompatible","El compartimento no está habilitado o su temperatura está fuera del rango permitido.");
    }

    private static async Task LiberarCasilleroReserva(SqlConnection c,SqlTransaction tx,int id,ReservaPrototipo? reserva,DateTimeOffset ahora,CancellationToken ct)
    {
        if(reserva is null)return;
        await using var q=Comando(c,tx,"""
            UPDATE c SET Estado=N'Disponible' FROM dbo.Casilleros c
            WHERE c.Codigo=@codigo AND c.Estado=N'Reservado'
              AND NOT EXISTS(SELECT 1 FROM dbo.UsosCasillero u WHERE u.CodigoCasillero=c.Codigo AND u.FechaRetiro IS NULL AND u.Estado IN(N'Reservado',N'Ocupado'))
              AND NOT EXISTS(SELECT 1 FROM dbo.AuditoriaEncomiendaOperativa a JOIN dbo.EncomiendasOperativas o ON o.ReservaId=a.ReservaId
                JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=o.ReservaId WHERE a.Accion=N'ReservaPrototipoCreada' AND a.ReservaId<>@id
                  AND JSON_VALUE(a.Detalle,'$.CodigoCasillero')=@codigo AND o.Estado IN(N'Preparacion',N'ListaParaDeposito') AND r.FechaExpiracion>@ahora);
            """,("@id",id),("@codigo",reserva.CodigoCasillero),("@ahora",ahora.UtcDateTime));await q.ExecuteNonQueryAsync(ct);
    }

    private async Task LiberarReservasVencidas(SqlConnection c,CancellationToken ct)
    {
        var ids=new List<int>();await using(var q=Comando(c,null,"""
            SELECT o.ReservaId FROM dbo.EncomiendasOperativas o JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=o.ReservaId
            WHERE o.Estado IN(N'Preparacion',N'ListaParaDeposito') AND r.FechaExpiracion<=@ahora
              AND EXISTS(SELECT 1 FROM dbo.AuditoriaEncomiendaOperativa a WHERE a.ReservaId=o.ReservaId AND a.Accion=N'ReservaPrototipoCreada'
                AND (@codigos IS NULL OR JSON_VALUE(a.Detalle,'$.CodigoCasillero') IN(SELECT value FROM OPENJSON(@codigos))))
              AND NOT EXISTS(SELECT 1 FROM dbo.AuditoriaEncomiendaOperativa a WHERE a.ReservaId=o.ReservaId AND a.Accion=N'ReservaLiberadaPorVencimiento');
            """,("@ahora",Ahora.UtcDateTime),("@codigos",CodigosPrototipo))) {
            await using var r=await q.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))ids.Add(r.GetInt32(0));
        }
        foreach(var id in ids) {
            await using var tx=(SqlTransaction)await c.BeginTransactionAsync(ct);var o=await Leer(c,tx,id,null,ct);
            if(o.Estado is "Preparacion" or "ListaParaDeposito"&&o.Vence<=Ahora) {
                await LiberarCasilleroReserva(c,tx,id,await LeerReserva(c,tx,id,ct),Ahora,ct);
                await using(var q=Comando(c,tx,"UPDATE dbo.PinesEncomiendaOperativa SET Activo=0,RevocadoUtc=@ahora WHERE ReservaId=@id AND Activo=1",("@id",id),("@ahora",Ahora.UtcDateTime)))await q.ExecuteNonQueryAsync(ct);
                await Auditar(c,tx,id,null,"Sistema",0,"ReservaLiberadaPorVencimiento","Vencida","Plazo de depósito vencido; casillero liberado sin borrar la reserva.",ct);
            }
            await tx.CommitAsync(ct);
        }
    }
}
