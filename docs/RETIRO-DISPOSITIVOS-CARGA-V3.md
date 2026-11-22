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
- Tarifas desde la tabla existente dbo.TarifasCarga; precios no predeterminados ni inventados.
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
5. Confirmar PREPARAR COBRO tras verificar instancia y marcador SQL. Revisar tarifas y restricciones CHECK de Estado.
6. Iniciar backend con clave DPAPI original: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Iniciar-BackendCargaSegura.ps1
7. Iniciar tablet: npm.cmd --prefix frontend/tablet-app run dev -- --host 0.0.0.0 --port 5173 --strictPort
8. Repetir rostro y PIN si hubo recarga y token efímero perdido, y pulsar Continuar al pago.

## Pendientes de validación
- Publicado el código en GitHub, pero NO se ha compilado .NET ni ejecutado SQL en la laptop del usuario.
- El estado final Finalizada debe estar permitido en el esquema local SesionesCargaSynchro. El preparador muestra restricciones CHECK, pero no las modifica.
- Prototipo sin pasarela real, confirmación de operador ni cerradura automática.
