# SYNCHRO - SOLO LECTURA. No imprime secretos, telefonos ni cadenas SQL.
$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$api = Join-Path $raiz "backend\Synchro.Api"
$tablet = Join-Path $raiz "frontend\tablet-app"
$correcto = $true
$valores = @{}

function Estado([string]$nombre, [bool]$ok, [string]$descripcion) {
    if ($ok) { Write-Host "[OK] $nombre - $descripcion" }
    else {
        Write-Host "[PENDIENTE] $nombre - $descripcion"
        $script:correcto = $false
    }
}
function AgregarJson($objeto, [string]$prefijo) {
    if ($null -eq $objeto) { return }
    if ($objeto -is [pscustomobject]) {
        foreach ($propiedad in $objeto.PSObject.Properties) {
            $nombre = if ($prefijo) { ('{0}:{1}' -f $prefijo, $propiedad.Name) }
                      else { $propiedad.Name }
            AgregarJson $propiedad.Value $nombre
        }
    } elseif ($prefijo -and $objeto -isnot [System.Array]) {
        $script:valores[$prefijo] = [string]$objeto
    }
}
function LeerJson([string]$ruta) {
    if (-not (Test-Path -LiteralPath $ruta -PathType Leaf)) { return }
    try {
        $objeto = Get-Content -LiteralPath $ruta -Raw | ConvertFrom-Json -ErrorAction Stop
        AgregarJson $objeto ""
    } catch {
        Estado "Lectura de configuracion JSON" $false "Un archivo no se pudo leer."
    }
}

Write-Host "SYNCHRO | Configuracion de retiro - SOLO LECTURA"
Write-Host "Solo se imprimen estados; no se cambia la configuracion."
Write-Host ""
$proyecto = Join-Path $api "Synchro.Api.csproj"
if (-not (Test-Path -LiteralPath $proyecto) -or
    -not (Test-Path -LiteralPath (Join-Path $tablet "vite.config.js"))) {
    Estado "Repositorios" $false "No se encontraron API y tablet."
    exit 1
}
LeerJson (Join-Path $api "appsettings.json")
LeerJson (Join-Path $api "appsettings.Development.json")
try {
    [xml]$xml = Get-Content -LiteralPath $proyecto -Raw
    $id = [string]$xml.Project.PropertyGroup.UserSecretsId
    if ($id -match '^[a-zA-Z0-9-]{16,80}$' -and $env:APPDATA) {
        LeerJson (Join-Path $env:APPDATA ("Microsoft\UserSecrets\{0}\secrets.json" -f $id))
    }
} catch {
    Estado "User Secrets" $false "No se pudo inspeccionar su configuracion."
}

$claves = @(
    "ConnectionStrings:SynchroDB",
    "WhatsAppManual:Habilitado",
    "WhatsAppManual:NumeroE164",
    "CobroRetiroPersonal:Obligatorio",
    "CobroRetiroPersonal:MinutosPorBloque",
    "CobroRetiroPersonal:Tarifas:Pequeno",
    "CobroRetiroPersonal:Tarifas:Mediano",
    "CobroRetiroPersonal:Tarifas:Grande",
    "RetiroPersonal:ClaveHmac"
)
foreach ($clave in $claves) {
    $v = [Environment]::GetEnvironmentVariable($clave.Replace(":", "__"), "Process")
    if ($null -ne $v) { $valores[$clave] = $v }
}
try {
    $cadena = $valores["ConnectionStrings:SynchroDB"]
    $ok = $false
    if (-not [string]::IsNullOrWhiteSpace($cadena)) {
        Add-Type -AssemblyName System.Data -ErrorAction Stop
        $sql = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($cadena)
        $ok = ($sql.DataSource -ieq "(localdb)\SYNCHRO_Aislado" -and
               $sql.InitialCatalog -ieq "SynchroDB_Pruebas" -and
               [string]::IsNullOrWhiteSpace($sql.AttachDBFilename) -and
               $sql.IntegratedSecurity)
    }
    Estado "Conexion API" $ok "Debe usar SYNCHRO_Aislado/SynchroDB_Pruebas."
} catch {
    Estado "Conexion API" $false "Falta o es invalida."
}
Estado "Modulo manual" ($valores["WhatsAppManual:Habilitado"] -ieq "true") "WhatsAppManual:Habilitado."
Estado "Cobro obligatorio" ($valores["CobroRetiroPersonal:Obligatorio"] -ieq "true") "CobroRetiroPersonal:Obligatorio."
Estado "Telefono configurado" ($valores["WhatsAppManual:NumeroE164"] -match '^\+?504[0-9]{8}$') "Formato 504 seguido de 8 digitos."

[int]$minutos = 0
$tieneMinutos = [int]::TryParse($valores["CobroRetiroPersonal:MinutosPorBloque"], [ref]$minutos)
Estado "Minutos por bloque" ($tieneMinutos -and $minutos -ge 1 -and $minutos -le 1440) "Entre 1 y 1440."

foreach ($tamano in @("Pequeno", "Mediano", "Grande")) {
    $precio = $valores["CobroRetiroPersonal:Tarifas:$tamano"]
    [decimal]$cantidad = 0
    $ok = $false
    if ($null -ne $precio -and $precio -match '^[0-9]+(\.[0-9]{1,2})?$') {
        $ok = [decimal]::TryParse(
            $precio,
            [System.Globalization.NumberStyles]::AllowDecimalPoint,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$cantidad
        ) -and $cantidad -gt 0 -and $cantidad -le 100000
    }
    Estado "Tarifa $tamano" $ok "Precio positivo (punto decimal, hasta 2 decimales)."
}
$claveHmac = $valores["RetiroPersonal:ClaveHmac"]
$hmacCorrecto = $false
if (-not [string]::IsNullOrWhiteSpace($claveHmac)) {
    try {
        $bytes = [Convert]::FromBase64String($claveHmac)
        $hmacCorrecto = ($bytes.Length -eq 32)
        [array]::Clear($bytes, 0, $bytes.Length)
    } catch { $hmacCorrecto = $false }
}
Estado "Clave HMAC" $hmacCorrecto "Debe estar configurada con 32 bytes Base64."

# Para Vite 'npm run dev' se usa .env.development, NO .env.e2e.
$vitePago = $null
foreach ($nombre in @(".env", ".env.local", ".env.development", ".env.development.local")) {
    $ruta = Join-Path $tablet $nombre
    if (-not (Test-Path -LiteralPath $ruta -PathType Leaf)) { continue }
    foreach ($linea in (Get-Content -LiteralPath $ruta)) {
        if ($linea -match '^\s*VITE_RETIRO_PAGO_HABILITADO\s*=\s*(.*?)\s*$') {
            $vitePago = $Matches[1].Trim().Trim('"', "'")
        }
    }
}
$variableVite = [Environment]::GetEnvironmentVariable("VITE_RETIRO_PAGO_HABILITADO", "Process")
if ($null -ne $variableVite) { $vitePago = $variableVite }
Estado "Tablet con cobro" ($vitePago -ceq "true") "Configuracion de npm run dev; no cuenta .env.e2e."

Write-Host ""
Write-Host "API: usar perfil HTTPS y Development al realizar la prueba real."
Write-Host "No se han iniciado servicios, creado cobros ni modificado SQL."
if ($correcto) {
    Write-Host "[OK] Configuracion lista para comprobacion HTTP/SQL real."
    exit 0
}
Write-Host "[PENDIENTE] Revisar los estados anteriores antes del retiro integrado."
exit 1
