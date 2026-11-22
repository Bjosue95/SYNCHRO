import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const leer = ruta => readFileSync(new URL(ruta, import.meta.url), "utf8");

test("el catalogo tiene todas las pantallas de carga y retiro", () => {
  const registro = leer("../src/preview/pantallas.js");
  for (const id of [
    "carga-dispositivo", "carga-modalidad",
    "carga-espacios-inalambrica", "carga-espacios-cable",
    "carga-espacios-laptop", "carga-rostro", "carga-crear-pin",
    "carga-confirmar-pin", "carga-pin-confirmado",
    "carga-colocar", "carga-colocacion-confirmada",
    "retiro-carga-rostro", "retiro-carga-pin",
    "retiro-carga-pago", "retiro-carga-espera",
    "retiro-carga-resumen", "retiro-carga-completado"
  ]) {
    assert.ok(registro.includes(`id: "${id}"`), `Falta ${id}`);
  }
});

test("la vista previa de carga no necesita SQL ni credenciales", () => {
  const vista = leer("../src/preview/views/PantallaView.vue");
  const datos = leer("../src/preview/datos/carga.js");
  const rutas = leer("../src/preview/rutas.js");
  assert.match(rutas, /import\.meta\.env\.DEV/);
  assert.match(rutas, /MODE === "demo"/);
  assert.match(vista, /espaciosCargaEjemplo/);
  assert.match(vista, /ordenCargaEjemplo/);
  assert.match(vista, /modoVistaPrevia: true/);
  assert.doesNotMatch(datos, /\b(tokenColocacion|autorizacionPago|pinHash|rostroCifrado)\s*:/i);
});

test("las vistas con acciones automaticas tienen salidas seguras para preview", () => {
  for (const nombre of [
    "EspaciosCargaView",
    "CrearPinCargaView",
    "ColocarDispositivoCargaView",
    "RetiroCargaPinView",
    "RetiroCargaPagoView"
  ]) {
    const vista = leer(`../src/views/${nombre}.vue`);
    assert.match(vista, /const esVistaPrevia = import\.meta\.env\.DEV/);
    assert.match(vista, /if \(esVistaPrevia\) return;/);
  }
  const temporal = leer("../src/views/ConfirmacionPinCargaView.vue");
  assert.match(temporal, /import\.meta\.env\.DEV && propsVistaPrevia\.modoVistaPrevia/);
});

test("el contador de carga comparte los estilos con el deposito y permite minuto extra", () => {
  const personal = leer("../src/views/DepositoView.vue");
  const carga = leer("../src/views/ColocarDispositivoCargaView.vue");
  const estilos = contenido => contenido.slice(contenido.indexOf("<style scoped>"));
  assert.equal(estilos(personal), estilos(carga));
  assert.match(carga, /deposito-aviso-minuto/);
  assert.match(carga, /escalaTiempo/);
  assert.match(carga, /synchro-timer-gradient-v3/);
  assert.match(carga, /data-preview-permitido/);
  assert.match(carga, /confirmarColocacionCarga/);
});

test("la seleccion de pago del prototipo nunca confirma un cobro", () => {
  const vista = leer("../src/views/SeleccionMetodoPagoView.vue");
  const resumen = leer("../src/views/RetiroCargaPagoView.vue");
  assert.match(vista, /esCargaEjemplo/);
  assert.match(vista, /\/vista-previa\/retiro-carga-espera/);
  assert.match(resumen, /if \(esVistaPrevia\) return;/);
  assert.match(resumen, /confirmarRecogidaRetiroCarga/);
});
