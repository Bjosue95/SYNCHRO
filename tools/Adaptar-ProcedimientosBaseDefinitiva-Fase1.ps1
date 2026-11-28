# Adaptar solo procedimientos revisados de identidad y cancelacion.
# No ejecuta procedimientos, no activa modulos, no importa datos de prueba.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$procedimientos = [ordered]@{
    CancelarReservaPersonalManualSynchro = 57300
    ConsumirVerificacionInicioSesionSynchro = 56673
    EmitirPinRetiroSynchro = 51104
    TomarOutboxWhatsAppSynchro = 55800
    ValidarCodigoTelefonoConFinalidadSynchro = 56664
    ValidarCodigoTelefonoSynchro = 56663
    ValidarPinRetiroSynchro = 51403
}
$guardiaNueva = "IF DB_NAME() <> N'SynchroDB' OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'') <> N'SQLEXPRESS' OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1) <> 0"
$conexion = [System.Data.SqlClient.SqlConnection]::new('Server=.\SQLEXPRESS;Database=SynchroDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15')
$tx = $null
try {
    $conexion.Open()
    $q = $conexion.CreateCommand()
    $q.CommandTimeout = 60
    $q.CommandText = 'SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET XACT_ABORT ON; SET NOCOUNT ON;'
    [void]$q.ExecuteNonQuery()
    $tx = $conexion.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    $q.Transaction = $tx
    $q.CommandText = @'
IF DB_NAME()<>N'SynchroDB'
 OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS'
 OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1)<>0
 THROW 59060,'Destino no autorizado.',1;
IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
 THROW 59061,'La base tiene marcador de pruebas.',1;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0)<>55
 OR (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped=0)<>22
 THROW 59062,'Esquema inesperado.',1;
'@
    [void]$q.ExecuteNonQuery()
    $pendientes = [ordered]@{}
    foreach ($nombre in $procedimientos.Keys) {
        $q.Parameters.Clear()
        $q.CommandText = @'
SELECT m.definition
FROM sys.procedures p JOIN sys.sql_modules m ON m.object_id=p.object_id
JOIN sys.schemas s ON s.schema_id=p.schema_id
WHERE s.name=N'dbo' AND p.name=@nombre AND m.uses_ansi_nulls=1 AND m.uses_quoted_identifier=1;
'@
        [void]$q.Parameters.Add('@nombre',[System.Data.SqlDbType]::NVarChar,128)
        $q.Parameters['@nombre'].Value = $nombre
        $definicion = [string]$q.ExecuteScalar()
        if ([string]::IsNullOrWhiteSpace($definicion)) { throw "Definicion no disponible: $nombre" }
        if ($definicion -match 'SynchroFixtureDeposito|ConfirmadoSimulado|PagoSimulado') {
            throw "Procedimiento fuera de la fase autorizada: $nombre"
        }
        $errorSql = $procedimientos[$nombre]
        $patron = "(?i)\bIF\s+DB_NAME\(\)\s*<>\s*N'SynchroDB_Pruebas'(?=\s+THROW\s+$errorSql\s*,)"
        $coincidencias = [regex]::Matches($definicion,$patron)
        if ($coincidencias.Count -eq 0 -and $definicion.Contains($guardiaNueva) -and $definicion -notmatch 'SynchroDB_Pruebas') {
            Write-Host "[OK] Ya adaptado: $nombre"
            continue
        }
        if ($coincidencias.Count -ne 1 -or [regex]::Matches($definicion,'SynchroDB_Pruebas','IgnoreCase').Count -ne 1) {
            throw "Guardia no reconocida: $nombre"
        }
        $encabezado = '(?im)^[ \t]*CREATE\s+PROCEDURE\b'
        if ([regex]::Matches($definicion,$encabezado).Count -ne 1) {
            throw "Encabezado inesperado: $nombre"
        }
        $alter = [regex]::Replace($definicion,$encabezado,'ALTER PROCEDURE')
        $alter = $alter.Replace($coincidencias[0].Value,$guardiaNueva)
        if ($alter -match 'SynchroDB_Pruebas' -or -not $alter.Contains($guardiaNueva)) {
            throw "Adaptacion incompleta: $nombre"
        }
        $pendientes.Add($nombre,$alter)
    }

    # Todas las definiciones se verifican antes de modificar alguna.
    $q.Parameters.Clear()
    foreach ($nombre in $pendientes.Keys) {
        $q.CommandText = $pendientes[$nombre]
        [void]$q.ExecuteNonQuery()
    }

    $q.CommandText = @'
SELECT COUNT(*) FROM sys.procedures p JOIN sys.sql_modules m ON m.object_id=p.object_id
WHERE m.definition LIKE N'%SynchroDB[_]Pruebas%';
'@
    $restantes = [int]$q.ExecuteScalar()
    if ($restantes -ne 11) { throw "Cantidad de procedimientos pendientes inesperada: $restantes" }

    $tx.Commit()
    $tx.Dispose()
    $tx = $null
    Write-Host '=== FASE 1 DE PROCEDIMIENTOS DEFINITIVOS ===' -ForegroundColor Green
    foreach ($nombre in $pendientes.Keys) { Write-Host "[OK] Adaptado: $nombre" }
    Write-Host '[OK] Siete procedimientos preparados para SynchroDB en SQLEXPRESS.'
    Write-Host '[OK] Once procedimientos siguen restringidos al entorno de pruebas.'
    Write-Host '[OK] Sin cambios en datos, PIN, permisos, pagos o bases LocalDB.'
}
catch {
    if ($null -ne $tx) { try { $tx.Rollback() } catch {} }
    throw
}
finally {
    if ($null -ne $tx) { $tx.Dispose() }
    $conexion.Dispose()
}
