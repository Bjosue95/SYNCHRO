# SYNCHRO - inspeccion de metadatos de referencia visual, SOLO LECTURA.
# Sin extraccion de imagenes, patrones, numeros de telefono o PIN.
$ErrorActionPreference = "Stop"
$instancia = "(localdb)\SYNCHRO_Aislado"
$bd = "SynchroDB_Pruebas"
$modeloActual = "SYNCHRO-SIM-32x32-v1"
$conexion = $null

Write-Host "SYNCHRO | Referencias de rostro del retiro (SOLO LECTURA)"
Write-Host "No se consultan patrones visuales, PIN ni numeros de telefono."
Write-Host ""

try {
    Add-Type -AssemblyName System.Data
    $cadena = "Server=$instancia;Initial Catalog=$bd;Integrated Security=True;Connect Timeout=8;Encrypt=False;TrustServerCertificate=True"
    $conexion = [System.Data.SqlClient.SqlConnection]::new($cadena)
    $conexion.Open()

    $validar = $conexion.CreateCommand()
    $validar.CommandText = @'
SELECT CASE WHEN DB_NAME() = N'SynchroDB_Pruebas'
AND EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class = 0 AND name = N'SynchroFixtureDeposito'
    AND CONVERT(nvarchar(10), value) = N'1'
) THEN 1 ELSE 0 END
'@
    if ([int]$validar.ExecuteScalar() -ne 1) {
        throw "No se pudo validar el entorno de pruebas."
    }

    $consulta = $conexion.CreateCommand()
    $consulta.CommandTimeout = 10
    $consulta.CommandText = @'
SELECT DISTINCT
    u.CodigoCasillero,
    r.Modelo,
    r.RegistradoUtc,
    CASE WHEN DATALENGTH(r.PatronProtegido) > 0
        THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS TieneReferencia
FROM dbo.UsosCasillero AS u
JOIN dbo.Casilleros AS c
    ON c.Codigo = u.CodigoCasillero
JOIN dbo.RostrosRetiroPersonalManual AS r
    ON r.UsoId = u.Id
WHERE u.TipoServicio = N'ResguardoPersonal'
AND u.OrigenReserva = N'Terminal'
AND u.Estado = N'Ocupado'
AND u.FechaIngreso IS NOT NULL
AND u.FechaRetiro IS NULL
AND c.Estado = N'Ocupado'
AND EXISTS (
    SELECT 1 FROM dbo.AutorizacionesWhatsAppManuales AS a
    WHERE a.UsoId = u.Id AND a.Estado = N'Confirmado'
)
AND EXISTS (
    SELECT 1 FROM dbo.CodigosRetiroPersonal AS p
    WHERE p.UsoId = u.Id
    AND p.FechaUso IS NULL
    AND p.IntentosFallidos < 5
    AND (p.FechaVencimiento IS NULL
         OR p.FechaVencimiento > SYSUTCDATETIME())
)
ORDER BY r.RegistradoUtc DESC
'@
    $lector = $consulta.ExecuteReader()
    $total = 0
    $conModelo = 0
    while ($lector.Read()) {
        $total++
        $casillero = $lector.GetString(0)
        $modeloCoincide = $lector.GetString(1) -ceq $modeloActual
        $fecha = $lector.GetDateTime(2)
        $tieneReferencia = $lector.GetBoolean(3)
        if ($modeloCoincide -and $tieneReferencia) { $conModelo++ }
        Write-Host ("Casillero: {0}" -f $casillero)
        Write-Host ("  Modelo esperado: {0}" -f $(if ($modeloCoincide) { "SI" } else { "NO" }))
        Write-Host ("  Referencia cifrada presente: {0}" -f $(if ($tieneReferencia) { "SI" } else { "NO" }))
        Write-Host ("  Fecha de registro (UTC): {0:yyyy-MM-dd HH:mm}" -f $fecha)
    }
    $lector.Close()
    Write-Host ""
    Write-Host ("[INFO] Resguardos elegibles: {0}" -f $total)
    Write-Host ("[INFO] Con modelo actual y referencia: {0}" -f $conModelo)
    if ($total -eq 0) {
        Write-Host "[PENDIENTE] No hay referencias listas para este retiro."
    } elseif ($conModelo -eq 0) {
        Write-Host "[PENDIENTE] Ninguna referencia tiene el modelo visual actual."
    } else {
        Write-Host "[OK] Hay referencia compatible en formato; esto NO prueba coincidencia facial."
    }
    Write-Host "Un modelo compatible no implica coincidencia con una captura nueva."
} catch {
    Write-Host "[PENDIENTE] No se pudo completar la lectura de metadatos."
    $ex = $_.Exception
    while ($null -ne $ex -and
           -not ($ex -is [System.Data.SqlClient.SqlException])) {
        $ex = $ex.InnerException
    }
    if ($null -ne $ex) {
        Write-Host ("[INFO] Codigo SQL: {0}" -f $ex.Number)
    }
} finally {
    if ($null -ne $conexion) { $conexion.Dispose() }
}
