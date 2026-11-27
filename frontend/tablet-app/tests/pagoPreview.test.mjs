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
  // El resumen solo registra la confirmación diferida; no libera el casillero.
  assert.match(codigo, /registrarRecogidaRetiro\(\{codigo:codigoCasillero,tipo:"personal"/);
  assert.match(codigo, /confirmar:\(\)=>finalizarRetiroPersonalPagado\(cobroId,autorizacionPago\)/);
  assert.match(codigo, /await router\.replace\("\/retirar\/recoger"\)/);
  assert.doesNotMatch(codigo, /await finalizarRetiroPersonalPagado\(/);
  const recogida = leer("../src/views/DepositoView.vue");
  assert.match(recogida, /const datos=await actual\.confirmar\(\)/);
  assert.match(recogida, /if\(datos\?\.retiroConfirmado!==true\|\|datos\.codigoCasillero!==actual\.codigoCasillero\)throw new Error/);
  // El cierre se muestra únicamente después de validar la respuesta.
  assert.match(recogida, /if\(datos\?\.retiroConfirmado!==true[^\n]+\n\s+if\(!vivo\)return;\n\s+actual\.limpiar\(\);\n\s+recogida\.value=\{codigoCasillero:datos\.codigoCasillero\};\n\s+intento=null;\n\s+finalizar\(\);/);
  assert.match(recogida, /@click="confirmarDeposito"/);
  assert.match(recogida, /estado\.value = "confirmado"/);
  assert.match(codigo, /if \(esVistaPrevia\) return;/);
  // NFC comparte las validaciones protegidas.
  assert.match(codigo, /nombreMetodoConfirmado/);
  assert.match(codigo, /comprobarPago/);
});
