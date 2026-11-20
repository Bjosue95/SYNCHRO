import test from "node:test";
import assert from "node:assert/strict";
import {
  construirInicioFacialV3
} from "../src/services/solicitudInicioFacialV3.js";

const png = "data:image/png;base64,iVBORw0KGgo=";

function descriptor() {
  const valores = Array(1024).fill(0);
  valores[0] = 1;
  return valores;
}

test("V3 construye solicitud para el backend", () => {
  const solicitud = construirInicioFacialV3(
    png, descriptor(), true
  );

  const contenido = JSON.parse(solicitud.body);

  assert.equal(contenido.imagenPng, "iVBORw0KGgo=");
  assert.equal(contenido.descriptor.length, 1024);
  assert.equal(contenido.versionConsentimiento, "SYNCHRO-FACIAL-V3-2026-11");
  assert.equal(contenido.pin, undefined);
  assert.equal(contenido.tokenFacial, undefined);
});

test("V3 exige consentimiento especifico", () => {
  assert.throws(
    () => construirInicioFacialV3(png, descriptor(), false),
    /consentimiento/
  );
});

test("V3 rechaza descriptores incorrectos", () => {
  assert.throws(
    () => construirInicioFacialV3(png, Array(1024).fill(0), true),
    /no utilizable/
  );

  assert.throws(
    () => construirInicioFacialV3(png, Array(1023).fill(1), true),
    /inválido/
  );
});