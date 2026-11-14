# Recuperación de las pruebas históricas — 13/11/2026

Destino único de ejecución: `(localdb)\SYNCHRO_Aislado / SynchroDB_Pruebas`, con
`SynchroFixtureDeposito=1`. Trabajo en `main`, sin commit, push, ramas ni worktrees.

## Dependencias encontradas y adaptación

| Clase | Casos originales | Dependencia histórica | Adaptación |
|---|---:|---|---|
| AutenticacionTerminalRealTests | 4 | MSSQLLocalDB y una terminal activa preexistente | Terminal propia y cookies reales, conexión aislada |
| ConsultaConsentimientoSqlTests | 1 | MSSQLLocalDB y reservas preexistentes | Ocho escenarios SQL propios dentro del mismo caso, manteniendo sus aserciones |
| ConsumoEvidenciaFacialSqlTests | 1 | MSSQLLocalDB y procedimiento de consumo ausente en la fixture | Destino aislado y procedimiento histórico real |
| DetalleEncomiendaAutorizacionTests | 3 | SynchroDB_Detalle_Pruebas y participantes preexistentes | Encomienda y dos participantes propios; mantiene 200/404 y autorización |
| EmisionPinAisladoTests | 1 | User Secrets, catálogo SynchroDB, archivo MDF clonado y contratos SQL históricos | HMAC efímera, catálogo/marcador aislados y contratos reales; conserva consumo único y rechazo del retiro sin facial |
| PinRetiroDesarrolloAisladoTests | 2 | User Secrets y clon físico llamado SynchroDB | Modo Testing con destino y marcador aislados; conserva rechazo de instancia principal |
| PinRetiroIntegracionTests | 1 | SynchroDB_Detalle_Pruebas, secretos y procedimientos PIN | Configuración efímera, contratos PIN reales y limpieza de sus propios IDs |
| PinRetiroConexionTests | 3 | SynchroDBPruebas desde User Secrets | Configuración local de prueba; conserva salt inexistente, PIN no disponible y formato inválido |
| RechazosEvidenciaFacialSqlTests | 10 | MSSQLLocalDB y procedimiento histórico | Misma matriz de diez rechazos 55405 y rollback, en la base marcada |
| RevocacionTerminalTests | 1 | MSSQLLocalDB | Misma cookie real antes/después de desactivar su propia terminal |
| VinculacionTerminalCompletaTests | 1 | MSSQLLocalDB, administrador preexistente y esquema/procedimiento de vinculación | Administrador ficticio propio; conserva aprobación, CSRF, cookie, persistencia y rechazo de reutilización |

No se eliminaron casos ni se cambiaron sus resultados esperados para obtener aprobación.
Las comprobaciones del antiguo MDF se sustituyen por comprobaciones explícitas de la
instancia, el catálogo autorizado, ausencia de AttachDbFilename y el marcador de seguridad.
No se copian datos del propietario ni se abren las bases históricas.

## Fixture y scripts

`PruebaHistoricaAislada` comprueba el destino y el marcador antes de preparar cada caso
y antes de aplicar cada lote SQL. Usa los scripts idempotentes existentes y un complemento
aditivo `sql/historicos/Esquema.sql` para las tablas/columnas requeridas por los contratos
históricos. Todos los casos SQL recuperados comparten la colección serializada de pruebas.
Cada caso crea identidades, terminal, administrador, encomienda y reserva con IDs propios.
La limpieza usa esos IDs y etiquetas; no borra bases ni reinicia tablas.

Los archivos fuente históricos se copian a la salida del proyecto de pruebas mediante
enlaces de MSBuild. El cargador adapta únicamente el literal del catálogo
`SynchroDB_Detalle_Pruebas` a `SynchroDB_Pruebas`. En el script de retiro de desarrollo
extrae exclusivamente el lote CREATE OR ALTER PROCEDURE: no ejecuta su preámbulo,
su transacción de instalación ni sus comprobaciones contra SynchroDB. La lógica de
PIN, destinatario, evidencia facial, caducidad, bloqueos y consumo permanece real.
No se modifican los scripts originales ni se reemplazan sus procedimientos con mocks.

Los servicios PIN internos se adaptaron para aceptar el destino marcado de Testing.
PinRetiroService exige la instancia aislada; PinRetiroDesarrolloService conserva su
comportamiento de Development y exige el catálogo/marcador de pruebas en Testing.
Ninguna prueba utiliza el camino de Development hacia la base histórica.

## Corrección encontrada

`ValidacionDepositoEncomienda.ConsentimientoConfirmado` aceptaba una confirmación con
fecha futura si estaba dentro del intervalo creación/expiración. Ahora también exige
que la confirmación no sea posterior al reloj actual. El reloj puede proporcionarse
explícitamente para que las pruebas unitarias sean deterministas. No se exige que el
consentimiento firmado caduque después de su confirmación válida: se conserva la regla
del prototipo y los controles SQL adicionales de cada operación.

Se añadieron seis casos: tres de rechazo de instancia/base/archivo adjunto antes de abrir
conexión y tres sobre confirmaciones pasadas, presentes y futuras.

## Ejecución reproducible

`tools/ProbarBackendAislado.ps1` ejecuta por defecto la suite completa sin filtro.
Los grupos opcionales son `-Grupo Evidencias`, `Terminales`, `Pin` y `Detalle`.
Cada invocación verifica en modo de lectura instancia, catálogo y marcador antes de las
pruebas. Deshabilita los trabajadores de envío y restaura las variables de proceso.
No ejecutar dos invocaciones simultáneas sobre la misma base.

Si Windows bloquea el archivo por ExecutionPolicy, ejecutar con autorización:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./tools/ProbarBackendAislado.ps1
```

La excepción corresponde únicamente al proceso; no modifica la política permanente.
El proyecto requiere los paquetes restaurados previamente. No se utilizan credenciales
reales, entregas WhatsApp ni servicios externos de producción.

La primera validación por grupos aprobó los 28 casos originales: evidencias/consentimiento
12, terminales 6, PIN 7 y detalle 3. Evidencias/consentimiento volvió a aprobar sus 12 casos
tras incorporar los ocho escenarios y la corrección de fechas futuras. Las salidas están
en `docs/resultados/historicas-*-20261113.log`; el resultado final completo se registra
en `docs/resultados/backend-completo-20261113.log`.

## Resultado final confirmado

La ejecución completa sin filtro terminó con **375 correctas, 0 fallidas, 0 omitidas,
0 excluidas y 0 casos inseguros pendientes**. Duración: **1 minuto 34 segundos**.
El total se compone de 341 casos ya seleccionados, los 28 históricos recuperados
y seis casos nuevos de seguridad. Compilación sin advertencias ni errores.

Tras finalizar se volvió a comprobar catálogo y marcador en modo de lectura y se
contaron los registros ficticios de estas fixtures: **0 terminales, 0 personas y
0 administradores restantes**. No se ejecutaron eliminaciones adicionales para obtener
esos valores; corresponden a las limpiezas de las propias pruebas.

## Archivos cambiados en esta etapa

- Backend: `Services/ValidacionDepositoEncomienda.cs`,
  `seguridad/PinRetiroService.cs` y `seguridad/PinRetiroDesarrolloService.cs`,
  dentro de `backend/Synchro.Api/`.
- Pruebas: los once archivos `.cs` de las clases de la tabla anterior, más
  `FabricaSynchroPruebas.cs`, `ValidacionDepositoEncomiendaTests.cs` y
  `Synchro.Api.Tests.csproj`, dentro de `tests/Synchro.Api.Tests/`.
- Nuevos recursos de prueba: `tests/Synchro.Api.Tests/PruebaHistoricaAislada.cs`
  y `tests/Synchro.Api.Tests/sql/historicos/Esquema.sql`.
- Verificador: `tools/ProbarBackendAislado.ps1`, ahora sin exclusiones por defecto.
- Documentación: este informe y `docs/CIERRE-BACKEND-MAIN.md`.
- Evidencias: `docs/resultados/historicas-evidencias-20261113.log`,
  `historicas-terminales-20261113.log`, `historicas-pin-20261113.log`,
  `historicas-detalle-20261113.log` y `backend-completo-20261113.log`.

Se conservaron intactos `demos/` y
`backend/Synchro.Api/Endpoints/SolicitudesVerificacionTelefonoEndpoints.cs`.
No se crearon ramas, worktrees ni pull requests; no se hizo commit ni push.
