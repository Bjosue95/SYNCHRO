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

Los controles de las pantallas operativas permanecen bloqueados por
defecto. Solamente se habilitan controles marcados explícitamente
`data-preview-permitido` en el visor de desarrollo.

Los recorridos interactivos de Carga y Retiro de dispositivos permiten
elegir el dispositivo y el espacio, simular el escaneo sin cámara, introducir
PIN ficticios, observar la transición de confirmación, ampliar el contador,
seleccionar efectivo/tarjeta/NFC y observar una confirmación administrativa
ficticia. La colocación y el retiro finales cambian solamente el estado
visual en memoria: **no consultan endpoints, consumen PIN reales,
crean sesiones, procesan cobros ni liberan casilleros**.

Durante el recorrido, PIN confirmado dura 2 segundos, el escaneo visual
dura 1.8 segundos y la animación de pago pasa a resumen después de
4.8 segundos; la pantalla de PIN confirmado abierta desde el catálogo
permanece fija para inspección. Cada vista con efectos automáticos tiene
guardias de desarrollo para evitar ejecutar operaciones reales.

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

### Cómo revisar el recorrido animado

1. Abre `https://localhost:5173/vista-previa` (Vite en desarrollo).
2. Pulsa **Probar carga de dispositivo** y selecciona dispositivo, modalidad y
   espacio de muestra. Marca el consentimiento de demostración y pulsa
   **Comenzar escaneo**. El óvalo se anima sin encender la cámara.
3. Introduce cualquier PIN de seis dígitos y repítelo. Observa la
   confirmación durante 2 segundos, el contador de colocación,
   **Agregar 1 minuto más** y **Ya coloqué mi dispositivo**.
4. Desde el catálogo pulsa **Probar retiro de dispositivo**. Repite el
   escaneo visual, introduce seis dígitos ficticios, elige un método de pago
   y observa la secuencia hasta **Finalizar retiro**.
5. Cada pantalla sigue disponible por separado en el catálogo para revisar
   diseños, incluso los estados breves.

Después de cualquier cambio, ejecuta `npm.cmd --prefix frontend/tablet-app test`
y `npm.cmd --prefix frontend/tablet-app run build`.
