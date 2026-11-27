import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const s=readFileSync(new URL("../src/views/CobrosView.vue",import.meta.url),"utf8");
test("los tres servicios tienen pestañas y contador",()=>{
 assert.match(s,/class="cobros-tabs"/);
 assert.match(s,/v-if="categoria==='personal'"/);
 assert.match(s,/v-else-if="categoria==='carga'"/);
 assert.match(s,/nombre:"Reservas web y encomiendas"/);
 assert.match(s,/window.setInterval\(actualizarAlVolver,5000\)/);
});
test("la modalidad es la elegida por el cliente",()=>{
 assert.match(s,/const metodo = item\?\.metodoPago/);
 assert.equal([...s.matchAll(/metodoVisible\(item\.metodoPago\)\s*\}\}/g)].length,3);
 assert.doesNotMatch(s,/v-model="metodos\[|Seleccionar método/);
 assert.match(s,/item\.codigoCasillero/);
 assert.match(s,/item\.codigoEspacio/);
});
