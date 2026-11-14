# Integración del backend en main — 13/11/2026

Actualización: las 28 pruebas históricas ya fueron adaptadas al destino aislado.
El verificador predeterminado ya no las excluye. Ver
[recuperación y validación histórica](VALIDACION-HISTORICAS.md). Los resultados de 341
casos y las exclusiones descritas más abajo corresponden a la etapa anterior.
Resultado completo posterior: **375 aprobadas, 0 fallidas, 0 omitidas y 0 excluidas**,
con los 28 casos recuperados y seis pruebas adicionales de seguridad.

Este documento sustituye las indicaciones históricas de ramas y pendientes ya implementados.
Trabajo exclusivo en `main`; sin commit ni push. No se modifican `demos/` ni el endpoint
sin seguimiento `SolicitudesVerificacionTelefonoEndpoints.cs`.

## Inicio de sesión del destinatario por WhatsApp

El webhook firmado registra el comando existente de inicio. El trabajador de solicitudes
reclama, autoriza y prepara una verificación y su mensaje cifrado en una transacción.
Un consumidor independiente envía exclusivamente desde `OutboxVerificacionTelefono`.
No reutiliza la cola de PIN de encomiendas. Las dos colas comparten solamente la política
de clasificación de resultados externos.

Todos estos indicadores son **false por defecto**:

- `WhatsApp:SolicitudesInicioHabilitadas`: permite registrar solicitudes entrantes.
- `WhatsApp:SolicitudesInicioWorker:Habilitado`: procesa las solicitudes registradas.
- `WhatsApp:VerificacionOutbox:Habilitado`: permite enviar los códigos preparados.
- `WhatsApp:Outbox:Habilitado`: permite enviar los PIN de encomiendas.

No se habilitaron envíos externos durante el desarrollo. El inicio automático exige
Development, `(localdb)\SYNCHRO_Aislado`, `SynchroDB_Pruebas`; los servicios SQL verifican
el marcador `SynchroFixtureDeposito` antes de procesar la cola.

La plantilla `WhatsApp:PlantillaCodigoInicioSesion` debe admitir dos variables de cuerpo,
en este orden: código de seis dígitos y referencia decimal de verificación. Ambos viajan
en el contenido cifrado al teléfono autorizado. El destinatario introduce los dos en
`POST /api/destinatarios/login` como `codigo` y `verificacionId`, con HTTPS y CSRF.
La referencia no es una credencial por sí sola y no se publica un endpoint de consulta
de códigos o solicitudes ajenas. El código se valida y consume una sola vez antes de
emitir la cookie. Login tiene un límite de cinco solicitudes por minuto e IP.

La reclamación SQL es exclusiva, con arrendamiento de 60 segundos. Se revalida
consentimiento inmediatamente antes del envío. Solo los rechazos explícitos reintentables
(429 en el cliente Cloud existente) admiten reintento, con espera, máximo cinco intentos
y sin superar la caducidad. Excepciones de transporte, resultados inciertos y arrendamientos
abandonados no se reenvían automáticamente. El contenido cifrado se elimina al cerrar,
caducar o cancelar. La conciliación usa únicamente eventos persistidos por el webhook
firmado; contempla recepción anterior al registro del ID de Meta y no degrada Leido/Entregado.

Una revocación posterior al inicio de la llamada externa no puede retirar un mensaje
en tránsito. Un envío indeterminado sin ID de Meta requiere revisión; no debe reinsertarse
para forzar un segundo envío.

## Claves y configuración local

Al habilitar cualquiera de los dos trabajadores nuevos se exige Windows y
`ProteccionDatos:DirectorioClaves`, una ruta absoluta persistente privada de la cuenta
del servicio. Data Protection cifra las claves mediante DPAPI del usuario actual.
No cambiar de cuenta Windows ni perder el directorio: impediría abrir datos pendientes.
El administrador del equipo debe restringir sus permisos NTFS y conservar un respaldo
protegido. La prueba de persistencia usa dos proveedores, el mismo directorio temporal
y DPAPI; no configura ni sustituye las claves existentes del usuario.

HMAC, credenciales de Meta y plantillas se configuran por mecanismos privados. No se
incluyen credenciales reales. `AllowedHosts` admite localhost por defecto; para usar
la tablet por red, configurar explícitamente los nombres del host autorizado y HTTPS.

## Integración de negocio y seguridad

Se conserva la implementación de reservas web, encomiendas, rostro simulado, PIN,
reemisión, servicios prepagados, térmicos, administración y reportes. Con servicios
temporales habilitados, las rutas heredadas de reserva y depósito personal responden
409 para impedir eludir el prepago, como ya hacen las rutas antiguas de carga.
Los retiros personales históricos permanecen disponibles; no se eliminan reservas ni usos.
No se registran objetos de excepción en los trabajadores revisados ni códigos en el
ToString de la petición de login.

## Verificación reproducible

Ejecutar `./tools/ProbarBackendAislado.ps1` en Windows. Primero comprueba instancia,
catálogo y marcador en modo de lectura. Fija la conexión autorizada, deshabilita
trabajadores de envío y restaura las variables del proceso al finalizar. Los tests SQL
aplican sus scripts idempotentes sobre la fixture marcada y limpian sus datos sintéticos.
No ejecutar dos suites SQL sobre esta misma base simultáneamente.

El script conserva y excluye once clases históricas (28 casos de la referencia inicial)
que usan otras bases/instancias o secretos históricos. No se presentan como aprobadas
ni omitidas por el corredor: quedan fuera del filtro. La referencia de 353 pruebas no
equivale al total de esta ejecución, que añade pruebas nuevas. Para validar esas clases
hay que adaptar sus fixtures al destino autorizado sin perder sus comprobaciones.

No se probaron entregas reales de Meta, hardware ni pagos bancarios. Rostro, pagos,
carga y temperaturas siguen siendo las simulaciones previstas para el prototipo.

## Resultado confirmado del verificador

El 13/11/2026 se ejecutó `tools/ProbarBackendAislado.ps1` completo después de comprobar
que no había una suite activa ni resultados recuperables de la ejecución anterior.
Windows bloqueó inicialmente el archivo por ExecutionPolicy; se ejecutó con
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./tools/ProbarBackendAislado.ps1`,
con autorización y sin cambiar la política permanente del equipo.

Resultado: **341 aprobadas, 0 fallidas, 0 omitidas**, duración de pruebas 52 segundos.
Las **28 excluidas** no se ejecutaron. El verificador confirmó primero el catálogo
`SynchroDB_Pruebas` y su marcador en `(localdb)\SYNCHRO_Aislado`. Los trabajadores de
envíos externos permanecieron deshabilitados; los envíos de las pruebas fueron falsos.
La salida quedó guardada en `docs/resultados/backend-aislado-20261113.log`.

Esta ejecución incluye pruebas de integración SQL y HTTP de WhatsApp, consumo único
del código para emitir sesión, arrendamientos concurrentes/vencidos, consentimiento,
conciliación, protección persistente DPAPI, así como los recorridos existentes de
encomiendas, PIN, servicios temporales y permisos. No se hizo commit ni push.
