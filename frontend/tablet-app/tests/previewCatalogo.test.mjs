import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const leer = ruta =>
  readFileSync(new URL(ruta, import.meta.url), "utf8");

test("catalogo solo disponible en desarrollo", () => {
  const codigo = leer("../src/preview/rutas.js");
  assert.match(codigo, /import\.meta\.env\.DEV/);
  assert.match(codigo, /MODE === "demo"/);
});

test("el visor bloquea acciones", () => {
  const codigo = leer("../src/preview/views/PantallaView.vue");
  assert.match(codigo, /@click\.capture="bloquear"/);
  assert.match(codigo, /@submit\.capture="bloquear"/);
});

test("vistas con efectos automaticos tienen proteccion", () => {
  for (const nombre of [
    "SeleccionTamanoView",
    "SeleccionCasilleroView",
    "VerificacionWhatsappView",
    "DepositoView",
    "RetiroPinView"
  ]) {
    const codigo = leer(`../src/views/${nombre}.vue`);
    assert.match(codigo, /import\.meta\.env\.DEV && propsVistaPrevia/);
  }
});