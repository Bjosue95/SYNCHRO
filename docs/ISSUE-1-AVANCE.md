# Issue #1: implementación por etapas

Tarea: https://github.com/Bjosue95/SYNCHRO/issues/1
Base actualizada: `refactor/arquitectura-backend`, `5f6cd39`.
Rama de desarrollo: `feat/issue-1-encomiendas-end-to-end`, para revisión hacia esa base.
Se conserva la corrección SQL de reversión del propietario y no se modifica main ni demos/.

## Etapa 1: depósito y retiro conectados a HTTP

Rutas registradas únicamente en Development + SynchroDB_Pruebas, con cookie de terminal,
HTTPS, CSRF, rate limiting y respuestas `Cache-Control: no-store`:

| POST | Entrada | Requisito adicional |
|---|---|---|
| `/api/terminales/encomiendas/reservas/{id}/evidencias-remitente` | Captura binaria `application/octet-stream`, máximo 256 KiB | Credencial `X-SYNCHRO-RESERVA`; identidad del remitente desde vínculo verificado en SQL |
| `/api/terminales/encomiendas/reservas/{id}/depositar` | `{"evidenciaId":"GUID","codigoCasillero":"S01"}` | Misma credencial; el teléfono procede de la reserva, no se pide otra vez |
| `/api/terminales/encomiendas/usos/{id}/evidencias-destinatario` | Captura binaria, máximo 256 KiB | Cookie de sesión Destinatarios, además de la terminal |
| `/api/terminales/encomiendas/usos/{id}/retirar` | `{"evidenciaId":"GUID","pin":"PIN_TEMPORAL"}` | Sesión del destinatario propietario del uso |

Obtener CSRF desde el flujo existente, enviando `X-CSRF-TOKEN` junto a su cookie. La
terminal y el destinatario proceden de sus identidades autenticadas, nunca de IDs del JSON.
La credencial de reserva es de 32 bytes en hexadecimal y no viaja en URLs ni logs.

El contrato `IFlujoEncomiendasTerminal` tiene implementación SQL para estos endpoints y
reutiliza el servicio existente de depósito, HMAC de PIN y outbox. El hash de la credencial
se comprueba también dentro de la transacción SQL de depósito, evitando que una consulta
preliminar sea la única autorización. El código anterior conserva su uso interno de confianza.

El nuevo procedimiento de retiro comprueba terminal activa, pertenencia al destinatario,
uso ocupado, PIN con salt vigente, máximo cinco intentos y evidencia facial de la misma
persona/uso/terminal emitida después del depósito y durante un máximo de dos minutos.
En una transacción consume PIN y rostro, finaliza el uso, libera el casillero, cancela la
notificación pendiente y registra auditoría. Un fallo en auditoría debe revertir todos esos
cambios. La hora definitiva se obtiene después de adquirir los bloqueos.

La captura temporal se limita y se sobrescribe al salir; no se persisten imágenes ni
resultados de aprobación enviados por el navegador. El proveedor facial normal sigue siendo
`VerificadorFacialNoDisponible`: **las rutas sensibles devuelven 503 y no abren casilleros**.
El verificador simulado y el repositorio en memoria están solo en el proyecto de pruebas,
no son opciones activables en el binario de la API ni un sustituto de biometría real.
En producción estas rutas de prototipo no se registran (404).

### Migraciones / Windows

Aplicar con parada al primer error, solo en la base aislada y después de sus prerrequisitos:

1. Actualizar `20261110_DepositoEncomiendaAtomico_Pruebas.sql` (nuevo parámetro opcional de
   hash de credencial; conserva compatibilidad con llamadas internas y exige transacción).
2. Aplicar `20261110_RetiroEncomiendaTerminal_Pruebas.sql` (evidencias de retiro, auditoría y
   procedimiento de consumo conjunto). La guardia rechaza SynchroDB y cualquier otra base.

La fixture existente incorpora esta migración y limpia únicamente sus registros sintéticos.
Los cuatro casos externos nuevos llevan `[Trait("Category","SqlAislado")]` y Fact normal,
sin Skip añadido. Ejecutar en Windows con `SYNCHRO_SQL_DEPOSITO_TEST` configurada:

```powershell
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj --filter Category=SqlAislado
```

### Resultados ejecutados en Cloud (Linux)

- Compilación de API y pruebas: sin errores ni advertencias.
- Pruebas HTTP nuevas: **20 aprobadas**. Cubren recorrido de captura/depósito/retiro,
  concurrencia simulada, consumo único, bloqueo de PIN, consentimiento y credencial ajenos,
  rechazo facial, JSON booleano falso como evidencia, tamaño de captura, doble sesión,
  CSRF/HTTPS y ausencia de rutas en Production.
- Suite completa sin filtros: **211 casos: 171 aprobados, 36 fallidos, 4 omitidos**.
- Los 32 fallos de LocalDB/configuración ya documentados permanecen. Los cuatro fallos
  nuevos son exactamente los casos SQL externos sin conexión configurada: concurrencia
  de retiro, bloqueo de PIN, reversión tras fallo de auditoría y credencial de reserva ajena.
  Fallan por `Conexion de pruebas no configurada`; no se les atribuye resultado exitoso.
- Las cuatro omisiones son las anteriores de la fixture. No se añadieron nuevas omisiones.
- La tarea informa 196/196 aprobadas en Windows en 5f6cd39; esta máquina no ha reproducido
  esa ejecución ni las migraciones nuevas. Los tests HTTP usan estado simulado en memoria;
  no prueban persistencia ni aislamiento de transacciones de SQL Server.

TRX/log de esta etapa: `/workspace/synchro-test-results/issue-1/`.

## Pendientes del alcance global

No se declara terminado el backend. Faltan enrolamiento/vinculación confiable de identidades,
proveedor facial y prueba de vida reales, visitante integrado, reemisión segura de PIN,
completar verificación/consentimiento/invitaciones WhatsApp, resguardo web, carga prepagada,
frío/calor, administración/reportes, OpenAPI e instalación completa reproducible. Los casos
SQL nuevos requieren validación en la laptop; no se enviaron mensajes ni se simuló una entrega
real de WhatsApp. Las restricciones de hardware/credenciales no se deben usar para habilitar
aperturas sin verificación del servidor.

## Etapa 2: visitante de tablet y pre-reserva compartida

`POST /api/terminales/encomiendas/visitas` recibe `telefonoRemitente`,
`telefonoDestinatario` y `tamanoSolicitado`. Exige sesión de terminal, CSRF y HTTPS;
no recibe terminalId ni aprobación de consentimiento del navegador. Devuelve la credencial
una sola vez y conserva `PendienteConsentimiento` / `depositoHabilitado=false`.
El flujo web mantiene su contrato y usa el mismo servicio de creación, sin duplicar SQL.
Los teléfonos se normalizan a formato internacional con `+`; no se inventa código de país.
La reserva web existente no vuelve a solicitar el teléfono destinatario en el depósito.

`20261110_OrigenPreReservaEncomienda_Pruebas.sql` agrega, de forma idempotente y exclusivamente
en SynchroDB_Pruebas, el origen Web/Terminal y la terminal de creación. Los registros previos
se conservan como Web. El visitante debe continuar con el consentimiento de WhatsApp existente;
la creación por sí sola no verifica identidades, no genera biometría ni envía mensajes reales.

Pruebas nuevas: 10 aprobadas para web/tablet, formatos inválidos, respuesta sin teléfonos,
credencial protegida y rechazo de visitante anónimo. Tras añadir trazabilidad, las 30 pruebas
HTTP/validación de ambas etapas volvieron a pasar. Se añadió un caso SQL externo de origen,
terminal y hash de credencial, sin Skip: queda pendiente de ejecutar con conexión real.

## Etapa 3: servicios temporales prepagados y administración operativa

Implementado un motor común y un repositorio SQL transaccional para CargaCelular,
CargaLaptop, Frio, Calor y Personal, sin sustituir el inventario ni los procedimientos
anteriores. Las rutas conectan web, tablet y administrador. Incluye tarifas validadas,
precio congelado, pago simulado idempotente, PIN HMAC, arranque sin extender tiempo en
reintentos, vencimiento automático, cancelación/anulación simulada, renovación y retiro
incluso tras agotar el tiempo; incidencias térmicas simuladas, bloqueo, auditoría y reportes.
Al activar el módulo se bloquea el POST postpago antiguo como vía para eludir el prepago.

La activación requiere Development, SynchroDB_Pruebas, migración del módulo y clave HMAC;
se rechaza en producción. El hardware, NFC, pagos y temperatura son explícitamente simulados.
No se modificó ni se verificó el hardware físico. Configuración, contratos y sincronización
funcional se detallan en [SERVICIOS-TEMPORALES.md](SERVICIOS-TEMPORALES.md).

Se agregó OpenAPI oficial 10.0.14, generado por la aplicación y probado por HTTP; exige
administrador y HTTPS. No se copiaron contratos estáticos ni se enviaron mensajes reales.

Resultado de la suite completa tras esta etapa: **255 casos: 211 aprobados, 40 fallidos,
4 omitidos**; salida 1. API y herramienta administrativa compilan con 0 advertencias/errores.
Las 40 fallidas son las 32 previas más ocho casos externos nuevos sin conexión SQL: cuatro
retiro/credencial, uno visitante y tres servicios temporales. No hay omisiones nuevas.
El motor nuevo y sus recorridos HTTP suman 30 casos aprobados (19 de motor y 11 HTTP).
El primer intento detectó un fallo en la guardia de producción; se corrigió validando la
configuración final del host y la prueba ahora pasa. No se presenta como ejecución SQL ni
como aprobación de las nuevas transacciones en Windows.

Para ejecutar los servicios en VS Code, revisar la rama de esta tarea sin tocar main:

```powershell
git fetch origin
git switch feat/issue-1-encomiendas-end-to-end
git pull --ff-only origin feat/issue-1-encomiendas-end-to-end
dotnet restore tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj
```

Los pendientes globales anteriores se reducen con esta etapa: carga prepagada, resguardo
web, frío/calor, métricas y gestión operativa ya tienen código y endpoints. Permanecen
la validación SQL/Windows, integración con la base histórica, usuarios/roles granulares,
enrolamiento/vinculación biométrica y verificación telefónica real, reemisión de PIN de
encomiendas, invitaciones/entregas reales de WhatsApp y pruebas físicas. No está terminado
el backend completo ni se han satisfecho todos los criterios de la issue.
