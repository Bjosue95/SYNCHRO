import { test } from "node:test";
import assert from "node:assert/strict";
import {
  usuarioValido, contrasenaValida, telefonoValido,
  normalizarTelefono, tamanoValido, tokenReservaValido,
  referenciaReservaValida, TAMANOS_ENCOMIENDA
} from "../src/services/validaciones.js";

test("usuario valido segun las reglas del backend", () => {
  assert.equal(usuarioValido("cliente_1"), true);
  assert.equal(usuarioValido("cliente.2026"), true);
  assert.equal(usuarioValido("ab"), false);
  assert.equal(usuarioValido("con espacios"), false);
  assert.equal(usuarioValido("".padEnd(81, "x")), false);
});

test("contrasena entre 12 y 128 caracteres", () => {
  assert.equal(contrasenaValida("".padEnd(12, "a")), true);
  assert.equal(contrasenaValida("".padEnd(128, "a")), true);
  assert.equal(contrasenaValida("".padEnd(11, "a")), false);
  assert.equal(contrasenaValida("".padEnd(129, "a")), false);
});

test("numeros E164 normalizados sin alterar el destino", () => {
  assert.equal(telefonoValido("+50499998888"), true);
  assert.equal(normalizarTelefono("50499998888"), "+50499998888");
  assert.equal(normalizarTelefono("+50499998888"), "+50499998888");
  assert.equal(normalizarTelefono("999-8888"), null);
  assert.equal(normalizarTelefono(""), null);
});

test("los tamaños coinciden exactamente con el servicio de pre-reservas", () => {
  assert.deepEqual(TAMANOS_ENCOMIENDA.map(x => x.valor),
    ["Pequeno", "Mediano", "Grande"]);
  assert.equal(tamanoValido("Mediano"), true);
  assert.equal(tamanoValido("Pequeño"), false);
  assert.equal(tamanoValido("Frio"), false);
});

test("la clave de consulta es de 64 caracteres hexadecimales", () => {
  assert.equal(tokenReservaValido("A".repeat(64)), true);
  assert.equal(tokenReservaValido("b".repeat(64)), true);
  assert.equal(tokenReservaValido("G".repeat(64)), false);
  assert.equal(tokenReservaValido("A".repeat(63)), false);
  assert.equal(tokenReservaValido(""), false);
});

test("la referencia debe caber en un entero positivo SQL", () => {
  assert.equal(referenciaReservaValida("1"), true);
  assert.equal(referenciaReservaValida(123), true);
  assert.equal(referenciaReservaValida("2147483647"), true);
  assert.equal(referenciaReservaValida("2147483648"), false);
  assert.equal(referenciaReservaValida("0"), false);
  assert.equal(referenciaReservaValida("../123"), false);
});
