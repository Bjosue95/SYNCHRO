# Helper para pruebas manuales locales. Solo genera datos ficticios; no muestra
# contraseña, cookies, teléfonos ni credenciales. Conserva la cuenta para inspección.
function Nueva-SesionClienteSintetico {
    param([string]$Base, [string]$Telefono)
    $uri = [Uri]$Base
    if ($uri.Scheme -ne 'https' -or -not $uri.IsLoopback) {
        throw 'Este helper solo admite HTTPS en localhost.'
    }
    $csrf = Invoke-RestMethod -Uri "$Base/api/web/clientes/acceso/csrf-token" -SessionVariable cuentaSesion -UseBasicParsing
    $registro = @{
        nombreUsuario = "test_$([guid]::NewGuid().ToString('N'))"
        contrasena = "Prueba_$([guid]::NewGuid().ToString('N'))"
        telefono = $Telefono
    } | ConvertTo-Json -Compress
    Invoke-RestMethod -Uri "$Base/api/web/clientes/acceso/registro" -Method Post -WebSession $cuentaSesion `
        -Headers @{ 'X-CSRF-TOKEN' = $csrf.csrfToken } -ContentType 'application/json' -Body $registro -UseBasicParsing | Out-Null
    $registro = $null
    $privado = Invoke-RestMethod -Uri "$Base/api/web/clientes/csrf-token" -WebSession $cuentaSesion -UseBasicParsing
    return [PSCustomObject]@{ Sesion = $cuentaSesion; CsrfToken = $privado.csrfToken }
}
