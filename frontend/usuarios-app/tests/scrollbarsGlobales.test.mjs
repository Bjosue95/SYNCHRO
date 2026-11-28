import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const leer = ruta => readFileSync(new URL("../../"+ruta,import.meta.url),"utf8");

test("las tres interfaces importan el mismo estilo global de desplazamiento",()=>{
  const rutas=["usuarios-app/src/styles/syn-scrollbars.css","admin-app/src/syn-scrollbars.css","tablet-app/src/styles/syn-scrollbars.css"];
  const estilos=rutas.map(leer);
  assert.equal(estilos[0],estilos[1]);
  assert.equal(estilos[0],estilos[2]);
  for (const app of ["usuarios-app","admin-app","tablet-app"])
    assert.match(leer(app+"/src/main.js"),/import "\.\/(?:styles\/)?syn-scrollbars\.css"/);
  assert.match(estilos[0],/:root, :root \*\s*\{[\s\S]*?scrollbar-width: thin;[\s\S]*?scrollbar-color: var\(--syn-scroll-thumb\) var\(--syn-scroll-track\)/);
  assert.match(estilos[0],/@supports not \(\(scrollbar-width: thin\)/);
  assert.match(estilos[0],/\.synchro-app\.tema-oscuro/);
  assert.match(estilos[0],/forced-colors: none/);
  assert.doesNotMatch(estilos[0],/overflow\s*:|scrollbar-width:\s*none|display:\s*none/);
});
test("reservas e ilustración no sustituyen el estilo global",()=>{
  for (const ruta of ["usuarios-app/src/views/PreReservaView.vue","usuarios-app/src/components/ui/LockerJourney.vue"])
    assert.doesNotMatch(leer(ruta),/scrollbar-(?:width|color)\s*:/);
});
