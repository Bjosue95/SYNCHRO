# SYNCHRO - Diagnostico de disponibilidad de M04 (frio) y M09 (calor).
# SOLO LECTURA. No modifica casilleros, tarifas, configuracion ni operaciones.
$ErrorActionPreference = 'Stop'
$db = [System.Data.SqlClient.SqlConnection]::new(
  'Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False;Connect Timeout=10'
)
try {
  $db.Open()
  $consulta = $db.CreateCommand()
  $consulta.CommandText = @'
SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas'
    AND EXISTS (
      SELECT 1 FROM sys.extended_properties
      WHERE class=0 AND name=N'SynchroFixtureDeposito'
        AND CONVERT(nvarchar(10),value)=N'1'
    ) THEN 1 ELSE 0 END;
'@
  if ([int]$consulta.ExecuteScalar() -ne 1) {
    throw 'Esta comprobacion solo se permite en SynchroDB_Pruebas autorizada.'
  }
  $sql = @'
SELECT
    c.Codigo, c.Tamano, c.Tipo AS TipoEnSQL, c.Estado AS EstadoEnSQL,
    CASE WHEN t.Codigo IS NULL THEN N'FALTA'
         WHEN ISJSON(t.ConfiguracionJson) <> 1 THEN N'JSON INVALIDO'
         ELSE N'PRESENTE' END AS ConfiguracionTermica,
    JSON_VALUE(CASE WHEN ISJSON(t.ConfiguracionJson)=1
        THEN t.ConfiguracionJson ELSE N'{}' END,'$.Codigo') AS CodigoConfigurado,
    JSON_VALUE(CASE WHEN ISJSON(t.ConfiguracionJson)=1
        THEN t.ConfiguracionJson ELSE N'{}' END,'$.Clase') AS Clase,
    JSON_VALUE(CASE WHEN ISJSON(t.ConfiguracionJson)=1
        THEN t.ConfiguracionJson ELSE N'{}' END,'$.Habilitado') AS Habilitado,
    JSON_VALUE(CASE WHEN ISJSON(t.ConfiguracionJson)=1
        THEN t.ConfiguracionJson ELSE N'{}' END,'$.TemperaturaMinima') AS TempMin,
    JSON_VALUE(CASE WHEN ISJSON(t.ConfiguracionJson)=1
        THEN t.ConfiguracionJson ELSE N'{}' END,'$.TemperaturaMaxima') AS TempMax,
    JSON_VALUE(CASE WHEN ISJSON(t.ConfiguracionJson)=1
        THEN t.ConfiguracionJson ELSE N'{}' END,'$.TemperaturaSimulada') AS TempRegistrada,
    JSON_QUERY(CASE WHEN ISJSON(t.ConfiguracionJson)=1
        THEN t.ConfiguracionJson ELSE N'{}' END,'$.Tarifas') AS Tarifas,
    (SELECT COUNT(*) FROM dbo.UsosCasillero u
      WHERE u.CodigoCasillero=c.Codigo
        AND u.FechaRetiro IS NULL
        AND u.Estado IN(N'Reservado',N'Ocupado')) AS UsosActivos,
    (SELECT COUNT(*) FROM dbo.OperacionesServiciosTemporales s
      WHERE s.Codigo=c.Codigo
        AND s.Estado IN(N'Reservada',N'Pagada',N'Activa',N'Agotada')) AS TemporalesActivos
FROM dbo.Casilleros c
LEFT JOIN dbo.CompartimentosServiciosTemporales t ON t.Codigo=c.Codigo
WHERE c.Codigo IN(N'M04',N'M09')
ORDER BY c.Codigo;
'@
  $cmd = $db.CreateCommand()
  $cmd.CommandText = $sql
  $tabla = [System.Data.DataTable]::new()
  $adaptador = [System.Data.SqlClient.SqlDataAdapter]::new($cmd)
  [void]$adaptador.Fill($tabla)
  Write-Host '=== CASILLEROS TERMICOS DE LA ESTACION ===' -ForegroundColor Cyan
  if ($tabla.Rows.Count -ne 2) {
    Write-Host "ATENCION: solo se encontraron $($tabla.Rows.Count) de los 2 casilleros termicos." -ForegroundColor Yellow
  }
  foreach($r in $tabla.Rows) {
    $codigo = [string]$r['Codigo']
    $clase = [string]$r['Clase']
    $esperada = if ($codigo -eq 'M04') { 'Frio' } else { 'Calor' }
    $estado = [string]$r['EstadoEnSQL']
    $config = [string]$r['ConfiguracionTermica']
    $habilitado = [string]$r['Habilitado']
    $min = [decimal]0; $max = [decimal]0; $actual = [decimal]0
    $minOk=[decimal]::TryParse([string]$r['TempMin'],[System.Globalization.NumberStyles]::Number,[System.Globalization.CultureInfo]::InvariantCulture,[ref]$min)
    $maxOk=[decimal]::TryParse([string]$r['TempMax'],[System.Globalization.NumberStyles]::Number,[System.Globalization.CultureInfo]::InvariantCulture,[ref]$max)
    $actualOk=[decimal]::TryParse([string]$r['TempRegistrada'],[System.Globalization.NumberStyles]::Number,[System.Globalization.CultureInfo]::InvariantCulture,[ref]$actual)
    $razones=[System.Collections.Generic.List[string]]::new()
    if($estado -ne 'Disponible') { $razones.Add("Estado SQL: $estado") }
    if($config -ne 'PRESENTE') { $razones.Add('No tiene configuracion termica valida en tabla') }
    if([string]$r['CodigoConfigurado'] -ne $codigo) { $razones.Add('Codigo dentro de la configuracion no coincide') }
    if($clase -ne $esperada) { $razones.Add("Clase $clase distinta de $esperada") }
    if($habilitado -ne 'true') { $razones.Add('La configuracion no esta habilitada') }
    if(-not($minOk -and $maxOk -and $actualOk)) { $razones.Add('Faltan temperaturas en la configuracion') }
    elseif($min -ge $max -or $actual -lt $min -or $actual -gt $max) { $razones.Add('Temperatura registrada fuera del rango configurado') }
    if([int]$r['UsosActivos'] -gt 0 -or [int]$r['TemporalesActivos'] -gt 0) { $razones.Add('Existe una ocupacion o reserva activa') }
    if([string]$r['Tarifas'] -notmatch '\d') { $razones.Add('Falta el catalogo de tarifas termicas') }
    Write-Host ""
    Write-Host ("[{0}] {1} | Tamano: {2} | Configuracion: {3}" -f $codigo,$esperada,$r['Tamano'],$config)
    Write-Host ("Estado SQL: {0} | Ocupaciones: {1} | Temporales: {2}" -f $estado,$r['UsosActivos'],$r['TemporalesActivos'])
    Write-Host ("Clase: {0} | Habilitado: {1} | Rango: {2} a {3} | Registrada: {4}" -f $clase,$habilitado,$r['TempMin'],$r['TempMax'],$r['TempRegistrada'])
    Write-Host ("Tarifas: {0}" -f $r['Tarifas'])
    if($razones.Count) {
      foreach($razon in $razones) { Write-Host ("  REVISAR: " + $razon) -ForegroundColor Yellow }
    } else { Write-Host 'Configuracion basica apta. Revisar backend si sigue sin aparecer.' -ForegroundColor Green }
  }
}
catch {
  Write-Host ("No se pudo realizar el diagnostico: " + $_.Exception.GetBaseException().Message) -ForegroundColor Red
}
finally { $db.Dispose() }
