# SYNCHRO - Arranque de los cuatro componentes.
# No modifica SQL, certificados, claves ni Tailscale.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$certs = Join-Path $env:LOCALAPPDATA 'SYNCHRO\certificados'
$perfil = Join-Path $env:LOCALAPPDATA 'SYNCHRO\Definitivo'

function Exigir-Archivo([string]$ruta) {
    if (-not (Test-Path -LiteralPath $ruta -PathType Leaf)) {
        throw "Falta el archivo requerido: $ruta. No se generara un reemplazo."
    }
}
function Puerto-Ocupado([int]$puerto) {
    return (@(Get-NetTCPConnection -LocalPort $puerto -State Listen -ErrorAction SilentlyContinue).Count -gt 0)
}
function Esperar-Puerto([int]$puerto, [string]$nombre, [int]$segundos=90) {
    $limite = (Get-Date).AddSeconds($segundos)
    do {
        if (Puerto-Ocupado $puerto) {
            Write-Host "[OK] $nombre escucha en el puerto $puerto." -ForegroundColor Green
            return
        }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $limite)
    throw "$nombre no inicio en el puerto $puerto. Revisa su ventana. No se iniciaran los componentes restantes."
}
function Iniciar-Ventana([string]$directorio, [string]$comando, [hashtable]$variables) {
    $previas = @{}
    try {
        foreach ($nombre in $variables.Keys) {
            $previas[$nombre] = [Environment]::GetEnvironmentVariable($nombre, 'Process')
            [Environment]::SetEnvironmentVariable($nombre, $variables[$nombre], 'Process')
        }
        Start-Process -FilePath 'powershell.exe' -WorkingDirectory $directorio -ArgumentList @('-NoProfile','-NoExit','-Command',$comando)
    } finally {
        foreach ($nombre in $previas.Keys) {
            [Environment]::SetEnvironmentVariable($nombre, $previas[$nombre], 'Process')
        }
    }
}

Write-Host '=== SYNCHRO | ARRANQUE DEFINITIVO DE DEMOSTRACION ===' -ForegroundColor Cyan
foreach ($herramienta in @('dotnet','node','npm.cmd','tailscale','powershell.exe')) {
    Get-Command $herramienta -ErrorAction Stop | Out-Null
}
if ((Get-Service -Name 'MSSQL$SQLEXPRESS').Status -ne 'Running') {
    throw 'SQL Server Express no esta iniciado. Inicia el servicio antes de continuar.'
}
$estado = & tailscale status --json
if ($LASTEXITCODE -ne 0) { throw 'No se pudo comprobar Tailscale.' }
if (($estado -join [Environment]::NewLine | ConvertFrom-Json).BackendState -ne 'Running') {
    throw 'Tailscale no esta conectado. No se modificara su configuracion.'
}
$serve = (& tailscale serve status | Out-String)
if ($LASTEXITCODE -ne 0 -or $serve -notmatch 'https\+insecure://127\.0\.0\.1:5173' -or $serve -notmatch 'https\+insecure://127\.0\.0\.1:5174') {
    throw 'No se encontraron los dos proxies HTTPS existentes de Tailscale. Revisa tailscale serve status.'
}
foreach ($puerto in @(7278,5173,5174,5175)) {
    if (Puerto-Ocupado $puerto) {
        throw "El puerto $puerto ya esta ocupado. Cierra los servidores existentes con Ctrl+C antes de ejecutar este script."
    }
}
$certTablet = Join-Path $certs 'tablet-tailscale.pem'
$keyTablet = Join-Path $certs 'tablet-tailscale-key.pem'
$certLocal = Join-Path $certs 'usuarios-local.pem'
$keyLocal = Join-Path $certs 'usuarios-local-key.pem'
foreach ($ruta in @($certTablet,$keyTablet,$certLocal,$keyLocal,(Join-Path $perfil 'tablet-proxy.key'),(Join-Path $raiz 'backend\Synchro.Api\Synchro.Api.csproj'))) {
    Exigir-Archivo $ruta
}
foreach ($app in @('tablet-app','admin-app','usuarios-app')) {
    Exigir-Archivo (Join-Path $raiz "frontend\$app\node_modules\vite\bin\vite.js")
}
foreach ($par in @(@{Cert=$certTablet;Key=$keyTablet},@{Cert=$certLocal;Key=$keyLocal})) {
    & node -e "require('node:tls').createSecureContext({cert:require('node:fs').readFileSync(process.argv[1]),key:require('node:fs').readFileSync(process.argv[2])})" $par.Cert $par.Key
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo cargar un par de certificados existente. No se reemplazara.' }
}
Write-Host '[OK] SQL Server, Tailscale, proxies, dependencias y certificados comprobados.' -ForegroundColor Green

$numero = $env:SYNCHRO_WHATSAPP_DEFINITIVO
if ([string]::IsNullOrWhiteSpace($numero)) {
    $numero = Read-Host 'Numero de WhatsApp de SYNCHRO con codigo de pais'
}
$numero = $numero.Trim().TrimStart('+')
if ($numero -notmatch '^504[0-9]{8}$') { throw 'Introduce el numero oficial de Honduras con codigo 504 y ocho digitos.' }

Iniciar-Ventana $raiz 'dotnet run --project .\backend\Synchro.Api\Synchro.Api.csproj --no-launch-profile -- --administracion-definitiva' @{
    SYNCHRO_WHATSAPP_DEFINITIVO = $numero
}
$numero = $null
Esperar-Puerto 7278 'Backend' 120

$comun = @{
    SYNCHRO_API_TARGET = 'https://localhost:7278'
    SYNCHRO_CERT_PASSWORD = $null
    SYNCHRO_WHATSAPP_DEFINITIVO = $null
}
$tablet = $comun.Clone()
$tablet['SYNCHRO_TABLET_DEFINITIVA'] = '1'
$tablet['SYNCHRO_CERT_PATH'] = $certTablet
$tablet['SYNCHRO_KEY_PATH'] = $keyTablet
$local = $comun.Clone()
$local['SYNCHRO_TABLET_DEFINITIVA'] = $null
$local['SYNCHRO_CERT_PATH'] = $certLocal
$local['SYNCHRO_KEY_PATH'] = $keyLocal

Iniciar-Ventana (Join-Path $raiz 'frontend\tablet-app') 'npm.cmd run dev -- --force' $tablet
Esperar-Puerto 5173 'Tablet'
Iniciar-Ventana (Join-Path $raiz 'frontend\admin-app') 'npm.cmd run dev -- --force' $local
Esperar-Puerto 5174 'Administrador'
Iniciar-Ventana (Join-Path $raiz 'frontend\usuarios-app') 'npm.cmd run dev -- --force' $local
Esperar-Puerto 5175 'Usuarios'

Write-Host ''
Write-Host '=== LOS CUATRO PUERTOS ESTAN ACTIVOS ===' -ForegroundColor Green
Write-Host 'Usuarios: https://127.0.0.1:5175/'
Write-Host 'Administrador local: https://127.0.0.1:5174/'
Write-Host 'Direcciones de tablet y administrador en Tailscale:'
Write-Host $serve
Write-Host 'Comprueba las paginas e inicia sesion. Conserva abiertas las cuatro ventanas.'
Write-Host 'Para detener, pulsa Ctrl+C en cada ventana; no se detienen SQL Server ni Tailscale.'
