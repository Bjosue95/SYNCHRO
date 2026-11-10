# Interfaz tablet SYNCHRO

Implementación sobre `feat/issue-1-encomiendas-end-to-end`. No se modifica main ni
la aplicación estática original `frontend/tablet/`. Se conservan rutas/componentes
anteriores; el menú principal usa los contratos nuevos para las operaciones completadas.

## Referencia visual y arquitectura

No se encontró un manual de diseño independiente en el checkout. La referencia usada es
el sistema original de `frontend/tablet/styles.css`, los componentes Vue existentes y sus
assets. Se preservan Arial/Helvetica, logotipos oficiales, iconos claros/oscuros, fondos
suaves, esquinas redondeadas y separación por pasos.

| Uso | Color original |
|---|---|
| Azul principal | #01305c |
| Fondo oscuro | #061f36 |
| Turquesa | #00b9c7 |
| Azul claro | #5ec6e8 |
| Fondo claro | #f2f7fa |
| Texto secundario | #5b7282 |
| Borde | #dfeaf0 |

Se añade #007c89 para texto turquesa sobre fondo claro. Los botones principales tienen
mínimo 58px, los inputs 58px y controles de consentimiento visibles. Hay foco de teclado,
labels, mensajes con role=alert/status, movimiento reducido y rejillas adaptables a
1280/768/390px. El reloj común usa America/Tegucigalpa.

- `components/TabletLayout.vue`: reutiliza AppHeader, HelpDialog y useTema; marco común,
  progreso, avisos demo y pie de privacidad.
- `components/CapturaRostro.vue`: permiso explícito, getUserMedia sin audio, captura PNG
  512x384 (por debajo del límite de 1 MiB), apagado de pistas al capturar/salir e imágenes
  transitorias. Denegar permiso impide avanzar. La simulación no es biometría profesional.
- `views/`: menú de seis servicios; EncomiendaTerminalView para depósito/retiro;
  ServicioTemporalView compartida por Personal/carga/frío/calor; RetiroTemporalView.
- `services/tabletApi.js`: fetch con cookies, CSRF ligado al esquema correspondiente,
  encabezados de credenciales, cancelación y mensajes de error. Las vistas no envían
  identidades, aprobaciones de rostro, precios ni terminalId como autoridad al servidor.
- `services/demo/tabletDemo.js`: fixtures de navegación independientes, solo cargadas
  cuando DEV=true y MODE=demo. Sin llamadas externas y sin fallback desde errores reales.
- `styles/tablet.css`: variables y estilos comunes. Se mantienen componentes de marca
  e iconos existentes; no se incorpora otro framework UI.

## Rutas y comportamiento

| Ruta | Interfaz |
|---|---|
| /menu | Pertenencias, retirar, reserva existente, carga, frío y calor |
| /terminal/encomiendas/reserva | Consultar reserva → captura remitente → confirmar depósito |
| /terminal/encomiendas/retiro | Acceso destinatario → captura destinatario → PIN → retiro |
| /servicios/personal | Compartimento → tarifa/tiempo → prepago → inicio y comprobante |
| /servicios/carga | Celular/laptop y conector → 30/60/90 min → prepago → guardar cerrado |
| /servicios/frio y /servicios/calor | Disponibilidad, temperatura simulada, tarifa e inicio |
| /retirar | Elegir encomienda u objetos/dispositivo |
| /retirar/servicios | Retiro con compartimento, referencia, credencial y PIN |

La ruta anterior de visitantes redirige a reserva existente. No se añade ninguna creación
presencial de encomiendas ni se pide otra vez el teléfono del destinatario al depositar.
El consentimiento WhatsApp y las referencias de identidad se verifican en el backend.
El acceso de destinatario usa el contrato actual: referencia de verificación + código;
no inventa un alta presencial ni una API nueva de entrega de códigos.

Se muestran confirmaciones únicamente después de una respuesta exitosa. El depósito
informa notificación ENCOLADA, no entregada. La evidencia de rostro caduca y debe repetirse.
Las operaciones reales siguen dependiendo de SQL, configuración y sesión de terminal.
Los pagos, carga/temperatura/apertura física se identifican como simulados.

Los PIN/credenciales solo viven en el estado de la vista, se ocultan en inputs, se borran
al finalizar/salir y no se guardan en localStorage/sessionStorage, URL ni logs. En servicios
personales se muestran los datos de recuperación antes de finalizar; si el usuario sale
antes de conservarlos, la aplicación no los conserva por él. Las reservas abandonadas
quedan sujetas al vencimiento del backend; navegar atrás no cancela una operación activa.

## Demostración y límites de verificación

`npm run demo` sirve el recorrido con datos ficticios en http://localhost:4173/menu.
No acredita identidad ni demuestra SQL/WhatsApp/hardware. El comparador visual real de
prototipo permanece en el backend; la fixture de UI solo ensaya el envío de PNG y estados.
Una compilación normal no carga fixtures, aun si falla la API. El modo normal exige HTTPS
y confía en las decisiones del servidor. Instrucciones completas en frontend/tablet-app/README.md.

Validación ejecutada en Cloud:

- `npm run build`: aprobado.
- `npm test`: 2 pruebas aprobadas de fixtures (fallos, ocupación única, pago idempotente,
  PIN incorrecto y consumo único).
- `npm run test:e2e`: **16 aprobadas, 0 fallidas, 0 omitidas**. Navegación responsive,
  imágenes, tema/ayuda, visitantes retirados, depósito, retiro con PIN fallido/correcto,
  captura mediante API del navegador, rechazo de permiso, servicios temporales, laptop
  cerrada, cancelación, borrado de secretos y fallo cerrado en la compilación normal.
- La cámara de Playwright usa vídeo sintético. Se comprobaron recursos MediaStream y
  apagado, no una cámara física ni reconocimiento biométrico de personas.
- No se repitió la suite .NET porque esta etapa no cambia código del backend. Su última
  ejecución y bloqueos SQL constan en ISSUE-1-AVANCE.md; no se atribuye a estos tests de UI
  una integración SQL real. Los scripts de PowerShell tampoco se ejecutaron aquí.

Capturas reales del navegador, con datos ficticios:
[menú](capturas/tablet-menu.png), [oscuro](capturas/tablet-menu-oscuro.png),
[cámara](capturas/tablet-camara.png), [carga](capturas/tablet-carga.png).
