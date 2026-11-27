# SYNCHRO: catalogo de precios de feria autorizado para el prototipo aislado.
# Sin parametros: aplica SOLAMENTE tarifas de carga. -SoloVerificar: solo lectura.
# La configuracion de resguardo se carga como variables del proceso en cada inicio.
param([switch]$SoloVerificar)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$cultura = [System.Globalization.CultureInfo]::InvariantCulture
$ruta = Join-Path $PSScriptRoot 'TarifasFeria-Pruebas.json'
$cfg = Get-Content -LiteralPath $ruta -Raw | ConvertFrom-Json
$personal = $cfg.ResguardoPersonal
if ($personal.MinutosBase -ne 60 -or $personal.MinutosPorBloque -ne 30) {
    throw 'El procedimiento de resguardo requiere 60 minutos base y bloques de 30.'
}
function ValidarPrecio([decimal]$precio) {
    if ($precio -le 0 -or $precio -gt 100000 -or $precio -ne [decimal]::Round($precio,2)) {
        throw 'Precio invalido en el catalogo de feria.'
    }
}
foreach ($tamano in @('Pequeno','Mediano','Grande')) {
    ValidarPrecio $personal.Tarifas.$tamano
    ValidarPrecio $personal.Adicionales.$tamano
}
$modalidades = @('Inalambrica','Cable','Laptop')
if (@($cfg.Carga).Count -ne 3) { throw 'Se requieren las tres tarifas de carga.' }
foreach ($modalidad in $modalidades) {
    $tarifa = @($cfg.Carga | Where-Object { $_.Modalidad -ceq $modalidad })
    if ($tarifa.Count -ne 1 -or $tarifa[0].MinutosBase -ne 60 -or $tarifa[0].MinutosAdicionales -ne 30) {
        throw "Tarifa de $modalidad invalida."
    }
    ValidarPrecio $tarifa[0].PrecioBase
    ValidarPrecio $tarifa[0].PrecioAdicional
}

$db = [System.Data.SqlClient.SqlConnection]::new('Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Connect Timeout=10')
$tx = $null
try {
    $db.Open()
    $cmd = $db.CreateCommand()
    $cmd.CommandText = @'
SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas'
AND CONVERT(int,SERVERPROPERTY('IsLocalDB'))=1
AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito' AND CONVERT(nvarchar(10),value)=N'1')
AND OBJECT_ID(N'dbo.TarifasCargaSynchro',N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.CobrosRetiroCargaSynchro',N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.CobrosRetiroPersonal',N'U') IS NOT NULL
AND OBJECT_ID(N'dbo.PrevalidarRetiroPersonalSynchro',N'P') IS NOT NULL
AND OBJECT_ID(N'dbo.PrepararCobroRetiroPersonalSynchro',N'P') IS NOT NULL
AND OBJECT_ID(N'dbo.FinalizarRetiroPersonalPagadoSynchro',N'P') IS NOT NULL
AND EXISTS(SELECT 1 FROM sys.parameters WHERE object_id=OBJECT_ID(N'dbo.PrepararCobroRetiroPersonalSynchro') AND name=N'@AdicionalGrandeHnl')
THEN 1 ELSE 0 END;
'@
    if ([int]$cmd.ExecuteScalar() -ne 1) {
        throw 'La base aislada o el esquema de cobros no esta listo. No se aplicaron tarifas. Revisar las migraciones existentes.'
    }

    if (-not $SoloVerificar) {
        $tx = $db.BeginTransaction([System.Data.IsolationLevel]::Serializable)
        $cmd.Transaction = $tx
        $cmd.CommandText = @'
IF EXISTS(SELECT 1 FROM dbo.TarifasCargaSynchro WITH(UPDLOCK,HOLDLOCK) WHERE Modalidad=@modalidad)
BEGIN
    UPDATE dbo.TarifasCargaSynchro
    SET MinutosBase=@minBase,PrecioBase=@precioBase,MinutosAdicionales=@minExtra,PrecioAdicional=@precioExtra
    WHERE Modalidad=@modalidad;
    IF @@ROWCOUNT<>1 THROW 59701,'No se actualizo exactamente una tarifa.',1;
END
ELSE
BEGIN
    INSERT dbo.TarifasCargaSynchro(Modalidad,MinutosBase,PrecioBase,MinutosAdicionales,PrecioAdicional)
    VALUES(@modalidad,@minBase,@precioBase,@minExtra,@precioExtra);
    IF @@ROWCOUNT<>1 THROW 59701,'No se inserto exactamente una tarifa.',1;
END
'@
        [void]$cmd.Parameters.Add('@modalidad',[System.Data.SqlDbType]::NVarChar,20)
        [void]$cmd.Parameters.Add('@minBase',[System.Data.SqlDbType]::Int)
        [void]$cmd.Parameters.Add('@minExtra',[System.Data.SqlDbType]::Int)
        foreach ($nombre in @('@precioBase','@precioExtra')) {
            $p = $cmd.Parameters.Add($nombre,[System.Data.SqlDbType]::Decimal)
            $p.Precision=12
            $p.Scale=2
        }
        foreach ($tarifa in $cfg.Carga) {
            $cmd.Parameters['@modalidad'].Value=[string]$tarifa.Modalidad
            $cmd.Parameters['@minBase'].Value=[int]$tarifa.MinutosBase
            $cmd.Parameters['@minExtra'].Value=[int]$tarifa.MinutosAdicionales
            $cmd.Parameters['@precioBase'].Value=[decimal]$tarifa.PrecioBase
            $cmd.Parameters['@precioExtra'].Value=[decimal]$tarifa.PrecioAdicional
            [void]$cmd.ExecuteNonQuery()
        }
    }

    $consulta = $db.CreateCommand()
    if ($null -ne $tx) { $consulta.Transaction=$tx }
    $consulta.CommandText = @'
SELECT Modalidad,MinutosBase,PrecioBase,MinutosAdicionales,PrecioAdicional
FROM dbo.TarifasCargaSynchro
WHERE Modalidad IN(N'Inalambrica',N'Cable',N'Laptop')
ORDER BY Modalidad;
'@
    $tabla = [System.Data.DataTable]::new()
    $adaptador = [System.Data.SqlClient.SqlDataAdapter]::new($consulta)
    [void]$adaptador.Fill($tabla)
    if ($tabla.Rows.Count -ne 3) {
        throw 'Faltan modalidades de carga. Ejecuta Configurar-TarifasFeria-Pruebas.ps1 sin -SoloVerificar.'
    }
    foreach ($fila in $tabla.Rows) {
        if ([int]$fila.MinutosBase -lt 1 -or [int]$fila.MinutosBase -gt 1440 -or
            [int]$fila.MinutosAdicionales -lt 1 -or [int]$fila.MinutosAdicionales -gt 1440) {
            throw 'Duracion de carga invalida en SQL.'
        }
        ValidarPrecio ([decimal]$fila.PrecioBase)
        ValidarPrecio ([decimal]$fila.PrecioAdicional)
        if (-not $SoloVerificar) {
            $esperada = @($cfg.Carga | Where-Object { $_.Modalidad -ceq [string]$fila.Modalidad })[0]
            if ([int]$fila.MinutosBase -ne $esperada.MinutosBase -or
                [int]$fila.MinutosAdicionales -ne $esperada.MinutosAdicionales -or
                [decimal]$fila.PrecioBase -ne $esperada.PrecioBase -or
                [decimal]$fila.PrecioAdicional -ne $esperada.PrecioAdicional) {
                throw 'La tarifa guardada no coincide con el catalogo.'
            }
        }
    }
    if ($null -ne $tx) { $tx.Commit(); $tx=$null }

    # Precios de resguardo y cobro obligatorio: solo proceso del backend aislado.
    $env:CobroRetiroPersonal__Obligatorio='true'
    $env:CobroRetiroPersonal__MinutosPorBloque=([int]$personal.MinutosPorBloque).ToString($cultura)
    foreach ($tamano in @('Pequeno','Mediano','Grande')) {
        foreach ($grupo in @('Tarifas','Adicionales')) {
            [Environment]::SetEnvironmentVariable("CobroRetiroPersonal__${grupo}__${tamano}",([decimal]$personal.$grupo.$tamano).ToString($cultura),'Process')
        }
    }

    Write-Host '=== TARIFAS RESGUARDO PERSONAL: COBRO OBLIGATORIO ===' -ForegroundColor Cyan
    foreach ($tamano in @('Pequeno','Mediano','Grande')) {
        Write-Host ("{0}: 60 min L {1}; 30 min adicionales L {2}" -f $tamano,$personal.Tarifas.$tamano,$personal.Adicionales.$tamano)
    }
    Write-Host '=== TARIFAS DE CARGA EN SQL ===' -ForegroundColor Cyan
    $tabla | Format-Table -AutoSize | Out-Host
    if (-not $SoloVerificar) { Write-Host '[OK] Tres tarifas guardadas. No se modificaron sesiones, cobros ni reservas.' -ForegroundColor Green }
    else { Write-Host '[OK] Tarifas verificadas y configuracion personal cargada. SQL sin cambios.' -ForegroundColor Green }
}
catch {
    if ($null -ne $tx) { try { $tx.Rollback() } catch {} }
    throw
}
finally { $db.Dispose() }
