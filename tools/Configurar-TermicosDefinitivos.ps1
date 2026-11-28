# Importar exclusivamente los rangos y las tarifas autorizados, sin lectura simulada.
$ErrorActionPreference = 'Stop'
$c = [System.Data.SqlClient.SqlConnection]::new('Server=.\SQLEXPRESS;Database=SynchroDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15')
try {
    $c.Open()
    $q = $c.CreateCommand()
    $q.CommandTimeout = 60
    $q.CommandText = @'
SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME()<>N'SynchroDB'
 OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS'
 OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1)<>0
 THROW 59030,'Destino no autorizado.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF (SELECT COUNT(*) FROM dbo.Casilleros WITH(UPDLOCK,HOLDLOCK)
     WHERE Estado=N'Bloqueado' AND Tamano=N'Mediano' AND
      ((Codigo=N'M04' AND Tipo=N'Refrigerado') OR (Codigo=N'M09' AND Tipo=N'Caliente')))<>2
  THROW 59031,'Inventario termico inesperado.',1;
 IF EXISTS(SELECT 1 FROM dbo.CompartimentosServiciosTemporales WITH(UPDLOCK,HOLDLOCK) WHERE Codigo IN(N'M04',N'M09'))
  THROW 59032,'Ya existe configuracion termica. No se sobrescribe.',1;
 IF EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK) WHERE CodigoCasillero IN(N'M04',N'M09') AND FechaRetiro IS NULL)
  OR EXISTS(SELECT 1 FROM dbo.OperacionesServiciosTemporales WITH(UPDLOCK,HOLDLOCK) WHERE Codigo IN(N'M04',N'M09'))
  THROW 59033,'Hay operaciones termicas. No se modifica.',1;
 INSERT dbo.CompartimentosServiciosTemporales(Codigo,ConfiguracionJson)
 VALUES
 (N'M04',N'{"Codigo":"M04","Clase":"Frio","Conector":null,"Habilitado":false,"Tarifas":{"30":10,"60":20,"120":30},"TemperaturaMinima":-5,"TemperaturaMaxima":5,"TemperaturaSimulada":null,"Tamano":"Mediano"}'),
 (N'M09',N'{"Codigo":"M09","Clase":"Calor","Conector":null,"Habilitado":false,"Tarifas":{"30":10,"60":20,"120":30},"TemperaturaMinima":35,"TemperaturaMaxima":40,"TemperaturaSimulada":null,"Tamano":"Mediano"}');
 IF @@ROWCOUNT<>2 THROW 59034,'Configuracion incompleta.',1;
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
'@
    [void]$q.ExecuteNonQuery()
    $q.CommandText = @'
SELECT c.Codigo,c.Estado,
 JSON_VALUE(t.ConfiguracionJson,'$.Habilitado') AS Habilitado,
 JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaMinima') AS Minima,
 JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaMaxima') AS Maxima,
 JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaSimulada') AS LecturaSimulada,
 JSON_QUERY(t.ConfiguracionJson,'$.Tarifas') AS Tarifas
FROM dbo.Casilleros c JOIN dbo.CompartimentosServiciosTemporales t ON t.Codigo=c.Codigo
WHERE c.Codigo IN(N'M04',N'M09') ORDER BY c.Codigo;
'@
    $tabla = [System.Data.DataTable]::new()
    $adaptador = [System.Data.SqlClient.SqlDataAdapter]::new($q)
    [void]$adaptador.Fill($tabla)
    $tabla | Format-Table -AutoSize | Out-Host
    Write-Host '[OK] Rangos y tarifas termicos guardados. Ambos modulos siguen bloqueados y sin lectura simulada.' -ForegroundColor Green
}
finally { $c.Dispose() }
