
$ErrorActionPreference = "Stop"

$base = "https://localhost:7280"

$telefonoRemitente = "+50498888888"
$telefonoDestinatario = "+50499999999"
$numeroOrigen = "50499999999"

$phoneId = "123456789012345"
$secretoWebhook = "synchro-secreto-local-no-produccion-2026"

$cadena = "Server=(localdb)\MSSQLLocalDB;Database=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;"

function Consultar-Consentimiento {
    param([int]$ReservaId)

    $conexion = [System.Data.SqlClient.SqlConnection]::new($cadena)

    try {
        $conexion.Open()

        if ($conexion.Database -ne "SynchroDB_Pruebas") {
            throw "Base de datos incorrecta."
        }

        $comando = $conexion.CreateCommand()
        $comando.CommandText = @"
SELECT Estado, IntentosFallidos
FROM dbo.ConsentimientosPreReservaWhatsApp
WHERE ReservaId = @Id;
"@

        $null = $comando.Parameters.Add(
            "@Id",
            [System.Data.SqlDbType]::Int
        )

        $comando.Parameters["@Id"].Value = $ReservaId

        $lector = $comando.ExecuteReader()

        try {
            if (-not $lector.Read()) {
                throw "No existe el consentimiento."
            }

            return [PSCustomObject]@{
                Estado = $lector.GetString(0)
                Intentos = $lector.GetInt32(1)
            }
        }
        finally {
            $lector.Dispose()
        }
    }
    finally {
        $conexion.Dispose()
    }
}

function Enviar-EventoFirmado {
    param(
        [string]$Texto,
        [string]$MensajeId
    )

    $evento = @{
        object = "whatsapp_business_account"
        entry = @(
            @{
                changes = @(
                    @{
                        field = "messages"
                        value = @{
                            metadata = @{
                                phone_number_id = $phoneId
                            }
                            messages = @(
                                @{
                                    id = $MensajeId
                                    from = $numeroOrigen
                                    type = "text"
                                    text = @{
                                        body = $Texto
                                    }
                                }
                            )
                        }
                    }
                )
            }
        )
    }

    $json = ConvertTo-Json `
        -InputObject $evento `
        -Depth 15 `
        -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $clave = [System.Text.Encoding]::UTF8.GetBytes($secretoWebhook)

    $hmac = [System.Security.Cryptography.HMACSHA256]::new($clave)

    try {
        $hash = $hmac.ComputeHash($bytes)

        $firma = [BitConverter]::ToString($hash).
            Replace("-", "").ToLowerInvariant()
    }
    finally {
        $hmac.Dispose()
    }

    Invoke-RestMethod `
        -Uri "$base/api/webhooks/whatsapp" `
        -Method Post `
        -Body $bytes `
        -ContentType "application/json" `
        -Headers @{
            "X-Hub-Signature-256" = "sha256=$firma"
        } `
        -UseBasicParsing | Out-Null
}

Write-Host "1. Creando sesion de cliente ficticio..."
. "$PSScriptRoot/ClienteWebSintetico.ps1"
$clientePrueba = Nueva-SesionClienteSintetico -Base $base -Telefono $telefonoRemitente
$sesion = $clientePrueba.Sesion
$csrfRespuesta = @{ csrfToken = $clientePrueba.CsrfToken }
$headers = @{ "X-CSRF-TOKEN" = $csrfRespuesta.csrfToken }

Write-Host "2. Creando pre-reserva ficticia..."

$cuerpoReserva = @{
    telefonoDestinatario = $telefonoDestinatario
    tamanoSolicitado = "Mediano"
} | ConvertTo-Json -Compress

$reserva = Invoke-RestMethod `
    -Uri "$base/api/web/encomiendas/pre-reservas" `
    -Method Post `
    -WebSession $sesion `
    -Headers $headers `
    -ContentType "application/json" `
    -Body $cuerpoReserva `
    -UseBasicParsing

$id = [int]$reserva.id

Write-Host "Reserva ficticia creada: $id"

Write-Host "3. Iniciando consentimiento..."

$headersConsentimiento = @{
    "X-CSRF-TOKEN" = $csrfRespuesta.csrfToken
    "X-SYNCHRO-RESERVA" = $reserva.tokenGestion
}

$consentimiento = Invoke-RestMethod `
    -Uri "$base/api/web/encomiendas/pre-reservas/$id/consentimiento/iniciar" `
    -Method Post `
    -WebSession $sesion `
    -Headers $headersConsentimiento `
    -UseBasicParsing

$texto = [string]$consentimiento.mensajeParaCompartirConDestinatario

if ($texto -notmatch "SYNCHRO ACEPTO $id ([0-9A-F]{16}) RECIBIR AVISOS Y PIN DE RETIRO POR WHATSAPP") {
    throw "El mensaje de consentimiento no tiene el formato esperado."
}

$codigo = $Matches[1]

$estadoInicial = Consultar-Consentimiento -ReservaId $id

if ($estadoInicial.Estado -ne "Pendiente") {
    throw "El consentimiento no se registro como pendiente."
}

Write-Host "OK: consentimiento pendiente."


Write-Host "4. Simulando vencimiento de pre-reserva..."

$config = Get-Content `
    ".\backend\Synchro.Api\appsettings.Development.json" `
    -Raw | ConvertFrom-Json

$datos = [System.Data.SqlClient.SqlConnectionStringBuilder]::new(
    $config.ConnectionStrings.SynchroDB
)

$datos["Initial Catalog"] = "SynchroDB_Pruebas"

$conexion = [System.Data.SqlClient.SqlConnection]::new(
    $datos.ConnectionString
)

try {
    $conexion.Open()

    if ($conexion.Database -ne "SynchroDB_Pruebas") {
        throw "SEGURIDAD: No estamos en la base de pruebas."
    }

    $comando = $conexion.CreateCommand()

    $comando.CommandText = @"
DECLARE @Ahora DATETIME2 = SYSUTCDATETIME();

UPDATE r
SET FechaCreacion = DATEADD(HOUR, -25, @Ahora),
    FechaExpiracion = DATEADD(HOUR, -1, @Ahora)
FROM dbo.PreReservasEncomiendasWeb r
INNER JOIN dbo.ConsentimientosPreReservaWhatsApp c
    ON c.ReservaId = r.Id
WHERE r.Id = @Id
  AND r.Estado = N'PendienteConsentimiento'
  AND r.FechaCreacion >= DATEADD(MINUTE, -10, @Ahora)
  AND r.FechaExpiracion > @Ahora
  AND c.Estado = N'Pendiente'
  AND c.IntentosFallidos = 0
  AND c.FechaExpiracionUtc > @Ahora;
"@

    $null = $comando.Parameters.Add(
        "@Id", [System.Data.SqlDbType]::Int
    )

    $comando.Parameters["@Id"].Value = [int]$id

    $afectadas = $comando.ExecuteNonQuery()

    if ($afectadas -ne 1) {
        throw "No se pudo vencer exactamente una pre-reserva nueva."
    }

    # Comprobar las dos condiciones de vencimiento
    $verificar = $conexion.CreateCommand()

    $verificar.CommandText = @"
SELECT CASE
    WHEN r.FechaExpiracion <= SYSUTCDATETIME()
     AND c.FechaExpiracionUtc > SYSUTCDATETIME()
     AND c.Estado = N'Pendiente'
    THEN 1 ELSE 0
END
FROM dbo.PreReservasEncomiendasWeb r
INNER JOIN dbo.ConsentimientosPreReservaWhatsApp c
    ON c.ReservaId = r.Id
WHERE r.Id = @Id;
"@

    $null = $verificar.Parameters.Add(
        "@Id", [System.Data.SqlDbType]::Int
    )

    $verificar.Parameters["@Id"].Value = [int]$id

    if ([int]$verificar.ExecuteScalar() -ne 1) {
        throw "Las fechas no cumplen las condiciones de la prueba."
    }

    Write-Host "OK: pre-reserva vencida."
    Write-Host "OK: codigo WhatsApp todavia vigente."
}
finally {
    $conexion.Dispose()
}

Write-Host "5. Probando codigo correcto..."

$mensajeId = "wamid.SYNCHRO.TEST.$([guid]::NewGuid().ToString('N'))"

Enviar-EventoFirmado `
    -Texto $texto `
    -MensajeId $mensajeId

$estadoFinal = Consultar-Consentimiento -ReservaId $id

Write-Host "Estado: $($estadoFinal.Estado)"
Write-Host "Intentos fallidos: $($estadoFinal.Intentos)"

if ($estadoFinal.Estado -ne "Pendiente" -or
    [int]$estadoFinal.Intentos -ne 0) {
    throw "ERROR: pre-reserva vencida no fue rechazada correctamente."
}

Write-Host "OK: consentimiento no confirmado para pre-reserva vencida."
Write-Host ""
Write-Host "PRUEBA DE VENCIMIENTO DE PRE-RESERVA COMPLETADA."
Write-Host "Reserva ficticia: $id"
