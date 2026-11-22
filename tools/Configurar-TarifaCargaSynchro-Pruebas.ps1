# SYNCHRO V3 - Configurador explicito de tarifas de CARGA.
# Solo SynchroDB_Pruebas, nunca inventa ni modifica otras tarifas.
$ErrorActionPreference = "Stop"
$cadena = "Data Source=(localdb)\SYNCHRO_Aislado;Initial Catalog=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=10"
$cultura = [System.Globalization.CultureInfo]::InvariantCulture
$estilo = [System.Globalization.NumberStyles]::AllowDecimalPoint

function LeerMinutos([string]$etiqueta) {
    while ($true) {
        $texto = (Read-Host "$etiqueta (1 a 1440)").Trim()
        [int]$n = 0
        if ([int]::TryParse($texto, [ref]$n) -and $n -ge 1 -and $n -le 1440) {
            return $n
        }
        Write-Host "Ingresa un numero entero entre 1 y 1440."
    }
}
function LeerPrecio([string]$etiqueta) {
    while ($true) {
        $texto = (Read-Host "$etiqueta, en L (ejemplo: 12.50)").Trim()
        [decimal]$n = 0
        if ($texto -match '^[0-9]{1,7}(\.[0-9]{1,2})?$' -and
            [decimal]::TryParse($texto, $estilo, $cultura, [ref]$n) -and
            $n -gt 0 -and $n -le 100000) {
            return $n
        }
        Write-Host "Importe no valido. Usa punto decimal; mayor a cero y no mas de L 100000."
    }
}

$db = [System.Data.SqlClient.SqlConnection]::new($cadena)
$tx = $null
try {
    $db.Open()
    $verificar = $db.CreateCommand()
    $verificar.CommandText = @'
SELECT CASE WHEN DB_NAME()=N'SynchroDB_Pruebas' AND EXISTS (
  SELECT 1 FROM sys.extended_properties
  WHERE class=0 AND name=N'SynchroFixtureDeposito'
    AND CONVERT(nvarchar(10),value)=N'1'
)
AND OBJECT_ID(N'dbo.TarifasCargaSynchro',N'U') IS NOT NULL
THEN 1 ELSE 0 END;
'@
    if ([int]$verificar.ExecuteScalar() -ne 1) {
        throw "Falta el catalogo propio de tarifas. Ejecuta primero Preparar-CobroRetiroCarga-Pruebas.ps1."
    }

    Write-Host "=== CONFIGURACION DE TARIFAS DE CARGA (SOLO PROTOTIPO) ==="
    Write-Host "Se aplican a sesiones futuras de cobro que aun no tienen importe preparado."
    Write-Host "Modalidades reales: Inalambrica (W1/W2), Cable (C1-C4), Laptop (L1/L2)."
    $modalidad = (Read-Host "Modalidad: Inalambrica, Cable o Laptop").Trim()
    if ($modalidad -cnotin @("Inalambrica","Cable","Laptop")) {
        throw "Modalidad invalida. No se modifico nada."
    }

    $minBase = LeerMinutos "Minutos incluidos en precio base"
    $precioBase = LeerPrecio "Precio base"
    $minExtra = LeerMinutos "Minutos de cada bloque adicional"
    $precioExtra = LeerPrecio "Precio de cada bloque adicional"

    $comprobar = $db.CreateCommand()
    $comprobar.CommandText = "SELECT COUNT(*) FROM dbo.TarifasCargaSynchro WHERE Modalidad=@modalidad;"
    $null = $comprobar.Parameters.Add("@modalidad", [System.Data.SqlDbType]::NVarChar, 20)
    $comprobar.Parameters["@modalidad"].Value = $modalidad
    $n = [int]$comprobar.ExecuteScalar()
    if ($n -gt 1) { throw "Existen tarifas duplicadas para esa modalidad. No se modifico nada." }
    $existe = $n -eq 1

    Write-Host ""
    Write-Host ("RESUMEN: modalidad {0}, {1} min base por L {2}; {3} min extra por L {4}" -f
        $modalidad, $minBase, $precioBase, $minExtra, $precioExtra)
    $accion = if ($existe) { "ACTUALIZAR TARIFA" } else { "GUARDAR TARIFA" }
    $respuesta = Read-Host "Para confirmar escribe $accion"
    if ($respuesta -cne $accion) {
        Write-Host "Operacion cancelada: ninguna tarifa modificada."
        return
    }

    $tx = $db.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    $comando = $db.CreateCommand()
    $comando.Transaction = $tx
    $comando.CommandText = @'
IF EXISTS (
    SELECT 1 FROM dbo.TarifasCargaSynchro WITH (UPDLOCK,HOLDLOCK)
    WHERE Modalidad=@modalidad
)
    UPDATE dbo.TarifasCargaSynchro
    SET MinutosBase=@minBase, PrecioBase=@precioBase,
        MinutosAdicionales=@minExtra, PrecioAdicional=@precioExtra
    WHERE Modalidad=@modalidad;
ELSE
    INSERT dbo.TarifasCargaSynchro
        (Modalidad,MinutosBase,PrecioBase,MinutosAdicionales,PrecioAdicional)
    VALUES (@modalidad,@minBase,@precioBase,@minExtra,@precioExtra);
'@
    $null = $comando.Parameters.Add("@modalidad", [System.Data.SqlDbType]::NVarChar, 20)
    $comando.Parameters["@modalidad"].Value = $modalidad
    $null = $comando.Parameters.Add("@minBase", [System.Data.SqlDbType]::Int)
    $comando.Parameters["@minBase"].Value = $minBase
    $null = $comando.Parameters.Add("@minExtra", [System.Data.SqlDbType]::Int)
    $comando.Parameters["@minExtra"].Value = $minExtra
    foreach ($clave in @("precioBase","precioExtra")) {
        $p = $comando.Parameters.Add("@$clave", [System.Data.SqlDbType]::Decimal)
        $p.Precision = 12
        $p.Scale = 2
        $p.Value = if ($clave -eq "precioBase") { $precioBase } else { $precioExtra }
    }
    $filas = $comando.ExecuteNonQuery()
    if ($filas -lt 1) { throw "No se guardo la tarifa." }
    $tx.Commit()
    $tx = $null
    Write-Host "[OK] Tarifa de $modalidad guardada. Ninguna sesion ni cobro modificado."
}
catch {
    if ($null -ne $tx) {
        try { $tx.Rollback() } catch {}
    }
    Write-Host "[DETENIDO] $($_.Exception.Message)"
    exit 1
}
finally { $db.Dispose() }
