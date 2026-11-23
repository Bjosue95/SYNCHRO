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
