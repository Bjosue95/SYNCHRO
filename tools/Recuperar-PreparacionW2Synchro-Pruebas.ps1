# SYNCHRO: recuperacion puntual de preparacion abandonada de W2.
# SOLO entorno SQL aislado de pruebas. No libera una carga iniciada.
# No modifica tarifas, pagos ni operaciones de otros espacios.
# Ejecutar solo despues de que la tablet perdiera el token temporal.
$ErrorActionPreference = "Stop"

$cadena = "Data Source=(localdb)\SYNCHRO_Aislado;Initial Catalog=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
$db = [System.Data.SqlClient.SqlConnection]::new($cadena)
try {
    $db.Open()

    $cmd = $db.CreateCommand()
    $cmd.CommandTimeout = 20
    $cmd.CommandText = @'
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'SynchroDB_Pruebas'
    THROW 51201, 'No es la base aislada de pruebas.', 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.extended_properties
    WHERE class=0
      AND name=N'SynchroFixtureDeposito'
      AND CONVERT(nvarchar(10),value)=N'1'
)
    THROW 51202, 'Falta marcador del prototipo aislado.', 1;

IF OBJECT_ID(N'dbo.EspaciosCarga', N'U') IS NULL
   OR OBJECT_ID(N'dbo.SesionesCargaSynchro', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CobrosRetiroCargaSynchro', N'U') IS NULL
    THROW 51203, 'No existe el esquema esperado de carga.', 1;

BEGIN TRANSACTION;

DECLARE @estadoEspacio nvarchar(30);
SELECT @estadoEspacio = Estado
FROM dbo.EspaciosCarga WITH (UPDLOCK, HOLDLOCK)
WHERE Codigo = N'W2';

IF @estadoEspacio IS NULL OR @estadoEspacio <> N'Disponible'
    THROW 51204, 'W2 no esta disponible. No se modifico nada.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.SesionesCargaSynchro WITH (UPDLOCK, HOLDLOCK)
    WHERE CodigoEspacio = N'W2'
      AND (Estado = N'Activa' OR
           (InicioUtc IS NOT NULL AND RetiroUtc IS NULL))
)
    THROW 51205, 'W2 tiene o tuvo una sesion activa sin retiro. No se modifica.', 1;

DECLARE @preparadas int;
SELECT @preparadas = COUNT(*)
FROM dbo.SesionesCargaSynchro WITH (UPDLOCK, HOLDLOCK)
WHERE CodigoEspacio = N'W2' AND Estado = N'Preparada';

IF @preparadas <> 1
    THROW 51206, 'W2 no tiene exactamente una preparacion pendiente. No se modifica.', 1;

IF EXISTS (
    SELECT 1 FROM dbo.SesionesCargaSynchro AS s
    INNER JOIN dbo.CobrosRetiroCargaSynchro AS c
      ON c.SesionId = s.Id
    WHERE s.CodigoEspacio = N'W2' AND s.Estado = N'Preparada'
)
    THROW 51207, 'La preparacion de W2 tiene cobro. No se modifica.', 1;

UPDATE dbo.SesionesCargaSynchro
SET Estado = N'Cancelada'
WHERE CodigoEspacio = N'W2'
  AND Estado = N'Preparada'
  AND InicioUtc IS NULL AND RetiroUtc IS NULL;

IF @@ROWCOUNT <> 1
    THROW 51208, 'La preparacion cambio durante la revision. No se modifica.', 1;

COMMIT TRANSACTION;

SELECT N'W2: preparacion abandonada cancelada; espacio Disponible; cero sesiones activas; sin cobros modificados.' AS Resultado;
'@

    Write-Host "=== RECUPERACION SEGURA DE PREPARACION W2 ==="
    $resultado = $cmd.ExecuteScalar()
    Write-Host "[OK] $resultado"
    Write-Host "Ningun otro espacio, sesion activa, cobro o tarifa se modifico."
}
catch {
    Write-Host "[DETENIDO] $($_.Exception.Message)"
    Write-Host "No se fuerza ningun cambio. Ejecuta el diagnostico de solo lectura."
    exit 1
}
finally {
    $db.Dispose()
}
