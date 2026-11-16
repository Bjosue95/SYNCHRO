# SYNCHRO — MEMORIA MAESTRA Y DOCUMENTO DE CONTINUIDAD

**Actualización:** 14 de noviembre de 2026 (Honduras, UTC−06:00).  
**Destino:** adjuntar este archivo al comienzo de un chat nuevo con ChatGPT o al trabajar con Codex.  
**Repositorio:** <https://github.com/Bjosue95/SYNCHRO>  
**Propósito:** conservar decisiones, cambios de criterio, arquitectura, rutas, avances, pendientes y forma preferida de trabajo, para continuar sin comenzar desde cero.

> **INSTRUCCIÓN PARA EL PRÓXIMO ASISTENTE:** Lee **todo** este archivo antes de proponer cambios. No interpretes lo documentado como una verificación en tiempo real de la computadora o del repositorio: distingue decisiones aprobadas, avances reportados, comandos entregados y acciones efectivamente verificadas. Pide/consulta el código **local actual** o la salida de comandos de diagnóstico antes de modificar archivos, especialmente después de varios parches CSS. **No sobrescribas el backend, la base de datos, la rama `main` ni diseños aprobados.** Trabaja sobre una sola parte por vez y entrega comandos PowerShell seguros para ejecutar en la terminal de VS Code. Si el usuario aporta un requisito nuevo, prevalece sobre este resumen.

---

## 0. LECTURA RÁPIDA — DÓNDE NOS QUEDAMOS

1. SYNCHRO es un **emprendimiento hondureño de infraestructura modular de casilleros inteligentes y autoservicio**, no únicamente paquetería. Servicios: resguardo temporal de objetos/maletas; depósito, entrega y retiro de encomiendas; módulos frío/calor; y carga de celulares y laptops.
2. Marca: **SYNCHRO — «El punto donde todo se conecta»**. Colores: azul `#01305C`, turquesa `#00B9C7`, celeste `#5EC6E8`, gris `#5B6770`, blanco `#FFFFFF`. Respetar el **logo real suministrado por el usuario**; no recrearlo de memoria.
3. **Estado visual más reciente de la tablet:** se trabajó en Vue 3/Vite sobre la rama local `rebuild/tablet-desde-cero`. Se aprobaron la pantalla de bienvenida, menú, Guardar, selección de objetos, selección de tamaños y una variante de la selección de casillero. La transición de **Bienvenida → Menú** acaba de aprobarse como un **desvanecido uniforme y suave**, sin capas visibles, zoom ni desplazamientos. Mantenerlo.
4. **Punto visual aún sensible:** uniformidad entre «¿Qué deseas guardar?», «Selecciona el tamaño del casillero» y «Elige tu casillero». Los botones **Continuar** y **Volver** deben conservar misma apariencia, iconos originales, alineación y tamaño táctil. Las tarjetas seleccionadas deben mostrar check turquesa circular. Se entregaron scripts de corrección; **no se confirmó con captura final** que todos quedaran perfectos.
5. Se explicó que **`main` es la rama principal definitiva**, pero se pidió **guardar primero en `rebuild/tablet-desde-cero`** para evitar una integración peligrosa. Se entregaron comandos de `git add/commit/push`, pero **el usuario no mostró salida del commit/push**: el guardado remoto **no está confirmado**. La fusión a `main` sigue **pendiente** de revisión funcional y de conflictos.
6. Backend: C# **ASP.NET Core .NET 10**, SQL Server LocalDB, SQL real de prueba `SynchroDB_Pruebas`. Existen pruebas exitosas de reservas, seguridad PIN, depósitos y endpoints, pero son resultados de momentos/suites diferentes. No asegurar que «todo el backend está listo». **Pendientes críticos** sobre esquema aislado, servicio de vencimientos, autenticación de terminal y WhatsApp real.
7. Próxima sesión sugerida: **a) comprobar `git status`, rama y último commit**, b) inspeccionar código local actual de `App.vue`, `transitions.css`, `SeleccionTamanoView.vue`, `SeleccionCasilleroView.vue` si se retoma UI; c) después, si el usuario lo desea, seguir con el flujo funcional de Guardar. **No añadir nuevas funciones o tocar backend sin solicitud.**

---

## 1. IDENTIDAD, MISIÓN Y ALCANCE DEL NEGOCIO

- SYNCHRO se concibe como **red/solución de autoservicio modular** para aeropuertos, universidades, plazas, parques centrales, terminales de buses, centros comerciales, gasolineras y otros puntos de interés. **No** es obligatorio construir todas las funciones juntas en una estación: puede instalarse solo un módulo de frío/calor, solo resguardo de maletas, solo casilleros normales, solo carga, o combinaciones según ubicación/demanda.
- Servicios a representar **siempre** en identidad, FODA, modelo de negocio y comunicación: 
  1. Almacenamiento temporal de objetos personales, mochilas, compras, cajas, maletas/equipaje.
  2. Encomiendas: depósito por remitente, notificación/consentimiento y retiro por destinatario.
  3. Casilleros con temperatura controlada: **frío** y **calor** (en feria se simulan).
  4. Estaciones de carga de dispositivos, incluidos teléfonos y laptops en compartimientos seguros.
- Propósito: facilidad cotidiana, conveniencia, innovación, seguridad, inclusión/accesibilidad y aprovechamiento de tecnologías de autoservicio.
- **Misión trabajada** (texto de referencia, no necesariamente versión legal definitiva): «Facilitar la vida diaria de las personas brindando soluciones inteligentes de almacenamiento, gestión de encomiendas y carga de dispositivos electrónicos, mediante tecnología de autoservicio que integra seguridad, innovación y espacios con temperatura controlada, ofreciendo comodidad y confianza, con un compromiso de accesibilidad e inclusión que contribuya al bienestar de la comunidad».
- **Visión trabajada:** «Liderar la transformación del autoservicio inteligente en Honduras, siendo reconocidos por nuestra excelencia inquebrantable y un compromiso real con la sostenibilidad, conectando positivamente a cada comunidad». Se debatió cómo mejorarla; revisar la versión definitiva antes de materiales públicos.
- Se prepararon propuestas de **6 valores**, **FODA de 7 elementos por cuadrante**, cruces FODA y versiones más resumidas; no asumir un documento final sin cotejar los archivos del equipo.
- Se han explorado costos de fabricación local y viabilidad de módulos frío/calor independientes. **No hay cotización final confirmada** ni plano térmico de fabricación certificado.

## 2. MARCA, PALETA, ICONOS Y MATERIAL DE FERIA

- Nombre escrito exactamente: `SYNCHRO`. Eslogan: `El punto donde todo se conecta`.
- Paleta: `#01305C` azul principal, `#00B9C7` turquesa, `#5EC6E8` celeste, `#5B6770` gris, `#FFFFFF` blanco.
- El usuario aportó **imágenes del logotipo correcto**, corrigió intentos anteriores. **No usar una versión inventada** ni asumir que el SVG del repositorio es el logo definitivo sin contrastar con la imagen enviada.
- Tipografía: coherente con el logo real; no sustituir letras del logotipo por una fuente genérica.
- Iconografía aprobada: contornos limpios azul marino/turquesa, visual intuitivo, **versiones clara y oscura**. Biblioteca local `frontend/tablet-app/public/iconos/` y `public/iconos/complementarios/`. Algunos iconos específicos confirmados:
  - `28_regresar.svg`, `28_regresar-oscuro.svg`.
  - `29_continuar.svg`, `29_continuar-oscuro.svg`: el primero tiene círculo azul marino y flecha turquesa; el oscuro, círculo blanco y flecha turquesa.
  - `15_ubicacion_casillero.svg`, `15_ubicacion_casillero-oscuro.svg`.
  - `30_confirmar.svg` y `30_confirmar-oscuro.svg` (check verde); usar solo si corresponde al estilo aprobado, ya que el usuario pidió específicamente el **check de selección turquesa** en tarjetas.
  - Tamaños: `casillero-tamano-S.svg`, `-M.svg`, `-L.svg` y variantes `-oscuro.svg` en la carpeta de iconos (verificar nombres locales exactos).
- Se trabajaron diseños de camiseta polo azul, turquesa y celeste, con/sin logotipo, e impresiones de letras del logo separadas para carta. 
- **Feria de emprendimiento:** 5 de diciembre en el Parque Central de Villa de San Antonio (revisar convocatoria vigente). Stand disponible: **solo mesa** en carpa compartida de **3 × 3 m**; no asumir stand completo exclusivo.
- Se habló de un video promocional navideño grabado en Comayagua / Centro Universitario Regional del Centro (UNAH-CURC), con efectos realistas de IA agregados en postproducción; el usuario puede grabar solo y sin el prototipo físico.
- Existe documentación/dossier del negocio que puede aportar detalles: `SYNCHRO_Documento_Integral_del_Negocio.pdf`, `SYNCHRO_Dossier_Servicios_y_Plano_Preliminar.pdf`. Son documentos de referencia, no sustituyen los requisitos más recientes.

## 3. PROTOTIPO FÍSICO Y ACCESIBILIDAD

- Prototipo de feria **sin mecanismos de apertura reales**; simular manualmente la apertura. Frío/calor también se demuestran como concepto (no afirmar capacidad térmica real probada).
- Estructura conceptual del mueble: casilleros de varios tamaños, tablet de aprox. **10–11 pulgadas**, cámara/simulación facial, receptor de efectivo tipo máquina expendedora, estación de carga y pantalla publicitaria superior (referencia anterior ~32 pulgadas). El usuario pidió **reducir la tablet**, eliminar iconos redundantes bajo ella (silla de ruedas, verificación facial/audio, etc.) y añadir depósito de efectivo.
- **M04 = FRÍO**, **M09 = CALIENTE** es una asignación de diseño explícita. En un plano anterior se propusieron **10 compartimentos en total** distribuidos en 3 columnas, incluyendo especiales; otra iteración conceptual propuso 10 generales más 2 especiales. **La cifra/configuración final debe cotejarse con el plano aprobado y con SQL antes de fabricar**. No codificar geometría no verificada en la BD.
- Módulo de carga propuesto: `L1/L2` laptops cerradas; `W1/W2` carga inalámbrica; `C1..C4` cable USB-C o Lightning. Cantidades preliminares, verificables en nuevo diseño.
- Laptop: **se guarda cerrada** mientras se carga, cobro por tiempo; puede usar USB-C Power Delivery provisto o tomacorriente interno para conectar cargador propio.
- Rotulación exterior de cada casillero: **solo su código + braille equivalente** debajo; no mostrar rótulos de tamaños ni escribir «FRÍO/CALIENTE» si se distinguen por color, conforme al acuerdo visual. Braille **no en la tablet**; su transcripción física requiere comprobación con fuente confiable: no fabricar a partir de caracteres inventados por generadores de imágenes.
- Accesibilidad: botones táctiles grandes, alto contraste, texto legible, modo oscuro, reducir movimiento, navegación clara. Inclusión por etapas; voz no es requisito obligatorio de la feria.
- Investigar posteriormente dimensiones físicas, cierres, seguridad eléctrica, ventilación, ergonomía, temperatura y materiales antes de comercializar.

## 4. TECNOLOGÍA Y REPOSITORIO

**Repositorio:** `https://github.com/Bjosue95/SYNCHRO`.

### Ramas y estado (no confundir)

- `main`: rama principal prevista para la integración final. **No asumir que contiene el último estado de la tablet local**.
- `refactor/arquitectura-backend`: rama de refactorización y desarrollo del backend, con avances confirmados históricamente y distintas ramas de implementación paralelas.
- `rebuild/tablet-desde-cero`: **rama local de trabajo de la nueva interfaz de tablet**, donde sucedieron los cambios más recientes de este chat; el usuario aceptó conservarla de forma temporal y luego integrar en `main` tras verificar seguridad y funciones.
- Ramas backend auxiliares vistas en noviembre: `feat/encomiendas-transaccion-aislada`, `feat/notificaciones-y-servicios-prototipo`, `fix/seguridad-operaciones-carga`.
- **No se dispone de salida actual de `git push`/`git log` del usuario:** no escribir que los cambios del 14 de noviembre ya están en GitHub si no se comprueba.
- Evitar mezclar ramas y sobrescribir cambios locales sin inspeccionar `git status`, `git log`, `git diff`, remotos y merge base. Considerar PR y pruebas antes de integrar en `main`.

### Backend

- `backend/Synchro.Api` con **C# / ASP.NET Core .NET 10** y arquitectura modular de endpoints (se mencionaron 13 módulos).
- SQL Server LocalDB, datos persistentes; bases originales y aisladas distintas.
- Autenticación/administración: cookie `Synchro.Admin` con expiración, CSRF/antiforgery, políticas de acceso, limitación de peticiones, sesión de terminal.
- Modelos representativos: `Casillero { Codigo, Tamano, Tipo, Estado }` y `EspacioCarga { Codigo, Estado }`. El nombre de propiedades y forma exacta de JSON se deben revisar localmente antes de alterar filtros.
- Distinguir APIs: `GET /api/casilleros` (demo en memoria) frente a `GET /api/casilleros-bd` (SQL Server); `GET /api/espacios-carga` para espacios de carga. El frontend actual debería consultar los datos reales en `/api/casilleros-bd`.
- Servicio de frontend `src/services/casillerosApi.js`: `obtenerCasilleros()` con fetch a `/api/casilleros-bd` y aceptación de arreglo o `value`.
- `src/services/reservasTerminalApi.js`: `reservarCasilleroTerminal(codigoCasillero)` obtiene CSRF `/api/terminales/csrf-token` y luego POST `/api/terminales/reservas` con `X-CSRF-TOKEN` y cookies. **No supone permiso para ejecutar la reserva al pulsar una tarjeta.**
- Reserva SQL mediante `ReservarCasilleroSynchro` transaccional (`UPDLOCK/HOLDLOCK`), valida disponibilidad y registra uso. El usuario reportó una prueba de depósito personal desde tablet → API → SQL; ejemplo histórico `S01`/uso 16, estado `Ocupado` (8 de noviembre).
- PIN de retiro: 6 dígitos y hash/HMAC, vencimiento/intentos/uso único en desarrollo y pruebas; evidencia facial exigida lógicamente por algunos flujos. **Para feria, «verificación facial» se simula mediante captura, no se implementa biometría real.**
- WhatsApp Business Platform Cloud API: **único canal acordado** para consentimiento, notificaciones, confirmación de teléfono y envío del PIN. **No SMS.** Sin aceptación y confirmación de destinatario para encomienda, **NO permitir depósito**. El QR **no sirve para abrir casillero** (alguna idea antigua lo contemplaba para compartir invitación, no como autenticación).
- Remitente con reserva web: teléfono de destinatario ya registrado; no volver a pedirlo en tablet. Visitante en tablet: teléfono del remitente y destinatario, más consentimiento de destinatario por WhatsApp; NO reintroducir un flujo de «encomienda presencial» no acordado; usuario restringió esa opción en versiones recientes a clientes registrados.
- Retiro de encomienda: doble verificación **rostro simulado + PIN/OTP por WhatsApp**. No revelar ubicación de un casillero de retiro antes de autorizar correctamente.
- Endpoints destinatarios mencionados: `GET /api/destinatarios/mis-encomiendas` y detalle `{usoId}`.

### Pruebas históricas (no son certificado de estado actual)

- 6 de noviembre: 10 pruebas iniciales exitosas y 19/19 en otra fase; hash, PIN y restricciones SQL probados en entorno local.
- 9 de noviembre: 187 pruebas, 183 aprobadas y 4 omitidas en una etapa; otras migraciones SQL aisladas fallaron por LocalDB antes de ajustes.
- 10 de noviembre: se reportaron **196/196 pruebas xUnit aprobadas** (14 s aprox.) en una suite consolidada. Es una fotografía histórica y no demuestra ejecución de integraciones externas ni que el repositorio local actual permanezca idéntico.
- También hubo una suite específica de endpoints terminales **41/41 aprobadas**, seguida de cinco nuevas pruebas que fallaron por desacoplar el CSRF emitido anónimamente de la identidad de terminal; en esa etapa se propuso corregir las pruebas obteniendo CSRF autenticado. No confundir con las 196 de otra ejecución.
- Scripts de prueba SQL aislada para consentimiento, depósito, evidencias, outbox, PIN y esquema de depósito; pruebas de depósito SQL terminaron con **13/13 correctas** en la etapa narrada.
- **Atención:** una ruta antigua `/api/casilleros-bd/{codigo}/retirar` fue señalada como potencialmente insegura, ya que podía liberar `Casilleros` sin confirmar PIN/identidad ni sincronizar `UsosCasillero`; revisar o retirar ese endpoint antes de cualquier despliegue real.
- No afirmar reconocimiento facial auténtico, mensajería WhatsApp real, pasarela de pagos ni apertura automatizada funcionando solo porque tests simulados pasen.

### Último estado adicional de backend detectado en conversaciones del 13–14 nov. 2026

- HTTPS `https://localhost:7278` y HTTP `http://localhost:5229`, backend apuntando en ese momento a `SynchroDB_Pruebas` (verificar al reiniciar).
- `VencimientoReservasService` provocaba fallos por esquema SQL en la base aislada; se **deshabilitó temporalmente** y el backend volvió a iniciar. **No dejar desactivado en un sistema final.**
- `SynchroDB_Pruebas` no tenía completamente instalados `FechaVencimiento`, `TokenDepositoHash` y el procedimiento `CancelarReservaCasilleroSynchro`; había scripts parciales. Antes de habilitar el servicio, verificar migraciones de manera controlada en base de prueba.
- Terminal `SYNCHRO-DEMO-01`, ID `960`, registrada pero **inactiva** al momento de la observación; administrador ID `72` creado. Login administrativo devolvió **401**. Falta diagnosticar autenticación/activación de terminal y verificar permisos sin deshabilitar la protección.
- No confundir base original `SynchroDB` con aislada `SynchroDB_Pruebas`. Instancia LocalDB aislada reportada: `SYNCHRO_Aislado` (verificar instancia y conexión vigente). **Nunca ejecutar migraciones masivas sin respaldo/confirmación de destino.**

## 5. TABLET VUE 3 — ARCHIVOS, PANTALLAS Y REGLAS VISUALES

### Entorno

- Frontend reciente: `frontend/tablet-app` (Vue 3 + Vite + Vue Router), trabajado en **VS Code con PowerShell** desde la raíz del proyecto.
- Ejecutar compilación: `npm.cmd --prefix .\frontend\tablet-app run build`.
- La dirección local de Vite que se probó suele ser `https://localhost:5173/` (confirmar puerto/protocolo real).
- `src/App.vue` conserva el **encabezado compartido** fuera de la transición entre rutas; `src/components/AppHeader.vue` es el encabezado. No duplicar header por vista ni animarlo como si fuera una página nueva.
- `src/composables/useTema.js` administra modo claro/oscuro compartido; `src/styles/kiosk-layout.css` contiene ajustes responsivos que pueden afectar CSS `scoped` de las vistas.
- Archivos recientes: `src/views/BienvenidaView.vue`, `MenuView.vue`, `GuardarView.vue`, `SeleccionTamanoView.vue`, `SeleccionCasilleroView.vue`; componentes `ServiceCard.vue`, `TamanoCard.vue`, `CasilleroCard.vue`; `src/router/index.js`; `src/styles/transitions.css`; servicios `casillerosApi.js`, `reservasTerminalApi.js`.
- **Importante:** el `router/index.js` de `main` que se revisó en otro momento tenía rutas anteriores (`/guardar/pertenencias/...`) que no necesariamente coinciden con el **router local reconstruido**. No reutilizar un router de `main` para parchear la rama `rebuild/...`.

### UI acordada y referencia visual

- La **bienvenida** es pantalla única de fondo oscuro, logo original blanco/isotipo, eslogan, hora/fecha discretas y texto para tocar cualquier parte; sin botones ni selector de tema. La superficie completa es clicable.
- El **menú** debe tener cuatro servicios táctiles: Guardar, Retirar, Tengo una reserva, Cargar dispositivo. Estilo sobrio/moderno, sin extravagancias.
- El **header compartido** incluye logo SYNCHRO, botón «¿Ayuda?», selector claro/oscuro y reloj/fecha. Debe ser cómodo en tablet (screenshot validado visualmente en ~953×863 px), y no hacerse diminuto al ampliar viewport.
- En **Guardar** se presentan cuatro categorías:
  - `mochila` = «Mochila o bolso», tamaños permitidos M/L.
  - `maleta` = «Maleta o equipaje», M/L.
  - `compras` = «Bolsas de compras», S/M/L.
  - `caja` = «Objetos personales y cajas», S/M/L.
- **Selección de tamaño**: título centrado «Selecciona el tamaño del casillero», subtítulo «Para: [objeto]», texto «Elige el espacio adecuado para tus pertenencias». Ejemplo en captura del usuario: tres tarjetas grandes en fila, `Pequeño` No disponible (gris), `Mediano` 6 disponibles y seleccionado (fondo celeste claro/borde turquesa), `Grande` 2 disponibles. Parte inferior: mensaje «Seleccionaste el tamaño mediano.» y botón **Continuar** turquesa/azulado con **flecha SVG redonda original**.
- **Selección de casillero**: solo normales (`tipo = Normal`) con `estado = Disponible`, del tamaño elegido (S=Pequeño, M=Mediano, L=Grande). Orden natural por código, paginación si >8. Elegir tarjeta solo cambia selección; no reserva ni abre. El usuario quiere conservar **exactamente el lenguaje visual de la pantalla de tamaños**. Si 6 disponibles, buena distribución 3×2 en horizontal y 2 columnas en vertical. Selección: fondo claro turquesa, borde turquesa, texto «Seleccionado» y **check visible en círculo turquesa**. El check no debe duplicarse.
- **Botón Continuar** y **Volver**: unificar tamaño, borde, relieve, icono SVG real en claro/oscuro, respuesta táctil. No inventar flechas de caracteres o círculos CSS adicionales encima del SVG. El botón Continuar **no debe quedar desalineado o empujado hacia el extremo derecho por el texto del pie**; tomar de referencia la pantalla «¿Qué deseas guardar?» y el screenshot de la pantalla de tamaños. Para claro se aprobó botón `#F4FEFF` con borde `#00B9C7` y texto `#01305C`; oscuro `#123D5B` con borde `#5EC6E8` y texto blanco. La ubicación exacta debe comprobarse con captura real antes de imponer CSS nuevo.
- La intención no es agregar funcionalidades que el usuario no pida ni rediseñar por completo pantallas ya aprobadas. **La pantalla de casilleros tuvo varios ajustes de CSS enviados en PowerShell; la consistencia final estaba bajo revisión y no hay captura de aceptación final.**
- Prototipo de feria: no generar puertas automáticas reales, ni permisos de apertura antes de verificar identidad según flujo aplicable. Resguardo personal y encomienda son casos de uso distintos.

### Última transición aprobada: BIENVENIDA → MENÚ

- El usuario rechazó una transición brusca y después **rechazó el fundido cruzado con superposición y movimientos/zoom**: «parece que fuera en capas».
- El **14 nov 2026** aceptó visualmente **desvanecido parejo y suave**, entrada/salida **secuenciales**, sin `translate`, `scale` ni superposición. La solución propuesta para la transición `synchro-inicio`: `out-in` exclusivo de inicio y CSS de solo `opacity` de **420 ms `ease-in-out`** tanto para salida como para entrada. El encabezado (`cabecera-inicio-*`) aparece con opacidad coordinada. **No revertir a fade cruzado** y no hacer animaciones de capas.
- Fueron editados mediante scripts PowerShell, según conversación: `src/App.vue` y `src/styles/transitions.css`. El usuario confirmó «sí está mejor»; **es una aprobación visual, no equivalencia a revisión del último `git diff`**.
- En otras rutas mantener su transición previa salvo nueva instrucción. Con `prefers-reduced-motion: reduce`, evitar animación.

### Registro de cambios recientes y cautelas

- El usuario solicitó reiteradamente código **para pegar en la terminal**, no ZIP ni descargas para aplicar cada parche.
- Scripts previos instalaron estilos con marcadores como `SYNCHRO-CONTINUAR-ICONOS-DOBLE-TEMA-V2`, `SYNCHRO-COLOR-CONTINUAR-V3`, `SYNCHRO-CHECK-Y-CONTINUAR-UNIFICADOS-V1` y ajustes de diseño casilleros. **No asumir que todos se aplicaron**; comprobar el archivo real, porque algunos scripts hacían verificaciones estrictas y podían abortar, y algunos parches acumulaban reglas contradictorias.
- La última respuesta antes de este documento sugirió ejecutar `git add -A -- frontend/tablet-app`, `git commit -m "Tablet SYNCHRO: interfaces y transicion de inicio"` y `git push -u origin rebuild/tablet-desde-cero`. **No hay confirmación de ejecución.**

## 6. FLUJOS DEL NEGOCIO (NO CONFUNDIR)

1. **Guardar pertenencias propias:** elegir objeto (mochila, maleta, compras, cajas), tamaño, casillero disponible; después debe resolverse la reserva/uso/pago y una apertura **simulada**. En la UI reciente elegir un casillero NO hace reserva.
2. **Encomienda a destinatario:** registrar/reservar remitente y teléfono del destinatario; destinatario debe aceptar y confirmar WhatsApp **antes de permitir depósito**; al depósito notificar PIN por WhatsApp; retiro requiere rostro simulado + PIN vigente. No permitir que un extraño retire cambiando solo un estado SQL.
3. **Reservas desde web:** la tablet recupera información existente y no vuelve a pedir destinatario si ya está registrado en la reserva.
4. **Frío y calor:** reservables según módulo instalado; no ofrecer como temperatura real controlada en el prototipo de cartón; diseño modular a futuro.
5. **Carga móvil/laptop:** compartimentos de carga con control de tiempo; seguridad eléctrica y bloqueo físico reales son futuros.
6. **Retiro:** no publicar ubicación exacta antes de validaciones; impedir PIN repetido, vencido, intentos excesivos; administrar consentimiento/datos personales.

## 7. ¿CÓMO QUIERE TRABAJAR EL USUARIO?

- En **español**, guiado **paso a paso**, con instrucciones claras para la **tercera terminal PowerShell de VS Code**, iniciando normalmente en la raíz del repo.
- Prefiere **bloques listos para copiar y pegar**, sin descargar ZIP para cada modificación de código. Esta memoria sí es una excepción: se crea para transportarla a nuevos chats.
- **No introducir rediseños, nuevas funciones ni nuevas ramas** sin necesidad; concentrarse en el área solicitada. Cuidado con cambios en cascada de CSS, herencia, compatibilidad responsive y accesibilidad.
- Antes de un parche: inspeccionar el estado y código local pertinente. Usar comprobaciones de rutas y rama, copia de respaldo, UTF-8 sin BOM y **rollback** si falla `npm.cmd --prefix .\frontend\tablet-app run build`.
- Verificar **visualmente** la tablet horizontal y vertical. Una compilación exitosa no demuestra que las tarjetas o el encabezado estén alineados. Pedir captura cuando sea realmente necesaria para calibrar.
- No asumir que GitHub `main` contiene sus cambios locales. No atribuir éxito a un script que solo se entregó pero cuya salida aún no fue mostrada.
- El usuario ha expresado frustración cuando los parches cambian demasiadas cosas, fallan por patrones/reglas CSS, agregan iconos no originales o desalinean botones. Preferir **diagnóstico primero y parche quirúrgico**.
- En sistemas de seguridad de retiro y almacenamiento, nunca omitir controles reales por conveniencia del prototipo; indicar claramente cuando una funcionalidad es simulada.

## 8. PRÓXIMOS PASOS ORDENADOS (NO EJECUTAR SIN PETICIÓN)

**A. Retomar tablet/UI**

1. Comprobar estado local de Git y guardado remoto, sin tocar nada: `git branch --show-current`, `git status --short`, `git log -1 --oneline` y si hace falta `git remote -v`.
2. Verificar si el desvanecido uniforme aprobado sigue activo en `App.vue` y `transitions.css`. No reescribirlo si ya está bien.
3. Revisar `SeleccionTamanoView.vue`, `SeleccionCasilleroView.vue`, `TamanoCard.vue` y `kiosk-layout.css` solo si el usuario retoma el problema de diseño: alineación exacta de Continuar, check turquesa, responsive y duplicidades CSS.
4. Solo luego de aprobación visual, definir explícitamente el siguiente paso tras elegir casillero; **no llamar POST de reserva únicamente por tocar tarjeta**.

**B. Retomar backend (si el usuario lo pide)**

1. Identificar rama verdadera, connection string y base exacta; respaldar la base antes de migraciones. Revisar estado de migraciones aisladas faltantes (`FechaVencimiento`, `TokenDepositoHash`, `CancelarReservaCasilleroSynchro`).
2. Corregir/volver a habilitar `VencimientoReservasService` **tras** migración y pruebas.
3. Diagnosticar 401 de login administrativo y estado inactivo de terminal demo. No saltarse CSRF/sesiones por atajo.
4. Revisar endpoint antiguo de retiro potencialmente inseguro; confirmar que el camino real exige doble verificación y transacción consistente.
5. Verificar suites de pruebas y alcance de simulaciones; no presentar suite parcial como suite global.
6. Integrar WhatsApp Business Platform cuando corresponda con credenciales seguras (no guardar tokens en este archivo); consentimiento de destinatario obligatorio; facial simulado para feria.

**C. Integrar `main` más adelante**

- Revisar diferencias y conflictos entre `rebuild/tablet-desde-cero` y `main`, y entre ramas backend. Hacer copia/punto de restauración; preferible Pull Request y compilación/pruebas de integración. No mezclar solo porque `git push` funcionó.

## 9. COMANDOS DE LECTURA (SEGUROS, NO MODIFICAN ARCHIVOS)

Ejecutar desde la raíz del repo en PowerShell:

```powershell
# Estado exacto del trabajo local
git branch --show-current
git status --short
git log -1 --oneline

# Ver qué archivos de tablet cambiaron (sin editarlos)
git diff --name-only -- frontend/tablet-app

# Ver las reglas de transición sin modificarlas
Get-Content .\frontend\tablet-app\src\styles\transitions.css

# Comprobar compilación cuando sea necesario
npm.cmd --prefix .\frontend\tablet-app run build
```

**No ejecutar automáticamente `git reset --hard`, `git clean`, `git checkout main`, `git merge` ni migraciones SQL** para «arreglar» un problema de interfaz. Primero diagnosticar.

## 10. DOCUMENTOS ANTERIORES Y VIGENCIA DE LA INFORMACIÓN

- `SYNCHRO_Documento_Integral_del_Negocio.pdf`: negocio completo, servicios y propuesta de valor.
- `SYNCHRO_Dossier_Servicios_y_Plano_Preliminar.pdf`: diseño físico conceptual.
- `SYNCHRO_ESTADO_PARA_CONTINUAR_2026-11-07.md`: memoria de decisiones anteriores; **la parte de tablet de 7 nov está parcialmente superada** por reconstrucción de 14 nov.
- `SYNCHRO_Estado_del_proyecto_2026-11-05.txt`, `SYNCHRO_Estado_2026-11-05_Paso453.txt`: estado histórico temprano del backend, no estado actual.
- Las memorias de conversaciones anteriores pueden contener estados de pruebas y ramas incompatibles cronológicamente: **priorizar fecha y evidencia**, no combinar totales, versiones ni commits como si fueran una misma ejecución.
- Este archivo **no** contiene contraseñas, tokens de WhatsApp, credenciales, códigos privados, datos biométricos ni archivos fuente completos. No introducir secretos aquí al actualizarlo.

---

## 11. INSTRUCCIÓN BREVE PARA PEGAR EN UN NUEVO CHAT

> «Vamos a continuar el proyecto SYNCHRO. Lee completo el archivo `SYNCHRO_MEMORIA_MAESTRA_2026-11-14.md` que adjunto. Es la referencia del negocio, código, decisiones, interfaz y puntos pendientes de varios chats. Quiero trabajar sobre el código local real que tengo en VS Code, con comandos PowerShell seguros, en la rama correcta, sin rehacer lo aprobado. Antes de cambios, comprueba el estado actual; no asumas que todo lo mencionado se ha subido a GitHub. Continuemos desde el último punto que te indique.»

## 12. REGISTRO PARA LA PRÓXIMA ACTUALIZACIÓN

**Última revisión de este archivo:** 2026-11-14.  
**Estado del commit/push de la tablet:** PENDIENTE DE VERIFICACIÓN CON SALIDA DE GIT.  
**Último cambio de UI aprobado por el usuario:** desvanecido uniforme de Bienvenida → Menú.  
**Estado visual de Selección de casillero:** aprobado el **criterio de diseño** (igual a Tamaños/Guardar), pendiente verificar acabado final con captura si vuelve a trabajarse.  
**Prioridad futura:** a decisión del usuario. No asumir que el siguiente chat exige backend o UI sin leer su nueva solicitud.

**Plantilla para anotar avances:**

```text
FECHA:
RAMA LOCAL:
ULTIMO COMMIT LOCAL:
ULTIMO PUSH CONFIRMADO:
ARCHIVOS MODIFICADOS:
COMPILACION/TESTS VERIFICADOS:
CAMBIO VISUAL APROBADO:
PENDIENTES:
ERRORES Y PASOS PARA REPRODUCIR:
```

---

**Nota importante:** Esta memoria es un **documento portátil de continuidad**, no una copia del repositorio ni un mecanismo automático de sincronización con chats nuevos. Para que otro chat tenga este contexto completo, **adjunta el archivo** o súbelo al repositorio y pide expresamente leerlo. Actualízalo cuando se confirmen nuevos avances.
