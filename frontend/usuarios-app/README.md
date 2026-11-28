# SYNCHRO · Portal web de usuarios

Aplicación independiente de **tablet-app** y **admin-app**. Ubicada en
`frontend/usuarios-app/`, hecha con Vue 3, Vue Router y Vite. Utiliza el mismo
backend .NET de SYNCHRO, pero su sesión pertenece exclusivamente a clientes web.

## Servicios conectados

- Registro de clientes `POST /api/web/clientes/acceso/registro`.
- Login `POST /api/web/clientes/acceso/login`.
- Sesión y salida `GET /api/web/clientes/mi-sesion`,
  `POST /api/web/clientes/logout`.
- Pre-reserva de encomiendas `POST /api/web/encomiendas/pre-reservas`.
- Consulta privada `GET /api/web/encomiendas/pre-reservas/{id}` enviando
  `X-SYNCHRO-RESERVA` en cabecera HTTP.
- Inicio de consentimiento WhatsApp
  `POST /api/web/encomiendas/pre-reservas/{id}/consentimiento/iniciar`.
  La aplicación muestra el texto/enlace que devuelve el backend, **no simula
  confirmaciones ni envía mensajes automáticamente**.

No se asigna un casillero al crear una pre-reserva y esta vence a las 24 horas.
Las reservas personales por tiempo/fecha/ubicación y las reservas térmicas
**aún no cuentan con endpoints web de reserva completos**. La carga de dispositivos
es presencial, no se ofrece por reserva web.

## Ejecutar en Windows

Requiere Node.js 22.18+ o 24.12+, el certificado local HTTPS de SYNCHRO y
el backend existente en https://localhost:7278.

Desde la raíz del repositorio:

```powershell
Set-Location "C:\Users\velas\OneDrive\Escritorio\SYNCHRO"
npm.cmd --prefix frontend/usuarios-app install
npm.cmd --prefix frontend/usuarios-app test
npm.cmd --prefix frontend/usuarios-app run build
npm.cmd --prefix frontend/usuarios-app run dev
```

Visitar **https://localhost:5175**. El servidor Vite dispone de proxy `/api`
hacia el backend HTTPS de pruebas. Usa su propio puerto, sin ocupar 5173 de la
tablet ni 5174 del administrador.

El certificado se busca en
`%LOCALAPPDATA%\SYNCHRO\certificados\localhost.pfx`.
Si el entorno utiliza otro archivo o contraseña, especificar
`SYNCHRO_CERT_PATH` y `SYNCHRO_CERT_PASSWORD`, como en admin-app.

## Dependencias del backend

El backend actual registra el módulo web **solo en Development** y con la
cadena SQL apuntando a **SynchroDB_Pruebas**. Deben existir las tablas
`ClientesWebPrototipo`, `Personas`, `PreReservasEncomiendasWeb` y las
tablas de consentimiento ya previstas por las migraciones del prototipo.
Este frontend no instala tablas ni crea migraciones SQL.

El endpoint de consentimiento requiere la clave
`WhatsApp:ConsentimientoHmacKey` en la configuración segura del backend.
Sin ella puede devolver HTTP 503; la pre-reserva sigue registrada.

## Seguridad

- Las operaciones usan HTTPS, cookies HttpOnly del backend y antifalsificación
  `X-CSRF-TOKEN` solicitada antes de cada POST.
- `X-SYNCHRO-RESERVA` se envía únicamente en una cabecera HTTPS.
  El token de gestión nunca se pone en una ruta, consola ni localStorage.
- La clave se presenta una vez tras crear la solicitud para que el usuario
  la conserve de forma privada. El backend no proporciona listado de
  pre-reservas o recuperación de credenciales: no se inventa ese servicio.
- Las consultas privadas se verifican del lado del servidor con la sesión
  del cliente propietario y la clave de gestión.
- La confirmación de consentimiento es exclusiva del sistema WhatsApp
  del backend; mostrar el enlace o texto no supone consentimiento real.

## Organización

```text
src/
  App.vue
  main.js
  router/index.js
  services/
    clientesApi.js
    sesion.js
    validaciones.js
  views/
    InicioView.vue
    AccesoView.vue
    PanelView.vue
    PreReservaView.vue
    ConsultarReservaView.vue
  styles/base.css
tests/
  validaciones.test.mjs
  contratos.test.mjs
```

Para futuras reservas personales, primero se necesita diseñar su contrato HTTP
seguro y su integración con disponibilidad, pagos y autorizaciones en el backend.
No conectar el portal directamente a SQL ni reutilizar credenciales del
administrador o de la tablet.

## Identidad visual e interfaz

El portal usa los **logos originales de SYNCHRO** del repositorio
`frontend/tablet-app/public/assets/`, adaptados a recursos locales
en `public/branding/` (nombre azul/blanco, isotipo claro/oscuro y marca completa).
Las cinco piezas conservan su contenido gráfico original.

La web usa ahora una **familia propia de 64 iconos SVG** en cada tema,
distribuidos en dos sprites (`public/iconos/claro/web-iconos.svg` y
`public/iconos/oscuro/web-iconos.svg`). `src/components/ui/AppIcon.vue`
reutiliza estos símbolos para servicios, seguridad facial, PIN, alarmas,
sensores, braille, energía solar, baterías, redes, correo, teléfono,
mapas y navegación. Se mantienen los SVG históricos individuales para
compatibilidad, pero los componentes nuevos usan la familia propia.

El modo oscuro y claro se controla mediante `useTheme.js` y tokens CSS
semánticos en `src/styles/tokens.css`. La preferencia visual se guarda
localmente como `synchro-usuarios-tema`; **no se guardan en el navegador
contraseñas, PIN, tokens de reserva ni sesiones**.

Hay dos disposiciones separadas:

- `PublicLayout.vue`: inicio y acceso de clientes.
- `AppShell.vue`: menú lateral y barra superior para cuentas autenticadas,
  con apertura adaptable para móviles, cierre con Esc y selector de tema.

La interfaz evita indicadores falsos de reservas o actividad: sólo ofrece
pre-reservas web y la consulta del estado protegido por credenciales. El
servicio de carga se mantiene presencial y las reservas web personales no se
presentan como operativas.

Las pruebas de iconografía, layouts y modo oscuro están en
`tests/diseno.test.mjs`, junto a las pruebas de validaciones y contratos.


## Panel informativo animado de acceso

`src/components/ui/AccessHighlights.vue` presenta tres mensajes de SYNCHRO
en la columna lateral de inicio de sesión: acceso protegido y solicitudes,
pre-reservas consultables desde la cuenta y consentimiento por WhatsApp del
destinatario. Reutiliza `AppIcon.vue` y las mismas variables de diseño.

- Cambio automático cada 6 segundos, desvanecido y barra de avance interactiva.
- Barras que permiten elegir directamente cualquiera de los mensajes.
- Sin botón visible de pausa: el carrusel se reproduce automáticamente.
  La selección manual detiene el avance para leer el mensaje, y al volver
  a pulsar la barra seleccionada se reanuda. También respeta foco, cursor
  y cambio de pestaña.
- Preferencia de movimiento reducido: inicia pausado y no aplica animaciones.
- La rotación solo modifica textos e iconos; no consulta al backend, no modifica
  la cuenta ni envía WhatsApp.
- En móvil y tablet estrecha se oculta la columna informativa para que el
  formulario conserve el espacio y la altura disponibles.

La cápsula de tema se fijó a **132 x 44 px en pantallas grandes** para evitar
saltos entre "Claro" y "Oscuro"; por debajo de 540 px se mantiene compacta
a 44 x 44 px en ambos modos.

Referencia de accesibilidad: W3C/WAI, Carousels Tutorial y ARIA Carousel
Pattern. La nueva comprobación está en `tests/carruselAcceso.test.mjs`.


## CSRF para operaciones con y sin sesión

El backend publica dos rutas diferentes de emisión del token antifalsificación:

- **Acceso público**: `GET /api/web/clientes/acceso/csrf-token`, únicamente
  antes de `registro` y `login`.
- **Cuenta autenticada**: `GET /api/web/clientes/csrf-token`, antes de
  `logout`, crear pre-reservas e iniciar consentimiento WhatsApp.

El token se envía por `X-CSRF-TOKEN` con la cookie de sesión HTTPS. **No se
desactiva la validación CSRF** y no se reutiliza un token anónimo al cerrar
una sesión ya autenticada. Se solicita uno nuevo por cada operación mutable.

Las pruebas `tests/csrfAutenticado.test.mjs` verifican que cada operación
utiliza la ruta correspondiente, y que un CSRF privado no disponible no
provoca un intento de cerrar sesión sin protección.


## Landing empresarial, cotizaciones y ubicaciones

Los datos oficiales se centralizan en src/data/empresa.js: correo, WhatsApp, Instagram, TikTok, horario y dirección de referencia CURC.

Componentes independientes en src/components/landing/:
- AboutSynchro.vue: origen, misión y visión tomados de los documentos de SYNCHRO.
- LocationsMap.vue: mapa interactivo con filtros y listado accesible de siete ubicaciones contempladas. No son estaciones instaladas. Plaza Ciudad Nueva está en Comayagua; el pin exacto queda pendiente de coordenadas verificadas.
- QuoteForm.vue: envía directamente la solicitud por EmailJS mediante una plantilla con destinatario fijo. Requiere configurar EmailJS; no abre aplicaciones de correo ni guarda los datos en el navegador.
- ContactSection.vue: WhatsApp oficial, correo, horario y dirección.
- SiteFooter.vue: logo, soluciones, navegación, Instagram, TikTok y contacto.

### Dependencia del mapa y rendimiento

Leaflet 1.9.4 es la única dependencia añadida al portal. Su JavaScript se importa dinámicamente al acercarse al mapa mediante IntersectionObserver; no afecta la carga inicial de la portada. Las teselas cartográficas requieren internet; se muestra atribución OpenStreetMap y no se descargan teselas por adelantado.

Para un despliegue de alto tráfico revisar la política de teselas y elegir un proveedor adecuado: https://operations.osmfoundation.org/policies/tiles/

### Pruebas

Después de actualizar GitHub, ejecutar:

    npm.cmd --prefix frontend/usuarios-app install
    npm.cmd --prefix frontend/usuarios-app test
    npm.cmd --prefix frontend/usuarios-app run build

tests/landingComercial.test.mjs verifica datos oficiales, Plaza Ciudad Nueva en Comayagua, mapa, cotizaciones, historia institucional y footer.

## Soluciones integrales y representación de la estación

`src/components/landing/SmartSolutions.vue` presenta la arquitectura de
seguridad integral (facial, PIN, accesos, alertas, sensores, integridad,
protección de datos, trazabilidad, identificación braille, monitoreo térmico),
continuidad operativa y soluciones energéticas (captación solar, respaldo,
eficiencia y gestión del consumo). Las capacidades en desarrollo se describen
como parte de la solución diseñada, no como funciones instaladas verificadas.

`src/components/ui/LockerJourney.vue` mantiene una representación editorial
de la estación con diez compartimentos principales, dos inferiores especiales,
distinción visual de M04 frío y M09 calor, braille bajo el código de
casillero, pantalla y carga inalámbrica/cableada. La animación abre un
casillero general M03 y no interviene en reservas ni operaciones reales.

`tests/iconografiaPropia.test.mjs` verifica los 64 iconos de cada modo y
los principales puntos de contacto que los utilizan.


## Envío directo de cotizaciones con EmailJS

1. En https://dashboard.emailjs.com/ conectar Gmail de SYNCHRO en Email Services. Probar el servicio.
2. Crear una plantilla en Email Templates. Configurar **To Email** fijo: `synchrohn@gmail.com`, **From Name**: SYNCHRO, correo remitente del servicio conectado y **Reply To**: `{{correo}}`. No usar destinatario dinámico, ni el correo del visitante como remitente.
3. Subject: `Cotización SYNCHRO - {{solucion}}`. Contenido de la plantilla (usar variables escapadas de doble llave, no HTML sin escapar):

```text
Nueva solicitud de cotización — SYNCHRO
Nombre: {{nombre}}
Correo: {{correo}}
Teléfono: {{telefono}}
Organización: {{organizacion}}
Solución: {{solucion}}
Ciudad o ubicación: {{ciudad}}

Proyecto:
{{descripcion}}
```

4. Copiar `.env.example` a `.env.local` en este directorio y rellenar Service ID, Template ID y Public Key (Account). No incluir contraseña de Gmail, clave privada ni token de acceso. Las variables VITE son públicas por diseño. El archivo .env.local está ignorado por Git.
5. Reiniciar Vite. En producción configurar las mismas variables antes de compilar y limitar los orígenes autorizados en EmailJS a los de SYNCHRO (en desarrollo incluir únicamente los orígenes locales necesarios). Para exposición pública considerar CAPTCHA con integración completa del widget y token antes de activarlo en la plantilla; los límites de origen y de frecuencia no reemplazan la protección antibots.
6. Probar una solicitud ficticia y verificar recepción en el buzón, incluso spam. Solo se muestra éxito si EmailJS responde HTTP 200; un fallo conserva los campos. La aceptación del proveedor no garantiza llegada a bandeja principal.

El envío transmite los campos a EmailJS y al proveedor de correo, no a SQL. El servicio usa la API REST oficial sin añadir dependencias. Configurar las políticas de privacidad y conservación del correo de acuerdo con el uso comercial.


## Política de cookies y contenido externo

El aviso global de usuarios aparece hasta que se guarda una decisión, con vigencia de 180 días. Solo esenciales y Aceptar todas son opciones equivalentes; Configurar permite elegir el mapa externo. La preferencia no identifica al visitante. La política está en /politica-de-cookies y el enlace para modificarla está en el pie público y el menú de cuenta. La sesión, CSRF, tema solicitado y EmailJS al enviar el formulario siguen funcionando independientemente de la elección. OpenStreetMap no se carga hasta permitirlo; al retirar el permiso se desmonta el mapa. No añadir analítica, publicidad o nuevos proveedores sin actualizar la política y los controles. Pruebas: tests/cookies.test.mjs.
