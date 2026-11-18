# SYNCHRO / Prueba integrada de retiro personal.
# Configuracion TEMPORAL de una sesion de PowerShell, sin guardar
# precios, claves ni telefonos en archivos de GitHub.
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Backend", "Tablet", "Administrador")]
    [string]$Modo
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$api = Join-Path $raiz "backend\Synchro.Api"
$tablet = Join-Path $raiz "frontend\tablet-app"
$admin = Join-Path $raiz "frontend\admin-app"
$diagnostico = Join-Path $PSScriptRoot "Diagnostico-ConfiguracionRetiro.ps1"

if (-not (Test-Path (Join-Path $api "Synchro.Api.csproj")) -or
    -not (Test-Path (Join-Path $tablet "package.json")) -or
    -not (Test-Path (Join-Path $admin "package.json")) -or
    -not (Test-Path $diagnostico)) {
    throw "No se encontraron los proyectos y el diagnostico del repositorio SYNCHRO."
}

# Mantener los servidores y la base dentro del entorno de desarrollo.
$env:DOTNET_ENVIRONMENT = "Development"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:SYNCHRO_API_TARGET = "https://localhost:7278"

# Valores FICTICIOS exclusivos de la prueba, NO son tarifas comerciales.
# Dotnet acepta configuracion anidada mediante doble guion bajo.
$env:CobroRetiroPersonal__Obligatorio = "true"
$env:CobroRetiroPersonal__MinutosPorBloque = "60"
$env:CobroRetiroPersonal__Tarifas__Pequeno = "1.00"
$env:CobroRetiroPersonal__Tarifas__Mediano = "2.00"
$env:CobroRetiroPersonal__Tarifas__Grande = "3.00"

# Esta opcion se aplicara al Vite de desarrollo, no a produccion.
$env:VITE_RETIRO_PAGO_HABILITADO = "true"

Write-Host "SYNCHRO | Inicio controlado de $Modo"
Write-Host "ENTORNO: Development. BD autorizada: SynchroDB_Pruebas."
Write-Host "TARIFAS FICTICIAS DE PRUEBA: Pequeno L 1, Mediano L 2, Grande L 3 por 60 minutos."
Write-Host "Estas tarifas NO constituyen precios comerciales y NO se guardan."
Write-Host "El proceso permanecera abierto en esta terminal. Ctrl+C para detenerlo."
Write-Host ""

# El diagnostico revisa user-secrets, DB objetivo, HMAC y la opcion Vite
# sin imprimir valores privados. Fallar de forma cerrada.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $diagnostico
if ($LASTEXITCODE -ne 0) {
    throw "La configuracion no supero el diagnostico. No se inicia el servicio."
}
Write-Host ""
if ($Modo -eq "Backend") {
    Write-Host "Iniciando API HTTPS en https://localhost:7278 ..."
    Push-Location $api
    try {
        & dotnet run --project (Join-Path $api "Synchro.Api.csproj") --launch-profile https
    } finally {
        Pop-Location
    }
} elseif ($Modo -eq "Tablet") {
    Write-Host "Iniciando tablet HTTPS y proxy hacia https://localhost:7278 ..."
    Push-Location $tablet
    try {
        & npm.cmd run dev -- --host localhost --port 5173 --strictPort
    } finally {
        Pop-Location
    }
} else {
    Write-Host "Iniciando ADMINISTRADOR ORIGINAL en https://localhost:5174/admin ..."
    Write-Host "Panel separado de la tablet; no modifica el backend ni los datos."
    if (-not (Test-Path (Join-Path $admin "node_modules"))) {
        throw "Faltan dependencias de admin-app. Desde frontend/tablet-app ejecuta: npm.cmd --prefix ..\admin-app ci"
    }
    Push-Location $admin
    try {
        & npm.cmd run dev -- --host localhost --port 5174 --strictPort
    } finally {
        Pop-Location
    }
}

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
