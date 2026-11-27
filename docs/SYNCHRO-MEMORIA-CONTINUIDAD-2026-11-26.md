# SYNCHRO — MEMORIA INTEGRAL DE CONTINUIDAD

**Fecha de corte:** 26 de noviembre de 2026 (Honduras).  
**Finalidad:** llevar el contexto técnico, decisiones funcionales, avances, preferencias y tareas pendientes a un chat nuevo de ChatGPT o a Codex, sin rehacer lo ya trabajado.  
**Repositorio:** https://github.com/Bjosue95/SYNCHRO  
**Rama vigente:** `rebuild/tablet-desde-cero` (**no cambiar a `main` ni crear otra rama sin autorización del usuario**).  
**Último commit observado ANTES de publicar este documento:** `956707ca47fbd408519d5d5ed098aee7ff9f86d9` (actualización de las pruebas para retirar la vinculación obligatoria del flujo de reservas).  
**Ruta local habitual:** `<RUTA_LOCAL_DE_SYNCHRO>`.

> ## LEER PRIMERO — REGLA MÁS RECIENTE (PREVALECE SOBRE DECISIONES ANTERIORES)
>
> **SYNCHRO es actualmente un prototipo de uso controlado por su creador. Debe poder abrirse y probar el flujo «Tengo una reserva» desde CUALQUIER TABLET, sin registrar, vincular, autorizar ni verificar el ID del dispositivo.** No añadir de nuevo pantallas de «Autorizar estación», comprobaciones de la sesión `Synchro.Terminal`, asignación de `TerminalId` por dispositivo ni bloqueos antes del PIN en ese flujo. **Sí** conservar la identificación de la reserva mediante PIN real de seis dígitos, fotografía/evidencia de prueba, consentimiento cuando aplica, comprobación de pago, confirmación de depósito, protección antifalsificación CSRF, y reglas de vencimiento y uso único del PIN. La simplificación de autorización de dispositivo está limitada en el código al grupo `/api/terminales/reservas` cuando se inicia el modo de pruebas aislado; no equivale a cambiar otros servicios operativos ni al panel administrador.
>
> **Este requisito es posterior y sustituye cualquier texto antiguo que recomiende vincular «una última vez» la Galaxy Tab S7 para poder entrar en «Tengo una reserva».** El usuario corrigió explícitamente esas recomendaciones y no quiere volver a ese enfoque.

## 1. Situación precisa al cerrar este chat

- Se reparó el flujo de reservas después de varios obstáculos introducidos por la vinculación de terminales: mensaje «La sesión de la terminal ha vencido», después «Esta terminal no está autorizada para estas reservas». El usuario aclaró que la tablet es un medio para una demostración y que debe poder usarse desde cualquier tablet sin autorización de hardware.
- **El cambio definitivo más reciente está PUBLICADO EN GITHUB**, no probado todavía en ejecución local tras ese último commit. Se retiraron los bloqueos de dispositivo del frontend `ReservaWebElegirView.vue` y del cliente `reservasWebTerminalApi.js`. El backend permite un modo libre en el grupo de reservas del prototipo, asignando un ID **interno de pruebas gestionado por SQL**, no del navegador. El script de arranque de pruebas activa dicho modo.
- El último commit antes de este archivo fue `956707c`; **NO** afirmar que se ejecutaron correctamente `dotnet build`, `npm.cmd test`, pruebas SQL o un depósito completo después de estos cambios si el usuario no aporta el resultado. Verificar en la laptop.
- No reabrir el debate de vinculación de tablet al volver al PIN. Si aparece un error en este flujo, revisar error HTTP real, configuración de pruebas y reglas de PIN/reserva, no pedir registrar de nuevo el dispositivo.
- El próximo trabajo razonable es comprobar que **«Tengo una reserva → elegir servicio → (tipo de casillero si es encomienda) → captura de foto → PIN de depósito → pantalla de depósito/temporizador → confirmar»** funcione desde la Galaxy Tab S7 por Tailscale y, si es posible, desde otro navegador/tablet, con una reserva y PIN verdaderamente elegibles.

## 2. Marca, negocio y representación pública

**SYNCHRO — «El punto donde todo se conecta»**. Solución modular de casilleros inteligentes y autoservicio con cuatro servicios integrados:

1. Resguardo temporal de pertenencias personales, documentos, bolsos y maletas.
2. Depósito/recepción/entrega y retiro de encomiendas por remitentes y destinatarios autorizados.
3. Casilleros especiales de temperatura controlada, **frío y calor**.
4. Estaciones de carga de celulares y laptops (cable e inalámbrica), incluida carga de emergencia presencial.

Se ofertan **estaciones multifuncionales o módulos especializados independientes** según el lugar, no únicamente una máquina combinada. Lugares objetivo: universidades (CURC de Comayagua), aeropuertos, parques centrales y otras zonas urbanas. La visión del producto contempla alarmas, sensores, controles sólidos de seguridad, accesibilidad con braille, cámaras/registro y respaldo energético/paneles solares. Diferenciar lo implementado realmente de capacidades futuras cuando se describan técnicamente; en presentaciones comerciales no repetir innecesariamente «prototipo» en cada frase.

**Paleta oficial:** azul `#01305C`, turquesa `#00B9C7`, celeste `#5EC6E8`, gris `#5B6770`, blanco `#FFFFFF`. Emplear el **logo oficial existente**, sin inventarlo. Estilo moderno, responsivo, claro/oscuro, iconografía propia consistente, animaciones suaves y accesibles.

**Prototipo físico inicial:** estructura de 10 casilleros verticales y elementos adicionales, M04 frío y M09 caliente, variedad de tamaños, módulo de carga de dispositivos. Prototipo de feria con apertura física manual; la web presenta visión modular ampliada. El modelo interactivo web aprobado presenta tres columnas de casilleros a la izquierda, tablet al centro, monitor de aproximadamente 32 pulgadas a la derecha y compartimentos de carga abajo. La estación multifuncional interactiva aparece primero en el landing, seguidas de módulos alternativos.

**Feria navideña:** 5 de diciembre de 2026, Parque Central de la Villa de San Antonio (Honduras), stand con mesa en carpa 3×3 m. Guiones para TikTok/Reels 30–60 s, videos realistas en plaza central y universidad; branding en polos y material impreso. FODA, misión, visión, valores y modelo Canvas tratados anteriormente, siempre reflejando los cuatro servicios, costos fijos/variables, inversión y necesidad de financiamiento.

## 3. Arquitectura y fuentes de verdad

- **Repositorio:** `Bjosue95/SYNCHRO`, rama `rebuild/tablet-desde-cero`, no editar `main` por defecto. Otros historiales: `refactor/arquitectura-backend`, `feat/encomiendas-transaccion-aislada`, `fix/seguridad-operaciones-carga`; sus contenidos no deben imponerse sobre la rama actual.
- **Backend:** ASP.NET Core / .NET 10: `backend/Synchro.Api/Synchro.Api.csproj`, HTTPS local usual `https://localhost:7278`.
- **SQL:** LocalDB `(localdb)\SYNCHRO_Aislado`, base aislada `SynchroDB_Pruebas`. No ejecutar migraciones o limpiezas masivas sin consultar SQL real y confirmar que se trata de la base de pruebas.
- **Tablet:** Vue 3/Vite `frontend/tablet-app`, puerto `5173`. Tablet física **Samsung Galaxy Tab S7** (no Galaxy Tab A11). La tablet accede por **Tailscale** y HTTPS a los servicios que corren en el equipo Windows. No recomendar `localhost` en la tablet: apuntaría a ella misma.
- **Administrador:** Vue 3/Vite `frontend/admin-app`, puerto usual `5174` en un documento de estado anterior; comprobar el script/terminal real, pues otros despliegues usaron `5176`.
- **Usuarios web:** Vue 3/Vite/TypeScript `frontend/usuarios-app` (el nombre exacto del paquete debe verificarse), usualmente puerto `5175`.
- **Certificados:** `mkcert` y CA confiada en Galaxy Tab S7, HTTPS y Tailscale. Conservar origen HTTPS y cabeceras/cookies cuando las necesiten otros módulos.
- **Seguridad backend:** secretos y HMAC bajo el perfil Windows, claves DataProtection originales y persistentes bajo `%LOCALAPPDATA%\SYNCHRO\CargaSegura\DataProtectionKeys` por el script de arranque. No copiar claves ni PIN reales a este documento o al repositorio.
- **Advertencia de sincronización:** el código remoto fue modificado a través de GitHub en esta conversación; actualizar el checkout con `git pull --ff-only origin rebuild/tablet-desde-cero` solo si el árbol local lo permite. Si hay cambios locales, diagnosticar antes de sobrescribir.

## 4. Decisiones funcionales — reservas web y tablet (ACTUAL)

### Reservar desde la web de usuario

La cuenta del cliente gestiona **Reservar casillero**, **Mis reservas** y **Mi perfil**. Elegir tipo normal/frío/caliente según servicio, tamaño y duración, disponibilidad real de SQL, pagador/modalidad y pago pendiente. La web no debe obligar al registro facial para reservar. En la página de reserva el usuario revisa y confirma los detalles; puede pagar el remitente/usuario o el destinatario cuando esa opción corresponda. Cuando el pagador es el usuario y se muestran datos de tarjeta de **prueba**, no guardar, registrar ni transmitir número, fecha de vencimiento ni código de seguridad al backend; el administrador confirma el cobro desde **Cobros pendientes** indicando que proviene de Web. No duplicar cobros. El usuario quiere que la tarjeta pase a estado «esperando depósito» tras entregar el PIN por WhatsApp.

### «Tengo una reserva» en cualquier tablet

Recorrido:
1. Entrada desde «Guardar» → **Tengo una reserva**.
2. Dos tarjetas: **Pertenencias personales** y **Encomienda**.
3. Si encomienda: tres tarjetas **Normal**, **Frío**, **Calor** (no permitir frío/calor para «Personal»).
4. Captura fotográfica como evidencia de la persona que deposita, sin registrar biometría nueva desde ese flujo de reserva. Existen otros flujos con facial V3; no mezclarlos sin necesidad.
5. PIN real de **6 dígitos**, emitido/entregado por operación administrativa WhatsApp según reglas. No usar PIN ficticio para saltar SQL.
6. Identificación de reserva en `/api/terminales/reservas/depositos/identificar`, que devuelve reserva/casillero/servicio/tipo cuando el PIN es elegible.
7. Pantalla de depósito con contador ~1 minuto y opción de añadir 1 minuto, confirmación final **«Ya deposité»**. Backend comprueba PIN, foto, consentimiento y depósito, ocupa casillero y registra operaciones. No mostrar número interno de reserva al usuario.
8. Dejar la tablet lista para la siguiente persona, limpiando PIN/foto y datos temporales cuando termina.

**Decisión más reciente:** este flujo **NO** depende de vinculación de dispositivo, cookie de terminal, `mi-sesion`, `ReservasPrototipo:TerminalId` individual, aprobación administrativa ni renovación de sesión de terminal. La excepción se activa solo en el entorno aislado y con `ReservasPrototipo:AccesoLibrePruebas=true`. La UI no debe mostrar «Autorizar estación» en este recorrido. El PIN y la elegibilidad de la reserva siguen siendo validaciones del backend. Si existe otra política de acceso global de Tailscale o red, no confundirla con autorización por dispositivo dentro de SYNCHRO.

### Encomiendas web

Remitente hace reserva desde web; operador verifica consentimiento de WhatsApp y pago correspondiente, emite PIN de **depósito**, lo envía manualmente por WhatsApp. Remitente deposita tras foto/facial y PIN. Solo después del depósito se habilita autorización del **destinatario**: consentimiento WhatsApp, PIN **diferente de retiro**, captura del receptor y retiro. Si remitente pagó, destinatario no vuelve a pagar. Los PIN son temporales, de un solo uso, sin reutilización; QR/SMS descartados. No exponer el número de reserva en WhatsApp. No introducir alta presencial de encomienda en tablet. La cancelación de la reserva debe ser independiente y solo antes del depósito.

### Servicio de carga

Uso **presencial, sin reserva web**, para celulares (cable e inalámbrica) y laptops; pago **después del uso**, como resguardo personal. Espacios C1–C4 (cable), L1–L2 (laptop), W1–W2 (inalámbrica). Tarifa inalámbrica de prueba confirmada: **60 minutos por L 10, 30 minutos adicionales por L 5**. Se trabajó con PIN/rostro en distintas iteraciones; antes de modificar la longitud o reglas del PIN de carga, leer el código actual. No mostrar «simulación» en las pantallas visibles por defecto. Cobros simulados en admin, sin tarjeta real.

## 5. Diseño tablet y web que se debe conservar

- Bienvenida, Menú, Guardar, Tamaño, Casillero, PIN, Foto, Depósito, Retiro y Carga comparten la identidad visual SYNCHRO. **No crear una pantalla de estilo ajeno** a Guardar/Menu.
- Botones Volver y Continuar grandes, alineados, con íconos existentes `28_regresar` y `29_continuar` (versiones claro/oscuro), selección con check, sin duplicaciones; tarjetas accesibles para pulsación en Galaxy Tab S7.
- Transición **Bienvenida → Menú** aprobada: desvanecido homogéneo, suave, sin capas visibles, zoom ni movimientos bruscos.
- En `ReservaWebElegirView.vue`: primer paso dos tarjetas 2 columnas; segundo tres opciones en cuadrícula 2×2 con hueco invisible; modo claro/oscuro; mismo patrón que `GuardarView.vue`. Eliminadas después las comprobaciones de terminal.
- Web usuarios: hero cuadriculado azul también en claro; navegación fija Home/Servicios/Nosotros/Contáctanos/Ingresar; modal/página de acceso con ver contraseña, errores de credenciales claros. Animaciones automáticas e interactividad real; no añadir botón «Pausar» sin necesidad. Iconografía propia para todos los elementos: teléfono, correo, casilleros, botón y estados.
- Administrador: identidad moderna, panel WhatsApp/PIN con pestañas Tablet, Remitente y Destinatario. Un solo botón «Actualizar» de cabecera y contadores reales de pendientes fueron solicitados; revisar implementación actual en código porque un documento anterior los marcaba pendientes, y trabajos posteriores pudieron avanzar.

## 6. Historia inmediata de código — del error de sesión al acceso libre

**Secuencia real de esta conversación** (todos los commits siguientes se publicaron en `rebuild/tablet-desde-cero`; siempre revisar último `git log`):

1. Se armonizó la vista de «Tengo una reserva» con Guardar, iconos y botones (`ReservaWebElegirView.vue`), y se añadieron tests.
2. Apareció error en PIN «La sesión de la terminal ha vencido». Se comprobó que `/api/terminales/reservas/csrf-token` exigía cookie `Synchro.Terminal`; antes de verificar el PIN recibía `401`. Se introdujo comprobación previa y mensaje de reautorización.
3. Se creó/extendió vinculación y renovación administrativa (pantalla `AutorizarTerminalView.vue`, endpoint autorización y sección admin Terminales), más credencial persistente `TerminalDispositivo` y renovación automática. **ESTA ARQUITECTURA SIGUE EN CÓDIGO PARA OTROS FINES, PERO YA NO ES REQUISITO PARA RESERVAS DEL PROTOTIPO. No reintroducirla en este flujo**.
4. Apareció «Esta terminal no está autorizada para estas reservas»: `IdentificarParaDeposito` comparaba un `ReservasPrototipo:TerminalId` ausente. Primero se implementó elegir la única terminal activa o exigir asignación si hay varias. **El usuario RECHAZÓ explícitamente esa restricción por dispositivo**.
5. **Decisión final**: acceso a **reservas de prueba desde cualquier tablet**, sin verificación/registro/vinculación de dispositivo. Backend resuelve un ID de estación interna `SYNCHRO-RESERVAS-DEMO` desde SQL de pruebas, solo si la bandera `ReservasPrototipo:AccesoLibrePruebas` está activa. El grupo `/api/terminales/reservas` no requiere `SoloTerminal` en ese modo; el grupo `/api/terminales/encomiendas/operativas` y los demás servicios conservan sus mecanismos propios.
6. `tools/Iniciar-BackendCargaSegura.ps1` fue actualizado para habilitar automáticamente `EncomiendasOperacion__Habilitado='true'` y `ReservasPrototipo__AccesoLibrePruebas='true'`, además del HMAC original y claves persistentes de pruebas.
7. Frontend `ReservaWebElegirView.vue`: **se retiraron** `comprobarSesionTerminalReservaWeb`, `comprobarAsignacionTerminalReservasWeb`, pantalla de error y link de vinculación. `Continuar` depende de la elección de tarjeta, no de la cookie de terminal.
8. `frontend/tablet-app/src/services/reservasWebTerminalApi.js`: se simplificó para llamadas a `reservas` con `csrf-token`, PIN real, captura, confirmación; **se retiraron** reintentos por renovar cookie de terminal y el error `SesionTerminalNoAutorizada` en ese cliente.
9. Se actualizaron tests `frontend/tablet-app/tests/renovacionTerminal.test.mjs` (archivo con nombre histórico) y `frontend/tablet-app/tests/reservaWebDepositoFlujo.test.mjs` para reflejar el acceso libre y el PIN obligatorio.
10. `backend/Synchro.Api/Program.cs` exige que el modo libre esté en `Development` + base SQL de pruebas aislada + encomiendas operativas habilitadas; **no extender a producción por defecto**. El usuario quiere sencillez en el prototipo, no nueva arquitectura de permisos o explicaciones de seguridad recurrentes.

**Archivos relevantes** (leer versiones actuales antes de editar):

- `frontend/tablet-app/src/views/ReservaWebElegirView.vue`
- `frontend/tablet-app/src/views/VerificacionFacialView.vue`
- `frontend/tablet-app/src/views/RetiroPinView.vue` (reutilizada en depósito de reserva)
- `frontend/tablet-app/src/views/DepositoView.vue`
- `frontend/tablet-app/src/services/reservasWebTerminalApi.js`
- `frontend/tablet-app/src/services/reservaWebSesion.js` (en memoria, expiración local de 5 minutos)
- `frontend/tablet-app/src/router/index.js`
- `frontend/tablet-app/tests/reservaWebDepositoFlujo.test.mjs`
- `frontend/tablet-app/tests/renovacionTerminal.test.mjs`
- `backend/Synchro.Api/Endpoints/EncomiendasOperativasEndpoints.cs`
- `backend/Synchro.Api/Services/Encomiendas/EncomiendasOperativasTerminal.cs`
- `backend/Synchro.Api/Services/Encomiendas/ReservasCapturaDemostrativa.cs`
- `backend/Synchro.Api/Services/Encomiendas/ReservasPrototipo.cs`
- `backend/Synchro.Api/Services/Encomiendas/ReservasPagosDemostracion.cs`
- `backend/Synchro.Api/Program.cs`
- `tools/Iniciar-BackendCargaSegura.ps1`
- `tests/Synchro.Api.Tests/ReservasPrototipoSqlTests.cs`

**Crítico para diagnosticar el nuevo comportamiento:** comprobar qué mensaje HTTP devuelve de verdad el endpoint `/api/terminales/reservas/depositos/identificar` antes de suponer que es un problema de terminal. Posibles causas diferentes: PIN no entregado/vencido, reserva ya vencida/no elegible, falta pago/consentimiento, falta bandera de demo en el proceso correcto, incompatibilidad frontend/backend, ausencia de scripts/migraciones SQL. No inventar ni regenerar PIN a ciegas.

## 7. Otros avances relevantes y estado de pruebas

- **Resguardo personal:** un documento de continuidad del 26 de noviembre informó un recorrido completo **confirmado por el usuario**: WhatsApp manual → PIN → depósito → rostro+PIN → cobro → confirmación admin → retiro. Conservarlo. Tarifas personales de pruebas de esa etapa: primera hora L 10 pequeño, L 15 mediano, L 20 grande; bloques adicionales de 30 minutos: L 5, L 8, L 10 respectivamente. Valores de pruebas, no comerciales definitivos.
- **Encomiendas:** integración web, WhatsApp manual remitente/destinatario y pago. El recorrido end-to-end de una **encomienda operativa nueva** no estaba verificado completamente en el último informe previo. Captura de depositante y de destinatario independientes; PIN de depósito y retiro diferentes.
- **Cobros:** pagos de prueba de usuarios web confirmables por admin, con origen Web o Tablet; no guardar datos de tarjeta, distinguir quién paga, evitar cobrar al destinatario cuando el remitente ya pagó. Cobro de carga al finalizar servicio.
- **Facial:** se desarrolló V2/V3 con uso en flujos de resguardo/retiro; hubo comparaciones con umbral que rechazaron rostros de pruebas. No reducir umbrales a ciegas. El flujo de reserva web reciente utiliza captura de prueba/evidencia, no una equivalencia verificada al sistema biométrico de retiro.
- **SQL:** una memoria previa informó limpieza confirmada de 90 solicitudes antiguas pendientes (0 casilleros liberados) y respaldo creado; **no repetir limpieza sin diagnóstico SQL actual**. También hay incidencias históricas M03, W1/W2; **no asumir estados actuales por los resultados antiguos**.
- **Pruebas:** en distintas fechas se reportaron suites .NET y tablet Vite correctas, pero **las modificaciones posteriores cambian ese estado**. Ejecutar build y tests de los cambios actuales; para archivos SQL aislados usar entornos preparados, no la BD de usuario real.

## 8. Qué hacer en el próximo chat

1. **Primero leer este archivo y el mensaje nuevo del usuario; las instrucciones nuevas prevalecen.** Recuperar versión actual de GitHub y, si el trabajo depende de archivos locales, pedir/supervisar la salida real de PowerShell.
2. Confirmar cuál es la última rama/commit y sincronizar local sin perder cambios. La memoria anterior o un commit no sustituyen inspección.
3. Ejecutar tests y compilación después del último acceso libre. Nunca declarar «solucionado y probado» por haber publicado código solamente.
4. Reiniciar backend de pruebas (script habitual) y recargar tablet via la **misma dirección HTTPS de Tailscale**. Probar `/api/terminales/reservas/csrf-token` y flujo con reserva legítima; si ocurre error, leer el código/estado real y depurar.
5. Confirmar `Tengo una reserva` desde **cualquier tablet sin vincular**, con selección, foto, PIN correcto, depósito y confirmación SQL. Revisar que el servicio normal, retirada personal, carga y admin no se rompan.
6. Reanudar pendientes funcionales: flujo completo de encomiendas remitente/destinatario, WhatsApp manual, origen de cobros Web/Tablet, reserva elegible, modo claro/oscuro y estilo unificado, feria del 5 dic.
7. No proponer nuevamente enlazar el dispositivo, configuraciones `TerminalId` individuales, QR/SMS, registrar biometría web para reservar, ni introducción de encomiendas presencial, salvo petición expresa nueva.

## 9. Comandos habituales — Windows / PowerShell

Detener primero el backend anterior con `Ctrl+C` en su terminal. En una **terminal nueva**:

```powershell
cd "<RUTA_LOCAL_DE_SYNCHRO>"
git status --short --branch
git pull --ff-only origin rebuild/tablet-desde-cero

# El script conserva clave HMAC original y DataProtection de pruebas.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Iniciar-BackendCargaSegura.ps1"
```

Para pruebas en **otra terminal** (mientras backend corre):

```powershell
cd "<RUTA_LOCAL_DE_SYNCHRO>"
dotnet build .\backend\Synchro.Api\Synchro.Api.csproj
npm.cmd --prefix frontend/tablet-app test
npm.cmd --prefix frontend/tablet-app run build
npm.cmd --prefix frontend/admin-app run typecheck
npm.cmd --prefix frontend/admin-app run build
```

Si el build backend reporta `MSB3021/MSB3027` con DLL/EXE bloqueada, probablemente sigue ejecutándose la API: detener proceso antes de compilar. No asumir error de código.

## 10. Forma de trabajo que el usuario exige

- **Español, directo, respuestas útiles y comandos PowerShell copiables**, sin preguntarle otra vez datos que ya se conocen.
- Quiere **cambios reales en archivos de la rama** que está usando, no solo explicación o instrucciones a Codex. Antes de cambiar, **leer código real** y comparar con pantallas existentes.
- Prefiere **soluciones integrales, limpias**, no parches CSS dispersos, regex frágiles, líneas duplicadas ni nuevos estilos que rompan coherencia.
- **No crear ramas adicionales** ni subir a `main` sin pedirlo. Cuidar trabajo local no publicado. Utilizar el repo para continuidad.
- Mantener menús simples, iconos existentes, botones grandes Regresar/Continuar y transiciones suaves sin capas; no colocar «simulación» innecesariamente en interfaz.
- Acepta que el backend use base SQL y pagos de prueba, pero **no quiere restricciones de dispositivo para entrar en las reservas del prototipo**. Respeta los controles de PIN y los flujos funcionales.
- **Separar lo verificado de lo propuesto**. Si se publican commits por GitHub pero no se ejecutaron tests ni se inició la versión nueva, decirlo.
- Para continuar en nuevo chat, este archivo contiene un **resumen técnico extenso, no una copia literal de todos los mensajes**. Las decisiones finales prevalecen sobre versiones anteriores.

## 11. Otras memorias documentales ya existentes

Existen en la Biblioteca de ChatGPT archivos de continuidad de versiones anteriores que pueden complementar antecedentes, pero **no sustituyen las decisiones más recientes**:

- `SYNCHRO_ESTADO_ACTUAL_2026-11-26.md` (detalles de SQL, WhatsApp/PIN, pruebas, tarifas y diseño admin).
- `SYNCHRO_MEMORIA_CONTINUIDAD_2026-11-20.md` (consolidación V3, estados de carga y ramas).
- `SYNCHRO_MEMORIA_MAESTRA_2026-11-14.md` (arquitectura, diseño inicial y decisiones previas).

---

### TEXTO PARA PEGAR AL INICIAR EL PRÓXIMO CHAT

> Continuemos el desarrollo de **SYNCHRO** desde el estado actual. Lee primero el archivo `SYNCHRO_MEMORIA_CONTINUIDAD_2026-11-26_ACTUALIZADA.md` que adjunto, luego revisa GitHub `Bjosue95/SYNCHRO` rama `rebuild/tablet-desde-cero` y el último código vigente. **Regla final: en este prototipo debo entrar en «Tengo una reserva» desde cualquier tablet, sin vinculación/autorización ni validación del ID del dispositivo.** El backend de pruebas usa `ReservasPrototipo__AccesoLibrePruebas=true` desde `tools/Iniciar-BackendCargaSegura.ps1`; se mantienen fotografía, PIN real de seis dígitos, comprobación de pago, consentimiento y confirmación SQL del depósito. Último commit funcional confirmado publicado antes de guardar esta memoria: `956707ca`. Todavía falta probar build/tests y el flujo integrado en mi Galaxy Tab S7 por Tailscale. No toques `main`, no reintroduzcas autorización de tablet, revisa código antes de editar y dame comandos PowerShell claros. Continúa exactamente desde mi siguiente indicación.