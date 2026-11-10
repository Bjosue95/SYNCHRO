# Desarrollo integral: auditoría y punto de continuación

> Las reglas de encomiendas se actualizaron: [clientes web y rostro simulado](ENCOMIENDAS-CLIENTES-Y-ROSTRO-SIMULADO.md). Las referencias anteriores a visitantes o biometría profesional quedan sustituidas.

Estado histórico de la primera entrega. Para el avance en la rama solicitada, ver [AVANCE-REFACTOR.md](AVANCE-REFACTOR.md).

Base revisada: `refactor/arquitectura-backend`, commit `de977cd`.
Se conserva el código existente. No se ejecutó ninguna migración sobre `SynchroDB`.

## Resultado inicial reproducido

`dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj`

110 casos ejecutados en Linux: 78 aprobados, 32 fallidos, ninguno omitido.
25 fallaron por LocalDB incompatible con Linux; 5 por `secrets.json` privado ausente.
Otros 2 esperaban 401/404 y recibieron 500 porque su consulta usa LocalDB.
Estos 32 casos siguen pendientes de validación en Windows con las bases originales de pruebas;
no se eliminaron ni sustituyeron sus aserciones. Los resultados de esta máquina no prueban
que los 110 casos pasen con SQL disponible.

## Inventario y faltantes

| Área | Existe | Pendiente para completar el prototipo |
|---|---|---|
| Resguardo personal | Reserva, token de depósito, PIN protegido, retiro, recuperación, cancelación periódica | SQL inicial reproducible completo, validar concurrencia y vencimientos en SQL |
| Encomiendas web | Pre-reserva, credencial de gestión, consulta, consentimiento WhatsApp | Vincular identidades verificadas, emitir evidencia desde captura confiable, conectar depósito con PIN y notificación y retiro atómico |
| Encomiendas visitante | Piezas de participantes y verificación telefónica | Flujo tablet integrado con consentimiento y evidencias |
| Biometría | Comparador de descriptores, política, preparador, evidencia de dos minutos, consumo SQL | Enrolamiento confiable, captura/prueba de vida del servidor, protección y eliminación de plantillas; Human.js no es una autoridad confiable |
| WhatsApp | Cliente Cloud API, firma HMAC del webhook, deduplicación SQL, consentimiento de pre-reserva | Cola duradera, reintentos según resultado, conciliación de entregas, plantillas y credenciales reales |
| Carga | Reserva, inicio, finalización y cobro posterior | Pago anticipado de 30/60/90 min, conectores, vencimiento sin impedir recuperar el dispositivo |
| Frío/calor | Restricción de tipos en procedimiento de reservas | Sesiones y tarifas específicas, estados/temperatura simulados y auditoría |
| Administración | Sesión, terminales vinculadas/revocables, consulta de pre-reservas | CRUD con roles, incidencias, reportes agregados y estadísticas |
| Seguridad | Cookies Secure/HttpOnly, CSRF en diversos endpoints, rate limiting de login, SQL parametrizado | Uniformar autorización/CSRF en rutas heredadas de carga, errores, límites por operación, auditoría y retención de datos |

No hay un script inicial de todas las tablas/procedimientos. Las migraciones existentes
incluyen archivos exclusivos de desarrollo `SynchroDB`: **no ejecutarlos** en esta tarea.

## Primera entrega: fundamento del depósito atómico

`Services/DepositoEncomiendaService.cs` proporciona un contrato interno parametrizado,
cancelable y restringido a `SynchroDB_Pruebas`. La migración
`20261110_DepositoEncomiendaAtomico_Pruebas.sql` comprueba terminal activa, disponibilidad,
tamaño, reserva vigente, vínculo de participantes verificado en el servidor, consentimiento
confirmado por el teléfono esperado y evidencia facial vigente de la misma reserva/persona/terminal.

En una transacción registra el uso, participantes y asociación de pre-reserva, ocupa el
casillero y consume la evidencia. Las claves únicas impiden reutilizar reserva/evidencia.
El vínculo de depósito conserva la pre-reserva original; consumidores deberán consultar ese
vínculo antes de presentar una pre-reserva como disponible. La respuesta HTTP existente sigue
indicando `depositoHabilitado=false`.

**No está registrado como endpoint ni habilita aperturas.** No hay todavía un emisor confiable
del vínculo `ParticipantesPreReservaVerificados`, biometría del servidor, PIN/notificación
transaccional ni integración del retiro. No insertar vínculos/evidencias sintéticos fuera de la
fixture para habilitar operaciones. El servicio SQL nuevo no reemplaza el retiro existente.

Pruebas: validación del comando, aislamiento de bases y cancelación; además casos SQL para
idempotencia de migraciones, consumo único, carrera concurrente, rechazos y reversión de una
falla inyectada después del consumo. Los casos SQL se omiten explícitamente cuando falta
`SYNCHRO_SQL_DEPOSITO_TEST`; **compilar no equivale a verificar la transacción**.

## Ejecutar en VS Code / Windows

Requisitos: SDK .NET 10, extensión C# y SQL Server LocalDB o una instancia de desarrollo.
Desde la raíz del repositorio:

```powershell
dotnet restore tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj
dotnet dev-certs https --trust
dotnet run --project backend/Synchro.Api --launch-profile https
```

Las pruebas antiguas conservan sus conexiones `(localdb)\MSSQLLocalDB`, sus datos de prueba
y sus User Secrets. Configurar los secretos localmente y nunca versionarlos. No restaurar
información personal en fixtures compartidas ni apuntar conexiones de prueba a `SynchroDB`.

### Fixture nueva, separada de las bases originales

El esquema `backend/Synchro.Api/Database/Fixtures/EsquemaPrototipo.sql` es **mínimo** y exclusivo del
contrato de depósito; no reconstruye toda la aplicación ni permite ejecutar los 110 casos
antiguos. Usar otra instancia LocalDB (o contenedor SQL) dedicada a este contrato, con una
base vacía llamada `SynchroDB_Pruebas`. Por ejemplo, sin tocar `MSSQLLocalDB`:

```powershell
sqllocaldb create SYNCHRO_ContratoDeposito
sqllocaldb start SYNCHRO_ContratoDeposito
sqlcmd -S '(localdb)\SYNCHRO_ContratoDeposito' -E -b -Q "IF DB_ID(N'SynchroDB_Pruebas') IS NULL CREATE DATABASE SynchroDB_Pruebas;"
$env:SYNCHRO_SQL_DEPOSITO_TEST='Server=(localdb)\SYNCHRO_ContratoDeposito;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj --filter Category=SqlAislado
Remove-Item Env:SYNCHRO_SQL_DEPOSITO_TEST
```

La confianza de certificado del ejemplo se limita a esta LocalDB desechable. Para un servidor
remoto usar certificado válido y `TrustServerCertificate=False`. Introducir credenciales SQL
por mecanismos seguros; no pegarlas en el chat, comandos guardados ni archivos versionados.

Las pruebas aplican lotes `GO` con parada al primer error. La inicialización rechaza bases con
tablas preexistentes que no tengan su marca de fixture. Conservan la estructura para repetir
la ejecución y eliminan solo sus registros sintéticos. No borran bases existentes.

## Bloqueos externos observados

- Linux no puede usar LocalDB del equipo Windows.
- Docker está disponible, pero la descarga oficial de SQL Server fue rechazada al redirigir
  a `centralus.data.mcr.microsoft.com` (HTTP 403). Hace falta permitir ese dominio de descarga
  en el entorno antes de ejecutar estas pruebas aquí.
- No se han configurado credenciales/plantillas de WhatsApp ni existe un proveedor facial
  confiable. No se enviaron mensajes reales ni se habilitaron operaciones sensibles.
- El guardado del borrador de entorno ya falló durante el onboarding; no se presume persistido.

## Continuación sin repetir la auditoría

1. Ejecutar los casos `SqlAislado` y corregir cualquier incompatibilidad real de SQL.
2. Crear el enrolamiento/vínculo de participantes desde identidad y teléfono comprobados;
   ningún ID o booleano del navegador puede constituir evidencia.
3. Integrar emisión de PIN y outbox de WhatsApp en la transacción; luego conectar el endpoint
   de terminal con HTTPS, CSRF y la terminal de la sesión. Mantener bloqueo facial hasta disponer
   del verificador confiable; añadir prueba end-to-end antes de habilitar.
4. Completar retiro con PIN, biometría, terminal y consumo único en la misma transacción.
5. Continuar WhatsApp, carga prepagada, frío/calor, administración y pruebas integrales en ese orden.

Este documento es un informe de avance y riesgos, no una certificación de seguridad ni una
indicación de que el backend solicitado está completo.

## Comprobación de la primera entrega

En esta rama la suite completa terminó con 131 casos: 95 aprobados, 32 fallidos y 4
pruebas SQL omitidas explícitamente. Se añadieron 17 casos unitarios aprobados; ninguna de
las 110 pruebas originales cambió de resultado. Una de las cuatro pruebas omitidas es
una teoría que ejecutará diez escenarios de rechazo cuando exista conexión SQL.
Compilación sin advertencias. Las transacciones SQL siguen sin validación de ejecución.

También se publicó la corrección independiente `fix/seguridad-operaciones-carga`, basada
en el mismo commit inicial y sin fusiones: autorización de terminal, HTTPS, CSRF y límite
por IP en las tres rutas POST heredadas. Sus diez pruebas nuevas aprobaron; la suite de
esa rama terminó con 88 aprobadas y los mismos 32 fallos iniciales. Consultar su documento
`docs/SEGURIDAD-CARGA.md`. Las ramas aún no están combinadas.

El push de ambas ramas funcionó, pero `gh api` recibió `Forbidden` en `api.github.com`.
No hay solicitudes de incorporación creadas. Permitir ese dominio para intentar crearlas
con la autenticación existente; no se presupone que haga falta un token nuevo. Base prevista:
`refactor/arquitectura-backend`. No se modificó `main` ni se fusionó ninguna rama.
