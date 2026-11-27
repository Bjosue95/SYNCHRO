import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import {
  identificarDepositoReservaWeb,
  identificarRetiroReservaWeb,
  depositarReservaWeb
} from "../src/services/reservasWebTerminalApi.js";

const leer = p => readFileSync(new URL("../src/" + p, import.meta.url), "utf8");

test("reservas de pruebas acceden desde cualquier tablet sin vinculación", async () => {
  const anterior = globalThis.fetch;
  const rutas = [];
  try {
    globalThis.fetch = async (url, opciones = {}) => {
      rutas.push(String(url));
      if (String(url).endsWith("/csrf-token"))
        return new Response(JSON.stringify({ csrfToken: "csrf-demo" }), {
          status: 200, headers: { "Content-Type": "application/json" }
        });
      assert.equal(opciones.method, "POST");
      assert.equal(opciones.credentials, "same-origin");
      assert.equal(opciones.headers["X-CSRF-TOKEN"], "csrf-demo");
      assert.deepEqual(JSON.parse(opciones.body), { pin: "123456" });
      return new Response(JSON.stringify({
        reservaId: 5012, casillero: "M03", servicio: "Personal", tipoCasillero: "Normal"
      }), { status: 200, headers: { "Content-Type": "application/json" } });
    };
    const identificada = await identificarDepositoReservaWeb("123456");
    assert.equal(identificada.reservaId, 5012);
    assert.deepEqual(rutas, [
      "/api/terminales/reservas/csrf-token",
      "/api/terminales/reservas/depositos/identificar"
    ]);
  } finally { globalThis.fetch = anterior; }
});

test("PIN incorrecto devuelve su error real sin solicitar autorizacion de dispositivo", async () => {
  const anterior = globalThis.fetch;
  try {
    globalThis.fetch = async url => {
      if (String(url).endsWith("/csrf-token"))
        return new Response(JSON.stringify({ csrfToken: "csrf-demo" }), { status: 200 });
      return new Response(JSON.stringify({
        codigo: "PinNoValido", mensaje: "No se pudo validar el PIN de depósito."
      }), { status: 403 });
    };
    await assert.rejects(identificarDepositoReservaWeb("654321"), error =>
      error.status === 403 && error.codigo === "PinNoValido" &&
      error.message === "No se pudo validar el PIN de depósito.");
    await assert.rejects(identificarRetiroReservaWeb("1234"), /seis dígitos/);
  } finally { globalThis.fetch = anterior; }
});

test("depositar sigue exigiendo PIN, evidencia y confirmación sin usar credenciales de estación", async () => {
  const anterior = globalThis.fetch;
  try {
    await assert.rejects(depositarReservaWeb(5012, { pin: "123456" }), /Falta confirmar/);
    globalThis.fetch = async (url, opciones = {}) => {
      if (String(url).endsWith("/csrf-token"))
        return new Response(JSON.stringify({ csrfToken: "csrf-demo" }), { status: 200 });
      assert.equal(opciones.headers["X-SYNCHRO-CONSENTIMIENTO"], "ACEPTO");
      assert.equal(JSON.parse(opciones.body).depositoComprobado, true);
      return new Response(JSON.stringify({ depositoConfirmado: true }), { status: 200 });
    };
    assert.equal((await depositarReservaWeb(5012, {
      pin: "123456", captura: { imagenPng: "QUJD" }, depositoComprobado: true
    })).depositoConfirmado, true);
  } finally { globalThis.fetch = anterior; }
});

test("modo de acceso libre no desactiva PIN ni los controles de otros servicios", () => {
  const vista = leer("views/ReservaWebElegirView.vue");
  const api = leer("services/reservasWebTerminalApi.js");
  const script = readFileSync(new URL(
    "../../../tools/Iniciar-BackendCargaSegura.ps1", import.meta.url), "utf8");
  const backend = readFileSync(new URL(
    "../../../backend/Synchro.Api/Endpoints/EncomiendasOperativasEndpoints.cs",
    import.meta.url), "utf8");
  assert.doesNotMatch(vista, /comprobarSesionTerminalReservaWeb|comprobarAsignacionTerminalReservasWeb|Autorizar estación/);
  assert.doesNotMatch(api, /renovarSesionTerminalReservaWeb|comprobarSesionTerminalReservaWeb|SesionTerminalNoAutorizada/);
  assert.match(script, /ReservasPrototipo__AccesoLibrePruebas = 'true'/);
  assert.match(backend, /if\(!libre\)prototipo.RequireAuthorization\("SoloTerminal"\)/);
  assert.match(backend, /RequireRateLimiting\("IdentificarRetiroEncomienda"\)/);
  assert.match(backend, /Consentimiento\(h\)/);
});
