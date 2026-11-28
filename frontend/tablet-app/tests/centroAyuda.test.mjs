import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";
import { temasAyuda } from "../src/services/temasAyuda.js";
test("seis temas claros con pasos y los iconos reales de ambos temas",()=>{
  assert.equal(temasAyuda.length,6);
  assert.equal(new Set(temasAyuda.map(t=>t.id)).size,6);
  for (const tema of temasAyuda) {
    assert.equal(tema.pasos.length,4);
    assert.ok(tema.nota && tema.resumen);
    for (const sufijo of [".svg","-oscuro.svg"])
      assert.ok(existsSync(new URL("../public"+tema.icono+sufijo,import.meta.url)));
  }
  assert.match(temasAyuda.find(t=>t.id==="carga").pasos.join(" "),/6 dígitos/);
  assert.match(temasAyuda.find(t=>t.id==="reservas").pasos.join(" "),/Frío o Calor/);
});
test("ayuda modal no navega ni cambia la operación y mantiene acciones fijas",()=>{
  const fuente=readFileSync(new URL("../src/components/HelpDialog.vue",import.meta.url),"utf8");
  assert.match(fuente,/showModal\(\)/);
  assert.match(fuente,/defineExpose\(\{ abrir, cerrar \}\)/);
  assert.match(fuente,/@keydown.stop/);
  assert.match(fuente,/\.ayuda-cuerpo\s*\{[^}]*overflow-y:auto/);
  assert.match(fuente,/\.ayuda-pie\s*\{[^}]*flex:none/);
  assert.doesNotMatch(fuente,/router\.(push|replace)|fetch\(|sessionStorage|localStorage/);
});
