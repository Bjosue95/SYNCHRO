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

test("encomiendas reutilizan captura, PIN, pago y recogida",()=>{
  const camara=fuente("../src/views/VerificacionFacialView.vue");
  const pin=fuente("../src/views/RetiroPinView.vue");
  const router=fuente("../src/router/index.js");
  const servicio=fuente("../src/services/retiroEncomiendaSesion.js");
  const selector=fuente("../src/views/SeleccionMetodoPagoView.vue");
  const pago=fuente("../src/views/RetiroPagoView.vue");
  assert.match(camara,/registrarFotoRetiroEncomienda\(foto\[1\]\)/);
  assert.match(camara,/router\.push\("\/retirar\/encomienda\/pin"\)/);
  assert.match(router,/path: "\/retirar\/encomienda\/pin", component: \(\) => import\("\.\.\/views\/RetiroPinView\.vue"\)/);
  assert.match(pin,/if\(orden\.pagoConfirmado===true\)/);
  assert.match(pin,/else if\(orden\.pagoAdicionalRequerido===true\)/);
  assert.match(selector,/elegirPagoEncomienda\(seleccion\.value\)/);
  assert.match(pago,/consultarPagoEncomienda\(\)/);
  assert.match(pago,/await prepararRecogidaEncomienda\(\)/);
  assert.match(servicio,/if\(pago\.pagoConfirmado!==true\)throw/);
  assert.match(servicio,/capturarRetiroReservaWeb\(id,/);
  assert.match(servicio,/autorizarRecogidaEncomienda\(id,s\.pin,evidencia\.evidenciaId\)/);
  assert.match(servicio,/finalizarRecogidaEncomienda\(id,autorizacion\)/);
  assert.doesNotMatch(servicio,/sessionStorage|localStorage/);
});

test("las peticiones de cobro solo transmiten PIN y método, nunca información de tarjetas", () => {
  const api = fuente("../src/services/reservasWebTerminalApi.js");
  assert.match(api, /solicitarPagoReservaWebTablet\(id, pin, metodo\)/);
  assert.match(api, /enviar\(`\/\$\{id\}\/pago\/solicitar`, \{ pin, metodo \}\)/);
  assert.match(api, /consultarPagoReservaWebTablet/);
  assert.doesNotMatch(api, /numeroTarjeta|codigoSeguridad|fechaVencimiento|cvv/);
});
