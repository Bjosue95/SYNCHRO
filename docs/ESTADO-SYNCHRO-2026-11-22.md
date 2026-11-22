# SYNCHRO — Punto de continuidad verificado
**Fecha:** 22 de noviembre de 2026 (Honduras)
**Repositorio:** `Bjosue95/SYNCHRO`
**Rama activa:** `rebuild/tablet-desde-cero`
**Commit de código examinado:** `ee5bc1dc2d83bace4d31652b03c94353fbe421cf`
**Propósito:** documentar el trabajo actual para continuar entre sesiones sin inventar funcionalidades ni perder decisiones de diseño.

## 1. Alcance del emprendimiento
SYNCHRO («El punto donde todo se conecta») combina almacenamiento temporal de pertenencias, recepción/retiro de encomiendas de clientes registrados, casilleros de temperatura controlada (frío y calor) y estaciones presenciales de carga de celulares/laptops. El prototipo de feria es combinado; los módulos reales podrían instalarse por servicio según ubicación (universidades, aeropuertos, parques, etc.). La demo no implica equipamiento físico ni pagos reales.

## 2. Línea de diseño aprobada
- Paleta: azul `#01305C`, turquesa `#00B9C7`, azul claro `#5EC6E8`, gris `#5B6770` y blanco `#FFFFFF`.
- Tablet prioritaria vertical (Galaxy Tab A11), adaptable a otras dimensiones y modos claro/oscuro.
- Menú de cuatro tarjetas en 2×2. Reutilizar las mismas proporciones, títulos centrados, tarjetas uniformes, selección visible con marca de verificación, controles táctiles amplios y pie consistente para «Continuar».
- Navegaciones con desvanecimiento suave y uniforme (sin capas superpuestas ni desplazamientos bruscos).
- Mantener logo, iconos oficiales y estilos preexistentes. Reutilizar componentes; no inventar otro administrador dentro de la tablet.
- Referencia principal: `docs/SYNCHRO_ARQUITECTURA_UI_UX.md`, además de código real en `frontend/tablet-app/src/`.

## 3. Arquitectura y entorno
- Tablet: Vue 3, Vue Router, Vite; `src/views`, `components`, `services`, `composables`, `styles` y `preview`.
- API: C# con ASP.NET Core .NET 10 bajo `backend/Synchro.Api`.
- Ensayos locales: HTTPS `https://localhost:7278`; SQL Server LocalDB con base `SynchroDB_Pruebas` y entorno aislado.
- Administración: backend administrativo existente, separado de tablet; no crear una administración nueva por error.
- No publicar `.env`, claves HMAC, registros con datos privados, respaldos SQL, fotos ni descriptores faciales.
- El backend compiló e inició correctamente el 22/11 según la salida de terminal compartida. Eso **no** acredita que todas las pruebas funcionales o la suite completa hayan pasado.

## 4. Funciones nuevas presentes en el commit ee5bc1d
**Carga presencial de dispositivos:**
- Rutas `/cargar`, `/cargar/modalidad`, `/cargar/espacios`, `/cargar/rostro`, `/cargar/crear-pin`, `/cargar/pin-confirmado`, `/cargar/colocar`, `/cargar/condiciones`.
- Catálogo de espacios de prueba: `W1/W2` inalámbrica; `C1-C4` cable; `L1/L2` laptop.
- Selección y consulta de espacios libres; captura/descriptor facial V3; PIN; preparación protegida; confirmación de colocación para iniciar sesión.
- Endpoints nuevos: `CargaSesionSeguraEndpoints.cs`, `RetiroCargaAutoIdentificacionEndpoints.cs`, `RetiroCargaIdentidadEndpoints.cs`.
- Servicios JS nuevos para preparar carga, retirar, estado temporal del rostro y catálogo.
- Las operaciones de carga segura están condicionadas a configuración de **Development** y a la base de pruebas; no asumir que funcionan en producción.

**Retiro de carga:**
- La entrada `/retirar/dispositivo` redirige automáticamente a `/retirar/dispositivo/rostro` e inicializa la sesión de retiro.
- La identificación facial consulta candidatas activas y compara descriptores V3 mediante distancia coseno, sin revelar la ubicación antes de verificar el PIN.
- La pantalla `/retirar/dispositivo/pin` valida el PIN, pero indica expresamente que **el cobro y la recogida siguen pendientes**. No marcar el retiro de carga como completo.

**PIN de carga:** el código **actual** exige **6 dígitos** en frontend y backend. Existió una propuesta anterior de 4 dígitos; esta discrepancia funcional debe revisarse antes de fijar especificación, sin cambiarla inadvertidamente.

## 5. Diagnóstico facial V3 (problema actual)
- Archivo: `backend/Synchro.Api/Endpoints/RetiroCargaAutoIdentificacionEndpoints.cs`.
- Ruta: `POST /api/terminales/carga/retiro/identificar-v3`.
- Modelo: `SYNCHRO-HUMANJS-EMBEDDING-1024-v3`; consentimiento `SYNCHRO-FACIAL-V3-2026-11`.
- Umbral por defecto configurado: distancia coseno `0.22`. El diagnóstico agregado escribe `SYNCHRO_DIAG_V3` con dimensión, distancia, umbral y resultado. No registra vectores, PIN ni claves.
- Según el diagnóstico previo compartido por terminal, hay una sesión activa elegible y el rechazo podría estar en la comparación, **sin causa definitiva confirmada**.
- Próximo paso: ejecutar un intento real de verificación, consultar solo dimensión/distancia/resultado del log y determinar la causa **antes** de modificar umbrales, algoritmos o datos SQL.
- No limpiar ni alterar las sesiones de prueba para ocultar el fallo; conservar la clave HMAC original.

## 6. Hallazgos de la revisión estática que requieren atención
1. En `MenuView.vue` la tarjeta «Tengo una reserva» no define `ruta`; `ServiceCard.vue` solo navega con ruta no vacía. Por ello, en este commit dicha tarjeta no abre una vista.
2. La vista `RetiroCargaRostroView.vue` usa `espacioCarga` desde `route.query.espacio` y tiene fallback «Sin seleccionar», aunque el retiro automático no pide espacio al usuario; revisar el texto de encabezado.
3. `RetirarDispositivoView.vue` tiene un flujo de elección de espacio, pero `router.beforeEach` redirige `/retirar/dispositivo` directamente al rostro; revisar si es pantalla heredada no usada.
4. La opción «Una encomienda» se muestra deshabilitada en `RetirarView.vue`. Verificar alcance del prototipo antes de activarla.
5. Algunas vistas de carga/retiro facial contienen más de 1,000 líneas. Revisar duplicaciones **después** de estabilizar el flujo, para evitar romper el diseño aprobado.
6. El commit añadió `catalogoEspaciosCarga.test.mjs`, pero no se ha demostrado por los mensajes actuales una suite completa exitosa ni una prueba integral real de carga/retiro/pago.

## 7. Prioridades acordes al estado del prototipo
1. Diagnosticar y corregir rechazo de reconocimiento facial V3, sin reducir el umbral a ciegas.
2. Validar recorrido completo de carga desde tablet hasta SQL con sesión activa.
3. Completar/probar retiro seguro, cobro posterior simulado y finalización real de sesión **sin liberar antes del pago confirmado**.
4. Revisar navegación faltante, encabezados, tamaños y consistencia visual en tablet vertical.
5. Ejecutar pruebas específicas, compilación y verificación integral; registrar resultados reales.
6. Mantener documentación de continuidad alineada al código. **No añadir nuevas funciones fuera del alcance de cierre.**

## 8. Normas operativas
- Trabajar sobre `rebuild/tablet-desde-cero`; no modificar ni fusionar `main` por accidente.
- Revisar archivos existentes antes de proponer parches; probar los cambios en la terminal de VS Code.
- Respaldar antes de tocar lógica sensible; no usar comandos destructivos de SQL/Git.
- Los archivos excluidos al publicar ee5bc1d quedaron locales: `demos/`, respaldo SQL de cobro, archivos `.log`, y copia `.bak` de servicio JS. No se presume que estén en GitHub.
- Este documento es una **fotografía de código y conversaciones**, no un certificado de aprobación técnica o prueba biométrica.
