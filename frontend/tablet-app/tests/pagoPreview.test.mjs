import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const leer = ruta =>
  readFileSync(new URL(ruta, import.meta.url), "utf8");

test("tres metodos de pago disponibles", () => {
  const codigo = leer("../src/components/pagos/SelectorMetodoPago.vue");

  for (const metodo of ["Efectivo", "Tarjeta", "NFC"]) {
    assert.ok(codigo.includes(metodo));
  }

  assert.match(codigo, /aria-pressed/);
  assert.match(codigo, /update:modelValue/);
});

test("la pantalla real sigue protegiendo el backend", () => {
  const codigo = leer("../src/views/RetiroPagoView.vue");

  assert.match(codigo, /if \(esVistaPrevia\) return;/);
  assert.match(codigo, /finalizarRetiroPersonalPagado/);
  assert.match(codigo, /nombreMetodoPagoDemo/);
  assert.match(codigo, /metodoEjemplo/);
  assert.match(codigo, /avanzarDemostracion/);
});

test("el visor solo habilita opciones de pago", () => {
  const codigo = leer("../src/preview/views/PantallaView.vue");

  assert.match(codigo, /"pago", "pago-resumen", "retiro-carga-pago"/);
  assert.match(codigo, /\[data-preview-permitido\]/);
  assert.match(codigo, /evento\.preventDefault\(\)/);
});

test("retirar solo se completa despues de confirmacion protegida", () => {
  const codigo = leer("../src/views/RetiroPagoView.vue");

  // El resumen y la pantalla final son estados distintos.
  assert.match(codigo, /v-if="mostrarPantallaFinal"/);
  assert.match(codigo, /esVistaPrevia \? faseDemostracion\.value === "completado" : completado\.value/);
  assert.match(codigo, /Retiro completado/);
  assert.match(codigo, /Casillero/);
  assert.match(codigo, /obtenerMetodoPagoRetiro/);

  // Nunca se concede la finalizacion local sin la respuesta del servidor.
  assert.match(codigo, /const resultado = await finalizarRetiroPersonalPagado/);
  assert.match(codigo, /completado\.value = true/);
  assert.match(codigo, /if \(esVistaPrevia\) return;/);
  // NFC comparte las validaciones protegidas.
  assert.match(codigo, /nombreMetodoConfirmado/);
  assert.match(codigo, /comprobarPago/);
});
