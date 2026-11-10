# Preparación reproducible de la base aislada

El instalador usa los scripts versionados; no contiene datos del propietario y no ejecuta
mensajes WhatsApp. Está destinado exclusivamente a `SynchroDB_Pruebas` vacía o marcada
por la fixture. No crea, borra ni reinicia bases. No representa el esquema histórico completo
que falta en el repositorio: las verificaciones telefónicas y procedimientos antiguos deben
validarse/actualizarse por separado en la laptop. Los módulos nuevos de depósito/retiro,
cuentas, rostro simulado, outbox, servicios temporales y roles sí están incluidos en el plan.

## Windows / VS Code

Desde la raíz del repositorio con SDK .NET 10:

```powershell
git fetch origin
git switch feat/issue-1-encomiendas-end-to-end
git pull --ff-only origin feat/issue-1-encomiendas-end-to-end
dotnet restore tools/Synchro.AdminSetup/Synchro.AdminSetup.csproj
dotnet run --project tools/Synchro.AdminSetup -- --plan-prototipo
```

Crear una base **vacía nueva** `SynchroDB_Pruebas` en la instancia de pruebas elegida, sin
copiar datos reales. Si ese nombre ya contiene datos históricos, conservarlos y respaldarlos;
el instalador los rechaza si no tienen su marcador. No eliminar ni resetear esa base para
forzar la instalación. Revisar con el propietario una instancia aislada para el prototipo.

Configurar la conexión en la sesión local (sustituir instancia, sin guardar secretos en Git):

```powershell
$env:SYNCHRO_SQL_DEPOSITO_TEST = 'Server=(localdb)\MSSQLLocalDB;Database=SynchroDB_Pruebas;Integrated Security=true;TrustServerCertificate=true'
dotnet run --project tools/Synchro.AdminSetup -- --preparar-prototipo
if ($LASTEXITCODE -ne 0) { throw 'Instalación detenida; revisar antes de continuar.' }
dotnet test tests/Synchro.Api.Tests/Synchro.Api.Tests.csproj --filter Category=SqlAislado
```

El instalador comprueba el catálogo y ausencia de AttachDbFilename antes de abrir conexión,
confirma el nombre de base en SQL, obtiene un bloqueo exclusivo de instalación, carga todos
los archivos antes de escribir y se detiene al primer error. Cada script conserva sus
transacciones/idempotencia. Muestra únicamente nombre de script y SHA256 del código;
no imprime cadena de conexión, teléfonos ni credenciales. Una segunda ejecución aplica
los scripts idempotentes sin borrar datos. No ejecutar dos suites SQL concurrentes sobre
la misma base; la fixture reutiliza un conjunto aislado de tablas y limpia sus propios registros.

## Actualizar una base histórica de desarrollo

No ejecutar la fixture sobre ella. Revisar respaldo/esquema inicial y aplicar únicamente
migraciones compatibles con parada al primer error. El orden de referencia se consulta
con `--plan-prototipo`, omitiendo el primer archivo de Fixtures. La migración ClientesWeb
va antes de DepositoEncomiendaAtomico. Los registros anteriores sin cuenta permanecen
sin vincular y no habilitan nuevas entregas; no fabricar cuentas de clientes para migrarlos.
El esquema mínimo no debe sustituir el esquema ni los procedimientos del propietario.

## Configuración de aplicación

Usar Development y `ConnectionStrings:SynchroDB` apuntando a la base aislada. Configurar
claves HMAC por user-secrets/variables de entorno, nunca archivos versionados. Consultar
ENCOMIENDAS-CLIENTES-Y-ROSTRO-SIMULADO.md y SERVICIOS-TEMPORALES.md. WhatsApp outbox
permanece deshabilitado hasta configurar credenciales/plantillas y verificar la integración.
AdminSetup sin argumentos conserva el alta inicial interactiva del administrador; su
conexión usa `SYNCHRO_DB_CONNECTION`, que también debe apuntar a la base de pruebas.

Cloud: se comprobó compilación, plan, archivos copiados y guardias sin conectar a SQL.
No se ha comprobado ejecución de migraciones en SQL Server; la prueba de instalación
real y la suite SQL completa están pendientes de la laptop.
