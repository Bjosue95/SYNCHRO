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
## Pantallas de carga y retiro de dispositivo (noviembre 2026)

El catalogo incluye `Carga` y `Retiro de dispositivos`, con 17
entradas nuevas que reutilizan los componentes operativos. Se pueden
revisar por separado el registro de rostro, los dos pasos de PIN,
la transicion fugaz `PIN confirmado`, el contador con `Agregar 1 minuto mas`,
la confirmacion de colocacion, la seleccion de pago, la espera del
administrador, el resumen del consumo y el retiro finalizado.

Para facilitar la comparacion, las pantallas que desaparecen solas en el
recorrido real permanecen visibles en la vista previa. El contador
utiliza el mismo reloj y estilos que `DepositoView.vue`; solo en
`/vista-previa/carga-colocar` se permite pulsar `Agregar 1 minuto mas`.
La seleccion de efectivo/tarjeta/NFC en la vista previa tambien es local.

El catalogo usa `src/preview/datos/carga.js` con espacios ficticios
W1/W2/C1-C4/L1-L2 y un importe ilustrativo de L 10; no consulta SQL ni
inicia sesiones, no solicita un rostro o PIN, y no genera cobros.
Los componentes de creacion de PIN, seleccion de espacios, confirmacion
de colocacion y retiro de carga incorporan guardias explicitas de vista
previa para no navegar a operaciones reales o llamar al backend.

Abrir con el servidor Vite en modo desarrollo normal:

```powershell
npm.cmd --prefix frontend/tablet-app run dev -- --host 0.0.0.0 --port 5173 --strictPort
```

Desde la computadora, navegar a `https://localhost:5173/vista-previa`.
El modo demo (puerto 4173) no ofrece este catalogo.
