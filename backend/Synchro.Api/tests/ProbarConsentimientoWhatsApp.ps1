
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

Write-Host "4. Probando codigo incorrecto..."

$ultimo = if ($codigo.EndsWith("A")) { "B" } else { "A" }
$codigoIncorrecto = $codigo.Substring(0, 15) + $ultimo
$textoIncorrecto = $texto.Replace($codigo, $codigoIncorrecto)

$idIncorrecto = "wamid.SYNCHRO.TEST.$([guid]::NewGuid().ToString('N'))"

Enviar-EventoFirmado `
    -Texto $textoIncorrecto `
    -MensajeId $idIncorrecto

$estadoIncorrecto = Consultar-Consentimiento -ReservaId $id
Write-Host "DIAGNOSTICO SYNCHRO:"
Write-Host "Estado: [$($estadoIncorrecto.Estado)]"
Write-Host "Intentos: [$($estadoIncorrecto.Intentos)]"
if ($estadoIncorrecto.Estado -ne "Pendiente" -or
    $estadoIncorrecto.Intentos -ne 1) {
    throw "El codigo incorrecto no fue procesado correctamente."
}

Write-Host "OK: codigo incorrecto rechazado."

Write-Host "5. Probando codigo correcto..."

$idCorrecto = "wamid.SYNCHRO.TEST.$([guid]::NewGuid().ToString('N'))"

Enviar-EventoFirmado `
    -Texto $texto `
    -MensajeId $idCorrecto

$estadoCorrecto = Consultar-Consentimiento -ReservaId $id

if ($estadoCorrecto.Estado -ne "Confirmado") {
    throw "No se confirmo el consentimiento."
}

Write-Host "OK: consentimiento confirmado en la base de pruebas."

Write-Host "6. Probando mensaje repetido..."

Enviar-EventoFirmado `
    -Texto $texto `
    -MensajeId $idCorrecto

$estadoFinal = Consultar-Consentimiento -ReservaId $id

if ($estadoFinal.Estado -ne "Confirmado" -or
    $estadoFinal.Intentos -ne 1) {
    throw "El mensaje repetido altero el consentimiento."
}

Write-Host "OK: mensaje repetido no altero el consentimiento."

Write-Host ""
Write-Host "PRUEBAS DE CONSENTIMIENTO COMPLETADAS."
Write-Host "Reserva ficticia: $id"
Write-Host "Estado: $($estadoFinal.Estado)"
Write-Host "Intentos incorrectos: $($estadoFinal.Intentos)"
Write-Host "Deposito de encomiendas: NO HABILITADO"
