import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { evaluarFortalezaContrasena as evaluar } from "../src/services/fortalezaContrasena.js";
import { contrasenaValida } from "../src/services/validaciones.js";

test("estado vacío y mínimo obligatorio coherentes con el registro",()=>{
  assert.equal(evaluar("").nivel,0);
  for(const valor of ["Ab9!x", "Zr!q9-Lm2xy", "x".repeat(129)]) {
    assert.ok(evaluar(valor).nivel<=2);
    assert.equal(evaluar(valor).reglas[0].cumple,false);
    assert.equal(contrasenaValida(valor),false);
  }
  assert.equal(evaluar("abcdefghijkL").reglas[0].cumple,true);
});
test("penaliza claves comunes, repeticiones, secuencias y datos del titular",()=>{
  for(const valor of ["Password12345!", "A1!A1!A1!A1!A1!", "123456789012", "Synchro2026!!", "a".repeat(30)]) assert.equal(evaluar(valor).nivel,1);
  assert.equal(evaluar("PxAbcdef!8x9").nivel,2);
  assert.equal(evaluar("LunaRocaUser123!","rocauser").nivel,1);
  assert.equal(evaluar("Zp!99990001-Ab","otro","+50499990001").nivel,1);
});
test("reconoce contraseñas variadas y frases largas sin imponer símbolos",()=>{
  assert.equal(evaluar("M7!pK4%zL9&bW2?rxF5@").nivel,5);
  const frase="bruma cedro nogal puente cantera";
  assert.equal(evaluar(frase).nivel,5);
  assert.equal(evaluar(frase).reglas.find(r=>r.clave==="simbolo").cumple,false);
  assert.equal(contrasenaValida(frase),true);
  assert.equal(evaluar("Árbol Ñandú ٧ + luz").reglas.filter(r=>r.cumple).length,5);
});
test("solo el registro presenta el medidor y conserva la iconografía propia",()=>{
  const view=readFileSync(new URL("../src/views/AccesoView.vue",import.meta.url),"utf8");
  const widget=readFileSync(new URL("../src/components/ui/PasswordStrength.vue",import.meta.url),"utf8");
  assert.match(view,/<PasswordStrength v-if="esRegistro && formulario.contrasena"/);
  assert.match(view,/autocomplete="[^"]*"/);
  assert.match(widget,/role="meter"/);
  assert.match(widget,/Recomendaciones opcionales/);
  assert.match(widget,/nombre="check"/);
  assert.match(widget,/prefers-reduced-motion/);
  assert.doesNotMatch(widget,/\bfetch\s*\(|localStorage|sessionStorage|console\./);
});
