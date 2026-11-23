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


## 14. Uniformidad visual: retiro de dispositivo = retiro personal tras PIN (22/11/2026)
- Solicitud de usuario: despues del PIN de retiro de dispositivo se deben utilizar exactamente los mismos diseños y secuencia del retiro de pertenencias.
- Se modifico RetiroCargaPinView.vue: despues de validar el PIN, el frontend prepara la cotizacion de carga y navega DIRECTAMENTE al selector de pagos. Si falla la cotizacion se permite reintentar sin volver a introducir el PIN mientras el token siga vigente.
- SeleccionMetodoPagoView.vue ahora se comparte entre ambos servicios y distingue el contexto por la ruta: /retirar/pago (personal) y /retirar/dispositivo/pago (carga). Es la MISMA cuadrícula 2x2, tarjetas ServiceCard, iconos, boton Continuar, dark/light mode, geometria y CSS.
- RetiroCargaPagoView.vue utiliza el MISMO CSS scoped de RetiroPagoView.vue, con resumen-alto-v5, animaciones de efectivo/tarjeta/NFC y tarjeta final. La secuencia es: facial -> PIN -> seleccion del metodo -> animacion y confirmacion simulada -> resumen del consumo -> comprobacion de recogida -> pantalla Retiro completado.
- Ruta /retirar/dispositivo/pago/resumen añadida para la pantalla de resumen, /retirar/dispositivo/pago ahora es selector. Tarjetas y CSS de retiro personal conservados.
- La unica diferencia funcional es que el pago de carga se confirma como SIMULADO en backend Development, sin dinero real, y la recogida fisica se marca explicitamente antes de liberar W1. En retiro personal la confirmacion corresponde al operador administrativo.
- Token PIN, orden y seleccion de metodo para carga siguen solo en memoria en retiroCargaSesion.js; nunca URL/localStorage. No se alteran tablas SQL ni el backend de carga en este cambio visual.
- Se verificaron estaticamente importaciones, rutas, CSS identico, aislamiento de servicios y que la pantalla final depende de confirmarRecogidaRetiroCarga; falta npm build y prueba real en la tablet.


## 15. Administracion de cobros y espacios de carga (22/11/2026)
- Pedido del usuario: que cobros de carga se verifiquen por administrador igual que retiro de pertenencias y que la vista Casilleros muestre tambien espacios de carga.
- Backend agregado `AdminCobrosCargaEndpoints.cs`: GET `/api/admin/carga/cobros/pendientes` lista pagos pendientes con metodo ya seleccionado en tablet; POST `/api/admin/carga/cobros/confirmar` exige sesion administrativa con OperarServicios (Administrador/Operador), HTTPS, CSRF y pagoComprobado=true. Confirma atomica y exclusivamente `Estado=ConfirmadoSimulado` si sesion Activa, cobro Pendiente, espacio Ocupado y metodo coincide. No hay cobro bancario ni cambio de sesion al aprobar; registra operador en log de aplicacion.
- Backend `RetiroCargaCobroEndpoints.cs`: la ruta antigua de confirmacion directa desde tablet fue eliminada; nueva `/seleccionar-metodo` guarda la preferencia sin confirmar y `/estado` permite a tablet consultar si admin ya aprobo. `/confirmar-recogida` sigue requiriendo ConfirmadoSimulado y confirmacion explicita de retiro fisico.
- Tablet `RetiroCargaPagoView.vue`: reutiliza animaciones y resumen del retiro personal, ahora aguarda aprobacion de admin. Consulta cada 10 segundos (politica 12 solicitudes/min) y boton Comprobar pago. Token de rostro+PIN caduco requiere repetir solo verificacion; cobro preparado y metodo seleccionado persisten SQL. Si ya aprobado, el PIN revalidado permite continuar al resumen.
- Admin `CobrosView.vue`: conserva resguardo personal e incorpora bloque separado Carga de dispositivos con W/C/L, importe, tiempo, dispositivo, modalidad y metodo elegido, checkbox de comprobacion y confirmacion por operador.
- Backend `AdminEspaciosCargaEndpoints.cs`: GET `/api/admin/carga/espacios` protegido por OperarServicios, solo lectura en base SQL aislada, obtiene codigos W1/W2/C1-C4/L1/L2, estado y sesiones activas reales.
- Admin `CasillerosView.vue`: MISMA ruta y menu `/admin/casilleros`, dos bloques: casilleros resguardo existentes + espacios de carga con el mismo formato/filtrados/estado. No se crea menu adicional ni tablas nuevas.
- Se mapearon rutas en Program.cs. Revision estatica de codigos, tokens/CSRF, grupos roles, aislamiento SQL, y referencias completada. No hay ejecucion dotnet/npm en laptop validada tras estos commits. PENDIENTE: `dotnet build`, `npm admin build`, `npm tablet build` y prueba integral por admin; W1 previamente retirado no aparece en cobros pendientes: se requiere nueva sesion de prueba (sin reusar ni alterar las antiguas).
- Reiniciar backend con `tools/Iniciar-BackendCargaSegura.ps1` que importa la clave HMAC original y DPAPI. Admin local Vite puerto 5174, tablet Vite 5173.


## 16. Simplificacion de pantallas de retiro de carga (22/11/2026)
- Revisados `frontend/tablet-app/src/views/RetiroCargaPinView.vue`, `RetiroCargaPagoView.vue`, `RetiroPinView.vue`, `RetiroPagoView.vue`, `SeleccionMetodoPagoView.vue`, router y servicios de carga.
- Eliminada la pantalla intermedia `PIN validado`: la vista del PIN mantiene su misma interfaz mientras valida, prepara la orden y navega al mismo selector de pago usado por pertenencias. En caso de fallo de preparacion, reintenta con autorizacion temporal sin volver a consumir el PIN.
- Retirado el boton redundante `Comprobar pago` en la pantalla de espera de carga; sigue la consulta automatica del estado de aprobacion por administrador cada 10 segundos.
- Retirada la casilla de verificacion de recogida. Unicamente el clic explicito en `Finalizar retiro`, despues de mostrar resumen y pedir retirar el dispositivo, manda al backend la confirmacion de recogida; no se libera W1/W2/C/L antes de pago aprobado y esta accion.
- Se conservaron las animaciones, la cuadricula de seleccion de metodo, el resumen y la pantalla final con estilos identicos al retiro personal. Se conservaron las rutas y las exigencias del backend.
- Revision estatica de 13 invariantes OK. Pendiente de validar `npm.cmd --prefix frontend/tablet-app run build` en el equipo y prueba visual en Tab A11. Ninguna migracion SQL ni backend requerida.


## 17. Resultados locales de diagnostico y correccion de fabrica HTTP (22/11/2026)
- GitHub pull de `IntegracionCargaAdminHttpTests.cs` y `Diagnosticar-IntegracionCargaSynchro-Pruebas.ps1` correcto.
- Diagnostico SQL solo lectura confirmado: base `SynchroDB_Pruebas` y fixture correctos; tarifa Inalambrica 60 minutos/L 10 y bloques 30 minutos/L 5; todos los espacios W1/W2/C1-C4/L1/L2 Disponibles, sin sesiones activas; ultima sesion de W1 Retirada y cobro Aplicado L 10. Cuatro cobros Aplicado total L 40, sin pendientes administrativos.
- Las 4 pruebas HTTP fallaron DURANTE ARRANQUE del servidor de test, no por respuesta del endpoint: `WhatsAppManualEndpoints.MapWhatsAppManualEndpoints` detecto una conexion `SYNCHRO_Suite_Test` sin autorizacion `SYNCHRO_SQL_DEPOSITO_TEST` para el modulo WhatsApp. No se deben alterar controles de autorizacion ni cambiar conexion a base operativa.
- Solucion: en `tests/Synchro.Api.Tests/IntegracionCargaAdminHttpTests.cs` `Fabrica()` crea el host a partir de `FabricaTerminalCookieReal` y pone `WhatsAppManual:Habilitado=false` SOLO en el test HTTP de rutas de carga. Esta prueba no necesita WhatsApp y verifica permisos ANTES de cualquier lectura/escritura SQL; evita tocar otras fabricas.
- Pendiente nueva ejecucion en laptop: `dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj --filter "FullyQualifiedName~IntegracionCargaAdminHttpTests"`. No hay cambios de SQL/backend productivo ni frontend por este ajuste.


## 18. Paridad de contador y limpieza de mensajes en tablet de carga
- El usuario confirmo retiro completo W2: Disponible, 0 sesiones activas, ultima sesion Retirada, cobro Aplicado L 10; total seis cobros Aplicado L 60, sin pendientes.
- Se compararon directamente `DepositoView.vue` (guardar pertenencias) y `ColocarDispositivoCargaView.vue`. Los estilos scoped eran identicos, pero el SVG de carga no definia `synchro-timer-gradient-v3`, carecia de halo, anillo punteado, escala, animacion +1:00 y clave de animacion numerica.
- Carga ahora incorpora el mismo contador grafico y respuesta al boton Agregar 1 minuto mas; se mantienen `confirmarColocacionCarga` y los controles de disponibilidad/SQL originales.
- Se limpiaron textos visibles para el usuario de ensayo, backend de pruebas y version de descriptor en `VerificacionFacialCargaView.vue`, `RetiroCargaRostroView.vue`, `ConfirmacionPinCargaView.vue`, `RetiroCargaPagoView.vue` y vista heredada `CondicionesCargaView.vue`. Las casillas de consentimiento permanecen, con redaccion accesible. En pago la autorizacion administrativa sigue siendo confirmacion simulada de prototipo; no existe pasarela real.
- Revision estatica posterior: CSS del contador de deposito y carga identico; elementos visuales presentes; operaciones de preparar/confirmar recogida/pagos permanecen. No se han ejecutado builds ni pruebas visuales en la laptop despues de estos commits.
- Importante: el token de colocacion de carga sigue solo en memoria. Al refrescar la tablet no se recupera la preparacion ni el contador, por seguridad; no se ha implementado recuperacion de credenciales en esta correccion. La autorizacion de preparar carga vence en 10 minutos en el servidor.


## 19. Catalogo visual de tablet ampliado con carga y retiro de dispositivos
- Se incorporaron 17 entradas nuevas al visor local `/vista-previa`: dispositivos, modalidad, espacios inalambricos/cable/laptop, rostro de carga, crear/confirmar PIN, transicion PIN confirmado, colocacion y su confirmacion, rostro/PIN de retiro, seleccionar pago, espera del administrador, resumen y retiro completado.
- Se reutilizan los mismos componentes Vue del flujo operativo. Las pantallas fugaces se fijan visualmente para poder revisarlas sin que desaparezcan a los 2 segundos. El contador se deja funcionar en la vista previa y permite agregar un minuto.
- La vista previa usa datos ficticios en `frontend/tablet-app/src/preview/datos/carga.js` y guardias de `import.meta.env.DEV`; no consulta SQL, no inicia/termina sesiones, no activa camara ni confirma cobros.
- Se amplio `preview/rutas.js`, `pantallas.js`, `views/PantallaView.vue`, `views/CatalogoView.vue` y modo preview de seis componentes de carga/retiro; se conserva la ruta operativa original.
- Hay pruebas estaticas nuevas en `tests/previewCarga.test.mjs` y se adapto `tests/pagoPreview.test.mjs`. Pendiente ejecutar `npm.cmd --prefix frontend/tablet-app test` y `npm.cmd --prefix frontend/tablet-app run build` en la laptop; no es posible comprobar visualmente `https://localhost:5173` desde este entorno.


## 20. Recorridos animados de vista previa sin backend
- Peticion: la vista previa debe reproducir acciones, no solo mostrar pantallas fijas.
- Nuevas acciones del catalogo: **Probar carga de dispositivo** y **Probar retiro de dispositivo**.
- Carga: seleccionar dispositivo/modalidad/espacio ficticio; simular escaneo facial con linea animada y marca de confirmacion sin usar camara; crear/confirmar PIN en teclado local; transicion PIN confirmado de 2 s; contador visual real con +1 minuto; confirmar colocacion local y retorno al catalogo. En preview las combinaciones de W1/W2, C1-C4 y L1-L2 provienen solo de `preview/datos/carga.js`.
- Retiro: verificacion facial visual sin camara; PIN ficticio; seleccionar Efectivo/Tarjeta/NFC; animacion de pago 4.8 s; resumen y boton de finalizar que lleva a retiro completado solo visualmente.
- Seguridad: controles permitidos marcados con `data-preview-permitido`, resto bloqueado en `preview/views/PantallaView.vue`. Toda accion animada con guardia `esVistaPrevia` / desarrollo antes de fetch, confirmar carga, validar rostro/PIN, preparar cobro o confirmar retiro. Los componentes reales y SQL no cambian de estado en preview.
- Las vistas sueltas del catalogo se conservan para inspeccion; los estados fugaces se mantienen abiertos al acceder por separado.
- Se creo `tests/previewAnimaciones.test.mjs` y se actualizo el README del preview.
- Se verificaron estaticamente 18 invariantes de rutas, guardias y animaciones mediante lectura de archivos del repositorio: 18/18. Pendiente ejecutar `npm.cmd --prefix frontend/tablet-app test` y `npm.cmd --prefix frontend/tablet-app run build` en la maquina del usuario. No se tiene acceso a `localhost` para abrir la pantalla directamente desde aqui.


## 21. Frontend separado para clientes y pre-reservas web
- Usuario solicito comenzar sistema de reservas web en carpeta propia, independiente de tablet y administrador, conectado al backend existente.
- Se creo `frontend/usuarios-app/` con Vue 3, Vue Router, Vite, certificado HTTPS compartido y **puerto 5175**. El backend sigue 7278; tablet 5173; admin 5174. Sin cambios operativos a tablet, admin ni SQL.
- Archivos: `src/main.js`, `src/App.vue`, `src/router/index.js`, `src/styles/base.css`, `src/services/clientesApi.js`, `src/services/sesion.js`, `src/services/validaciones.js`, `src/views/{Inicio,Acceso,Panel,PreReserva,ConsultarReserva}View.vue`, pruebas `tests/{validaciones,contratos}.test.mjs`, `vite.config.js`, `package.json`, `index.html` y `README.md`.
- Autenticacion: endpoints reales `/api/web/clientes/acceso/{csrf-token,registro,login}` y `/api/web/clientes/{mi-sesion,logout}`; cookies HttpOnly del backend y token `X-CSRF-TOKEN` antes de cada POST.
- Reservas: único contrato actual completo del frontend es la **pre-reserva web de encomiendas**, con `telefonoDestinatario`, tamaños exactos SQL `Pequeno/Mediano/Grande`, retorno referencia/token de 64 hex, consulta privada por encabezado `X-SYNCHRO-RESERVA`, y preparación opcional del consentimiento WhatsApp a través de endpoint existente. El mensaje/enlace solo se comparte con destinatario, NO equivale a consentimiento ni depósito.
- Limitaciones reales: pre-reserva expira a las 24h, no asigna casillero, no habilita deposito; backend no ofrece todavía reservas personales web por fecha, ubicacion, tamaño/tiempo, ni listado web/recuperacion de claves. Carga sigue presencial. No inventar flujos para esas capacidades.
- El backend de clientes y pre-reservas está condicionado por `Program.cs` a Development y cadena SQL `SynchroDB_Pruebas`. WhatsApp consentimiento exige `WhatsApp:ConsentimientoHmacKey`.
- Comprobado por lectura de código y contratatos backend: seis verificaciones de rutas y protecciones pasan, todas las carpetas/archivos nuevos existen. NO se pudieron ejecutar `npm test` ni `npm run build` de la app en el equipo local; pendiente al hacer `git pull`.
- Inicio local: `npm.cmd --prefix frontend/usuarios-app install`, `npm.cmd --prefix frontend/usuarios-app test`, `npm.cmd --prefix frontend/usuarios-app run build`, `npm.cmd --prefix frontend/usuarios-app run dev`; abrir `https://localhost:5175`.


## 22. Rediseño portal usuarios con identidad tablet y temas claro/oscuro
- Usuario pidio que interfaz de clientes deje de parecer genérica, sea seria/profesional, conserve identidad SYNCHRO real de Github y que los iconos tengan exactamente el estilo de la tablet en ambos temas.
- Se tomaron cinco assets originales de `frontend/tablet-app/public/assets/` (marca completa, nombre azul/blanco, isotipo claro/oscuro) y se integraron localmente en `frontend/usuarios-app/public/branding/` mediante SVG con imagen PNG original embebida.
- `frontend/usuarios-app/public/iconos/{claro,oscuro}/`: 20 iconos por tema, 40 total. 19 proceden directamente de SVG de tablet (`frontend/tablet-app/public/iconos/` y `complementarios/`); `menu.svg` mantiene el mismo viewBox 128x128, grosor y paleta azul/turquesa. Los iconos están integrados con `components/ui/AppIcon.vue`.
- Arquitectura frontend: `src/composables/useTheme.js`, `src/components/ui/{AppIcon,BrandLogo,ThemeToggle}.vue`, `src/components/layout/{PublicLayout,AppShell}.vue`, `src/styles/tokens.css` y rediseño total de `src/styles/base.css`. Tema persiste solo preferencias visuales locales; credenciales siguen por cookie HTTPS.
- Vistas rediseñadas con iconografía original: `InicioView.vue`, `AccesoView.vue`, `PanelView.vue` y ajustes en `PreReservaView.vue` y `ConsultarReservaView.vue`. El panel NO muestra métricas, historiales ni reservas ficticias, solo las dos acciones realmente conectadas. Se eliminaron menciones técnicas de prototipo, backend, demo y 'visión general' de textos visibles.
- El panel tiene sidebar, topbar, responsive y modo claro/oscuro, con selector de tema y navegación independiente de la tablet y admin; backend y SQL sin cambios.
- `src/services/formatos.js` presenta 'Pendiente de consentimiento' y fechas en Honduras; datos de reserva no se alteran.
- `tests/diseno.test.mjs` verifica logotipos, iconos ambos temas, tokens, layouts y ausencia de estadísticas ficticias; las otras pruebas de contrato siguen.
- Revisión estática de 17 archivos efectuada: Vue SFC bien cerrados, todos los nombres de AppIcon conocidos, modo oscuro definido, sin menciones de simulación o backend en cinco vistas principales. Falta ejecutar `npm.cmd --prefix frontend/usuarios-app test` y `npm.cmd --prefix frontend/usuarios-app run build` en la laptop; no se obtuvo acceso de red en el contenedor para clonar GitHub y ejecutarlos aquí.


## 23. Acceso responsive y fundido global de tema como en tablet
- Se eliminaron las pestañas `Inicio` y `Servicios` del encabezado público, y en acceso se muestra solo el logo original y el selector de tema (sin footer).
- La vista `AccesoView.vue` usa títulos cortos "Inicia sesión" y "Crea tu cuenta", sin microtítulo de cuenta ni pestañas superiores. Se conserva el formulario real, su validación, el login, el registro y el enlace entre ambos modos.
- `src/styles/access.css` implementa ajuste a `100dvh`, centrado en escritorio y una columna en móvil. Cuando el alto es insuficiente (teclado/zoom), se habilita desplazamiento para mantener controles accesibles.
- Nuevos iconos de luna y sol con estilo de tablet, en ambos temas. `ThemeToggle.vue` ofrece botón compacto con nombre de tema y solo icono a menos de 540px.
- Basado directamente en `frontend/tablet-app/src/composables/useTema.js` y estilo `SYNCHRO-FUNDIDO-TEMA-V2` de `frontend/tablet-app/src/App.vue`, el portal usa `document.startViewTransition`, `nextTick`, bloqueo de cambios en menos de 250ms, clase temporal `synchro-tema-instantaneo` y un único fundido de 220ms para la pantalla completa. Prefers-reduced-motion y falta de soporte tienen alternativa sin View Transition.
- Añadidos `src/styles/theme-transition.css` y pruebas `tests/accesoResponsive.test.mjs`, `tests/temaTransicion.test.mjs`.
- Cambios solo en `frontend/usuarios-app`; ningún endpoint, dato SQL, tablet o admin fue modificado. Pendiente ejecutar pruebas y compilación en laptop: no se tiene acceso al navegador del usuario.
