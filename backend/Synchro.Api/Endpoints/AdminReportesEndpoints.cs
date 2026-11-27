using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Synchro.Api.Models;
using Synchro.Api.Services;

namespace Synchro.Api.Endpoints;

// Solo lectura, sin datos personales. Se limita a la estación física del prototipo.
public static class AdminReportesEndpoints
{
    private const int Limite = 30000;
    private const string Consulta = """
        DECLARE @inventario TABLE (Codigo nvarchar(20) PRIMARY KEY, Tipo nvarchar(40), Tamano nvarchar(40), Estado nvarchar(40));
        INSERT @inventario
        SELECT c.Codigo, CASE WHEN c.Codigo=N'M04' THEN N'Refrigerado' WHEN c.Codigo=N'M09' THEN N'Caliente' ELSE N'Normal' END,
               c.Tamano,c.Estado
        FROM dbo.Casilleros c JOIN OPENJSON(@codigos) j ON j.value=c.Codigo;
        INSERT @inventario
        SELECT e.Codigo,CASE WHEN e.Codigo LIKE N'W%' THEN N'Inalambrica' WHEN e.Codigo LIKE N'C%' THEN N'Cable' ELSE N'Laptop' END,
               N'No aplica',e.Estado
        FROM dbo.EspaciosCarga e WHERE e.Codigo IN(N'W1',N'W2',N'C1',N'C2',N'C3',N'C4',N'L1',N'L2');

        SELECT Codigo,Tipo,Tamano,Estado FROM @inventario ORDER BY Codigo;

        ;WITH Usos AS (
          SELECT u.CodigoCasillero Codigo,
            CASE WHEN u.TipoServicio=N'ResguardoPersonal' THEN N'Resguardo' ELSE N'Encomienda' END Servicio,
            u.Estado, u.FechaIngreso Inicio, u.FechaRetiro Fin
          FROM dbo.UsosCasillero u JOIN @inventario i ON i.Codigo=u.CodigoCasillero
          WHERE u.TipoServicio IN(N'ResguardoPersonal',N'Encomienda') AND u.FechaIngreso IS NOT NULL
          UNION ALL
          SELECT s.CodigoEspacio,N'Carga',s.Estado,s.InicioUtc,s.RetiroUtc
          FROM dbo.SesionesCargaSynchro s JOIN @inventario i ON i.Codigo=s.CodigoEspacio
        )
        SELECT TOP(30001) Codigo,Servicio,Estado,Inicio,Fin FROM Usos
        WHERE Inicio<@hasta AND (Fin>=@anterior OR (Fin IS NULL AND Estado IN(N'Ocupado',N'Activa')) OR Inicio>=@anterior)
        ORDER BY Inicio;

        -- Una fila por cobro confirmado. No sumar el importe de la reserva otra vez al retirar.
        ;WITH Pagos AS (
          SELECT u.CodigoCasillero Codigo,N'Resguardo' Servicio,N'Tablet' Origen,
            c.MetodoPago Metodo,c.ImporteHnl Importe,c.ConfirmadoUtc Fecha
          FROM dbo.CobrosRetiroPersonal c JOIN dbo.UsosCasillero u ON u.Id=c.UsoId
          WHERE c.Estado IN(N'ConfirmadoSimulado',N'Aplicado') AND u.OrigenReserva=N'Terminal'
          UNION ALL
          SELECT c.CodigoEspacio,N'Carga',N'Tablet',c.MetodoPago,c.ImporteHnl,c.ConfirmadoUtc
          FROM dbo.CobrosRetiroCargaSynchro c WHERE c.Estado IN(N'ConfirmadoSimulado',N'Aplicado')
          UNION ALL
          SELECT COALESCE(u.CodigoCasillero,JSON_VALUE(CASE WHEN ISJSON(a.Detalle)=1 THEN a.Detalle ELSE N'{}' END,'$.CodigoCasillero')),
            CASE WHEN JSON_VALUE(CASE WHEN ISJSON(a.Detalle)=1 THEN a.Detalle ELSE N'{}' END,'$.Servicio')=N'Personal' THEN N'Resguardo' ELSE N'Encomienda' END,
            CASE WHEN JSON_VALUE(CASE WHEN ISJSON(a.Detalle)=1 THEN a.Detalle ELSE N'{}' END,'$.ModalidadPago')=N'WebTarjeta' THEN N'Web' ELSE N'Tablet' END,
            CASE WHEN m.Detalle IN(N'Efectivo',N'Tarjeta',N'NFC') THEN m.Detalle ELSE o.MetodoPago END,
            o.ImporteHnl,o.PagoConfirmadoUtc
          FROM dbo.EncomiendasOperativas o
          LEFT JOIN dbo.UsosCasillero u ON u.Id=o.UsoId
          CROSS APPLY (SELECT TOP(1) Detalle FROM dbo.AuditoriaEncomiendaOperativa
                       WHERE ReservaId=o.ReservaId AND Accion=N'ReservaPrototipoCreada' ORDER BY Id) a
          OUTER APPLY (SELECT TOP(1) Detalle FROM dbo.AuditoriaEncomiendaOperativa
                       WHERE ReservaId=o.ReservaId AND Accion=N'PagoTabletPendiente' AND FechaUtc<=o.PagoConfirmadoUtc ORDER BY Id DESC) m
          WHERE o.PagoEstado=N'ConfirmadoManual'
            AND JSON_VALUE(CASE WHEN ISJSON(a.Detalle)=1 THEN a.Detalle ELSE N'{}' END,'$.ModalidadPago') IN(N'WebTarjeta',N'Tablet')
            AND (o.PagoReferencia LIKE N'WEB-PRUEBA-%' OR o.PagoReferencia LIKE N'TABLET-PRUEBA-%')
        )
        SELECT TOP(30001) p.Codigo,p.Servicio,p.Origen,p.Metodo,p.Importe,p.Fecha
        FROM Pagos p JOIN @inventario i ON i.Codigo=p.Codigo
        WHERE p.Fecha>=@anterior AND p.Fecha<@hasta AND p.Importe>0 ORDER BY p.Fecha;

        SELECT TOP(30001) i.Codigo,CASE WHEN JSON_VALUE(j.Json,'$.Servicio')=N'Personal' THEN N'Resguardo' ELSE N'Encomienda' END,
           o.Estado,o.PagoEstado,r.FechaCreacion
        FROM dbo.EncomiendasOperativas o JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=o.ReservaId
        CROSS APPLY (SELECT TOP(1) Detalle FROM dbo.AuditoriaEncomiendaOperativa
                     WHERE ReservaId=o.ReservaId AND Accion=N'ReservaPrototipoCreada' ORDER BY Id) a
        CROSS APPLY (SELECT CASE WHEN ISJSON(a.Detalle)=1 THEN a.Detalle ELSE N'{}' END AS Json) j
        JOIN @inventario i ON i.Codigo=JSON_VALUE(j.Json,'$.CodigoCasillero')
        WHERE r.FechaCreacion>=@desde AND r.FechaCreacion<@hasta ORDER BY r.FechaCreacion;
        """;

    private static string Metodo(string? valor) => valor switch
    {
        "Efectivo" or "EfectivoSimulado" => "Efectivo",
        "Tarjeta" or "TarjetaSimulada" or "TarjetaTerminalSimulada" or "WebSimulado" => "Tarjeta",
        "NFC" or "NfcSimulado" => "NFC",
        _ => "Sin clasificar"
    };

    private static DateTime Utc(SqlDataReader r,int i) => DateTime.SpecifyKind(r.GetDateTime(i),DateTimeKind.Utc);
    // Honduras usa UTC-06:00; la fecha final del usuario es inclusiva.
    private static DateTime LimiteUtc(DateOnly dia) => DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue).AddHours(6),DateTimeKind.Utc);

    public static void MapAdminReportesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/admin/reportes", async (string? desde,string? hasta,HttpContext http,IConfiguration config) =>
        {
            http.Response.Headers.CacheControl="no-store";
            if(!app.Environment.IsDevelopment()) return Results.NotFound();
            if(!http.Request.IsHttps) return Results.StatusCode(403);
            var hoy=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6));
            if(!DateOnly.TryParseExact(desde,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var inicio) ||
               !DateOnly.TryParseExact(hasta,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var fin) ||
               inicio<new DateOnly(2000,1,1) || fin>hoy || inicio>fin || fin.DayNumber-inicio.DayNumber>=366)
                return Results.BadRequest(new {mensaje="Selecciona un intervalo válido de hasta 366 días, sin fechas futuras."});
            var dias=fin.DayNumber-inicio.DayNumber+1;
            var anterior=LimiteUtc(inicio.AddDays(-dias));
            var comienzo=LimiteUtc(inicio);
            var finTeorico=LimiteUtc(fin.AddDays(1));
            var ahora=DateTime.UtcNow;
            var cierre=finTeorico<ahora?finTeorico:ahora;
            try
            {
                var cadena=config.GetConnectionString("SynchroDB");
                if(string.IsNullOrWhiteSpace(cadena))return Results.Problem(statusCode:503,title:"Conexión SQL no configurada.");
                if(!ConexionSqlAislada.EsWhatsAppPermitido(new SqlConnectionStringBuilder(cadena)))return Results.NotFound();
                await using var db=new SqlConnection(cadena);
                await db.OpenAsync(http.RequestAborted);
                await using(var validar=new SqlCommand("""
                    SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas' AND EXISTS(
                      SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito'
                      AND CONVERT(nvarchar(10),value)=N'1') THEN 1 ELSE 0 END;
                    """,db))
                    if(Convert.ToInt32(await validar.ExecuteScalarAsync(http.RequestAborted))!=1)return Results.NotFound();

                await using var cmd=new SqlCommand(Consulta,db){CommandTimeout=30};
                cmd.Parameters.Add("@codigos",SqlDbType.NVarChar,-1).Value=InventarioEstacionPrototipo.CodigosJson;
                cmd.Parameters.Add("@anterior",SqlDbType.DateTime2).Value=anterior;
                cmd.Parameters.Add("@desde",SqlDbType.DateTime2).Value=comienzo;
                cmd.Parameters.Add("@hasta",SqlDbType.DateTime2).Value=cierre;
                var espacios=new List<object>();var usos=new List<object>();var pagos=new List<object>();var reservas=new List<object>();
                await using var r=await cmd.ExecuteReaderAsync(http.RequestAborted);
                while(await r.ReadAsync(http.RequestAborted))
                    espacios.Add(new{codigo=r.GetString(0),tipo=r.GetString(1),tamano=r.GetString(2),estado=r.GetString(3)});
                if(espacios.Count!=InventarioEstacionPrototipo.Codigos.Length+8)
                    return Results.Problem(statusCode:503,title:"Inventario de la estación incompleto.");
                await r.NextResultAsync(http.RequestAborted);
                while(await r.ReadAsync(http.RequestAborted))
                    usos.Add(new{codigo=r.GetString(0),servicio=r.GetString(1),estado=r.GetString(2),inicioUtc=Utc(r,3),finUtc=r.IsDBNull(4)?(DateTime?)null:Utc(r,4)});
                await r.NextResultAsync(http.RequestAborted);
                while(await r.ReadAsync(http.RequestAborted))
                    pagos.Add(new{codigo=r.GetString(0),servicio=r.GetString(1),origen=r.GetString(2),metodo=Metodo(r.IsDBNull(3)?null:r.GetString(3)),importeHnl=r.GetDecimal(4),fechaUtc=Utc(r,5)});
                await r.NextResultAsync(http.RequestAborted);
                while(await r.ReadAsync(http.RequestAborted))
                    reservas.Add(new{codigo=r.GetString(0),servicio=r.GetString(1),estado=r.GetString(2),pagoEstado=r.GetString(3),creadoUtc=Utc(r,4)});
                if(usos.Count>Limite||pagos.Count>Limite||reservas.Count>Limite)
                    return Results.BadRequest(new{mensaje="Hay demasiados registros. Selecciona un período más corto."});
                return Results.Ok(new{desdeUtc=comienzo,hastaUtc=cierre,anteriorUtc=anterior,consultadoUtc=ahora,espacios,usos,pagos,reservas,pagoSimulado=true});
            }
            catch(SqlException){return Results.Problem(statusCode:503,title:"No se pudieron consultar los reportes. Verifica el esquema de la base de pruebas.");}
            catch(ArgumentException){return Results.NotFound();}
        }).RequireAuthorization("VerReportes").RequireRateLimiting("EncomiendasOperativas");
    }
}
