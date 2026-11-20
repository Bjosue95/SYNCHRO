import test from "node:test";
import assert from "node:assert/strict";
import {
  registrarDescriptorV3,
  consumirDescriptorV3,
  borrarDescriptorV3,
  copiarDescriptorV3
} from "../src/services/descriptorFacialV3Temporal.js";

test("descriptor V3 se consume una sola vez", () => {
  const datos = Array(1024).fill(0);
  datos[0] = 1;

  assert.equal(registrarDescriptorV3(datos), true);
  datos[0] = 0;

  const recuperado = consumirDescriptorV3();

  assert.equal(recuperado.length, 1024);
  assert.equal(recuperado[0], 1);
  assert.equal(consumirDescriptorV3(), null);
});

test("descriptor V3 invalido se rechaza", () => {
  assert.equal(registrarDescriptorV3(Array(1023).fill(1)), false);
  assert.equal(registrarDescriptorV3(Array(1024).fill(0)), false);

  const invalidos = Array(1024).fill(0);
  invalidos[0] = Number.NaN;

  assert.equal(registrarDescriptorV3(invalidos), false);
  assert.equal(consumirDescriptorV3(), null);
});

test("descriptor V3 puede descartarse", () => {
  const datos = Array(1024).fill(0);
  datos[0] = 1;

  assert.equal(registrarDescriptorV3(datos), true);
  borrarDescriptorV3();

  assert.equal(consumirDescriptorV3(), null);
});
test("diagnostico V3 conserva descriptor hasta el deposito", () => {
  const datos = Array(1024).fill(0);
  datos[0] = 1;

  assert.equal(registrarDescriptorV3(datos), true);

  const diagnostico = copiarDescriptorV3();
  assert.equal(diagnostico[0], 1);

  diagnostico.fill(0);

  const deposito = consumirDescriptorV3();
  assert.equal(deposito[0], 1);
  assert.equal(consumirDescriptorV3(), null);
});