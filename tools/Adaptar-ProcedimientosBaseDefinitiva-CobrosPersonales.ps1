# Adaptar exclusivamente los tres procedimientos de cobro ficticio.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$guardia = "IF DB_NAME() <> N'SynchroDB' OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'') <> N'SQLEXPRESS' OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1) <> 0"
$noFixture = "IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')"
$procedimientos = [ordered]@{
    PrepararCobroRetiroPersonalSynchro = 57800
    ConfirmarCobroRetiroPersonalAuditableSynchro = 57900
    FinalizarRetiroPersonalPagadoSynchro = 57700
}
$conexion = [System.Data.SqlClient.SqlConnection]::new('Server=.\SQLEXPRESS;Database=SynchroDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15')
$tx = $null
try {
    $conexion.Open()
    $q = $conexion.CreateCommand()
    $q.CommandTimeout = 60
    $q.CommandText = 'SET XACT_ABORT ON; SET NOCOUNT ON;'
    [void]$q.ExecuteNonQuery()
    $tx = $conexion.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    $q.Transaction = $tx
    $q.CommandText = @'
IF DB_NAME()<>N'SynchroDB' OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS'
 OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1)<>0
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
 THROW 59060,'Destino no autorizado.',1;
IF OBJECT_ID(N'dbo.CobrosRetiroPersonal',N'U') IS NULL
 OR OBJECT_ID(N'dbo.AuditoriaCobrosRetiroPersonal',N'U') IS NULL
 THROW 59062,'Falta el esquema de cobros.',1;
'@
    [void]$q.ExecuteNonQuery()
    $cambios = @()
    foreach($nombre in $procedimientos.Keys) {
        $q.Parameters.Clear()
        $q.CommandText = @'
SELECT m.definition,m.uses_ansi_nulls,m.uses_quoted_identifier
FROM sys.procedures p JOIN sys.sql_modules m ON m.object_id=p.object_id
JOIN sys.schemas s ON s.schema_id=p.schema_id
WHERE s.name=N'dbo' AND p.name=@nombre;
'@
        [void]$q.Parameters.Add('@nombre',[System.Data.SqlDbType]::NVarChar,128)
        $q.Parameters['@nombre'].Value=$nombre
        $lector=$q.ExecuteReader()
        try {
            if(!$lector.Read()){throw "Procedimiento inexistente: $nombre"}
            $def=$lector.GetString(0)
            $ansi=$lector.GetBoolean(1)
            $quoted=$lector.GetBoolean(2)
        } finally {$lector.Dispose()}
        if($def.Contains($guardia) -and $def -notmatch 'SynchroDB_Pruebas'){
            if($nombre -ne 'FinalizarRetiroPersonalPagadoSynchro' -and !$def.Contains($noFixture)){throw "Guardia incompleta: $nombre"}
            Write-Host "[OK] Ya adaptado: $nombre"
            continue
        }
        $errorSql=$procedimientos[$nombre]
        $patron="(?i)\bIF\s+DB_NAME\(\)\s*<>\s*N'SynchroDB_Pruebas'(?=\s+THROW\s+$errorSql\s*,)"
        $m=[regex]::Matches($def,$patron)
        if($m.Count -ne 1 -or [regex]::Matches($def,'SynchroDB_Pruebas','IgnoreCase').Count -ne 1){throw "Guardia no reconocida: $nombre"}
        $def=$def.Replace($m[0].Value,$guardia)
        if($nombre -ne 'FinalizarRetiroPersonalPagadoSynchro'){
            $errFixture=$errorSql+1
            $patronFixture="(?is)\bIF\s+NOT\s+EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+sys\.extended_properties\s+WHERE\s+class\s*=\s*0\s+AND\s+name\s*=\s*N'SynchroFixtureDeposito'\s+AND\s+CONVERT\s*\(\s*nvarchar\s*\(\s*10\s*\)\s*,\s*value\s*\)\s*=\s*N'1'\s*\)(?=\s+THROW\s+$errFixture\s*,)"
            $f=[regex]::Matches($def,$patronFixture)
            if($f.Count -ne 1){throw "Marcador de pruebas no reconocido: $nombre"}
            $def=$def.Replace($f[0].Value,$noFixture)
        }
        $encabezado='(?im)^[ \t]*CREATE\s+(?:OR\s+ALTER\s+)?PROCEDURE\b'
        if([regex]::Matches($def,$encabezado).Count -ne 1){throw "Encabezado no reconocido: $nombre"}
        $def=[regex]::Replace($def,$encabezado,'ALTER PROCEDURE')
        if($def -match 'SynchroDB_Pruebas'){throw "Adaptacion incompleta: $nombre"}
        $cambios+=@{Nombre=$nombre;Def=$def;Ansi=$ansi;Quoted=$quoted}
    }
    $q.Parameters.Clear()
    foreach($c in $cambios){
        $ansi=if($c.Ansi){'ON'}else{'OFF'}
        $quoted=if($c.Quoted){'ON'}else{'OFF'}
        $q.CommandText="SET ANSI_NULLS $ansi; SET QUOTED_IDENTIFIER $quoted;"
        [void]$q.ExecuteNonQuery()
        $q.CommandText=$c.Def
        [void]$q.ExecuteNonQuery()
    }
    foreach($nombre in $procedimientos.Keys){
        $q.Parameters.Clear()
        $q.CommandText="SELECT COUNT(*) FROM sys.procedures p JOIN sys.sql_modules m ON m.object_id=p.object_id WHERE p.name=@nombre AND CHARINDEX(@guardia,m.definition)>0 AND m.definition NOT LIKE N'%SynchroDB[_]Pruebas%';"
        [void]$q.Parameters.Add('@nombre',[System.Data.SqlDbType]::NVarChar,128)
        [void]$q.Parameters.Add('@guardia',[System.Data.SqlDbType]::NVarChar,4000)
        $q.Parameters['@nombre'].Value=$nombre
        $q.Parameters['@guardia'].Value=$guardia
        if([int]$q.ExecuteScalar() -ne 1){throw "Validacion final incorrecta: $nombre"}
    }
    $tx.Commit()
    foreach($c in $cambios){Write-Host "[OK] Adaptado: $($c.Nombre)"}
    Write-Host '[OK] Cobros simulados preparados; sin modificar registros de usuarios, reservas, PIN o pagos.' -ForegroundColor Green
} catch {
    if($null -ne $tx){try{$tx.Rollback()}catch{}}
    throw
} finally {
    if($null -ne $tx){$tx.Dispose()}
    $conexion.Dispose()
}
