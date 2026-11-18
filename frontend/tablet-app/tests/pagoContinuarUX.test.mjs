import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const leer = ruta =>
  readFileSync(new URL(ruta, import.meta.url), "utf8");

test("pago conserva tres opciones y el cuarto espacio libre", () => {
  const codigo = leer("../src/views/SeleccionMetodoPagoView.vue");

  for (const metodo of ["Efectivo", "Tarjeta", "NFC"]) {
    assert.ok(codigo.includes(metodo));
  }

  assert.match(codigo, /espacio-vacio/);
  assert.match(codigo, /seleccionada: seleccion === metodo.codigo/);
  assert.match(codigo, /aria-pressed/);
});

test("Continuar exige una seleccion", () => {
  const codigo = leer("../src/views/SeleccionMetodoPagoView.vue");

  assert.match(codigo, /:disabled="!seleccion"/);
  assert.match(codigo, /registrarMetodoPagoRetiro/);
  assert.match(codigo, /\/retirar\/pago\/resumen/);
  assert.doesNotMatch(codigo, /finalizarRetiroPersonalPagado/);
});

test("vista previa permite consultar el resumen sin cobrar", () => {
  const codigo = leer("../src/views/SeleccionMetodoPagoView.vue");
  const rutas = leer("../src/preview/rutas.js");
  const visor = leer("../src/preview/views/PantallaView.vue");

  assert.match(codigo, /\/vista-previa\/pago-resumen/);
  assert.match(rutas, /pago-resumen/);
  assert.match(visor, /data-preview-permitido/);
});

test("el resumen mantiene el flujo del backend", () => {
  const codigo = leer("../src/views/RetiroPagoView.vue");

  assert.match(codigo, /Resumen de tu consumo/);
  assert.match(codigo, /finalizarRetiroPersonalPagado/);
});