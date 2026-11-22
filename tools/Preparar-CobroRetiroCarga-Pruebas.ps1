# SYNCHRO: preparar catalogo y cobros de carga SIN recrear tablas existentes.
# No inventa tarifas ni modifica sesiones activas.
$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$rutaSql = Join-Path $raiz "backend\Synchro.Api\Database\Migrations\20261122_CobroRetiroCargaSynchro_Pruebas.sql"
if (-not (Test-Path -LiteralPath $rutaSql -PathType Leaf)) {
    throw "No se encontro la migracion versionada."
}

$cadena = "Data Source=(localdb)\SYNCHRO_Aislado;Initial Catalog=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
$conexion = [System.Data.SqlClient.SqlConnection]::new($cadena)

try {
    $conexion.Open()
    $verificar = $conexion.CreateCommand()
    $verificar.CommandText = @'
SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas'
AND EXISTS (SELECT 1 FROM sys.extended_properties
  WHERE class=0 AND name=N'SynchroFixtureDeposito'
    AND CONVERT(nvarchar(10),value)=N'1')
AND OBJECT_ID(N'dbo.SesionesCargaSynchro',N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.EspaciosCarga',N'U') IS NOT NULL
THEN 1 ELSE 0 END
'@
    if ([int]$verificar.ExecuteScalar() -ne 1) {
        throw "Base de pruebas, marcador o esquema necesario no comprobado. No se aplico nada."
    }
    Write-Host "[OK] Base aislada y esquema existente verificados."
    $confirmacion = Read-Host "Para comprobar y preparar las tablas de carga escribe PREPARAR COBRO"
    if ($confirmacion -cne "PREPARAR COBRO") {
        Write-Host "Operacion cancelada. Sin cambios."
        return
    }
    $cmd = $conexion.CreateCommand()
    $cmd.CommandTimeout = 45
    $cmd.CommandText = Get-Content -LiteralPath $rutaSql -Raw
    $null = $cmd.ExecuteNonQuery()
    Write-Host "[OK] Catalogo de tarifas y cobros de carga preparados; sesiones sin cambios."
    Write-Host "=== TARIFAS DE CARGA EXISTENTES ==="
    $tarifas = $conexion.CreateCommand()
    $tarifas.CommandText = "SELECT Modalidad, MinutosBase, PrecioBase, MinutosAdicionales, PrecioAdicional FROM dbo.TarifasCargaSynchro ORDER BY Modalidad"
    $lector = $tarifas.ExecuteReader()
    try {
        while ($lector.Read()) {
            Write-Host ("{0}: {1} min base L {2}; {3} min extra L {4}" -f
                $lector.GetString(0), $lector.GetInt32(1),
                $lector.GetValue(2), $lector.GetInt32(3), $lector.GetValue(4))
        }
    } finally {
        $lector.Close()
    }
    Write-Host "Si no hay tarifas listadas, ejecutar el configurador de tarifa." 
    Write-Host "=== REGLAS DE ESTADO EN SESIONES DE CARGA ==="
    $reglas = $conexion.CreateCommand()
    $reglas.CommandText = @'
SELECT name, definition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.SesionesCargaSynchro')
  AND definition LIKE N'%Estado%'
'@
    $lectorReglas = $reglas.ExecuteReader()
    try {
        $cantidad = 0
        while ($lectorReglas.Read()) {
            $cantidad++
            Write-Host $lectorReglas.GetString(0)
            Write-Host $lectorReglas.GetString(1)
        }
        if ($cantidad -eq 0) { Write-Host "No se registraron restricciones CHECK de estado." }
    } finally { $lectorReglas.Close() }
} catch {
    Write-Host ("OPERACION DETENIDA: " + $_.Exception.Message)
    throw
} finally {
    $conexion.Dispose()
}
