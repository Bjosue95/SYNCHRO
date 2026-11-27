import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const leer=p=>readFileSync(new URL(p,import.meta.url),"utf8").replace(/\r\n/g,"\n");
test("recogida conserva titulo superior, tarjeta adaptable y acciones en su propia fila",()=>{
  const v=leer("../src/views/DepositoView.vue");
  assert.match(v,/\.deposito-pantalla\.retiro-web \.deposito-contenedor \{[\s\S]*?width: min\(100%, 1060px\);\s*height: auto;\s*min-height: 100%/);
  assert.match(v,/\.retiro-web \.deposito-acciones \{\s*grid-column: 1 \/ -1; display: grid/);
  assert.match(v,/Tiempo máximo disponible/);
  assert.match(v,/formatearTiempoDeposito\(segundosAgregados\)/);
});
test("el permiso personal de diez minutos viene del backend y se respeta en tablet",()=>{
  const servidor=leer("../../../backend/Synchro.Api/Endpoints/RetiroFacialManualEndpoints.cs");
  const pin=leer("../src/views/RetiroPinView.vue");
  assert.match(servidor,/autorizacionPago,\s*vigenciaSegundos = 600/);
  assert.match(pin,/Number\(respuesta\.vigenciaSegundos\) \|\| 300/);
  assert.match(pin,/orden,\s*venceAutorizacionPago/);
});
