import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const leer=p=>readFileSync(new URL("../src/"+p,import.meta.url),"utf8");

test("solo administradores pueden aprobar sesiones de terminal",()=>{
  const rutas=leer("router/index.js");
  const menu=leer("layouts/AdminLayout.vue");
  const vista=leer("views/TerminalesView.vue");
  const backend=readFileSync(new URL("../../../backend/Synchro.Api/Endpoints/AprobacionTerminalEndpoints.cs",import.meta.url),"utf8");
  assert.match(rutas,/path: "terminales", name: "terminales"/);
  assert.match(menu,/ruta: "\/admin\/terminales"/);
  assert.match(vista,/\/api\/admin\/terminales\/vinculacion\/listar/);
  assert.match(vista,/\/api\/admin\/terminales\/vinculacion\/aprobar/);
  assert.match(vista,/X-CSRF-TOKEN/);
  assert.match(vista,/tipo="submit"/);
  assert.match(backend,/RequireAuthorization\("SoloAdministradores"\)/);
  assert.match(backend,/FechaExpiracion\s*>/);
});
