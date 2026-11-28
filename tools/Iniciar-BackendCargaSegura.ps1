# SYNCHRO V3 - Iniciar API con la clave HMAC de carga ORIGINAL.
# Reutiliza SecureString DPAPI guardado previamente en el perfil Windows.
# Nunca crea, imprime, cambia ni guarda una clave nueva.
# Solo para el prototipo aislado: SynchroDB_Pruebas.
$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$proyecto = Join-Path $raiz 'backend\Synchro.Api\Synchro.Api.csproj'
if (-not (Test-Path -LiteralPath $proyecto -PathType Leaf)) {
    throw 'No se encontro el proyecto de API SYNCHRO.'
}
if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    throw 'LOCALAPPDATA no esta disponible.'
}

$carpeta = Join-Path $env:LOCALAPPDATA 'SYNCHRO\CargaSegura'
$archivo = Join-Path $carpeta 'clave-hmac.clixml'
$directorioClaves = Join-Path $carpeta 'DataProtectionKeys'

if (-not (Test-Path -LiteralPath $archivo -PathType Leaf)) {
    throw 'No se encontro la clave HMAC original cifrada. No se creara otra.'
}
if (-not (Test-Path -LiteralPath $directorioClaves -PathType Container)) {
    throw 'No se encontro el directorio original DataProtectionKeys. No se creara otro.'
}
if (@(Get-ChildItem -LiteralPath $directorioClaves -Filter 'key-*.xml' -File -ErrorAction Stop).Count -eq 0) {
    throw 'No se encontraron claves DataProtection originales. Operacion detenida.'
}

$secretoSeguro = $null
$claveBase64 = $null
$bytes = $null
try {
    # Import-Clixml recupera SecureString solo con el mismo usuario de Windows.
    $secretoSeguro = Import-Clixml -LiteralPath $archivo
    if ($secretoSeguro -isnot [System.Security.SecureString]) {
        throw 'El archivo HMAC original no tiene el formato SecureString esperado.'
    }

    $claveBase64 = [System.Net.NetworkCredential]::new('synchro', $secretoSeguro).Password
    try {
        $bytes = [Convert]::FromBase64String($claveBase64)
    } catch [System.FormatException] {
        throw 'La clave HMAC original no tiene formato Base64 valido.'
    }
    if ($bytes.Length -ne 32) {
        throw 'La clave HMAC original no tiene 32 bytes. No se generara otra.'
    }

    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__SynchroDB = 'Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
    $env:CargaDispositivos__RegistroHabilitado = 'true'
    $env:EncomiendasOperacion__Habilitado = 'true'
    $env:ReservasPrototipo__AccesoLibrePruebas = 'true'
    $env:RostroV3__RetiroHabilitado = 'true'
    $env:RostroV3__RegistroHabilitado = 'true'
    $env:WhatsAppManual__Habilitado = 'true'
    # Separar las operaciones de los ensayos SQL de la bandeja de la estación.
    $env:Administracion__ExcluirFixturesBandejaWhatsApp = 'true'

    # Carga la configuracion de tarifas y verifica SQL, sin reescribir el catalogo.
    & (Join-Path $PSScriptRoot 'Configurar-TarifasFeria-Pruebas.ps1') -SoloVerificar
    # Migración idempotente de roles; la verificación anterior limita la base a pruebas.
    $dbRoles = [System.Data.SqlClient.SqlConnection]::new('Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False;TrustServerCertificate=True')
    try {
        $dbRoles.Open()
        $cmdRoles = $dbRoles.CreateCommand()
        $cmdRoles.CommandText = Get-Content -LiteralPath (Join-Path $raiz 'backend\Synchro.Api\Database\Migrations\20261110_UsuariosRoles_Pruebas.sql') -Raw
        [void]$cmdRoles.ExecuteNonQuery()
    } finally { $dbRoles.Dispose() }
    $env:Administracion__UsuariosHabilitados = 'true'
    $env:Administracion__ConfiguracionHabilitada = 'true'
    $env:ProteccionDatos__DirectorioClaves = $directorioClaves
    $env:CargaDispositivos__ClaveHmac = $claveBase64

    Write-Host '=== SYNCHRO V3: BACKEND DE CARGA SEGURA ==='
    Write-Host '[OK] Clave HMAC original recuperada (32 bytes; valor oculto).'
    Write-Host '[OK] DataProtectionKeys originales disponibles.'
    Write-Host '[OK] Configuracion aislada: SynchroDB_Pruebas.'
    Write-Host '[OK] Reservas de prueba accesibles desde cualquier tablet (sin vincular).'
    Write-Host 'Iniciando https://localhost:7278 ...'
    Write-Host 'Deja abierta esta terminal durante la prueba.'
    & dotnet run --project $proyecto --launch-profile https
    if ($LASTEXITCODE -ne 0) {
        throw 'El backend termino con error. Revisa los mensajes de dotnet.'
    }
}
finally {
    if ($null -ne $bytes) { [Array]::Clear($bytes, 0, $bytes.Length) }
    $bytes = $null
    $claveBase64 = $null
    $secretoSeguro = $null
    Remove-Item Env:\CargaDispositivos__ClaveHmac -ErrorAction SilentlyContinue
}
