param(
    [ValidateSet('Completa','Evidencias','Terminales','Pin','Detalle')]
    [string]$Grupo = 'Completa'
)
$ErrorActionPreference = 'Stop'
# No acepta destinos alternativos ni lee conexiones privadas.
$instancia = '(localdb)\SYNCHRO_Aislado'
$base = 'SynchroDB_Pruebas'
$marcador = & sqlcmd -S $instancia -d $base -E -b -h -1 -W -Q "SET NOCOUNT ON; IF DB_NAME()<>N'SynchroDB_Pruebas' THROW 57210,'Base no autorizada.',1; SELECT N'SYNCHRO_AISLADO_OK' FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito' AND CONVERT(nvarchar(10),value)=N'1';"
if ($LASTEXITCODE -ne 0 -or ($marcador -join '').Trim() -ne 'SYNCHRO_AISLADO_OK') {
    throw 'No se confirmó la base aislada y su marcador. No se ejecutan pruebas.'
}

# Las 11 clases historicas ya usan fixtures propias y el destino comprobado.
# La ejecucion predeterminada no aplica ningun filtro ni exclusion.
$filtro = $null
switch ($Grupo) {
    'Evidencias' { $filtro = 'FullyQualifiedName~ConsumoEvidenciaFacialSqlTests|FullyQualifiedName~RechazosEvidenciaFacialSqlTests|FullyQualifiedName~ConsultaConsentimientoSqlTests' }
    'Terminales' { $filtro = 'FullyQualifiedName~AutenticacionTerminalRealTests|FullyQualifiedName~RevocacionTerminalTests|FullyQualifiedName~VinculacionTerminalCompletaTests' }
    'Pin' { $filtro = 'FullyQualifiedName~EmisionPinAisladoTests|FullyQualifiedName~PinRetiroDesarrolloAisladoTests|FullyQualifiedName~PinRetiroIntegracionTests|FullyQualifiedName~PinRetiroConexionTests' }
    'Detalle' { $filtro = 'FullyQualifiedName~DetalleEncomiendaAutorizacionTests' }
}
$variables = @{
    'SYNCHRO_SQL_DEPOSITO_TEST' = "Server=$instancia;Database=$base;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    'WhatsApp__Outbox__Habilitado' = 'false'
    'WhatsApp__SolicitudesInicioWorker__Habilitado' = 'false'
    'WhatsApp__VerificacionOutbox__Habilitado' = 'false'
}
$anteriores = @{}
try {
    foreach ($nombre in $variables.Keys) {
        $anteriores[$nombre] = [Environment]::GetEnvironmentVariable($nombre, 'Process')
        [Environment]::SetEnvironmentVariable($nombre, $variables[$nombre], 'Process')
    }
    $argumentos = @('test', (Join-Path $PSScriptRoot '../tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj'), '--no-restore', '--logger', 'console;verbosity=minimal')
    if ($filtro) { $argumentos += @('--filter', $filtro) }
    & dotnet @argumentos
    if ($LASTEXITCODE -ne 0) { throw 'La regresión del backend falló.' }
}
finally {
    foreach ($nombre in $anteriores.Keys) {
        [Environment]::SetEnvironmentVariable($nombre, $anteriores[$nombre], 'Process')
    }
}
