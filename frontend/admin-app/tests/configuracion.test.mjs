import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const leer=p=>readFileSync(new URL(p,import.meta.url),"utf8");
const vista=leer("../src/views/ConfiguracionView.vue");
const backend=leer("../../../backend/Synchro.Api/Endpoints/AdminConfiguracionEndpoints.cs");
test("configuración conecta cuentas, tarifas, limpieza y respaldos",()=>{
 assert.match(vista,/\/api\/admin\/usuarios\//);
 assert.match(vista,/\/api\/admin\/configuracion\/tarifas/);
 assert.match(vista,/\/api\/admin\/configuracion\/limpieza/);
 assert.match(vista,/\/api\/admin\/configuracion\/respaldos/);
 assert.match(vista,/X-CSRF-TOKEN/);
 assert.match(backend,/RequireAuthorization\("SoloAdministradores"\)/);
 assert.match(backend,/AddEndpointFilter<OperacionTerminalSeguraFilter>/);
});
test("limpieza protege usos asociados, registros activos y fotos con extensión",()=>{
 assert.match(backend,/a\.UsoId IS NULL/);
 assert.match(backend,/u\.Estado NOT IN\(N'Ocupado',N'Reservado'\)/);
 assert.match(backend,/a\.ExtensionHastaUtc IS NULL OR a\.ExtensionHastaUtc<=@corte/);
 assert.match(backend,/datos\.Confirmacion!="ELIMINAR"/);
 assert.match(backend,/esperado!=n/);
 assert.match(backend,/IsolationLevel.Serializable/);
 assert.doesNotMatch(backend,/DELETE FROM dbo\.UsosCasillero|DELETE FROM dbo\.Cobros/);
});
test("respaldo verifica la copia y no expone un archivo descargable",()=>{
 assert.match(backend,/BACKUP DATABASE \[SynchroDB_Pruebas\] TO DISK=@ruta WITH COPY_ONLY,CHECKSUM/);
 assert.match(backend,/RESTORE VERIFYONLY FROM DISK=@ruta WITH CHECKSUM/);
 assert.doesNotMatch(backend,/Results\.File/);
});
