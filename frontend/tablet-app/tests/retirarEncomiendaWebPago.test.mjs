import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const fuente = ruta => readFileSync(new URL(ruta, import.meta.url), "utf8");

test("la Tablet permite elegir retiro de encomienda y conserva flujos actuales", () => {
  const router = fuente("../src/router/index.js");
  const retiro = fuente("../src/views/RetirarView.vue");
  assert.match(router, /path: "\/retirar\/encomienda"/);
  assert.match(router, /RetiroEncomiendaWebView\.vue/);
  assert.match(retiro, /router\.push\("\/retirar\/encomienda"\)/);
  assert.match(retiro, /iniciarRetiro\(\)/);
  assert.match(retiro, /"dispositivo"/);
});

test("pago del destinatario usa componente original de la Tablet y necesita aprobación", () => {
  const vista = fuente("../src/views/RetiroEncomiendaWebView.vue");
  const selector = fuente("../src/components/pagos/SelectorMetodoPago.vue");
  assert.match(vista, /<SelectorMetodoPago v-model="metodo" :oscuro="oscuro"/);
  assert.match(selector, /"Efectivo"/);
  assert.match(selector, /"Tarjeta"/);
  assert.match(selector, /"NFC"/);
  assert.match(vista, /fase\.value="espera"/);
  assert.match(vista, /pagoConfirmado===true/);
  assert.match(vista, /confirmarRetiroReservaWeb\(/);
  assert.match(vista, /consentimiento\.value/);
  assert.match(vista, /detenerCamara\(\)/);
  assert.doesNotMatch(vista, /sessionStorage|localStorage/);
});

test("las peticiones de cobro solo transmiten PIN y método, nunca información de tarjetas", () => {
  const api = fuente("../src/services/reservasWebTerminalApi.js");
  assert.match(api, /solicitarPagoReservaWebTablet\(id, pin, metodo\)/);
  assert.match(api, /enviar\(`\/\$\{id\}\/pago\/solicitar`, \{ pin, metodo \}\)/);
  assert.match(api, /consultarPagoReservaWebTablet/);
  assert.doesNotMatch(api, /numeroTarjeta|codigoSeguridad|fechaVencimiento|cvv/);
});
