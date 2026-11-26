# SYNCHRO - Diagnostico de movimientos (SOLO LECTURA).
# Ejecuta la misma consulta SQL del endpoint y presenta el error real.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$fuenteCs = Join-Path $raiz 'backend\Synchro.Api\Endpoints\AdminMovimientosEndpoints.cs'
if (-not (Test-Path -LiteralPath $fuenteCs)) {
    throw 'Falta AdminMovimientosEndpoints.cs. Primero actualiza la rama.'
}
$cadena = 'Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False;Connect Timeout=10'
$db = [System.Data.SqlClient.SqlConnection]::new($cadena)
try {
    $db.Open()
    Write-Host '=== DIAGNOSTICO MOVIMIENTOS (SOLO LECTURA) ===' -ForegroundColor Cyan

    $meta = $db.CreateCommand()
    $meta.CommandText = @'
SELECT DB_NAME() AS BaseDatos,
   (SELECT COUNT(*) FROM dbo.UsosCasillero WHERE FechaIngreso IS NOT NULL) AS UsosIniciados,
   (SELECT COUNT(*) FROM dbo.SesionesCargaSynchro WHERE InicioUtc IS NOT NULL) AS SesionesCargaIniciadas;
'@
    $resumen = [System.Data.DataTable]::new()
    $adapt = [System.Data.SqlClient.SqlDataAdapter]::new($meta)
    [void]$adapt.Fill($resumen)
    $resumen | Format-Table -AutoSize

    $codigo = Get-Content -LiteralPath $fuenteCs -Raw
    $m = [regex]::Match(
        $codigo,
        'private\s+const\s+string\s+Fuente\s*=\s*"""(?<sql>[\s\S]*?)""";',
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )
    if (-not $m.Success) { throw 'No se encontro la consulta Fuente en el backend.' }
    $fuente = $m.Groups['sql'].Value
    $nl = [Environment]::NewLine
    $filtros = @'
WHERE (@servicio=N'Todos' OR Servicio=@servicio)
  AND (@codigo IS NULL OR CHARINDEX(@codigo, Codigo)>0)
'@
    $sql = $fuente + $nl + 'SELECT COUNT(*) AS Total FROM Registro ' +
        $nl + $filtros + ';' + $nl + $fuente + $nl +
        'SELECT MovimientoId,Codigo,Servicio,Estado,InicioUtc,FinUtc,Detalle,UsoId,EncomiendaId,SesionId FROM Registro ' +
        $nl + $filtros +
        ' ORDER BY InicioUtc DESC,MovimientoId DESC OFFSET @salto ROWS FETCH NEXT @cantidad ROWS ONLY;'

    $cmd = $db.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 30
    [void]$cmd.Parameters.Add('@servicio',[System.Data.SqlDbType]::NVarChar,20)
    $cmd.Parameters['@servicio'].Value = 'Todos'
    [void]$cmd.Parameters.Add('@codigo',[System.Data.SqlDbType]::NVarChar,20)
    $cmd.Parameters['@codigo'].Value = [DBNull]::Value
    [void]$cmd.Parameters.Add('@salto',[System.Data.SqlDbType]::Int)
    $cmd.Parameters['@salto'].Value = 0
    [void]$cmd.Parameters.Add('@cantidad',[System.Data.SqlDbType]::Int)
    $cmd.Parameters['@cantidad'].Value = 5

    $datos = [System.Data.DataSet]::new()
    $leer = [System.Data.SqlClient.SqlDataAdapter]::new($cmd)
    [void]$leer.Fill($datos)
    Write-Host '[OK] Consulta de Movimientos ejecutada:' -ForegroundColor Green
    $datos.Tables[0] | Format-Table -AutoSize
    if ($datos.Tables.Count -gt 1) {
        $datos.Tables[1] |
          Select-Object MovimientoId,Codigo,Servicio,Estado,InicioUtc,FinUtc |
          Format-Table -AutoSize
    }
}
catch {
    Write-Host '[ERROR] La consulta no se pudo completar.' -ForegroundColor Red
    $e = $_.Exception.GetBaseException()
    if ($e -is [System.Data.SqlClient.SqlException]) {
        foreach ($detalle in $e.Errors) {
            Write-Host ("SQL numero {0}; linea {1}; mensaje: {2}" -f
                $detalle.Number, $detalle.LineNumber, $detalle.Message) -ForegroundColor Yellow
        }
    } else {
        Write-Host $e.Message -ForegroundColor Yellow
    }
}
finally {
    $db.Dispose()
}
