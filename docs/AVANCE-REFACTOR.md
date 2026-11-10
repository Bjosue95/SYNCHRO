# Avance actual en refactor/arquitectura-backend

Última integración y suite completa: [INTEGRACION-RAMAS.md](INTEGRACION-RAMAS.md) (151 aprobadas, 32 fallidas y 4 omitidas).

Esta es la rama de trabajo indicada por el propietario. Se trasladaron aquí los cambios
anteriores de depósito atómico y seguridad de carga, conservándolos también en sus ramas
anteriores. No se modificó main ni se realizó una fusión automática.

## Implementado en esta etapa

- Preparación de PIN aleatorio de seis dígitos: HMAC vinculado al uso y destinatario,
  salt aleatorio y caducidad configurable (30 minutos por defecto, máximo 24 horas).
- Depósito, consumo facial, creación del PIN y encolado de la notificación dentro de
  la misma transacción de aplicación. El procedimiento de depósito exige esa transacción;
  no confirma por su cuenta. Cualquier error posterior revierte el conjunto.
- El teléfono destinatario procede de la pre-reserva verificada en SQL; no del navegador.
  La respuesta de depósito no devuelve el PIN a la terminal.
- Outbox SQL con identificador único de operación, arrendamiento exclusivo, límite de cinco
  intentos y caducidad. Solo retoma mensajes rechazados explícitamente con HTTP 429,
  respetando Retry-After y una espera creciente. Fallos de transporte, 5xx y respuestas
  ambiguas quedan indeterminados: no se reenvían automáticamente.
- Cifrado autenticado de teléfono/plantilla/variables (incluido el PIN) mediante ASP.NET
  Data Protection. El propósito vincula el mensaje a la operación y reserva; cambiar
  estos identificadores o alterar el contenido invalida el cifrado.
- Eliminación del contenido cifrado al aceptar, cancelar, caducar o cerrar definitivamente
  un mensaje. El trabajador no registra teléfonos, PIN, cuerpos ni mensajes de excepciones.
- Revalidación de consentimiento antes de tomar mensajes para envío. Un arrendamiento
  abandonado pasa a indeterminado, para evitar duplicar un envío posiblemente realizado.
- Conciliación de estados desde el webhook firmado; un evento atrasado no rebaja Leido
  o Entregado a un estado anterior. Se conserva la deduplicación existente del webhook.
- Validación estricta de entradas del cliente WhatsApp, límite de respuesta de 64 KiB y
  diferenciación entre aceptación por Meta y entrega al teléfono.

## Configuración para cuando se pruebe en la laptop

Además de las migraciones iniciales y del depósito:

1. `20261110_PinEncomienda_Pruebas.sql`
2. `20261110_OutboxWhatsApp_Pruebas.sql`

Los dos archivos rechazan bases distintas de `SynchroDB_Pruebas`. No ejecutarlos en SynchroDB.
Las pruebas SQL actualizadas aplican ambos y comprueban que depósito, PIN y outbox existan
juntos. La falla inyectada ahora ocurre al insertar la outbox, después de crear el PIN.

Configuración de aplicación (secretos por User Secrets o mecanismo seguro, nunca en Git):

- `RetiroPin:ClaveHmac`: clave de 32 bytes codificada en Base64.
- `RetiroPin:VigenciaMinutos`: 1 a 1440, valor predeterminado 30.
- `WhatsApp:PlantillaPinRetiro`: plantilla aprobada con variables PIN y código del casillero.
- Configuración existente de Meta: PhoneNumberId, AccessToken, AppSecret, WebhookVerifyToken.
- `WhatsApp:Outbox:Habilitado`: false por defecto. Solo puede activarse en Development
  con SynchroDB_Pruebas; no activa por sí mismo ningún depósito ni verificación facial.

La clave HMAC y las claves de Data Protection deben persistir de forma segura. Conservar
un key ring accesible solo al servicio y protegido mediante el mecanismo del sistema
operativo/certificado correspondiente. Perderlo impide descifrar mensajes pendientes;
no reemplazarlo y reenviar ciegamente. Las pruebas unitarias usan claves efímeras propias.

## Verificación y límites

73 casos seleccionados de PIN, cliente WhatsApp, outbox y seguridad de carga aprobaron
sin acceso a SQL ni envío de mensajes reales. Esto verifica políticas, validaciones,
criptografía y rechazos HTTP; no demuestra funcionamiento del SQL ni entrega real.

Por indicación del propietario, las pruebas SQL se harán posteriormente en su laptop.
El esquema nuevo es mínimo para las pruebas de depósito: no sustituye el esquema completo
ni reconstruye automáticamente las bases existentes del propietario.

El servicio de depósito sigue sin endpoint público. Falta completar y conectar el retiro,
el enrolamiento/vínculo de identidades y la verificación facial confiable del servidor.
La comparación matemática del prototipo no reemplaza la prueba de vida. No hay aperturas
habilitadas por datos de aprobación del navegador.

La outbox no puede garantizar exactamente una entrega externa: si Meta acepta y el proceso
se interrumpe antes de registrar el ID, queda para revisión. Una revocación de consentimiento
cuando un envío ya está en curso no permite recuperar el mensaje enviado. Los mensajes
permanentemente fallidos requieren corregir configuración/plantilla y una acción explícita;
no se reinsertan automáticamente.

También siguen pendientes carga prepagada, frío/calor, administración/reportes e integración
de todos los flujos. La protección de las rutas heredadas no cambia su cobro posterior.
