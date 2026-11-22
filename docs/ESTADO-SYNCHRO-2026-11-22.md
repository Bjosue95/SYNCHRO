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

## 9. Resultado real de comparación facial (22/11/2026)
- El backend respondió a `GET /api/espacios-carga`: `W1` ocupado; `W2,C1-C4,L1,L2` disponibles.
- En un intento de retirada de dispositivo mediante `POST /api/terminales/carga/retiro/identificar-v3`, el registro técnico devolvió `SYNCHRO_DIAG_V3 | Dimension=1024 | Distancia=0.3242 | Umbral=0.2200 | Resultado=NO_COINCIDE`. La distancia superó el umbral en `0.1042`, provocando HTTP 400.
- Esto confirma una referencia facial válida en formato y una comparación calculada, pero **no demuestra** ni suplantación ni un fallo del algoritmo. Faltan pruebas de repetibilidad con las mismas condiciones de cámara/luz y calibración de umbral.
- Se verificó en el código que ambas vistas de registro y retiro de carga usan `Human.js 3.3.6`, 1024 componentes y la misma configuración de modelos. El backend usa distancia coseno `1 - coseno`; no confundirla con la métrica nativa de coincidencia de Human.js.
- **Siguiente acción segura:** obtener como máximo otras dos distancias agregadas de intentos consentidos del mismo rostro y misma sesión, sin registrar imágenes, descriptores ni PIN. Comparar variación entre intentos. No modificar umbral, tokens, autorizaciones ni SQL para forzar una aceptación.

## 10. Tres resultados de W1 y diagnóstico de estabilidad (22/11)
- Rechazos reales: distancia coseno `0.3242`, `0.4353`, `0.2673`; umbral `0.2200`. Variación máxima-mínima `0.1680`. Todos mostraron `Dimension=1024` y `NO_COINCIDE`.
- La variación contra la referencia almacenada **no permite** saber si el origen del error es el registro o las capturas de retirada.
- Se añadió al frontend de retiro (`RetiroCargaRostroView.vue`) un diagnóstico solo en `import.meta.env.DEV`: compara la distancia coseno entre las tres capturas de un mismo escaneo y muestra solo esos tres valores en pantalla. Los vectores son temporales en memoria, se limpian tras el cálculo y no se envían al backend. Se mantienen las validaciones y el umbral sin cambios.
- **Prueba siguiente:** actualizar la rama local desde GitHub, reiniciar el frontend en modo desarrollo, ir al flujo facial de retiro W1 y copiar solo los tres números que aparecen tras la captura, incluso antes de pulsar Continuar.
- Si las distancias internas varían mucho, examinar estabilidad de la captura, rotación, luz y pose; si son bajas pero la comparación con la referencia sigue alta, investigar calidad del enrolamiento original. No aprobar la retirada ni reescribir una referencia activa para superar el rechazo.

## 11. Arranque persistente de carga V3 y corrección de HTTP 503
- El backend reiniciado con `dotnet run` sin variables de carga devolvió `SYNCHRO HTTP 503: La clave segura de carga no esta configurada.` al confirmar PIN de una carga nueva; el frontend y la base SQL sí respondían.
- Se recuperó de la configuración anterior que la clave HMAC original existe como `SecureString` exportado con DPAPI en `%LOCALAPPDATA%\SYNCHRO\CargaSegura\clave-hmac.clixml` y las claves DataProtection se encuentran en `%LOCALAPPDATA%\SYNCHRO\CargaSegura\DataProtectionKeys`.
- Nuevo iniciador versionado `tools/Iniciar-BackendCargaSegura.ps1`: carga el SecureString con Import-Clixml, verifica Base64 de 32 bytes, activa `CargaDispositivos__RegistroHabilitado=true`, la conexión a `SynchroDB_Pruebas`, `ProteccionDatos__DirectorioClaves` y `CargaDispositivos__ClaveHmac`, e inicia la API por HTTPS 7278. No genera ni muestra ninguna clave. Elimina la variable de entorno HMAC de su proceso al finalizar.
- **Usar ese iniciador cada vez que se reinicie el backend de carga**, desde la raíz: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Iniciar-BackendCargaSegura.ps1`; no usar `dotnet run` solo para esas pruebas. Si falta la clave DPAPI, detenerse, nunca crear otra para una sesión existente.
- Estado: script publicado en rama de trabajo; su ejecución real en el equipo del usuario aún debe comprobarse.

## 12. Tramo de cobro posterior a PIN de carga V3 (pendiente de comprobar en laptop)
- El usuario ya verificó correctamente rostro y PIN de la nueva sesión de carga; la vista anterior terminaba en «PIN validado», sin cobrar ni liberar espacio.
- Ahora se añadieron endpoints protegidos `/api/terminales/carga/retiro/cobro/preparar`, `/confirmar-simulado`, `/confirmar-recogida`, mapeados en Program.cs, que reutilizan el token del PIN y consultas transaccionales de la sesión identificada.
- La tabla nueva `dbo.CobrosRetiroCargaSynchro` registra un cobro por sesión con tarifa consultada desde dbo.TarifasCarga. Solo Development, HTTPS/CSRF, base aislada autorizada. La finalización fija `Estado='Finalizada'` y `RetiroUtc` y libera el espacio solo con cobro simulado confirmado y declaración de retirada física; **debe verificarse que el CHECK local admita Finalizada**.
- Frontend: `RetiroCargaPinView.vue` -> `/retirar/dispositivo/pago` (`RetiroCargaPagoView.vue`) -> resumen y método de pago -> confirmación simulada -> confirmación de recogida -> finalizar. Tokens en memoria, sin URL ni persistencia.
- Migración: `backend/Synchro.Api/Database/Migrations/20261122_CobroRetiroCargaSynchro_Pruebas.sql`; preparador seguro `tools/Preparar-CobroRetiroCarga-Pruebas.ps1`, con confirmación explícita.
- **Falta:** usuario debe hacer pull, dotnet build, npm build, aplicar migración con inspección de tarifas y CHECK de Estado, y probar HTTP/SQL real. No afirmar que ya funciona sin esta evidencia. Referencia: `docs/RETIRO-DISPOSITIVOS-CARGA-V3.md`.

## 13. Fallo real de preparacion de cobros y correccion de tarifas (22/11)
- Usuario confirmo compilacion backend .NET 10 y Vite 150 modulos correctos. Diagnostico SQL: `SynchroDB_Pruebas` y marcador OK; sesiones y espacios OK; `dbo.CobrosRetiroCargaSynchro` PENDIENTE y `dbo.TarifasCarga` NO EXISTE.
- La primera migracion fallaba ANTES de aplicar nada por su dependencia de `dbo.TarifasCarga` heredada. Reparacion: migracion crea dos tablas nuevas del servicio de carga (`dbo.TarifasCargaSynchro`, vacia por defecto, y `dbo.CobrosRetiroCargaSynchro`); API consulta el catalogo V3. Herramienta de preparacion ya no exige tablas de tarifa antiguas.
- Nuevo script `tools/Configurar-TarifaCargaSynchro-Pruebas.ps1` solicita precios y bloques explicitamente para Celular/Laptop. No inventar tarifas comerciales.
- Reiniciar backend con DPAPI original y repetir facial+PIN solo si el token efimero vencio. No cancelar W1 de nuevo. Verificar que `Estado=Finalizada` cumpla las restricciones CHECK locales antes de confirmar recogida.


## Esquema confirmado por diagnostico SQL real (22/11/2026)
- El usuario informo que las tablas dbo.CobrosRetiroCargaSynchro y dbo.TarifasCargaSynchro existen. La primera tiene 10 columnas, la segunda tiene Modalidad, MinutosBase, PrecioBase, MinutosAdicionales y PrecioAdicional.
- TarifasCargaSynchro utiliza Modalidad, NO TipoDispositivo, y carece de ActualizadoUtc. Conservar la tabla original; no recrearla.
- CK_SesionesCargaSynchro_Estado admite Preparada, Activa, CobroPendiente, PagoConfirmado, Retirada y Cancelada. El cierre correcto es Retirada, NO Finalizada.
- La migracion anterior confirmo SQL y fallo despues al intentar consultar TipoDispositivo, por eso no hay que repetirla ni cancelar W1.
- API corregida para buscar tarifas usando s.Modalidad (Inalambrica/Cable/Laptop) y cerrar con Estado=Retirada.
- Scripts de diagnostico, preparacion y configuracion ya utilizan Modalidad. El configurador no intenta actualizar una columna de fecha inexistente.
- Verificar por lectura que hay una tarifa valida para la modalidad Inalambrica (W1), y que el CHECK permite Retirada.
- Falta comprobacion del nuevo build .NET y flujo real en laptop; publicar codigo en GitHub no demuestra que ya se ejecuto.
- Si Vite o el backend recarga y se pierde el token efimero, repetir solo rostro y PIN, sin volver a depositar el dispositivo.
