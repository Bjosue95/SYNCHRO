# SYNCHRO — Arquitectura y estándares UI/UX

Este documento establece los criterios obligatorios para
continuar el desarrollo de SYNCHRO.

## 1. Arquitectura de archivos

- Mantener separados frontend, backend y administración.
- `src/views/`: pantallas y coordinación de sus estados.
- `src/components/`: componentes visuales reutilizables.
- `src/services/`: comunicación HTTP y lógica de servicios.
- `src/composables/`: estado y comportamiento compartido.
- `src/styles/`: estilos globales y transiciones.
- `src/preview/`: rutas y datos ficticios de diseño.
- Evitar archivos gigantes y responsabilidades mezcladas.
- No duplicar pantallas ni crear otro administrador
  dentro de la aplicación de la tablet.
- Reutilizar componentes cuando tengan la misma función.
- No modificar backend o SQL para resolver problemas de CSS.

## 2. Identidad visual

Colores principales:

- Azul SYNCHRO: #01305C
- Turquesa: #00B9C7
- Azul claro: #5EC6E8
- Gris: #5B6770
- Blanco: #FFFFFF

Modo claro:
- Fondo: #F2F7FA
- Texto principal: #01305C
- Tarjetas: #FFFFFF

Modo oscuro:
- Fondo: #061F36
- Texto principal: #FFFFFF
- Tarjetas: #103956
- Superficies secundarias: #123D5B
- Bordes: #42647E / #5EC6E8

## 3. Diseño de interfaces

- Respetar las pantallas ya aprobadas.
- Mantener encabezado, logotipo e iconos originales.
- Usar controles táctiles grandes y proporcionados.
- Diseñar para tablet horizontal y vertical.
- Mantener jerarquía visual clara y buen contraste.
- Evitar textos innecesarios y explicaciones técnicas
  en las pantallas que ve el cliente.
- Mostrar únicamente información útil y acciones claras.
- Emplear transiciones de desvanecido uniforme,
  sin desplazamientos ni capas superpuestas.
- Considerar accesibilidad, enfoque visible
  y preferencias de movimiento reducido.

## 4. Vista previa de diseño

- Disponible exclusivamente en desarrollo local.
- Mostrar la pantalla real, no una copia de su diseño.
- Utilizar datos ficticios sin credenciales.
- No realizar llamadas de pago ni operaciones SQL.
- No liberar casilleros, consumir PIN ni confirmar retiros.
- Cada nueva vista previa debe revisarse para impedir
  efectos secundarios al abrir, pulsar o salir.
- Aprobar primero el diseño y después integrar
  o validar las funciones del backend.

## 5. Control de calidad

Antes de aceptar cualquier modificación:

1. Revisar la estructura existente.
2. Definir exactamente qué archivos cambiar.
3. Comprobar que no se dupliquen responsabilidades.
4. Mantener respaldos y posibilidad de restauración.
5. Ejecutar las pruebas relacionadas.
6. Compilar sin errores.
7. Revisar modo claro, oscuro y diseño adaptable.
8. Comprobar que no se rompan funciones anteriores.

La compilación correcta no sustituye las pruebas
funcionales ni la revisión visual.

## 6. Pagos

- Los métodos previstos son efectivo, tarjeta y NFC.
- En el prototipo son simulados, sin hardware real.
- No introducir cobros reales sin integración autorizada.
- El backend determina el importe.
- Conservar auditoría, autenticación y protección del retiro.
- Nunca liberar el casillero sin confirmación válida.