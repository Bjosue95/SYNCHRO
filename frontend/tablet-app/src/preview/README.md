# SYNCHRO - Vista previa de interfaces

Ruta local:
https://localhost:5173/vista-previa

## Estructura

- rutas.js: acceso exclusivo en desarrollo.
- pantallas.js: registro de las vistas reales.
- views/CatalogoView.vue: selector de pantallas.
- views/PantallaView.vue: visor de solo lectura.
- datos/: ejemplos ficticios sin credenciales.

## Seguridad

Las pantallas en vista previa no deben:

- Procesar pagos.
- Consumir PIN.
- Liberar casilleros.
- Reservar espacios.
- Activar la camara.
- Modificar SQL Server.
- Crear sesiones reales.

Los botones permanecen bloqueados en el visor, excepto la secuencia
explicita de demostración de pago (Efectivo, Tarjeta y NFC). Esta secuencia
solo modifica estado visual en memoria y no consulta endpoints, procesa
cobros, consume PIN ni libera casilleros. La selección se transmite como
un valor enumerado no sensible en la ruta de vista previa.
Cada componente con carga automatica debe tener
una proteccion explicita para vista previa.

## UI/UX

- No duplicar pantallas.
- Reutilizar los componentes originales.
- Mantener el encabezado original de SYNCHRO.
- Conservar colores, tipografia e iconos aprobados.
- Revisar siempre modo claro y oscuro.
- Comprobar pantallas horizontales y verticales.
- Evitar textos tecnicos visibles al usuario.
- No mezclar CSS, servicios HTTP y logica de negocio.
- Mantener la administracion en admin-app.

## Pruebas

Ejecutar npm test y npm run build.

Estas verificaciones no sustituyen una revision visual
ni una prueba funcional con el backend.