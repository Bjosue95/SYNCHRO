import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const leer=p=>readFileSync(new URL("../src/"+p,import.meta.url),"utf8");

test("la estación principal se consulta desde configuración y la antigua ruta redirige",()=>{
 const rutas=leer("router/index.js"),menu=leer("layouts/AdminLayout.vue"),vista=leer("views/ConfiguracionView.vue");
 assert.doesNotMatch(menu,/ruta: "\/admin\/terminales"/);
 assert.match(rutas,/path: "terminales", name: "terminales", redirect: \{name:"configuracion",query:\{seccion:"estacion"\}\}/);
 assert.match(vista,/apiAdmin\("\/api\/admin\/terminales\/vinculacion\/listar"\)/);
 assert.match(vista,/filter\(t=>t\.codigo==="SYNCHRO-DEMO-01"\)/);
 assert.match(vista,/terminalPrincipal\.activa/);
 assert.doesNotMatch(vista,/\/api\/admin\/terminales\/vinculacion\/aprobar/);
});
test("se conserva la infraestructura de vinculación y sus permisos",()=>{
 const backend=readFileSync(new URL("../../../backend/Synchro.Api/Endpoints/AprobacionTerminalEndpoints.cs",import.meta.url),"utf8");
 assert.match(backend,/RequireAuthorization\("SoloAdministradores"\)/);
 assert.match(backend,/FechaExpiracion\s*>/);
 assert.match(backend,/\/api\/admin\/terminales\/vinculacion\/aprobar/);
});
