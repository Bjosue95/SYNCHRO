import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
test("el menú identifica al usuario autenticado, sin nombre fijo",()=>{
 const s=readFileSync(new URL("../src/layouts/AdminLayout.vue",import.meta.url),"utf8");
 assert.match(s,/nombreUsuario\.value = sesion\.usuario;/);
 assert.match(s,/<strong :title="nombreUsuario">\{\{ nombreUsuario \|\| "Cargando usuario…" \}\}<\/strong>/);
 assert.match(s,/<small>\{\{ rol \}\}<\/small>/);
 assert.doesNotMatch(s,/<strong>Operador SYNCHRO<\/strong>/);
});
