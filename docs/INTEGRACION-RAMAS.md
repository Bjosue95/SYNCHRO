# Integración de ramas del backend — 10 de noviembre de 2026

> Las reglas de encomiendas se actualizaron: [clientes web y rostro simulado](ENCOMIENDAS-CLIENTES-Y-ROSTRO-SIMULADO.md). Las referencias anteriores a visitantes o biometría profesional quedan sustituidas.

Rama de destino: `refactor/arquitectura-backend`.
Punto de partida local y remoto: `ed029fb2227c142dbf74b20ebb464cb7484230de`.
No se modificó `main`, no se eliminaron ramas y los commits/push de esta revisión
se realizaron exclusivamente en la rama de destino.

## Commits revisados e integrados

| Rama de origen | Commits incorporados a la ascendencia del destino | Contenido |
|---|---|---|
| `feat/encomiendas-transaccion-aislada` | `fee3ca7`, `89fda96` | Servicio y SQL de depósito atómico, aislamiento de bases, fixture, pruebas y documentación |
| `feat/notificaciones-y-servicios-prototipo` | Los anteriores y `00fae5f` | PIN ligado a uso/destinatario, depósito + PIN + outbox en una transacción, cifrado, trabajador, cliente Meta y conciliación de webhook |
| `fix/seguridad-operaciones-carga` | `69ae794`, `3bbecee` | Cookie de terminal, HTTPS, CSRF, límite de intentos, diez pruebas HTTP y documentación |

La rama remota de notificaciones seguía en `89fda96`; su copia local conservaba
`00fae5f` sin publicar en esa rama. Ese avance también se incluyó, sin hacer push
ni nuevos commits sobre la rama de origen. El merge de notificaciones es `d973238`;
incluye por ascendencia los dos commits de encomiendas. El merge de seguridad contiene
este informe y registra los dos commits de la rama de correcciones.

El código útil de estas ramas ya se había trasladado a `ed029fb`. Por eso no se
volvieron a aplicar sus parches ni se agregaron servicios, tablas o pruebas duplicadas:
se integró su historial con merges normales, conservando las versiones más completas
del destino. **No hay funcionalidad nueva adicional en esta revisión.**

## Conflictos y comprobación de cobertura

El único conflicto fue `add/add` en `docs/DESARROLLO-BACKEND.md`. Se conservó su contenido
completo y la referencia de la rama destino a `AVANCE-REFACTOR.md`. No hubo conflictos
pendientes en código, pruebas o migraciones.

Comparación de todos los archivos modificados desde `de977cd` por cada origen:

- Encomiendas: 8 archivos revisados; 3 idénticos y 5 evolucionados en el destino.
  Se conserva la transacción de aplicación del depósito para incluir PIN y outbox;
  también las ampliaciones del servicio, pruebas SQL, referencias de migraciones y nota documental.
  No se restaura la transacción SQL autónoma anterior, que excluiría el PIN y la notificación.
- Notificaciones (incluido `00fae5f`): 20 archivos revisados; 18 idénticos. Las dos diferencias
  son `Program.cs`, que además conserva la política de seguridad de carga, y la nota documental.
- Seguridad de carga: 5 archivos revisados; 4 idénticos. `Program.cs` conserva tanto la política
  de carga como los registros DI de PIN/notificaciones. Las tres rutas POST siguen usando
  el filtro y la política de autorización de terminal.

Después de resolver los merges, `git diff ed029fb -- backend tests tools` queda vacío.
La revisión no cambia la lógica que ya estaba en el destino. Los endpoints de depósito
siguen sin exponerse y el verificador facial sigue denegando mientras no haya proveedor confiable.

## Validación ejecutada

SDK .NET 10.0.403, Linux x64. Sin filtros ni exclusiones de la suite .NET:

```bash
dotnet build tools/Synchro.AdminSetup/Synchro.AdminSetup.csproj --no-restore -m:2
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj --no-restore \
  --logger 'trx;LogFileName=backend-completo.trx' \
  --results-directory /workspace/synchro-test-results/integracion-ramas-20261110
```

- API y herramienta administrativa: compilación aprobada, 0 errores y 0 advertencias.
- Suite completa: **187 casos, 151 aprobados, 32 fallidos y 4 omitidos**. Salida del comando: **1**.
- Las 110 pruebas originales se compararon por nombre y resultado con la ejecución inicial:
  conservan exactamente los mismos 78 aprobados y 32 fallidos. Sin regresiones observadas
  en esos resultados; esto no implica que los escenarios SQL estén validados.
- Los 73 casos nuevos sin SQL aprobaron: comandos de depósito (17), PIN/outbox (18), cliente
  WhatsApp (28) y seguridad HTTP de carga (10).

| Clase de pruebas | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| `AutenticacionTerminalRealTests` | 2 | 2 | 0 |
| `CodigoPinRetiroTests` | 2 | 0 | 0 |
| `ComparadorDescriptoresFacialesTests` | 10 | 0 | 0 |
| `ConfiguracionFacialServidorTests` | 2 | 0 | 0 |
| `ConsultaConsentimientoSqlTests` | 0 | 1 | 0 |
| `ConsultaEstadosReservaHttpTests` | 0 | 7 | 0 |
| `ConsultaReservaTerminalHttpTests` | 3 | 1 | 0 |
| `ConsumoEvidenciaFacialSqlTests` | 0 | 1 | 0 |
| `DepositoEncomiendaComandoTests` | 17 | 0 | 0 |
| `DepositoEncomiendaSqlTests` | 0 | 0 | 4 |
| `DetalleEncomiendaAutorizacionTests` | 0 | 3 | 0 |
| `DetalleEncomiendaTests` | 1 | 0 | 0 |
| `EmisionPinAisladoTests` | 0 | 1 | 0 |
| `EvidenciaFacialRemitenteTests` | 13 | 0 | 0 |
| `OutboxWhatsAppTests` | 18 | 0 | 0 |
| `PinRetiroConexionTests` | 1 | 2 | 0 |
| `PinRetiroDesarrolloAisladoTests` | 1 | 1 | 0 |
| `PinRetiroIntegracionTests` | 0 | 1 | 0 |
| `PoliticaDepositoEncomiendaTests` | 10 | 0 | 0 |
| `PreparadorEvidenciaFacialRemitenteTests` | 10 | 0 | 0 |
| `RechazosEvidenciaFacialSqlTests` | 0 | 10 | 0 |
| `ResultadoFacialTests` | 4 | 0 | 0 |
| `RevocacionTerminalTests` | 0 | 1 | 0 |
| `SeguridadCargaHttpTests` | 10 | 0 | 0 |
| `ValidacionDepositoEncomiendaTests` | 17 | 0 | 0 |
| `VerificadorFacialNoDisponibleTests` | 2 | 0 | 0 |
| `VinculacionTerminalCompletaTests` | 0 | 1 | 0 |
| `WhatsAppCloudServiceTests` | 28 | 0 | 0 |

### Fallos y omisiones

- 25 casos lanzan `PlatformNotSupportedException`: LocalDB no funciona en Linux.
- 5 requieren el archivo privado `secrets.json`, ausente en esta máquina.
- 2 fallan porque reciben HTTP 500 donde esperan 401/404: `AutenticacionTerminalRealTests.TerminalInexistente_DebeDevolver401`
  y `ConsultaReservaTerminalHttpTests.ReservaInexistente_DebeDevolver404`. Sus fábricas usan LocalDB;
  deben repetirse en Windows antes de considerar verificados esos comportamientos HTTP.
- 4 pruebas de `DepositoEncomiendaSqlTests` se omiten explícitamente por falta de
  `SYNCHRO_SQL_DEPOSITO_TEST`. Una es una teoría de diez escenarios que se expandirá cuando
  haya conexión. No se convirtió ningún fallo original en omitido.

El [resultado por cada caso](resultados/integracion-backend-20261110.json) incluye los 187 nombres,
estados, motivos de bloqueo, recuentos por clase y SHA-256 del TRX. No contiene credenciales,
valores de User Secrets ni cuerpos de mensajes. Los logs completos y el TRX se conservan
localmente en `/workspace/synchro-test-results/integracion-ramas-20261110/`.

### Pruebas externas a dotnet test

También se inventariaron los scripts PowerShell existentes:

- `tests/WhatsAppWebhookPruebas.ps1`
- `backend/Synchro.Api/tests/WhatsAppWebhookPruebas.ps1`
- `backend/Synchro.Api/tests/ProbarConsentimientoWhatsApp.ps1`
- `backend/Synchro.Api/tests/ProbarBloqueoConsentimientoWhatsApp.ps1`
- `backend/Synchro.Api/tests/ProbarVencimientoConsentimientoWhatsApp.ps1`
- `backend/Synchro.Api/tests/ProbarVencimientoPreReservaWhatsApp.ps1`

**No ejecutados**: no está instalado `pwsh` y dependen de LocalDB/configuración local y una API
HTTPS. Los archivos SQL de pruebas y migraciones tampoco se ejecutaron aisladamente.
Se respeta la indicación previa de ejecutar SQL posteriormente en la laptop del propietario.
No se tocaron datos de `SynchroDB` ni de las bases de pruebas.

## Pendientes conservados

Esta integración no completa el backend: siguen pendientes la validación SQL, el retiro
integrado, la vinculación de identidades y biometría confiable, carga prepagada, frío/calor
y administración/reportes. No se hicieron envíos reales a WhatsApp ni se activó la outbox.
Los pasos y configuración de la laptop están en [AVANCE-REFACTOR.md](AVANCE-REFACTOR.md)
y [DESARROLLO-BACKEND.md](DESARROLLO-BACKEND.md).

Se mantienen las tres ramas originales y el commit local pendiente, aun tras comprobar su
integración. No se solicitó ni se ejecutó su eliminación.
