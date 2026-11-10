
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


Write-Host "4. Simulando vencimiento del consentimiento..."

# La prueba solo puede modificar la base aislada.
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
        throw "SEGURIDAD: Base de datos incorrecta."
    }

    $comando = $conexion.CreateCommand()

    
$comando.CommandText = @"
DECLARE @Ahora DATETIME2 = SYSUTCDATETIME();

UPDATE c
SET FechaCreacionUtc = DATEADD(MINUTE, -16, @Ahora),
    FechaExpiracionUtc = DATEADD(MINUTE, -1, @Ahora)
FROM dbo.ConsentimientosPreReservaWhatsApp c
INNER JOIN dbo.PreReservasEncomiendasWeb r
    ON r.Id = c.ReservaId
WHERE c.ReservaId = @Id
  AND c.Estado = N'Pendiente'
  AND c.IntentosFallidos = 0
  AND c.FechaCreacionUtc >= DATEADD(MINUTE, -10, @Ahora)
  AND c.FechaExpiracionUtc > @Ahora
  AND r.Estado = N'PendienteConsentimiento'
  AND r.FechaExpiracion > @Ahora;
"@


    $null = $comando.Parameters.Add(
        "@Id", [System.Data.SqlDbType]::Int
    )

    $comando.Parameters["@Id"].Value = [int]$id

    $afectadas = $comando.ExecuteNonQuery()

    if ($afectadas -ne 1) {
        throw "No se pudo vencer exactamente un consentimiento nuevo."
    }

    Write-Host "OK: consentimiento vencido en base de pruebas."
}
finally {
    $conexion.Dispose()
}

Write-Host "5. Enviando codigo correcto despues del vencimiento..."

$mensajeId = "wamid.SYNCHRO.TEST.$([guid]::NewGuid().ToString('N'))"

Enviar-EventoFirmado `
    -Texto $texto `
    -MensajeId $mensajeId

$estadoFinal = Consultar-Consentimiento -ReservaId $id

Write-Host "Estado: $($estadoFinal.Estado)"
Write-Host "Intentos: $($estadoFinal.Intentos)"

if ($estadoFinal.Estado -ne "Pendiente" -or
    [int]$estadoFinal.Intentos -ne 0) {
    throw "ERROR: el consentimiento vencido no fue rechazado correctamente."
}

Write-Host "OK: codigo correcto rechazado por vencimiento."
Write-Host ""
Write-Host "PRUEBA DE VENCIMIENTO COMPLETADA."
Write-Host "Reserva ficticia: $id"
Write-Host "Confirmacion: RECHAZADA POR VENCIMIENTO"
