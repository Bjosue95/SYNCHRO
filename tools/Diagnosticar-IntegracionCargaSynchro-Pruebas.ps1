# SYNCHRO - comprobacion de integracion real de carga contra SQL aislado.
# SOLO LECTURA: no crear, eliminar, actualizar ni cerrar sesiones.
# No imprimir PIN, rostro, descriptores, tokens ni datos personales.
$ErrorActionPreference = 'Stop'

$cadena = 'Data Source=(localdb)\SYNCHRO_Aislado;Initial Catalog=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10'
$conexion = [System.Data.SqlClient.SqlConnection]::new($cadena)

function LeerTabla([System.Data.SqlClient.SqlConnection]$db, [string]$sql) {
    $comando = $db.CreateCommand()
    $comando.CommandText = $sql
    $tabla = [System.Data.DataTable]::new()
    try {
        $lector = $comando.ExecuteReader()
        try { $tabla.Load($lector) }
        finally { $lector.Dispose() }
    }
    finally { $comando.Dispose() }
    return ,$tabla
}

function Validar([bool]$bien, [string]$descripcion) {
    if ($bien) { Write-Host "[OK] $descripcion" }
    else { throw "[DETENIDO] $descripcion" }
}

try {
    Write-Host '=== SYNCHRO: INTEGRACION DE CARGA REAL (SOLO LECTURA) ==='
    $conexion.Open()

    $base = LeerTabla $conexion @'
SELECT DB_NAME() AS BaseDatos,
       CASE WHEN EXISTS(
          SELECT 1 FROM sys.extended_properties
          WHERE class=0 AND name=N'SynchroFixtureDeposito'
            AND CONVERT(nvarchar(10),value)=N'1'
       ) THEN 1 ELSE 0 END AS Fixture,
       CASE WHEN OBJECT_ID(N'dbo.EspaciosCarga',N'U') IS NOT NULL
          AND OBJECT_ID(N'dbo.SesionesCargaSynchro',N'U') IS NOT NULL
          AND OBJECT_ID(N'dbo.CredencialesCargaSynchro',N'U') IS NOT NULL
          AND OBJECT_ID(N'dbo.TarifasCargaSynchro',N'U') IS NOT NULL
          AND OBJECT_ID(N'dbo.CobrosRetiroCargaSynchro',N'U') IS NOT NULL
       THEN 1 ELSE 0 END AS Esquema;
'@
    Validar ($base.Rows.Count -eq 1 -and $base.Rows[0].BaseDatos -ceq 'SynchroDB_Pruebas') 'Base SQL aislada SynchroDB_Pruebas'
    Validar ([int]$base.Rows[0].Fixture -eq 1) 'Marcador del prototipo autorizado'
    Validar ([int]$base.Rows[0].Esquema -eq 1) 'Tablas de espacios, sesiones, identidad, tarifas y cobros'

    Write-Host ''
    Write-Host '=== TARIFAS CONFIGURADAS ==='
    $tarifas = LeerTabla $conexion @'
SELECT Modalidad, MinutosBase, PrecioBase,
       MinutosAdicionales, PrecioAdicional
FROM dbo.TarifasCargaSynchro
ORDER BY CASE Modalidad WHEN N'Inalambrica' THEN 1
  WHEN N'Cable' THEN 2 WHEN N'Laptop' THEN 3 ELSE 4 END;
'@
    if ($tarifas.Rows.Count -eq 0) {
        Write-Host '[PENDIENTE] No hay tarifas configuradas.'
    } else {
        $tarifas | Format-Table -AutoSize | Out-String | Write-Host
    }

    Write-Host '=== ESPACIOS Y SESIONES REALES ==='
    $espacios = LeerTabla $conexion @'
SELECT e.Codigo,
       CASE e.Codigo
         WHEN N'W1' THEN N'Inalambrica'
         WHEN N'W2' THEN N'Inalambrica'
         WHEN N'L1' THEN N'Laptop'
         WHEN N'L2' THEN N'Laptop'
         ELSE N'Cable' END AS Modalidad,
       e.Estado AS EstadoEspacio,
       (SELECT COUNT(*) FROM dbo.SesionesCargaSynchro s
        WHERE s.CodigoEspacio=e.Codigo
          AND s.Estado=N'Activa' AND s.RetiroUtc IS NULL) AS Activas,
       ult.Estado AS UltimaSesion,
       ult.Cobro AS EstadoCobro,
       ult.ImporteHnl
FROM dbo.EspaciosCarga AS e
OUTER APPLY (
    SELECT TOP(1) s.Estado, c.Estado AS Cobro, c.ImporteHnl
    FROM dbo.SesionesCargaSynchro s
    LEFT JOIN dbo.CobrosRetiroCargaSynchro c
      ON c.SesionId=s.Id
    WHERE s.CodigoEspacio=e.Codigo
    ORDER BY s.FechaCreacionUtc DESC, s.Id DESC
) ult
WHERE e.Codigo IN
  (N'W1',N'W2',N'C1',N'C2',N'C3',N'C4',N'L1',N'L2')
ORDER BY CASE
  WHEN e.Codigo=N'W1' THEN 1 WHEN e.Codigo=N'W2' THEN 2
  WHEN e.Codigo=N'C1' THEN 3 WHEN e.Codigo=N'C2' THEN 4
  WHEN e.Codigo=N'C3' THEN 5 WHEN e.Codigo=N'C4' THEN 6
  WHEN e.Codigo=N'L1' THEN 7 WHEN e.Codigo=N'L2' THEN 8
  ELSE 9 END;
'@
    if ($espacios.Rows.Count -eq 0) {
        Write-Host '[PENDIENTE] No aparecen espacios de carga en SQL.'
    } else {
        $espacios | Format-Table -AutoSize | Out-String | Write-Host
        foreach ($fila in $espacios.Rows) {
            if ([int]$fila.Activas -gt 1) {
                Write-Host "[ATENCION] $($fila.Codigo): varias sesiones activas."
            }
            if ([int]$fila.Activas -gt 0 -and $fila.EstadoEspacio -ne 'Ocupado') {
                Write-Host "[ATENCION] $($fila.Codigo): sesion activa pero espacio no ocupado."
            }
        }
    }

    Write-Host '=== COBROS EN EL BACKEND ==='
    $cobros = LeerTabla $conexion @'
SELECT c.Estado AS EstadoCobro,
       COUNT(*) AS Cantidad,
       SUM(c.ImporteHnl) AS TotalHnl
FROM dbo.CobrosRetiroCargaSynchro AS c
GROUP BY c.Estado
ORDER BY c.Estado;
'@
    if ($cobros.Rows.Count -eq 0) {
        Write-Host 'Todavia no hay cobros de carga registrados.'
    } else {
        $cobros | Format-Table -AutoSize | Out-String | Write-Host
    }

    Write-Host '=== COBROS VISIBLES PARA EL ADMINISTRADOR ==='
    $pendientes = LeerTabla $conexion @'
SELECT c.CodigoEspacio, s.Modalidad,
       c.MinutosUtilizados, c.ImporteHnl,
       c.Estado AS EstadoCobro,
       CASE c.MetodoPago
         WHEN N'EfectivoSimulado' THEN N'Efectivo'
         WHEN N'TarjetaSimulada' THEN N'Tarjeta'
         WHEN N'NfcSimulado' THEN N'NFC'
         ELSE N'No seleccionado' END AS Metodo
FROM dbo.CobrosRetiroCargaSynchro c
INNER JOIN dbo.SesionesCargaSynchro s ON s.Id=c.SesionId
INNER JOIN dbo.EspaciosCarga e ON e.Codigo=c.CodigoEspacio
WHERE c.Estado=N'Pendiente'
  AND c.MetodoPago IS NOT NULL
  AND s.CodigoEspacio=c.CodigoEspacio
  AND s.Estado=N'Activa' AND s.RetiroUtc IS NULL
  AND e.Estado=N'Ocupado'
ORDER BY c.CreadoUtc DESC;
'@
    if ($pendientes.Rows.Count -eq 0) {
        Write-Host 'No hay cobros esperando aprobacion administrativa.'
    } else {
        $pendientes | Format-Table -AutoSize | Out-String | Write-Host
    }

    Write-Host '[OK] Diagnostico completado. NO se modifico ninguna sesion.'
}
catch {
    Write-Host ("[ERROR] " + $_.Exception.Message)
    throw
}
finally {
    $conexion.Dispose()
}
