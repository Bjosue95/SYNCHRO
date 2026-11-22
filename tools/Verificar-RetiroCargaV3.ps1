# SYNCHRO V3 - preflight SOLO LECTURA del retiro de dispositivos.
# No muestra credenciales ni modifica sesiones, cobros o precios.
$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $raiz "backend\Synchro.Api\Synchro.Api.csproj"
$front = Join-Path $raiz "frontend\tablet-app\src\views\RetiroCargaPagoView.vue"
$valido = $true
function Estado([string]$nombre, [bool]$ok) {
    if ($ok) { Write-Host "[OK] $nombre" }
    else { Write-Host "[PENDIENTE] $nombre"; $script:valido = $false }
}
Write-Host "=== SYNCHRO V3: VERIFICACION DE CIERRE DE CARGA (SOLO LECTURA) ==="
Estado "Backend" (Test-Path $backend -PathType Leaf)
Estado "Pantalla de pago de carga" (Test-Path $front -PathType Leaf)

$cadena = "Data Source=(localdb)\SYNCHRO_Aislado;Initial Catalog=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
$db = [System.Data.SqlClient.SqlConnection]::new($cadena)
try {
    $db.Open()
    $cmd = $db.CreateCommand()
    $cmd.CommandText = @'
SELECT
  DB_NAME(),
  CASE WHEN EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class = 0 AND name=N'SynchroFixtureDeposito'
      AND CONVERT(nvarchar(10),value)=N'1'
  ) THEN 1 ELSE 0 END,
  CASE WHEN OBJECT_ID(N'dbo.CobrosRetiroCargaSynchro',N'U') IS NOT NULL
    THEN 1 ELSE 0 END,
  CASE WHEN OBJECT_ID(N'dbo.TarifasCarga',N'U') IS NOT NULL
    THEN 1 ELSE 0 END,
  CASE WHEN OBJECT_ID(N'dbo.SesionesCargaSynchro',N'U') IS NOT NULL
    THEN 1 ELSE 0 END,
  CASE WHEN OBJECT_ID(N'dbo.EspaciosCarga',N'U') IS NOT NULL
    THEN 1 ELSE 0 END;
'@
    $reader = $cmd.ExecuteReader()
    if (-not $reader.Read()) { throw "Sin respuesta de SQL." }
    $nombre = $reader.GetString(0)
    $marcador = ($reader.GetInt32(1) -eq 1)
    $cobro = ($reader.GetInt32(2) -eq 1)
    $tarifas = ($reader.GetInt32(3) -eq 1)
    $sesiones = ($reader.GetInt32(4) -eq 1)
    $espacios = ($reader.GetInt32(5) -eq 1)
    $reader.Close()

    Estado "Base SynchroDB_Pruebas" ($nombre -ceq "SynchroDB_Pruebas")
    Estado "Marcador de fixture autorizado" $marcador
    Estado "Tabla nueva de cobro" $cobro
    Estado "Tarifas de carga existentes" $tarifas
    Estado "Sesiones de carga" $sesiones
    Estado "Espacios de carga" $espacios

    if (-not ($marcador -and $sesiones -and $espacios -and $tarifas)) {
        throw "Faltan elementos necesarios. No se consultaran datos operativos."
    }

    $tar = $db.CreateCommand()
    $tar.CommandText = @'
SELECT TipoDispositivo, MinutosBase, PrecioBase,
       MinutosAdicionales, PrecioAdicional
FROM dbo.TarifasCarga ORDER BY TipoDispositivo;
'@
    Write-Host "=== TARIFAS EXISTENTES (NO MODIFICADAS) ==="
    $r = $tar.ExecuteReader()
    $n = 0
    try {
        while ($r.Read()) {
            $n++
            Write-Host ("{0}: {1} min/L {2}; extra {3} min/L {4}" -f
                $r.GetString(0), $r.GetInt32(1), $r.GetValue(2),
                $r.GetInt32(3), $r.GetValue(4))
        }
    } finally { $r.Close() }
    Estado "Al menos una tarifa configurada" ($n -gt 0)

    $restricciones = $db.CreateCommand()
    $restricciones.CommandText = @'
SELECT name, definition
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.SesionesCargaSynchro')
  AND definition LIKE N'%Estado%';
'@
    Write-Host "=== RESTRICCIONES DEL ESTADO DE SESION ==="
    $r = $restricciones.ExecuteReader()
    $n = 0
    try {
        while ($r.Read()) {
            $n++
            Write-Host ("{0}: {1}" -f $r.GetString(0), $r.GetString(1))
        }
    } finally { $r.Close() }
    if ($n -eq 0) { Write-Host "(Sin CHECK de Estado detectado)" }

    $actual = $db.CreateCommand()
    $actual.CommandText = @'
SELECT e.Codigo, e.Estado,
       (SELECT COUNT(*)
        FROM dbo.SesionesCargaSynchro AS s
        WHERE s.CodigoEspacio=e.Codigo
          AND s.Estado=N'Activa' AND s.RetiroUtc IS NULL) AS SesionesActivas
FROM dbo.EspaciosCarga AS e
WHERE e.Codigo=N'W1';
'@
    Write-Host "=== ESTADO DE W1 ==="
    $r = $actual.ExecuteReader()
    try {
        while ($r.Read()) {
            Write-Host ("Espacio {0}: {1}; sesiones activas: {2}" -f
                $r.GetString(0), $r.GetString(1), $r.GetInt32(2))
        }
    } finally { $r.Close() }

    Write-Host "=== FIN DEL DIAGNOSTICO ==="
    if ($valido) {
        Write-Host "[OK] Elementos principales encontrados. Aun falta compilacion y prueba real."
    } else {
        Write-Host "[PENDIENTE] Corregir estados mostrados antes de completar el retiro."
    }
}
catch {
    Write-Host ("[DETENIDO] " + $_.Exception.Message)
    exit 1
}
finally { $db.Dispose() }
if (-not $valido) { exit 1 }
