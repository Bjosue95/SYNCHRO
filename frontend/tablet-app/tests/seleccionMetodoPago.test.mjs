import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync, existsSync } from "node:fs";

const leer = ruta =>
  readFileSync(new URL(ruta, import.meta.url), "utf8");

test("pago usa la pantalla real y conserva el resumen", () => {
  const router = leer("../src/router/index.js");

  assert.match(router, /SeleccionMetodoPagoView\.vue/);
  assert.match(router, /retirar\/pago\/resumen/);
  assert.match(router, /RetiroPagoView\.vue/);
});

test("seleccion con cuadricula de dos por dos", () => {
  const vista = leer("../src/views/SeleccionMetodoPagoView.vue");

  assert.match(vista, /repeat\(2, minmax\(0, 1fr\)\)/);
  assert.match(vista, /espacio-vacio/);
  assert.match(vista, /ServiceCard/);

  for (const metodo of ["Efectivo", "Tarjeta", "NFC"]) {
    assert.ok(vista.includes(metodo));
  }

  assert.doesNotMatch(vista, /fetch\s*\(/);
});

test("eleccion de pago no confirma el cobro", () => {
  const sesion = leer("../src/services/retiroSesion.js");

  assert.match(sesion, /registrarMetodoPagoRetiro/);
  assert.match(sesion, /metodoPagoRetiro = null/);
  assert.doesNotMatch(
    leer("../src/views/SeleccionMetodoPagoView.vue"),
    /finalizarRetiroPersonalPagado/
  );
});

test("los seis iconos de pago existen", () => {
  for (const metodo of ["efectivo", "tarjeta", "nfc"]) {
    for (const sufijo of ["", "-oscuro"]) {
      const ruta = new URL(
        `../public/iconos/pagos/${metodo}${sufijo}.svg`,
        import.meta.url
      );
      assert.equal(existsSync(ruta), true);
    }
  }
});