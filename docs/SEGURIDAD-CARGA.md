# Protección de operaciones heredadas de carga

Problema: las rutas POST de carga estaban disponibles sin sesión ni comprobación CSRF.
Una petición anónima podía intentar reservar un compartimento, iniciar una sesión o finalizarla.

Las tres rutas ahora exigen la política `SoloTerminal` (cookie de terminal, cuya actividad
se revalida en SQL por el middleware existente), HTTPS y token CSRF válido:

| Método | Ruta | Cuerpo |
|---|---|---|
| POST | `/api/espacios-carga/{codigo}/reservar` | Sin cuerpo obligatorio |
| POST | `/api/espacios-carga/{codigo}/iniciar` | `{"tipoDispositivo":"Celular"}` o `Laptop` |
| POST | `/api/sesiones-carga/{id}/finalizar` | Sin cuerpo obligatorio |

El filtro compartido `OperacionTerminalSeguraFilter` rechaza HTTP y CSRF ausente/inválido
antes del código que modifica SQL. Añade `Cache-Control: no-store` a las respuestas del filtro.
El cliente usa `X-CSRF-TOKEN` y la cookie antiforgery que obtiene por el flujo existente de terminal.
No se acepta una credencial de administrador como sustituto de la cookie de terminal.

Hay una política de 30 intentos por minuto por IP, sin cola, independiente del límite de login.
Los excesos devuelven 429. El límite cuenta también intentos con CSRF inválido.

`SeguridadCargaHttpTests` incluye diez casos HTTP que cubren las tres rutas sin sesión,
sin CSRF, por HTTP y el límite de intentos. Utiliza autenticación sintética solo en el
servidor de pruebas. La base queda inaccesible en esas pruebas: si se alcanzara el SQL
heredado, fallarían en Linux en vez de dar los 400/401/429 esperados.

## Límites y trabajo pendiente

- Este cambio protege el acceso; no convierte las sesiones en carga prepagada ni garantiza
  que su propietario sea la misma terminal que finaliza la sesión. Para ello hace falta
  persistir y comprobar el vínculo sesión/terminal en el esquema siguiente.
- La lógica heredada todavía calcula y registra el cobro al finalizar. No usarla como
  implementación del flujo comercial prepagado de 30/60/90 minutos.
- Faltan las pruebas funcionales de SQL, concurrencia de sesiones, autorización por propietario,
  caducidad y pago anticipado. Las lecturas públicas de disponibilidad/tarifas conservan su comportamiento.
- La revocación real de terminales requiere SQL y mantiene sus pruebas existentes; estas
  pruebas nuevas no afirman haber validado esa consulta en Linux.
- No es una certificación de seguridad de toda la API.

Validar desde la raíz con:

```powershell
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj --filter FullyQualifiedName~SeguridadCargaHttpTests
```
