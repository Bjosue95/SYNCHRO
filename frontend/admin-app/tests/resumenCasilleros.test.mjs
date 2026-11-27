import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {resumirCasilleros} from "../src/services/resumenCasilleros.js";
const catalogo=(n,estado)=>Array.from({length:n},()=>({estado}));
test("incluye los 10 casilleros y los 8 espacios de carga",()=>{
  assert.deepEqual(resumirCasilleros(catalogo(10,"Disponible"),catalogo(8,"Disponible")),
    {total:18,disponibles:18,ocupados:0,resguardo:10,carga:8});
});
test("cuenta estados reales y no presenta los reservados como disponibles u ocupados",()=>{
  assert.deepEqual(resumirCasilleros([...catalogo(8,"Disponible"),{estado:"Reservado"},{estado:"Ocupado"}],
    [...catalogo(6,"Disponible"),{estado:"Ocupado"},{estado:"Ocupado"}]),
    {total:18,disponibles:14,ocupados:3,resguardo:10,carga:8});
  assert.equal(resumirCasilleros([],[]).total,0);
});
test("no muestra un total parcial si falla una consulta",()=>{
  assert.equal(resumirCasilleros(catalogo(10,"Disponible"),null),null);
  assert.equal(resumirCasilleros(null,catalogo(8,"Disponible")),null);
});
test("el resumen consulta ambos catálogos y comparte tipografía con los KPI",()=>{
  const vista=readFileSync(new URL("../src/views/ResumenView.vue",import.meta.url),"utf8");
  const css=readFileSync(new URL("../src/redisenio-admin.css",import.meta.url),"utf8");
  assert.match(vista,/apiAdmin\("\/api\/admin\/carga\/espacios"\)/);
  assert.match(vista,/resumirCasilleros\(casilleros\.value, espaciosCarga\.value\)/);
  assert.match(vista,/class="eyebrow">CASILLEROS<\/p>/);
  assert.match(css,/\.admin-page \.kpi-card strong,\s*\.admin-page \.inventory-number strong \{/);
});
