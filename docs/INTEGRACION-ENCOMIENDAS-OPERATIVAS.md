# Encomiendas operativas: diagnóstico y contrato de integración

Estado: backend operativo, web y ficha administrativa conectados localmente. Tablet pendiente por decisión expresa del usuario.
No representa una integración terminada ni acredita aperturas físicas o entregas de WhatsApp.

## Base del diagnóstico inicial (histórico)

- Primera base de reglas: `Services/Encomiendas/PoliticaEncomiendaOperativa.cs`.
- Extensión del HMAC existente para separar depósito/retiro sin cambiar los hashes históricos.
- 36 pruebas aprobadas: políticas nuevas, PIN operativo, compatibilidad de PIN anterior y descriptor V3.
- Compilación/pruebas en `bin/EncomiendasOperacion`, sin detener la API que estaba utilizando su ejecutable habitual.
- No se ejecutaron migraciones, escrituras SQL, envíos de WhatsApp ni aperturas. No se cambiaron las interfaces durante esta etapa.
- Estas reglas todavía no están conectadas a endpoints ni transacciones SQL; no se consideran protección operativa implantada.

Comando reproducible, sin pruebas que escriban en SQL:

```powershell
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj --filter "FullyQualifiedName~PoliticaEncomiendaOperativaTests|FullyQualifiedName~PinEncomiendaOperativaTests|FullyQualifiedName~CodigoPinRetiroTests|FullyQualifiedName~DescriptorFacialV3Tests" --no-restore -p:OutputPath=bin/EncomiendasOperacion/ -p:UseAppHost=false --verbosity minimal
```

Siguiente paso: resolver modalidad/tarifas y conservación, concretar DTO/estados persistidos y preparar la migración aditiva para revisión de impacto antes de ejecutarla en la base comprobada.

## Alcance y estado inicial

- Rama local existente: `rebuild/tablet-desde-cero`. No crear ramas, commits ni publicaciones.
- Conservar modificaciones locales previas de las tres aplicaciones.
- Conservar íntegros el hero, estación, servicios y diseño de tablet; modificar su integración de encomiendas.
- Base comprobada por consulta de solo lectura: `(localdb)\SYNCHRO_Aislado`, `SynchroDB_Pruebas`.
- No se han ejecutado migraciones ni escrituras de datos durante el diagnóstico.

| Funcionalidad | Implementación existente | Diferencia que debe resolverse |
| --- | --- | --- |
| Cuenta remitente | `ClientesWebEndpoints`, `CuentasClientesWeb`, cookie, CSRF, cuenta activa | Listado, detalle e historial propios de encomiendas |
| Reserva | `PreReservasEncomiendaWebEndpoints`, `PreReservaEncomiendaService` | Estado operativo canónico, conservando el identificador existente |
| Consentimiento encomienda | `ConsentimientosPreReservaWhatsApp`, confirmación por webhook | Consentimientos manuales independientes de ambos participantes |
| WhatsApp manual | `WhatsAppManualEndpoints`, `WhatsAppManualAdminView` | Su SQL está ligado a resguardo personal; no confirma encomiendas |
| Facial V3 | `VerificacionFacialView`, `solicitudInicioFacialV3`, `DescriptorFacialV3` | Reutilizar Human.js 3.3.6, 1024 valores y comparación coseno con identidad de cuenta |
| Facial antiguo encomienda | `IVerificadorFacial`, referencias de patrón visual | No mezclar este modelo con embeddings V3; no presentar simulación como biometría profesional |
| PIN | `CodigoPinRetiro`, `PreparadorPinEncomienda`, consumo y vencimiento | Depósito y retiro independientes; entrega manual temporal y auditada |
| Pago | `ModalidadesPagoSimulado`, cobros de resguardo y carga | No equivalen a pago real de encomienda; modalidad y tarifa pendientes de definición |
| Depósito | `DepositoEncomiendaService`, procedimiento transaccional | Comprobar pago, ambos consentimientos y PIN de depósito; impedir rutas alternativas |
| Retiro | `FlujoEncomiendasTerminal`, consumo de PIN y liberación transaccional | Captura como evidencia, sin referencia previa del destinatario ni otro cobro |
| Auditoría | `AuditoriaEncomiendasPrototipo`, auditorías de cobros | Historial desde la creación, con actor administrativo y participante de cada acción |
| Hardware | Respuestas existentes `hardwareSimulado=true` | Una prueba HTTP/SQL no demuestra una apertura o entrega física |

## Decisiones confirmadas y alcance vigente

- `frontend/tablet-app` es exclusivamente de lectura. No modificar ningún archivo; preparar sus contratos futuros en backend y documentación.
- El remitente paga; el destinatario retira sin otro cobro. Confirmación manual administrativa, con método, importe válido, referencia, fecha y actor; nunca usar cobros simulados como pago real.
- Tarifas configurables por tamaño, sin valores definitivos precargados. Sin tarifa configurada, el administrador deberá documentar el importe comprobado y su justificación; ningún importe cero/ausente habilita depósito.
- Referencia del remitente: Human.js V3/1024, cifrada, consentimiento explícito y revalidación anual. Revocación o cuenta inactiva elimina la plantilla activa.
- Destinatario: fotografía cifrada como evidencia, sin reconocimiento ni cuenta automática; metadatos y fotografía separados. Conservación inicial de 30 días tras entrega; prórroga justificada, auditada y con fecha límite.
- WhatsApp exclusivamente manual; los dos participantes y sus autorizaciones son independientes.
- Apertura física simulada. No declarar hardware validado.

## Contrato propuesto

### Identificador y estados

`PreReservasEncomiendasWeb.Id` continúa siendo la raíz. Las tablas operativas la extienden mediante claves foráneas, sin clonar encomiendas ni importar automáticamente filas históricas.

El detalle canónico incluye:

- `id`, `usoId`, `estado`, `tamanoSolicitado`, fechas y vencimientos.
- `remitente`: cuenta vinculada, teléfono, consentimiento, referencia/verificación facial, PIN de depósito y depósito.
- `destinatario`: teléfono, aceptación de encomienda, consentimiento de mensajes, PIN de retiro y captura de evidencia.
- `pago`: estado e importe solo cuando los proporciona la configuración/registro del servidor.
- `accionesPermitidas`: calculadas por el servidor; su presentación no sustituye la validación de la operación.
- `historial`: operaciones reales, fecha, estado y actor conforme a los permisos de consulta.

Los estados anteriores de encomiendas no autorizan por sí solos el nuevo flujo. Las rutas heredadas no deben permitir saltarse pago, PIN o consentimientos de una encomienda operativa.

### Roles y consentimiento

- La cuenta y teléfono del remitente proceden de la sesión y la base; no del cuerpo de una petición.
- El destinatario tiene un registro separado, aunque todavía no tenga cuenta web.
- Cada participante recibe un texto/código de consentimiento distinto, ligado a encomienda, rol, teléfono y vencimiento.
- Abrir un enlace `wa.me` no confirma aceptación, comunicación ni entrega de PIN.
- La confirmación manual exige acción administrativa explícita, teléfono coincidente y referencia del mensaje comprobado; registra actor y fecha.
- El destinatario acepta la encomienda y las comunicaciones; la captura de retiro tiene un consentimiento biométrico independiente.

### Identidad y evidencia

- Remitente: referencia V3 cifrada y vinculada a la cuenta. Verificación en servidor con el mismo modelo y dimensiones validados en tablet.
- No aceptar un booleano de verificación facial enviado por el navegador como autorización.
- Destinatario: captura como evidencia con consentimiento explícito y propósito de entrega. No exigir coincidencia con una identidad biométrica inexistente.
- Evidencias de operación breves, ligadas a encomienda, rol, finalidad y terminal; un solo uso.
- Las capturas y descriptores no se guardan en almacenamiento del navegador ni en registros de consola.
- La evidencia administrativa estará restringida y su disponibilidad dependerá de la política de conservación configurada.

### PIN, pago y apertura

- Dos PIN, separados por finalidad y participante. Almacenamiento mediante hash protegido/salt; comparación constante, vencimiento, intentos limitados y uso único.
- El administrador recibe el PIN temporalmente al emitirlo; no existe consulta permanente del valor. Reemitir invalida el anterior.
- La entrega manual requiere número autorizado y confirmación explícita de envío; nunca un webhook ficticio.
- Un pago simulado, pendiente, desconocido o rechazado no habilita depósito.
- La web no abre casilleros: solicita una autorización breve vinculada a una petición creada por una terminal autenticada.
- La terminal comprueba en la transacción estado, autorización, identidad, PIN, pago y disponibilidad antes del depósito.
- El retiro requiere depósito confirmado, PIN de retiro y evidencia de captura vigentes. Reutiliza el pago ya confirmado; no inicia otro cobro.
- Consumo de autorización/PIN, actualización de uso/casillero y auditoría deben ser atómicos.

## Estado conectado y comprobado — 24 de noviembre de 2026

| Parte | Implementación actual | Límite comprobado |
| --- | --- | --- |
| Backend | SQL transaccional, participantes independientes, consentimiento manual, pago, dos PIN, facial V3, auditoría y conservación | Solo Development y `(localdb)\SYNCHRO_Aislado` / `SynchroDB_Pruebas` |
| Web | Panel con datos propios, asistente, referencia facial consentida, detalle, enlaces de autorización y seguimiento | Resumen/listado de últimas 200 encomiendas; últimas 300 acciones por ficha |
| Administrador | Ficha de remitente/destinatario, comprobación manual de mensajes, pago e importe, emisión/entrega de PIN, historial y evidencia restringida | Abrir WhatsApp NO confirma consentimiento ni entrega; ninguna API de envío conectada |
| Facial del remitente | Human.js 3.3.6, modelo `SYNCHRO-HUMANJS-EMBEDDING-1024-v3`, normalización/comparación coseno en servidor, cifrado Data Protection/DPAPI | Descriptor e imagen sintéticos en QA. Falta comprobación con persona/cámara real; sin prueba de vida certificada |
| Fotografía del destinatario | Captura consentida cifrada, vínculo con encomienda, terminal, uso/casillero, PIN validado y entrega | Evidencia, SIN coincidencia biométrica ni cuenta creada para reconocerlo |
| Tablet | Contratos de depósito y retiro preparados y probados desde backend | No modificada, no conectada, no validada de extremo a extremo |
| Hardware | Respuestas identifican `hardwareSimulado=true`; autorizar desde web devuelve `aperturaRealizada=false` | Ninguna validación física |

La creación nueva usa `POST /api/web/encomiendas`. Conserva el identificador de `PreReservasEncomiendasWeb`, pero la raíz queda `GestionManual` hasta el depósito. Las rutas antiguas de pre-reserva y su clave de consulta siguen disponibles para el historial; no habilitan el flujo nuevo ni omiten pago/consentimientos.

### Archivos de esta etapa

- Backend: `Program.cs`, `seguridad/CodigoPinRetiro.cs`, `Endpoints/EncomiendasOperativasEndpoints.cs`, los servicios parciales y el worker de `Services/Encomiendas/`, y la migración indicada abajo.
- Usuarios: `PanelView.vue`, `PreReservaView.vue`, `EncomiendaDetalleView.vue`, `CapturaFacialEncomienda.vue`, `ReferenciaFacialEncomienda.vue`, `encomiendasApi.js`, `facialEncomiendaV3.ts`, `encomiendas.css`; ajustes de transporte compartido, formatos de estados, rutas, menú privado y retorno seguro después del ingreso.
- Administrador: `EncomiendasView.vue`, enlace en `AdminLayout.vue`, ruta y mutación con CSRF en `adminApi.js`. Los módulos de cobros simulados previos no se convierten en pagos reales.
- Pruebas: reglas/PIN, `EncomiendasOperativasSqlTests.cs`, `encomiendasOperativas.test.mjs`, actualización de la prueba del panel para exigir datos del backend y `tests/visual/encomiendas-operativas.visual.mjs`. El arnés de identidad/acceso se actualizó con respuestas controladas de los contratos nuevos del panel y una espera explícita de llegada de la petición antes de comprobar envíos repetidos; conserva las aserciones de seguridad existentes.
- Los cambios locales previos de diseño público/admin se conservaron. Esta etapa no modifica hero, estación, tarjetas, configuración de módulos ni archivos de la tablet.

## Migración revisada y aplicada

Script: `backend/Synchro.Api/Database/Migrations/20261123_EncomiendasOperativas_Pruebas.sql`.

Se revisó su impacto antes de ejecutarlo y se comprobó la instancia y `DB_NAME()`. Se crearon ocho tablas nuevas, claves foráneas, restricciones e índices. El pago está en la tabla operativa; las imágenes cifradas se guardan separadas de evidencias y auditoría. No hubo ALTER de tablas anteriores, conversión de registros antiguos, DROP ni borrado de datos para habilitar pruebas. El script tiene transacción, guarda de base de pruebas y marcador de versión para reejecución reconocida.

La comprobación inmediatamente posterior conservó las dos pre-reservas y los 37 usos que ya existían. Posteriormente las pruebas añadieron sus propios clientes, participantes, administradores, terminales, casilleros y operaciones ficticias. Se conservan sus operaciones y auditorías; los administradores creados por QA de navegador se desactivan al terminar. Las pruebas de expiración/revocación eliminan únicamente material biométrico sujeto a la política del módulo, no el historial operativo. No ejecutar otras suites SQL no revisadas que puedan reiniciar fixtures o borrar datos.

### Configuración local necesaria

- `ASPNETCORE_ENVIRONMENT=Development`.
- `ConnectionStrings:SynchroDB`: instancia y base autorizadas; autenticación integrada, sin AttachDBFilename. Esta LocalDB requiere `Encrypt=False`.
- `EncomiendasOperacion:Habilitado=true`.
- `ProteccionDatos:DirectorioClaves`: directorio absoluto privado, fuera del repositorio; claves persistentes protegidas por DPAPI de la cuenta Windows del servicio. Conservarlo para descifrar datos después de reiniciar.
- `RetiroPin:ClaveHmac`: reutilizar el secreto existente; no incluirlo en documentación, código ni registros.
- `WhatsAppManual:NumeroE164`: número oficial existente. Si no es válido, no se inventa un enlace de aceptación.
- Tarifas opcionales: `EncomiendasOperacion:Tarifas:Pequeno`, `Mediano`, `Grande`. Sin precios definitivos precargados. Importe positivo, hasta dos decimales; una tarifa configurada exige coincidencia exacta. Sin tarifa, el administrador debe registrar el importe realmente comprobado y justificarlo expresamente. Cero, ausencia, método simulado o casilla sin comprobar se rechazan.
- `EncomiendasOperacion:PinVigenciaMinutos`: 30 por defecto, intervalo admisible 1–1440. Máximo cinco intentos fallidos; cuatro emisiones por finalidad con al menos dos minutos entre reemisiones y reemplazo explícito.
- Limpiador habilitado por defecto cada minuto (`EncomiendasOperacion:LimpiarBiometria`). Elimina plantillas anuales vencidas/de cuentas inactivas y fotografías vencidas del módulo nuevo. Revocar elimina inmediatamente la plantilla activa e invalida verificaciones de depósito pendientes.

El servidor de revisión usa `https://localhost:7281`; no reemplaza la API anterior de `7278`. Web: `https://127.0.0.1:5185/mi-cuenta`; administrador: `https://127.0.0.1:5186/admin/encomiendas`. Ambos previews utilizan HTTPS y proxy al nuevo servidor. Las credenciales siguen siendo las cuentas existentes; las cuentas administrativas de QA quedan desactivadas.

### Arranque conjunto: encomiendas, WhatsApp manual y carga

Esta guía documenta la configuración existente; no activa módulos, modifica la tablet ni ejecuta migraciones. Los previews 5185/5186 anteriores son procesos de revisión temporales: no se restauran automáticamente después de reiniciar.

| Opción del backend | Valor o requisito |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development`; estos módulos no deben habilitarse como producción |
| `ConnectionStrings:SynchroDB` | `Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False;Connect Timeout=5` |
| `EncomiendasOperacion:Habilitado` | `true` para la integración operativa de encomiendas |
| `WhatsAppManual:Habilitado` | `true` para registrar las rutas del procedimiento manual y su endpoint CSRF compartido |
| `WhatsAppManual:NumeroE164` | Número oficial configurado privadamente: el módulo manual exige exactamente `504` seguido de ocho dígitos, con `+` inicial opcional. No utilizar el texto de ejemplo como valor |
| `CargaDispositivos:RegistroHabilitado` | `true` para preparación/identidad/retiro de carga y sus consultas administrativas |
| `RostroV3:RegistroHabilitado` | `true` para el registro V3 del procedimiento manual existente; no sustituye el indicador de carga ni cambia la tablet |
| `ProteccionDatos:DirectorioClaves` | Ruta absoluta privada fuera del repositorio, con claves persistentes cifradas por DPAPI de Windows. Se exige al habilitar encomiendas o registro de carga; no eliminarla ni cambiarla arbitrariamente |
| `RetiroPin:ClaveHmac` | Secreto existente de 32 bytes en Base64 para PIN de encomiendas; mantenerlo en User Secrets o configuración privada |
| `RetiroPersonal:ClaveHmac` | Secreto existente de 32 bytes en Base64 para PIN del procedimiento manual de resguardo; no confundirlo con el de encomiendas |
| `CargaDispositivos:ClaveHmac` | Secreto existente de 32 bytes en Base64 para carga. El código admite `ServiciosTemporales:ClaveHmac` como respaldo si la clave específica está ausente; si ninguna es válida, la operación devuelve 503 |
| `RostroV3:UmbralDistanciaCoseno` | Conservar el valor validado; valor predeterminado del servidor: `0.22`. No variar el umbral para forzar aprobaciones |
| `WhatsApp:SolicitudesInicioHabilitadas`, `WhatsApp:SolicitudesInicioWorker:Habilitado`, `WhatsApp:VerificacionOutbox:Habilitado`, `WhatsApp:Outbox:Habilitado` | `false` para este procedimiento manual, sin webhook ni envíos automáticos |
| `ServiciosTemporales:Habilitado` | Indicador independiente del servicio temporal anterior; no es requisito para registrar las sesiones seguras de carga. Conservar la configuración aprobada: activarlo cambia el comportamiento de rutas históricas y requiere su inventario/tarifas existentes |

Prerrequisitos de datos: disponer del esquema ya revisado de WhatsApp manual, carga y encomiendas en la base autorizada. Carga verifica, entre otros, `EspaciosCarga`, `SesionesCargaSynchro` y el marcador `SynchroFixtureDeposito=1`. Un 404 puede indicar módulo desactivado/entorno no autorizado; un 503 puede indicar clave o esquema incompleto. No ejecutar scripts de reconstrucción, fixtures destructivos ni migraciones automáticamente para resolverlos. Revisar primero su impacto y destino.

Al habilitar WhatsApp manual, el código registra también el limpiador de evidencia de retiro V3 existente. Los trabajadores de vencimiento (`Reservas:ProcesarVencimientos`, `ServiciosTemporales:ProcesarVencimientos`) son opciones separadas; no activarlos como parte de una comprobación de arranque si no se desea procesar vencimientos reales del entorno de pruebas. El limpiador nuevo de encomiendas conserva la política documentada anteriormente.

Ejemplo de variables **sin valores secretos**, desde la raíz del repositorio en PowerShell. Antes de iniciar, cargar el número oficial y las claves ya existentes mediante User Secrets o el mecanismo privado del entorno; no escribirlos en este documento, comandos registrados, archivos versionados ni en variables `VITE_*`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__SynchroDB = 'Server=(localdb)\SYNCHRO_Aislado;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=False;Connect Timeout=5'
$env:EncomiendasOperacion__Habilitado = 'true'
$env:WhatsAppManual__Habilitado = 'true'
$env:CargaDispositivos__RegistroHabilitado = 'true'
$env:RostroV3__RegistroHabilitado = 'true'
$env:ProteccionDatos__DirectorioClaves = Join-Path $env:LOCALAPPDATA 'SYNCHRO\EncomiendasOperacion\claves'
$env:WhatsApp__SolicitudesInicioHabilitadas = 'false'
$env:WhatsApp__SolicitudesInicioWorker__Habilitado = 'false'
$env:WhatsApp__VerificacionOutbox__Habilitado = 'false'
$env:WhatsApp__Outbox__Habilitado = 'false'

dotnet dev-certs https --check
dotnet build .\backend\Synchro.Api\Synchro.Api.csproj --no-restore -p:OutputPath=bin/EncomiendasOperacion/ -p:UseAppHost=false
Push-Location .\backend\Synchro.Api
dotnet .\bin\EncomiendasOperacion\Synchro.Api.dll --urls https://localhost:7281
Pop-Location
```

Usar un puerto libre; si ya existe una instancia en 7281, reutilizarla o detener explícitamente ese proceso antes de iniciar otra. No cambiar la API de 7278 ni la configuración de la tablet para esta revisión.

Web y administrador se inician en otras terminales, con las dependencias locales instaladas y desde sus respectivos directorios:

```powershell
$env:SYNCHRO_API_TARGET = 'https://localhost:7281'
# El certificado HTTPS y su contraseña, si la tiene, deben estar disponibles privadamente.
# SYNCHRO_CERT_PATH: ruta al PFX fuera del repositorio; predeterminado:
# %LOCALAPPDATA%\SYNCHRO\certificados\localhost.pfx
# SYNCHRO_CERT_PASSWORD: cargar desde el mecanismo privado, nunca escribir su valor aquí.

# Terminal 1, desde frontend/usuarios-app:
npm.cmd run dev
# https://127.0.0.1:5175/mi-cuenta

# Terminal 2, desde frontend/admin-app, con las mismas variables del proxy/certificado:
npm.cmd run dev
# https://127.0.0.1:5174/admin/encomiendas
```

Los `vite.config.js` existentes exigen ese PFX al servir y apuntan a 7278 si falta `SYNCHRO_API_TARGET`. Si los puertos predeterminados están ocupados por previews anteriores, cerrar solo esos procesos de revisión o elegir puertos libres con las opciones existentes de Vite. No versionar PFX, PEM, claves, contraseñas, User Secrets, `.env*` ni `appsettings.Development.json`.

Comprobaciones de arranque: iniciar sesión con una cuenta autorizada y consultar `/api/web/encomiendas/configuracion`, `/api/admin/encomiendas/operativas`, `/api/admin/whatsapp-manual/pendientes` y `/api/admin/carga/espacios`. Respetar los permisos de cada ruta; una respuesta 401/403 sin sesión no implica caída del módulo. El pago de encomiendas sigue siendo manual comprobado; los cobros simulados de otros servicios no lo acreditan. Esta guía no constituye una nueva validación del flujo de carga ni de la tablet.

## Contratos HTTP implementados

Todas las escrituras exigen HTTPS, sesión, CSRF y límites de frecuencia. La identidad/rol proceden del servidor. No se permite consultar descriptores ni PIN en texto plano desde las fichas. Respuestas sensibles sin caché.

### Web — cookie Clientes

| Método y ruta | Contrato |
| --- | --- |
| `GET /api/web/encomiendas/configuracion` | Tarifas nullable, modelo/versiones de consentimiento, conservación, modo manual, hardware simulado/tablet no integrada |
| `GET /api/web/encomiendas` | Hasta 200 operaciones propias, fechas, pago y casillero reales |
| `POST /api/web/encomiendas` | `{telefonoDestinatario,tamanoSolicitado}`: `Pequeno`, `Mediano` o `Grande`; remitente desde sesión/cuenta activa |
| `GET /api/web/encomiendas/{id}` | Propietario únicamente: dos participantes, enlaces/textos para WhatsApp oficial, pago, referencia, metadatos PIN y auditoría |
| `GET /api/web/encomiendas/referencia-facial` | Estado y fechas; nunca devuelve el descriptor |
| `POST /api/web/encomiendas/referencia-facial` | `{imagenPng,descriptor,modelo,versionConsentimiento,contrasena}` y `X-SYNCHRO-CONSENTIMIENTO: ACEPTO`; reautentica, cifra descriptor y fija revalidación anual; no conserva la foto del remitente |
| `DELETE /api/web/encomiendas/referencia-facial` | Revoca consentimiento y elimina plantilla activa |
| `POST /api/web/encomiendas/{id}/deposito/autorizar` | `{solicitudId,pin,captura:{imagenPng,descriptor,modelo,versionConsentimiento}}` y consentimiento en cabecera; compara descriptor en servidor y vincula autorización a terminal/PIN/referencia; NO abre |

### Administrador — Administrador u Operador

Base: `/api/admin/encomiendas/operativas`. Token CSRF del endpoint administrativo existente `/api/admin/csrf-token`.

| Método y sufijo | Contrato |
| --- | --- |
| `GET /` y `GET /{id}` | Listado y ficha con estado, administrador responsable y fechas |
| `POST /{id}/consentimientos/{rol}/confirmar` o `/rechazar` | Rol `remitente`/`destinatario`; `{telefono,codigo,mensajeReferencia,mensajeComprobado,aceptaEncomienda}`. Coincidencia de número/código, prueba manual explícita y aceptación independiente del destinatario |
| `POST /{id}/pago/confirmar` | `{metodo,importeHnl,referencia,pagoComprobado,justificacionImporte}`. Métodos Efectivo/Transferencia/Tarjeta; servidor comprueba ambos consentimientos e importe válido |
| `POST /{id}/pines/{finalidad}/emitir` | Finalidad `deposito`/`retiro`, `{reemplazar}`. Devuelve valor una sola vez para entrega manual; la UI lo borra a los 60 s, al salir/ocultarse y después de registrar entrega; al volver de WhatsApp conserva solo metadatos para registrar el envío, sin recuperar el valor |
| `POST /{id}/pines/entrega` | `{pinId,telefono,mensajeReferencia,envioComprobado}`. Revalida autorización del número, vigencia, estado y condiciones de la finalidad. No envía automáticamente |

Solo Administrador, nunca Operador: `GET /{id}/evidencias/{evidenciaId}/foto`, con `X-SYNCHRO-MOTIVO` obligatorio; y `POST /{id}/evidencias/{evidenciaId}/conservacion` con `{incidencia,motivo,hastaUtc}`. Consulta y extensión auditadas. Foto disponible solo tras entrega y dentro de conservación. La extensión debe ampliar el plazo existente y quedar dentro de los siguientes 30 días; no restaura una imagen eliminada. El navegador libera su URL temporal de imagen en 60 s o al salir/ocultarse.

## Operaciones preparadas para integrar posteriormente la tablet

No se agregó ninguna pantalla, botón, ruta, componente, estilo ni función a `frontend/tablet-app`. La comparación SHA-256 de sus 329 archivos de referencia no encontró diferencias.

Base `/api/terminales/encomiendas/operativas`; cookie Terminal, terminal activa y autorización del servidor. Obtener CSRF en `GET /csrf-token`; enviar `X-CSRF-TOKEN` en todas las escrituras.

1. `POST /depositos/solicitudes`, `{codigoCasillero}`. Casillero normal disponible y terminal activa. Devuelve `{solicitudId,venceUtc,casillero,aperturaRealizada:false}`. Solicitud de dos minutos.
2. La futura tablet podrá presentar el enlace web `/encomiendas/{id}?solicitud={solicitudId}` para la encomienda elegida. La web preserva el retorno al detalle al iniciar sesión. El remitente verifica su rostro e introduce el PIN de depósito en la web.
3. `GET /depositos/solicitudes/{solicitudId}`. Consulta de la propia terminal: Pendiente/Autorizada/Depositada/Vencida. Una autorización web no ocupa ni abre el casillero.
4. `POST /depositos/solicitudes/{solicitudId}/confirmar`, `{depositoComprobado:true}`. Revalida consentimiento, pago, referencia y cuenta vigentes, PIN entregado/vigente/no consumido, evidencia de dos minutos y disponibilidad. Consumo, uso, casillero y auditoría atómicos. Confirmaciones repetidas de la misma solicitud devuelven el mismo uso. Apertura física simulada.
5. Tras depósito, el administrador emite y entrega manualmente el PIN de retiro al destinatario autorizado. `GET /{id}` devuelve a la misma terminal el estado, tamaño, casillero, consentimiento informado y `pagoAdicionalRequerido:false`; no revela teléfonos, PIN ni plantillas. Otra terminal recibe 404. La futura tablet puede decidir el recorrido de retiro sin presentar cobro.
6. `POST /{id}/evidencias-retiro`, `{imagenPng,descriptor:null,modelo,versionConsentimiento}` con cabecera de consentimiento. La futura tablet deberá informar y obtener consentimiento explícito antes de capturar. Devuelve `{evidenciaId,venceUtc,coincidenciaBiometrica:null,evidenciaDeRetiro:true}`. No comparación contra remitente/destinatario, no cuenta automática. Captura pendiente caduca en dos minutos; al confirmar entrega se fija conservación de 30 días.
7. `POST /{id}/retirar`, `{evidenciaId,pin,entregaComprobada:true}`. Misma terminal, operación depositada, PIN exclusivo de retiro, pago previo confirmado y evidencia vigente. Entrega, consumo del PIN/evidencia, fecha de conservación y liberación del casillero atómicos. Devuelve `pagoAdicionalRequerido:false` y `hardwareSimulado:true`.

La futura interfaz debe omitir cualquier pantalla de cobro para el destinatario de esta operación pagada. No usar endpoints históricos de facial/pago de resguardo o carga para estas encomiendas. No hay endpoint web para apertura arbitraria.

## Validaciones realizadas y límites

- Backend: 39 pruebas seleccionadas aprobadas. Reglas de autorización/rechazo/vencimiento/pago, HMAC por finalidad y compatibilidad anterior, V3, SQL real, PIN con cinco intentos persistentes/uso único, entrega sin nuevo cobro, cifrado, revocación, revalidación anual, prórroga justificada y eliminación de fotografía vencida conservando auditoría.
- HTTP real en TestServer: 401, separación de roles, propietario, HTTPS, CSRF y consentimiento biométrico obligatorio. Identidades de ese arnés son ficticias y solo existen en el ensamblado de pruebas; el backend de aplicación no acepta sus cabeceras.
- Se encontró y corrigió un deadlock en la prueba concurrente. Se bloquea primero la operación canónica y después la solicitud, antes de consultar/actualizar la pre-reserva. Dos confirmaciones simultáneas crean/devuelven un solo uso; el reintento es idempotente.
- Usuarios: 53 pruebas aprobadas, typecheck y compilación correctos. Administrador: typecheck y compilación correctos.
- Chrome real/Playwright: creación guiada y gestión administrativa contra el backend SQL real, con cuentas propias y comprobantes/descriptor/imagen ficticios claramente identificados. Dos consentimientos, pago manual, PIN efímero, teléfono autorizado y entrega comprobada reflejados en la misma ficha de usuario.
- Capturas de panel/detalle en claro y oscuro a 1440/834/390 px y ficha administrativa a esos tamaños. Revisión de legibilidad, composición y ausencia de desbordamiento de página. Cámara con consentimiento obligatorio y error de permiso controlado. Se corrigió el desbordamiento móvil causado por una etiqueta accesible de la tabla; la tabla conserva desplazamiento interno y acceso por teclado. Se comprobó el retorno desde una pestaña externa ocultando el PIN y conservando únicamente los metadatos necesarios para registrar la entrega. No confundirlo con validación facial real.
- Pruebas visuales públicas conservadas para estación, 51 puertas de módulos, navegación sticky, temas y acceso; no representan validación de la tablet.
- Artefactos de revisión ignorados por Git: `frontend/usuarios-app/node_modules/.cache/synchro-visual/encomiendas-operativas/`. Capturas no incluyen PIN en texto plano ni contraseñas.

Comando backend revisado (evita las suites SQL ajenas):

```powershell
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj --filter "FullyQualifiedName~EncomiendasOperativasSqlTests|FullyQualifiedName~PoliticaEncomiendaOperativaTests|FullyQualifiedName~PinEncomiendaOperativaTests|FullyQualifiedName~CodigoPinRetiroTests|FullyQualifiedName~DescriptorFacialV3Tests" --no-restore -p:OutputPath=bin/EncomiendasOperacion/ -p:UseAppHost=false --verbosity minimal
```

Pendiente: revisión directa de la tablet con el usuario; verificación de Human.js con cámara/persona real en la web; revisión de operación física de depósito/entrega cuando exista hardware; pasarela de pago y webhook de WhatsApp (no implementados ni simulados); aprobación de tarifas definitivas y de políticas operativas para un despliegue fuera de este entorno de pruebas. No se declara integración completa de las cuatro aplicaciones.
