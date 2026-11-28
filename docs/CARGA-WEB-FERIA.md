# SYNCHRO | Capacidad web para feria (pruebas)
No es certificación de capacidad. La meta es 100 visitantes concurrentes y ensayo hasta 200.
## Activar modo feria
Solo Development, con los endpoints web existentes restringidos a SynchroDB_Pruebas.
Desde la raíz, en la terminal del backend, tras detenerlo con Ctrl+C:
```powershell
$env:ClientesWeb__ModoFeria = 'true'
& .\tools\Iniciar-BackendCargaSegura.ps1
```
Para volver a los límites normales, detener backend, Remove-Item Env:\ClientesWeb__ModoFeria y reiniciar con el mismo script.
Mantener acceso controlado; no publicar este entorno de desarrollo en internet.
## Límites
Presupuesto por IP vista por el backend: CSRF 1200/min, registro 300/min, login 600/min, sesiones 2400/min, reservas lectura 2400/min y escritura 600/min. No se confía en cabeceras de IP que el visitante puede falsificar. El proxy puede agrupar IPs: el presupuesto aumentado permite compartirlo, no identifica cada dispositivo. Registro/login comparten 8 trabajos activos y cola FIFO de 240; 5 intentos de login por nombre/minuto (también con mayúsculas). Mantener CSRF, HTTPS y hash. Admin y tablet conservan sus cuotas. Límites de acceso originales si modo feria desactivado.
## Pruebas sin tocar SQL
```powershell
dotnet test .\tests\Synchro.Api.Tests\Synchro.Api.Tests.csproj --filter "FullyQualifiedName~CargaClientesWebHttpTests|FullyQualifiedName~ClientesWebHttpTests" --logger "console;verbosity=detailed"
```
El ensayo HTTP usa servidor de pruebas en proceso y repositorio en memoria; usa contraseñas hasheadas, cookies separadas, CSRF, alta, logout, login y consulta de identidad. Mide total, p95 y máximo, no representa red Wi-Fi ni SQL real.
## Prueba SQL optativa
Requiere una copia existente y marcada de SynchroDB_Pruebas en (localdb)\SYNCHRO_Suite_Test. NO utilizar SYNCHRO_Aislado. El script/test no crea ni restaura la copia. Las altas son ficticias y se limpian por coincidencia exacta del usuario y teléfono generado.
```powershell
$env:SYNCHRO_SQL_DEPOSITO_TEST = 'Server=(localdb)\SYNCHRO_Suite_Test;Database=SynchroDB_Pruebas;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
$env:SYNCHRO_CARGA_SQL = '1'
dotnet test .\tests\Synchro.Api.Tests\Synchro.Api.Tests.csproj --filter "FullyQualifiedName~CargaClientesWebSqlTests" --logger "console;verbosity=detailed"
Remove-Item Env:\SYNCHRO_CARGA_SQL -ErrorAction SilentlyContinue
```
El ensayo SQL prueba altas e inicios con 8 trabajos activos y 25/50/100/200 tareas por ronda; no es prueba HTTP completa con SQL ni genera reservas. Prueba existente de exclusión de casillero: ReservasPrototipoSqlTests.PrototipoNoSobreReservaYLiberaAlCancelarOVencer (revisar fixture antes de ejecutar). Aún se debe medir en equipo de feria, con red y todos los servicios abiertos. No inventar disponibilidad superior al inventario.
