import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const leer = nombre => readFileSync(new URL(
  "../src/" + nombre, import.meta.url), "utf8");

test("el catalogo inicia recorridos animados de carga y retiro", () => {
  const catalogo = leer("preview/views/CatalogoView.vue");
  assert.match(catalogo, /\/vista-previa\/carga-dispositivo/);
  assert.match(catalogo, /\/vista-previa\/retiro-carga-rostro/);
  const visor = leer("preview/views/PantallaView.vue");
  assert.match(visor, /vistasInteractivas/);
  assert.match(visor, /data-preview-permitido/);
  assert.match(visor, /evento\.preventDefault\(\)/);
});

test("ambos escaneos animados de preview evitan la camara", () => {
  for (const nombre of ["VerificacionFacialCargaView", "RetiroCargaRostroView"]) {
    const s = leer("views/" + nombre + ".vue");
    assert.match(s, /if \(esVistaPrevia\) \{/);
    assert.match(s, /estado\.value = "Escaneando rostro\.\.\."/);
    assert.match(s, /temporizadorDemo = window\.setTimeout/);
    assert.match(s, /tomada\.value = true/);
    assert.match(s, /data-preview-permitido/);
    assert.match(s, /v-if="tomada && !esVistaPrevia"/);
  }
});

test("PIN permite seguir visualmente sin escribir en backend", () => {
  const crear = leer("views/CrearPinCargaView.vue");
  const retiro = leer("views/RetiroCargaPinView.vue");
  const confirmacion = leer("views/ConfirmacionPinCargaView.vue");
  assert.match(crear, /\/vista-previa\/carga-pin-confirmado/);
  assert.match(crear, /if \(esVistaPrevia\) \{/);
  assert.match(confirmacion, /route\.query\.recorrido === "1"/);
  assert.match(retiro, /\/vista-previa\/retiro-carga-pago/);
  assert.match(retiro, /if \(esVistaPrevia\) \{/);
});

test("contador se anima y la colocacion no confirma una sesion real en preview", () => {
  const s = leer("views/ColocarDispositivoCargaView.vue");
  assert.match(s, /recargaReloj\.value \+= 1/);
  assert.match(s, /avisoMinuto\.value = true/);
  assert.match(s, /if \(esVistaPrevia\) \{/);
  assert.match(s, /estado\.value = "confirmado"/);
  assert.match(s, /confirmarColocacionCarga\(\)/);
});

test("el pago animado no solicita cobros ni libera espacios reales", () => {
  const s = leer("views/RetiroCargaPagoView.vue");
  assert.match(s, /if \(esVistaPrevia\) \{/);
  assert.match(s, /fase\.value = "resumen"/);
  assert.match(s, /fase\.value = "completado"/);
  assert.match(s, /pago-preview-animado/);
  assert.match(s, /data-preview-permitido/);
  assert.match(s, /confirmarRecogidaRetiroCarga\(\)/);
});
