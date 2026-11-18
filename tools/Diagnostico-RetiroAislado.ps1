# SYNCHRO / Retiro personal: preflight de solo lectura.
# No crea tablas, no ejecuta migraciones ni modifica cobros o casilleros.
param()

$ErrorActionPreference = "Stop"
$correcto = $true
$raiz = Split-Path -Parent $PSScriptRoot
$archivoApi = Join-Path $raiz "backend\Synchro.Api\Synchro.Api.csproj"
$archivoVite = Join-Path $raiz "frontend\tablet-app\vite.config.js"
$instancia = "(localdb)\SYNCHRO_Aislado"
$base = "SynchroDB_Pruebas"

function Estado($titulo, $exito, $detalle) {
    if ($exito) {
        Write-Host "[OK] $titulo - $detalle"
    } else {
        Write-Host "[PENDIENTE] $titulo - $detalle"
        $script:correcto = $false
    }
}

Write-Host "SYNCHRO | Diagnostico de retiro aislado (SOLO LECTURA)"
Write-Host "Instancia esperada: SYNCHRO_Aislado | Base: SynchroDB_Pruebas"
Write-Host ""

Estado "Proyecto API" (Test-Path -LiteralPath $archivoApi) "backend ASP.NET Core"
Estado "Proyecto tablet" (Test-Path -LiteralPath $archivoVite) "Vite"
if (-not (Test-Path -LiteralPath $archivoApi) -or
    -not (Test-Path -LiteralPath $archivoVite)) {
    exit 1
}

try {
    $sdk = & dotnet --list-sdks 2>$null
    $tieneDotNet10 = @($sdk | Where-Object { $_ -match '^10\.' }).Count -gt 0
    Estado ".NET 10" $tieneDotNet10 "SDK necesario para Synchro.Api"
} catch {
    Estado ".NET 10" $false "dotnet no esta disponible"
}

try {
    $localdb = Get-Command sqllocaldb.exe -ErrorAction Stop
    $instancias = & $localdb.Source info 2>$null
    $existeInstancia = @($instancias | Where-Object { $_.Trim() -eq "SYNCHRO_Aislado" }).Count -gt 0
    Estado "Instancia LocalDB" $existeInstancia "SYNCHRO_Aislado"
} catch {
    Write-Host "[INFORMATIVO] No se pudo usar sqllocaldb.exe; se intentara la conexion SQL."
}

$conexion = $null
try {
    # Cadena fija: NUNCA se lee una cadena real de configuracion o secrets.
    Add-Type -AssemblyName System.Data -ErrorAction Stop
    $cadena = "Server=$instancia;Initial Catalog=$base;Integrated Security=True;Connect Timeout=8;Encrypt=False;TrustServerCertificate=True"
    $conexion = New-Object System.Data.SqlClient.SqlConnection($cadena)
    $conexion.Open()

    $comando = $conexion.CreateCommand()
    $comando.CommandTimeout = 10
    $comando.CommandText = @'
SELECT DB_NAME() AS BaseActual,
       CASE WHEN EXISTS (
           SELECT 1
           FROM sys.extended_properties
           WHERE class = 0
             AND name = N'SynchroFixtureDeposito'
             AND CONVERT(nvarchar(10), value) = N'1'
       ) THEN 1 ELSE 0 END AS Marcador
'@
    $lector = $comando.ExecuteReader()
    if (-not $lector.Read()) {
        throw "La base no devolvio la comprobacion inicial."
    }
    $baseReal = $lector.GetString(0)
    $marcador = $lector.GetInt32(1) -eq 1
    $lector.Close()

    Estado "Base de pruebas" ($baseReal -eq $base) "Conectado a $baseReal"
    Estado "Marcador de fixture" $marcador "SynchroFixtureDeposito=1"

    $objetos = @(
        @{ Nombre = "dbo.CobrosRetiroPersonal"; Tipo = "U" },
        @{ Nombre = "dbo.AuditoriaCobrosRetiroPersonal"; Tipo = "U" },
        @{ Nombre = "dbo.CodigosRetiroPersonal"; Tipo = "U" },
        @{ Nombre = "dbo.AutorizacionesWhatsAppManuales"; Tipo = "U" },
        @{ Nombre = "dbo.PrevalidarRetiroPersonalSynchro"; Tipo = "P" },
        @{ Nombre = "dbo.PrepararCobroRetiroPersonalSynchro"; Tipo = "P" },
        @{ Nombre = "dbo.ConfirmarCobroRetiroPersonalAuditableSynchro"; Tipo = "P" },
        @{ Nombre = "dbo.FinalizarRetiroPersonalPagadoSynchro"; Tipo = "P" }
    )
    foreach ($objeto in $objetos) {
        $cmd = $conexion.CreateCommand()
        $cmd.CommandText = "SELECT CASE WHEN OBJECT_ID(@nombre, @tipo) IS NOT NULL THEN 1 ELSE 0 END"
        $null = $cmd.Parameters.Add("@nombre", [System.Data.SqlDbType]::NVarChar, 160)
        $cmd.Parameters["@nombre"].Value = $objeto.Nombre
        $null = $cmd.Parameters.Add("@tipo", [System.Data.SqlDbType]::NVarChar, 2)
        $cmd.Parameters["@tipo"].Value = $objeto.Tipo
        $existe = ([int]$cmd.ExecuteScalar() -eq 1)
        Estado "SQL $($objeto.Nombre)" $existe "objeto necesario"
        $cmd.Dispose()
    }
} catch {
    Estado "Conexion y esquema SQL" $false "No se pudo completar la lectura (sin mostrar credenciales)."
    Write-Host "[INFORMATIVO] Revisa que LocalDB exista, este accesible y que SynchroDB_Pruebas no haya cambiado."
    Write-Host "[INFORMATIVO] Si usas PowerShell 7 y falta System.Data.SqlClient, ejecuta este diagnostico desde Windows PowerShell 5.1."
} finally {
    if ($null -ne $conexion) {
        $conexion.Dispose()
    }
}

Write-Host ""
Write-Host "Configuracion por comprobar en backend (no se leen ni publican secrets):"
Write-Host "  WhatsAppManual:Habilitado = true (solo Development)"
Write-Host "  CobroRetiroPersonal:Obligatorio = true"
Write-Host "  CobroRetiroPersonal:MinutosPorBloque = numero valido"
Write-Host "  CobroRetiroPersonal:Tarifas:Pequeno / Mediano / Grande"
Write-Host "  WhatsAppManual:NumeroE164 = telefono de pruebas configurado"
Write-Host "  ConnectionStrings:SynchroDB = exclusivamente SynchroDB_Pruebas"
Write-Host "Tablet: VITE_RETIRO_PAGO_HABILITADO=true para el proceso pagado."
Write-Host "No ejecutes migraciones hasta revisar los pendientes del esquema."
if ($correcto) {
    Write-Host ""
    Write-Host "[OK] Preflight de archivos/SQL disponible. Faltan configuracion y prueba funcional real."
    exit 0
}
Write-Host ""
Write-Host "[PENDIENTE] Corregir los puntos anteriores antes de la prueba integrada."
exit 1
