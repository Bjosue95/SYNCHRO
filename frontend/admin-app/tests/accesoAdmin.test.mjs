import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync,existsSync } from "node:fs";
const leer=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");
test("acceso conserva la autenticación y no almacena credenciales",()=>{
 const s=leer("src/views/LoginView.vue");
 assert.match(s,/apiAdmin\("\/api\/admin\/login"/);
 assert.match(s,/await consultarSesion\(\)/);
 assert.match(s,/await router\.replace\(destinoSeguro\(\)\)/);
 assert.match(s,/autocomplete="current-password"/);
 assert.match(s,/aria-controls="admin-clave"/);
 assert.doesNotMatch(s,/localStorage|sessionStorage/);
});
test("diseño adaptable, acceso claro y favicon original",()=>{
 const s=leer("src/layouts/AuthLayout.vue");
 assert.doesNotMatch(s,/data-acceso-tema|auth-theme|cambiarTema/);
 assert.doesNotMatch(s,/localStorage|sessionStorage/);
 assert.match(s,/@media\(max-width:820px\)/);
 const h=leer("index.html");
 for(const archivo of ["isotipo-fondo-claro.png","isotipo-fondo-oscuro.png"]){
   assert.ok(h.includes('href="/assets/'+archivo+'"'));
   assert.ok(existsSync(new URL("../public/assets/"+archivo,import.meta.url)));
 }
});
