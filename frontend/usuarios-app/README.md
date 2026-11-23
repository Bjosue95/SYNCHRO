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

El catálogo `public/iconos/claro/` y `public/iconos/oscuro/` incluye
**20 iconos SVG por tema** (40 recursos), con los trazos y colores del sistema
de iconos de la tablet: 19 están tomados de sus archivos SVG originales;
`menu.svg` utiliza el mismo grosor de línea y paleta. La aplicación usa
`src/components/ui/AppIcon.vue` para elegir el recurso correcto según el tema.

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
- Control visible para pausar o reanudar, pausa temporal al situar el cursor
  sobre el carrusel, recibir foco o cambiar de pestaña.
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
