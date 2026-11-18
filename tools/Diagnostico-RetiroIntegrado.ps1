# SYNCHRO: conexion local y candidatos de retiro de prueba. SOLO LECTURA.
# No imprime PIN, telefonos, rostros, secretos ni cadenas de conexion.
$ErrorActionPreference = "Stop"
$ok = $true

function Estado([string]$titulo, [bool]$correcto, [string]$detalle) {
    if ($correcto) { Write-Host "[OK] $titulo - $detalle" }
    else {
        Write-Host "[PENDIENTE] $titulo - $detalle"
        $script:ok = $false
    }
}

Write-Host "SYNCHRO | Prueba de conexion y registros existentes (SOLO LECTURA)"
Write-Host "Solo localhost y SynchroDB_Pruebas. No autoriza retiros ni confirma cobros."
Write-Host ""

# El curl --insecure solo se emplea contra HTTPS LOCAL por el certificado de desarrollo.
$curl = Get-Command curl.exe -ErrorAction SilentlyContinue
if ($null -eq $curl) {
    Estado "Comprobacion HTTPS" $false "No se encontro curl.exe en Windows."
} else {
    foreach ($sitio in @(
        @{ Nombre = "API directa"; Direccion = "https://localhost:7278/api/estado" },
        @{ Nombre = "Proxy tablet -> API"; Direccion = "https://localhost:5173/api/estado" }
    )) {
        try {
            $json = & $curl.Source --insecure --silent --show-error --max-time 8 --fail-with-body $sitio.Direccion 2>$null
            if ($LASTEXITCODE -ne 0) { throw "Sin respuesta HTTP valida" }
            $respuesta = ($json -join "") | ConvertFrom-Json -ErrorAction Stop
            Estado $sitio.Nombre ($respuesta.proyecto -eq "SYNCHRO" -and $respuesta.estado -eq "en desarrollo") "Ruta /api/estado responde correctamente."
        } catch {
            Estado $sitio.Nombre $false "Revisa que ambos procesos esten abiertos y su certificado de desarrollo."
        }
    }
}

$conexion = $null
try {
    Add-Type -AssemblyName System.Data
    $servidor = "(localdb)\SYNCHRO_Aislado"
    $catalogo = "SynchroDB_Pruebas"
    $cadena = "Server=$servidor;Initial Catalog=$catalogo;Integrated Security=True;Connect Timeout=8;Encrypt=False;TrustServerCertificate=True"
    $conexion = [System.Data.SqlClient.SqlConnection]::new($cadena)
    $conexion.Open()

    $cmd = $conexion.CreateCommand()
    $cmd.CommandTimeout = 10
    $cmd.CommandText = @'
SELECT CASE WHEN DB_NAME() = N'SynchroDB_Pruebas'
        AND EXISTS (
            SELECT 1 FROM sys.extended_properties
            WHERE class = 0
              AND name = N'SynchroFixtureDeposito'
              AND CONVERT(nvarchar(10), value) = N'1'
        ) THEN 1 ELSE 0 END
'@
    if ([int]$cmd.ExecuteScalar() -ne 1) {
        Estado "Base SQL" $false "La base no tiene el marcador de entorno aislado."
    } else {
        Estado "Base SQL" $true "SynchroDB_Pruebas con marcador validado."

        $cmd.CommandText = @'
SELECT
    COUNT(*) AS Ocupados,
    COALESCE(SUM(CASE WHEN
      EXISTS (
        SELECT 1 FROM dbo.AutorizacionesWhatsAppManuales a
        WHERE a.UsoId = u.Id AND a.Estado = N'Confirmado'
      ) AND EXISTS (
        SELECT 1 FROM dbo.RostrosRetiroPersonalManual r
        WHERE r.UsoId = u.Id
      ) AND EXISTS (
        SELECT 1 FROM dbo.CodigosRetiroPersonal p
        WHERE p.UsoId = u.Id
          AND p.FechaUso IS NULL
          AND p.IntentosFallidos < 5
          AND (p.FechaVencimiento IS NULL OR p.FechaVencimiento > SYSUTCDATETIME())
      ) THEN 1 ELSE 0 END), 0) AS Elegibles
FROM dbo.UsosCasillero u
JOIN dbo.Casilleros c ON c.Codigo = u.CodigoCasillero
WHERE u.TipoServicio = N'ResguardoPersonal'
  AND u.OrigenReserva = N'Terminal'
  AND u.Estado = N'Ocupado'
  AND u.FechaIngreso IS NOT NULL
  AND u.FechaRetiro IS NULL
  AND c.Estado = N'Ocupado'
'@
        $lector = $cmd.ExecuteReader()
        if (-not $lector.Read()) { throw "No hay respuesta del recuento." }
        $ocupados = $lector.GetInt32(0)
        $elegibles = $lector.GetInt32(1)
        $lector.Close()
        Write-Host ("[INFO] Resguardos personales ocupados: {0}" -f $ocupados)
        Write-Host ("[INFO] Resguardos con WA, rostro y PIN vigente: {0}" -f $elegibles)
        if ($elegibles -eq 0) {
            Estado "Operacion para retirar" $false "No hay resguardo con autorizacion, rostro y PIN vigentes."
        } else {
            Write-Host "[OK] Hay al menos una operacion potencialmente valida."
        }
    }
} catch {
    Estado "Lectura de resguardos" $false "No pudo completarse la consulta. No se escribio en SQL."
} finally {
    if ($null -ne $conexion) { $conexion.Dispose() }
}
Write-Host ""
Write-Host "Este diagnostico no verifica el pago ni demuestra que la finalizacion funcione."
Write-Host "No confirma ningun cobro ni cambia casilleros."
if ($ok) { exit 0 }
exit 1
