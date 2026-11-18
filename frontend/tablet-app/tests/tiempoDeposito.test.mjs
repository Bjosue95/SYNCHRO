import test from "node:test";
import assert from "node:assert/strict";

import {
  MINUTO_DEPOSITO_MS,
  segundosRestantesDeposito,
  extenderLimiteDeposito,
  formatearTiempoDeposito
} from "../src/utils/tiempoDeposito.js";

test("comienza con un minuto", () => {
  const ahora = 100000;
  const limite = ahora + MINUTO_DEPOSITO_MS;

  assert.equal(segundosRestantesDeposito(limite, ahora), 60);
  assert.equal(formatearTiempoDeposito(60), "01:00");
});

test("permite agregar varios minutos", () => {
  const ahora = 100000;
  let limite = ahora + MINUTO_DEPOSITO_MS;

  for (let n = 0; n < 4; n++) {
    limite = extenderLimiteDeposito(limite, ahora);
  }

  assert.equal(segundosRestantesDeposito(limite, ahora), 300);
  assert.equal(formatearTiempoDeposito(300), "05:00");
});

test("permite agregar un minuto despues de llegar a cero", () => {
  const ahora = 200000;
  const vencido = ahora - 15000;

  const nuevoLimite = extenderLimiteDeposito(vencido, ahora);

  assert.equal(segundosRestantesDeposito(nuevoLimite, ahora), 60);
});

test("el tiempo se calcula segun la hora actual", () => {
  const inicio = 100000;
  const limite = inicio + MINUTO_DEPOSITO_MS;

  assert.equal(segundosRestantesDeposito(limite, inicio + 20000), 40);
  assert.equal(segundosRestantesDeposito(limite, inicio + 80000), 0);
});