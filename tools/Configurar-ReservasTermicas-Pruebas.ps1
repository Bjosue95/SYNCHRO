# SYNCHRO: configurar M04/M09 en la base AISLADA de pruebas.
# Solicita valores: NO inventa tarifas ni temperaturas.
# SOLO M04 y M09; respaldo previo, validaciones y transaccion atomica.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$cultura = [System.Globalization.CultureInfo]::InvariantCulture
$estilo = [System.Globalization.NumberStyles]::AllowLeadingSign -bor [System.Globalization.NumberStyles]::AllowDecimalPoint

function LeerNumero([string]$pregunta) {
    while ($true) {
        $valor = (Read-Host $pregunta).Trim().Replace(',', '.')
        $numero = [decimal]0
        if ([decimal]::TryParse($valor,$estilo,$cultura,[ref]$numero)) { return $numero }
        Write-Host 'Numero invalido. Ejemplo: 12.50' -ForegroundColor Yellow
    }
}

Write-Host '=== CONFIGURAR M04 FRIO Y M09 CALOR ===' -ForegroundColor Cyan
Write-Host 'Valores de temperatura para pruebas: NO son lecturas de sensores fisicos.' -ForegroundColor Yellow

$db = [System.Data.SqlClient.SqlConnection]::new(
    'Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False;Connect Timeout=10'
)
try {
    $db.Open()
    $validar = $db.CreateCommand()
    $validar.CommandText = @'
SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas'
 AND CONVERT(int,SERVERPROPERTY('IsLocalDB'))=1
 AND EXISTS(SELECT 1 FROM sys.extended_properties
    WHERE class=0 AND name=N'SynchroFixtureDeposito'
      AND CONVERT(nvarchar(10),value)=N'1')
 AND OBJECT_ID(N'dbo.CompartimentosServiciosTemporales',N'U') IS NOT NULL
 AND OBJECT_ID(N'dbo.OperacionesServiciosTemporales',N'U') IS NOT NULL
 AND OBJECT_ID(N'dbo.AuditoriaServiciosTemporales',N'U') IS NOT NULL
 THEN 1 ELSE 0 END;
'@
    if ([int]$validar.ExecuteScalar() -ne 1) { throw 'La base o las tablas de pruebas no son compatibles.' }

    $revisar = $db.CreateCommand()
    $revisar.CommandText = @'
SELECT c.Codigo,c.Tipo,c.Tamano,c.Estado,
       CASE WHEN t.Codigo IS NULL THEN 0 ELSE 1 END AS Configurada,
       (SELECT COUNT(*) FROM dbo.UsosCasillero u
         WHERE u.CodigoCasillero=c.Codigo AND u.FechaRetiro IS NULL
           AND u.Estado IN(N'Reservado',N'Ocupado')) AS UsosActivos,
       (SELECT COUNT(*) FROM dbo.OperacionesServiciosTemporales s
         WHERE s.Codigo=c.Codigo AND s.Estado IN(N'Reservada',N'Pagada',N'Activa',N'Agotada')) AS ServiciosActivos
FROM dbo.Casilleros c
LEFT JOIN dbo.CompartimentosServiciosTemporales t ON t.Codigo=c.Codigo
WHERE c.Codigo IN(N'M04',N'M09');
'@
    $estado = [System.Data.DataTable]::new()
    $adapter = [System.Data.SqlClient.SqlDataAdapter]::new($revisar)
    [void]$adapter.Fill($estado)
    if ($estado.Rows.Count -ne 2) { throw 'El inventario no contiene ambos casilleros M04 y M09.' }
    foreach ($r in $estado.Rows) {
        $codigo = [string]$r['Codigo']
        $tipos = if ($codigo -eq 'M04') { @('Normal','Frio','Refrigerado') } else { @('Normal','Calor','Caliente') }
        if ([string]$r['Estado'] -ne 'Disponible' -or [string]$r['Tamano'] -ne 'Mediano' -or
            [string]$r['Tipo'] -notin $tipos -or [int]$r['Configurada'] -ne 0 -or
            [int]$r['UsosActivos'] -ne 0 -or [int]$r['ServiciosActivos'] -ne 0) {
            throw "El estado de $codigo cambio o ya tiene configuracion/ocupacion. No se modificara nada."
        }
        Write-Host ("[OK] {0} libre, sin configuracion termica" -f $codigo) -ForegroundColor Green
    }

    $entradas = @{}
    foreach ($modulo in @(
        [pscustomobject]@{ Codigo='M04'; Clase='Frio' },
        [pscustomobject]@{ Codigo='M09'; Clase='Calor' }
    )) {
        Write-Host ''
        Write-Host ("=== {0} / {1} ===" -f $modulo.Codigo,$modulo.Clase) -ForegroundColor Cyan
        $minimo = LeerNumero 'Temperatura MINIMA configurada (C)'
        $maximo = LeerNumero 'Temperatura MAXIMA configurada (C)'
        if ($minimo -ge $maximo -or $minimo -lt -273.15 -or $maximo -gt 200) {
            throw "Rango de temperatura invalido para $($modulo.Codigo)."
        }
        $valor = LeerNumero 'Temperatura usada para pruebas (NO lectura de sensor) (C)'
        if ($valor -lt $minimo -or $valor -gt $maximo) {
            throw "El valor de temperatura de $($modulo.Codigo) no esta dentro del rango."
        }
        $tarifas = [ordered]@{}
        foreach ($minutos in @(30,60,120)) {
            $precio = LeerNumero ("Precio en Lempiras por {0} minutos" -f $minutos)
            if ($precio -le 0 -or $precio -gt 100000 -or $precio -ne [decimal]::Round($precio,2)) {
                throw "Precio invalido para $($modulo.Codigo): debe ser positivo y con maximo dos decimales."
            }
            $tarifas[[string]$minutos] = $precio
        }
        $config = [ordered]@{
            Codigo=$modulo.Codigo; Clase=$modulo.Clase; Conector=$null;
            Habilitado=$true; Tarifas=$tarifas;
            TemperaturaMinima=$minimo; TemperaturaMaxima=$maximo;
            TemperaturaSimulada=$valor; Tamano='Mediano'
        }
        $entradas[$modulo.Codigo] = ConvertTo-Json -InputObject $config -Depth 6 -Compress
    }

    Write-Host ''
    Write-Host 'Se respaldara la base y se configuraran SOLO M04 y M09.' -ForegroundColor Yellow
    if ((Read-Host 'Escribe CONFIGURAR para confirmar') -cne 'CONFIGURAR') {
        Write-Host 'Operacion cancelada. No se modificaron registros.'
        return
    }

    $carpeta = Join-Path $env:LOCALAPPDATA 'SYNCHRO\RespaldosSQL'
    New-Item -ItemType Directory -Force -Path $carpeta | Out-Null
    $ruta = Join-Path $carpeta ('SynchroDB_Pruebas_termicos_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'.bak')
    $backup = $db.CreateCommand()
    $backup.CommandTimeout = 180
    $backup.CommandText = 'BACKUP DATABASE [SynchroDB_Pruebas] TO DISK=@ruta WITH COPY_ONLY,INIT,CHECKSUM;'
    [void]$backup.Parameters.Add('@ruta',[System.Data.SqlDbType]::NVarChar,4000)
    $backup.Parameters['@ruta'].Value=$ruta
    [void]$backup.ExecuteNonQuery()
    Write-Host ("Respaldo creado: "+$ruta) -ForegroundColor Green

    $cmd=$db.CreateCommand()
    $cmd.CommandTimeout=60
    $cmd.CommandText=@'
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;

 IF DB_NAME()<>N'SynchroDB_Pruebas'
    OR CONVERT(int,SERVERPROPERTY('IsLocalDB'))<>1
    OR NOT EXISTS(SELECT 1 FROM sys.extended_properties
        WHERE class=0 AND name=N'SynchroFixtureDeposito'
          AND CONVERT(nvarchar(10),value)=N'1')
   THROW 59601,'Base de pruebas no autorizada.',1;

 IF (SELECT COUNT(*) FROM dbo.Casilleros WITH(UPDLOCK,HOLDLOCK)
    WHERE Codigo IN(N'M04',N'M09') AND Estado=N'Disponible' AND Tamano=N'Mediano'
    AND (Codigo=N'M04' AND Tipo IN(N'Normal',N'Frio',N'Refrigerado')
         OR Codigo=N'M09' AND Tipo IN(N'Normal',N'Calor',N'Caliente')))<>2
   THROW 59602,'Estado o tipo de casillero cambio. No se modifico nada.',1;

 IF EXISTS(SELECT 1 FROM dbo.CompartimentosServiciosTemporales WITH(UPDLOCK,HOLDLOCK)
     WHERE Codigo IN(N'M04',N'M09'))
   THROW 59603,'Ya existe configuracion termica. No se sobreescribe.',1;

 IF EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK)
     WHERE CodigoCasillero IN(N'M04',N'M09') AND FechaRetiro IS NULL
        AND Estado IN(N'Reservado',N'Ocupado'))
   OR EXISTS(SELECT 1 FROM dbo.OperacionesServiciosTemporales WITH(UPDLOCK,HOLDLOCK)
     WHERE Codigo IN(N'M04',N'M09')
       AND Estado IN(N'Reservada',N'Pagada',N'Activa',N'Agotada'))
   OR EXISTS(
     SELECT 1 FROM dbo.AuditoriaEncomiendaOperativa a
     JOIN dbo.EncomiendasOperativas o ON o.ReservaId=a.ReservaId
     JOIN dbo.PreReservasEncomiendasWeb r ON r.Id=o.ReservaId
     WHERE a.Accion=N'ReservaPrototipoCreada'
       AND o.Estado IN(N'Preparacion',N'ListaParaDeposito')
       AND r.FechaExpiracion>SYSUTCDATETIME()
       AND JSON_VALUE(CASE WHEN ISJSON(a.Detalle)=1
           THEN a.Detalle ELSE N'{}' END,'$.CodigoCasillero') IN(N'M04',N'M09'))
   THROW 59604,'Existe una operacion activa. No se modifico nada.',1;

 UPDATE dbo.Casilleros SET Tipo =
   CASE Codigo WHEN N'M04' THEN N'Refrigerado'
               WHEN N'M09' THEN N'Caliente' END
 WHERE Codigo IN(N'M04',N'M09');
 IF @@ROWCOUNT<>2 THROW 59605,'No se actualizaron los dos tipos termicos.',1;

 INSERT dbo.CompartimentosServiciosTemporales(Codigo,ConfiguracionJson)
 VALUES(N'M04',@frio),(N'M09',@calor);

 INSERT dbo.AuditoriaServiciosTemporales(Codigo,OperacionId,Accion,Actor,FechaUtc)
 VALUES
   (N'M04',NULL,N'ConfiguracionTermicaPruebas',N'ConfiguradorLocal',SYSUTCDATETIME()),
   (N'M09',NULL,N'ConfiguracionTermicaPruebas',N'ConfiguradorLocal',SYSUTCDATETIME());

 COMMIT;
 SELECT Codigo,Tipo,Tamano,Estado FROM dbo.Casilleros
 WHERE Codigo IN(N'M04',N'M09') ORDER BY Codigo;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
'@
    [void]$cmd.Parameters.Add('@frio',[System.Data.SqlDbType]::NVarChar,-1)
    [void]$cmd.Parameters.Add('@calor',[System.Data.SqlDbType]::NVarChar,-1)
    $cmd.Parameters['@frio'].Value=$entradas['M04']
    $cmd.Parameters['@calor'].Value=$entradas['M09']
    $resultado = [System.Data.DataTable]::new()
    $adaptador=[System.Data.SqlClient.SqlDataAdapter]::new($cmd)
    [void]$adaptador.Fill($resultado)
    Write-Host ''
    Write-Host 'CONFIGURACION TERMICA GUARDADA' -ForegroundColor Green
    $resultado | Format-Table -AutoSize
    Write-Host 'Estos valores no verifican sensores ni temperatura fisica.' -ForegroundColor Yellow
}
finally { $db.Dispose() }
