import test from "node:test";
import assert from "node:assert/strict";
import {
  validarDescriptorFacialV3Temporal
} from "../src/services/retiroFacialManualApi.js";

function descriptorValido() {
  const vector = Array(1024).fill(0);
  vector[0] = 1;
  return vector;
}

test("V3 usa CSRF y consentimiento sin solicitar retiro", async () => {
  const anterior = globalThis.fetch;
  const llamadas = [];

  try {
    globalThis.fetch = async (url, opciones) => {
      llamadas.push({ url, opciones });

      if (url.endsWith("/csrf-token")) {
        return {
          ok: true,
          json: async () => ({ csrfToken: "csrf-de-prueba" })
        };
      }

      return {
        ok: true,
        json: async () => ({
          valido: true,
          modelo: "SYNCHRO-HUMANJS-EMBEDDING-1024-v3"
        })
      };
    };

    const resultado = await validarDescriptorFacialV3Temporal(
      descriptorValido()
    );

    assert.equal(resultado, true);
    assert.equal(llamadas.length, 2);

    const envio = llamadas[1];
    assert.equal(
      envio.url,
      "/api/terminales/guardar/whatsapp/v3/validar"
    );
    assert.equal(envio.opciones.method, "POST");
    assert.equal(envio.opciones.headers["X-CSRF-TOKEN"], "csrf-de-prueba");
    assert.equal(envio.opciones.headers["X-SYNCHRO-CONSENTIMIENTO"], "ACEPTO");

    const contenido = JSON.parse(envio.opciones.body);
    assert.equal(contenido.descriptor.length, 1024);
    assert.equal(contenido.pin, undefined);
    assert.equal(contenido.tokenFacial, undefined);
  } finally {
    globalThis.fetch = anterior;
  }
});

test("V3 no envia descriptores invalidos", async () => {
  const anterior = globalThis.fetch;
  let solicitudes = 0;

  try {
    globalThis.fetch = async () => {
      solicitudes++;
      throw new Error("No deberia consultar el servidor");
    };

    await assert.rejects(
      validarDescriptorFacialV3Temporal(Array(1023).fill(1)),
      /invalido/
    );

    await assert.rejects(
      validarDescriptorFacialV3Temporal(Array(1024).fill(0)),
      /no utilizable/
    );

    assert.equal(solicitudes, 0);
  } finally {
    globalThis.fetch = anterior;
  }
});