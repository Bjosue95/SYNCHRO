import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { CLAVE_COOKIES, DURACION_COOKIES, leerPreferencias, crearPreferencias, persistirPreferencias } from "../src/services/preferenciasCookies.js";
const leer = ruta => readFileSync(new URL("../src/"+ruta, import.meta.url), "utf8");
function memoria() { const m=new Map(); return { getItem:k=>m.get(k)??null, setItem:(k,v)=>m.set(k,v) }; }
test("primera visita, elección y vencimiento de la preferencia", () => {
  const a=memoria();
  assert.equal(leerPreferencias(a, 1000), null);
  const d=crearPreferencias(false,1000);
  assert.equal(persistirPreferencias(a,d),true);
  assert.deepEqual(leerPreferencias(a,1001),d);
  assert.equal(leerPreferencias(a,1000+DURACION_COOKIES),null);
  persistirPreferencias(a,crearPreferencias(true,2000));
  assert.equal(leerPreferencias(a,2001).mapa,true);
  a.setItem(CLAVE_COOKIES,"{mal");
  assert.equal(leerPreferencias(a,2001),null);
  assert.equal(persistirPreferencias(null,d),false);
  assert.equal(leerPreferencias(null),null);
});
test("aviso global, decisiones equivalentes y política accesible",()=>{
  assert.match(leer("App.vue"),/<CookieNotice\s*\/>/);
  const aviso=leer("components/ui/CookieNotice.vue");
  assert.match(aviso,/guardarPreferencias\(false\)/);
  assert.match(aviso,/guardarPreferencias\(true\)/);
  assert.match(aviso,/guardarPreferencias\(opcionMapa\)/);
  assert.match(leer("router/index.js"),/path: "\/politica-de-cookies"/);
  assert.match(leer("components/landing/SiteFooter.vue"),/abrirPreferencias/);
  assert.match(leer("components/layout/AppShell.vue"),/abrirPreferencias/);
});
test("mapa no se inicializa sin permiso y se desmonta al retirarlo",()=>{
  const mapa=leer("components/landing/LocationsMap.vue");
  assert.match(mapa,/if \(!mapaPermitido.value \|\| preparando/);
  assert.match(mapa,/if \(desmontado \|\| !mapaPermitido.value/);
  assert.match(mapa,/watch\(mapaPermitido/);
  assert.match(mapa,/else retirarMapa\(\)/);
  assert.match(mapa,/mapa\?\.remove\(\)/);
});
