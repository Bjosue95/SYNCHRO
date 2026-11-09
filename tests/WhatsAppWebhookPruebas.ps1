
$ErrorActionPreference = "Stop"

$base = "https://localhost:7280/api/webhooks/whatsapp"

$token = "synchro-prueba-local-2026"
$secreto = "synchro-secreto-local-no-produccion-2026"
$phoneId = "123456789012345"

function ObtenerEstado {
    param([scriptblock]$Operacion)

    try {
        $respuesta = & $Operacion
        return [int]$respuesta.StatusCode
    }
    catch [System.Net.WebException] {
        if ($null -eq $_.Exception.Response) {
            throw
        }

        return [int]$_.Exception.Response.StatusCode
    }
}

Write-Host "1. Probando verificacion GET..."

$urlCorrecta = "$base`?hub.mode=subscribe&hub.verify_token=$token&hub.challenge=123456"

$respuesta = Invoke-WebRequest `
    -Uri $urlCorrecta `
    -UseBasicParsing

if ($respuesta.StatusCode -ne 200 -or
    $respuesta.Content.Trim() -ne "123456") {
    throw "Fallo la verificacion del webhook."
}

Write-Host "OK: verificacion correcta."

Write-Host "2. Probando token incorrecto..."

$urlIncorrecta = "$base`?hub.mode=subscribe&hub.verify_token=incorrecto&hub.challenge=123456"

$estado = ObtenerEstado {
    Invoke-WebRequest `
        -Uri $urlIncorrecta `
        -UseBasicParsing
}

if ($estado -ne 403) {
    throw "El webhook no rechazo el token incorrecto."
}

Write-Host "OK: token incorrecto rechazado."

Write-Host "3. Preparando mensaje ficticio..."

$idMensaje = "wamid.SYNCHRO.TEST.$([guid]::NewGuid().ToString('N'))"

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
                                id = $idMensaje
                                from = "50499999999"
                                type = "text"
                                text = @{
                                    body = "ACEPTO"
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
    -Depth 12 `
    -Compress

$bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
$clave = [System.Text.Encoding]::UTF8.GetBytes($secreto)

$hmac = [System.Security.Cryptography.HMACSHA256]::new($clave)

try {
    $hash = $hmac.ComputeHash($bytes)
    $firma = [BitConverter]::ToString($hash).
        Replace("-", "").ToLowerInvariant()
}
finally {
    $hmac.Dispose()
}

Write-Host "4. Probando firma incorrecta..."

$firmaMala = "sha256=" + ("0" * 64)

$estado = ObtenerEstado {
    Invoke-WebRequest `
        -Uri $base `
        -Method Post `
        -Body $bytes `
        -ContentType "application/json" `
        -Headers @{
            "X-Hub-Signature-256" = $firmaMala
        } `
        -UseBasicParsing
}

if ($estado -ne 401) {
    throw "El webhook acepto una firma incorrecta."
}

Write-Host "OK: firma incorrecta rechazada."

Write-Host "5. Probando firma correcta..."

$headers = @{
    "X-Hub-Signature-256" = "sha256=$firma"
}

$estado = ObtenerEstado {
    Invoke-WebRequest `
        -Uri $base `
        -Method Post `
        -Body $bytes `
        -ContentType "application/json" `
        -Headers $headers `
        -UseBasicParsing
}

if ($estado -ne 200) {
    throw "No se recibio el evento valido. HTTP $estado"
}

Write-Host "OK: mensaje autentico recibido."

Write-Host "6. Probando mensaje repetido..."

$estado = ObtenerEstado {
    Invoke-WebRequest `
        -Uri $base `
        -Method Post `
        -Body $bytes `
        -ContentType "application/json" `
        -Headers $headers `
        -UseBasicParsing
}

if ($estado -ne 200) {
    throw "Fallo el procesamiento del mensaje repetido."
}

Write-Host "OK: reenvio aceptado."

Write-Host "7. Verificando SQL Server..."

$cadena = "Server=(localdb)\MSSQLLocalDB;Database=SynchroDB_Pruebas;Integrated Security=True;TrustServerCertificate=True;"

$conexion = [System.Data.SqlClient.SqlConnection]::new($cadena)

try {
    $conexion.Open()

    $comando = $conexion.CreateCommand()

    $comando.CommandText = @"
SELECT COUNT(*)
FROM dbo.WhatsAppWebhookEventos
WHERE MensajeId = @Id
  AND TipoEvento = N'Mensaje'
  AND RespuestaClasificada = N'ACEPTO';
"@

    $parametro = $comando.Parameters.Add(
        "@Id",
        [System.Data.SqlDbType]::NVarChar,
        255
    )
    $parametro.Value = $idMensaje

    $cantidad = [int]$comando.ExecuteScalar()

    if ($cantidad -ne 1) {
        throw "Se esperaban 1 registro, se encontraron $cantidad."
    }

    Write-Host "OK: un unico registro almacenado."
}
finally {
    $conexion.Dispose()
}

Write-Host ""
Write-Host "TODAS LAS PRUEBAS DEL WEBHOOK FUERON CORRECTAS."
Write-Host "El consentimiento aun NO esta autorizado."
