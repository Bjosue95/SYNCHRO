# Activacion de demostracion local: no hay sensores, refrigeracion ni calefaccion real.
$ErrorActionPreference = 'Stop'
$c = [System.Data.SqlClient.SqlConnection]::new('Server=.\SQLEXPRESS;Database=SynchroDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15')
try {
    $c.Open()
    $q = $c.CreateCommand()
    $q.CommandTimeout = 60
    $q.CommandText = @'
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'SynchroDB'
 OR ISNULL(CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')),N'')<>N'SQLEXPRESS'
 OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),1)<>0
 OR EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'SynchroFixtureDeposito')
 THROW 59080,'Destino no autorizado.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF EXISTS(SELECT 1 FROM dbo.UsosCasillero WITH(UPDLOCK,HOLDLOCK) WHERE CodigoCasillero IN(N'M04',N'M09') AND FechaRetiro IS NULL)
 OR EXISTS(SELECT 1 FROM dbo.OperacionesServiciosTemporales WITH(UPDLOCK,HOLDLOCK) WHERE Codigo IN(N'M04',N'M09'))
 THROW 59081,'Existen operaciones termicas.',1;
 IF (SELECT COUNT(*) FROM dbo.Casilleros c WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.CompartimentosServiciosTemporales t WITH(UPDLOCK,HOLDLOCK) ON t.Codigo=c.Codigo
 WHERE c.Tamano=N'Mediano' AND c.Estado=N'Bloqueado'
 AND ISJSON(t.ConfiguracionJson)=1 AND JSON_VALUE(t.ConfiguracionJson,'$.Habilitado')=N'false'
 AND JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaSimulada') IS NULL
 AND ((c.Codigo=N'M04' AND c.Tipo=N'Refrigerado' AND JSON_VALUE(t.ConfiguracionJson,'$.Clase')=N'Frio'
 AND TRY_CONVERT(decimal(10,2),JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaMinima'))=-5
 AND TRY_CONVERT(decimal(10,2),JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaMaxima'))=5)
 OR (c.Codigo=N'M09' AND c.Tipo=N'Caliente' AND JSON_VALUE(t.ConfiguracionJson,'$.Clase')=N'Calor'
 AND TRY_CONVERT(decimal(10,2),JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaMinima'))=35
 AND TRY_CONVERT(decimal(10,2),JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaMaxima'))=40)))<>2
 THROW 59082,'Configuracion termica inicial inesperada o ya activada.',1;
 UPDATE dbo.CompartimentosServiciosTemporales
 SET ConfiguracionJson=JSON_MODIFY(JSON_MODIFY(ConfiguracionJson,'$.Habilitado',CAST(1 AS bit)),
 '$.TemperaturaSimulada',CASE Codigo WHEN N'M04' THEN 5 ELSE 40 END)
 WHERE Codigo IN(N'M04',N'M09');
 IF @@ROWCOUNT<>2 THROW 59083,'Configuracion incompleta.',1;
 UPDATE dbo.Casilleros SET Estado=N'Disponible' WHERE Codigo IN(N'M04',N'M09') AND Estado=N'Bloqueado';
 IF @@ROWCOUNT<>2 THROW 59084,'Inventario incompleto.',1;
 COMMIT;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 THROW;
END CATCH;
SELECT c.Codigo,c.Estado,JSON_VALUE(t.ConfiguracionJson,'$.Habilitado') AS Habilitado,
 JSON_VALUE(t.ConfiguracionJson,'$.TemperaturaSimulada') AS TemperaturaFicticia,
 JSON_QUERY(t.ConfiguracionJson,'$.Tarifas') AS Tarifas
FROM dbo.Casilleros c JOIN dbo.CompartimentosServiciosTemporales t ON c.Codigo=t.Codigo
WHERE c.Codigo IN(N'M04',N'M09') ORDER BY c.Codigo;
'@
    $tabla = [System.Data.DataTable]::new()
    $adaptador = [System.Data.SqlClient.SqlDataAdapter]::new($q)
    [void]$adaptador.Fill($tabla)
    $tabla | Format-Table -AutoSize | Out-Host
    Write-Host '[OK] Modulos termicos disponibles en la demostracion. Lecturas ficticias; no usar para almacenamiento con temperatura controlada real.' -ForegroundColor Green
}
finally { $c.Dispose() }
