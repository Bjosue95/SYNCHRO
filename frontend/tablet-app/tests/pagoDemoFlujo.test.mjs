import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  METODOS_PAGO_DEMO,
  normalizarMetodoPagoDemo,
  nombreMetodoPagoDemo,
  etiquetaLecturaDemo,
  siguienteFasePagoDemo
} from "../src/preview/pagoDemo.js";

const leer = ruta => readFileSync(new URL(ruta, import.meta.url), "utf8");

test("demo admite los tres métodos, sin seleccionar uno inexistente", () => {
  assert.deepEqual(METODOS_PAGO_DEMO, ["Efectivo", "Tarjeta", "NFC"]);
  for (const metodo of METODOS_PAGO_DEMO) {
    assert.equal(normalizarMetodoPagoDemo(metodo), metodo);
    assert.ok(etiquetaLecturaDemo(metodo).startsWith("Simular"));
  }
  for (const invalido of [null, "NfcSimulado", "Otro", ["NFC"], ""]) {
    assert.equal(normalizarMetodoPagoDemo(invalido), null);
  }
  assert.equal(nombreMetodoPagoDemo("NFC"), "NFC del celular");
});

test("demo avanza de resumen a pago, operador y retiro completado, sin retroceder", () => {
  for (const metodo of METODOS_PAGO_DEMO) {
    let fase = "resumen";
    for (const esperado of ["lectura", "espera", "confirmado", "completado"]) {
      fase = siguienteFasePagoDemo(fase);
      assert.equal(fase, esperado, `${metodo}: debería llegar a ${esperado}`);
    }
    assert.equal(siguienteFasePagoDemo(fase), "completado");
  }
});

test("selector conserva elección mediante query sin pagos ni confirmación real", () => {
  const codigo = leer("../src/views/SeleccionMetodoPagoView.vue");
  assert.match(codigo, /query: \{ metodo: seleccion\.value \}/);
  assert.match(codigo, /<span>Continuar<\/span>/);
  assert.doesNotMatch(codigo, /Cambiar m[eé?]todo de pago/);
  assert.doesNotMatch(codigo, /finalizarRetiroPersonalPagado/);
});

test("visor valida el método y limita los botones permitidos", () => {
  const codigo = leer("../src/preview/views/PantallaView.vue");
  assert.match(codigo, /normalizarMetodoPagoDemo\(route\.query\.metodo\)/);
  assert.match(codigo, /\[data-preview-permitido\]/);
  assert.match(codigo, /evento\.preventDefault\(\)/);
});

test("flujo preview usa animación y tarjeta final originales sin ejecutar cobros", () => {
  const codigo = leer("../src/views/RetiroPagoView.vue");
  assert.match(codigo, /v-else-if="mostrarAnimacionPago"/);
  assert.match(codigo, /v-if="mostrarPantallaFinal"/);
  assert.match(codigo, /Simular confirmación del operador/);
  assert.match(codigo, /Finalizar retiro \(demo\)/);
  assert.match(codigo, /30_confirmar\.svg/);
  assert.match(codigo, /Total simulado/);
  assert.match(codigo, /Disponible \(demo\)/);
  assert.doesNotMatch(codigo, /\? router\.push\('\/vista-previa\/pago'\)\s*: comprobarPago\(\)/);
  assert.match(codigo, /function avanzarDemostracion\(\)/);
  assert.match(codigo, /if \(!esVistaPrevia \|\| !orden\.value\) return;/);
  assert.match(codigo, /if \(esVistaPrevia\) return;/);
  assert.match(codigo, /if \(!pagoConfirmado\.value \|\| !orden\.value/);
  assert.match(codigo, /const resultado = await finalizarRetiroPersonalPagado/);
});
