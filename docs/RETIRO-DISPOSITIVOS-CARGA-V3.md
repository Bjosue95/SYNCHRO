# SYNCHRO V3 — retiro de carga, cobro y recogida

## Alcance
Esta integración añade el tramo posterior al PIN para cargar y retirar dispositivos W1/W2, C1-C4 y L1/L2.
Recorrido: rostro -> PIN -> resumen de consumo -> elección de Efectivo/Tarjeta/NFC -> confirmación de pago SIMULADO -> declaración de recogida física -> liberación transaccional del espacio.

## Archivos
- Backend: backend/Synchro.Api/Endpoints/RetiroCargaCobroEndpoints.cs
- Migración: backend/Synchro.Api/Database/Migrations/20261122_CobroRetiroCargaSynchro_Pruebas.sql
- Preparación SQL: tools/Preparar-CobroRetiroCarga-Pruebas.ps1
- Tablet: RetiroCargaPagoView.vue, RetiroCargaPinView.vue, retiroCargaApi.js, router/index.js

## Seguridad
- API solo Development, LocalDB aislado autorizado, HTTPS y antiforgery CSRF.
- Token protegido vinculado a sesión: emitido solamente tras rostro y PIN, válido cinco minutos.
- Tarifas exclusivamente desde dbo.TarifasCargaSynchro, tabla que crea la migracion; sin precios predeterminados ni inventados.
- Una cotización persistente por sesión; idempotencia de preparación y pago simulado.
- El pago en tablet es SIMULADO; no se mueve dinero y no sirve para cobrar comercialmente.
- Confirmación de pago y recogida física obligatorias antes de finalizar/liberar el espacio.
- Finalizar sesión, liberar espacio y aplicar cobro en una transacción SQL.
- No usar el endpoint heredado de finalizar carga, porque opera otras tablas.

## Activación en la laptop
1. Desde la raíz: git pull --ff-only origin rebuild/tablet-desde-cero
2. Compilar API: dotnet build backend/Synchro.Api/Synchro.Api.csproj
3. Compilar tablet: npm.cmd --prefix frontend/tablet-app run build
4. Preparar SQL: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Preparar-CobroRetiroCarga-Pruebas.ps1
5. Confirmar PREPARAR COBRO tras verificar instancia y marcador SQL. La migracion crea tabla de cobro y catalogo de tarifas vacio.
6. Introducir tarifa explicita para Celular con tools/Configurar-TarifaCargaSynchro-Pruebas.ps1 (y para Laptop cuando corresponda). Usar importes y bloques elegidos por el equipo.
7. Revisar restricciones CHECK de Estado. No alterar sesiones de W1 ni forzar finalizacion.
8. Iniciar backend con clave DPAPI original: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Iniciar-BackendCargaSegura.ps1
9. Iniciar tablet: npm.cmd --prefix frontend/tablet-app run dev -- --host 0.0.0.0 --port 5173 --strictPort
10. Repetir rostro y PIN si hubo recarga y token efímero perdido, y pulsar Continuar al pago.

## Pendientes de validación
- Publicado el código en GitHub, pero NO se ha compilado .NET ni ejecutado SQL en la laptop del usuario.
- El estado final Finalizada debe estar permitido en el esquema local SesionesCargaSynchro. El preparador muestra restricciones CHECK, pero no las modifica.
- Prototipo sin pasarela real, confirmación de operador ni cerradura automática.

## Correccion tras diagnostico real (22/11/2026)
- El usuario compilo correctamente .NET y Vite; la base SynchroDB_Pruebas y el marcador son validos, existen SesionesCargaSynchro y EspaciosCarga.
- No existen ni dbo.CobrosRetiroCargaSynchro ni dbo.TarifasCarga. El preparador antiguo fallaba antes de la migracion por exigir TarifasCarga heredada.
- Migracion ajustada: crea CobrosRetiroCargaSynchro y **TarifasCargaSynchro** vacia; la API cotiza usando esta tabla, no TarifasCarga.
- Preparar-CobroRetiroCarga-Pruebas.ps1 y Verificar-RetiroCargaV3.ps1 ahora usan esquema V3. Configurar-TarifaCargaSynchro-Pruebas.ps1 permite introducir de forma expresa tarifas para Celular y Laptop, sin precios asumidos.
- Requiere nueva compilacion local y ejecucion/validacion de tarifas; codigo publicado en rama de trabajo, sin cambios de SQL ejecutados por ChatGPT.


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
