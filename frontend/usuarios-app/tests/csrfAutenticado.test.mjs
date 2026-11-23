import test from "node:test";
import assert from "node:assert/strict";
import { apiClientes, ErrorApi } from "../src/services/clientesApi.js";

const respuesta = (datos, status = 200) =>
  new Response(JSON.stringify(datos), {
    status,
    headers: { "Content-Type": "application/json" }
  });

test("login usa token anonimo y logout uno del cliente autenticado", async () => {
  const anterior = globalThis.fetch;
  const llamadas = [];
  globalThis.fetch = async (ruta, opciones) => {
    llamadas.push({ ruta, opciones });
    if (ruta === "/api/web/clientes/acceso/csrf-token") {
      return respuesta({ csrfToken: "csrf-anonimo" });
    }
    if (ruta === "/api/web/clientes/csrf-token") {
      return respuesta({ csrfToken: "csrf-autenticado" });
    }
    if (ruta === "/api/web/clientes/acceso/login") {
      return respuesta({ nombreUsuario: "cliente" });
    }
    if (ruta === "/api/web/clientes/logout") {
      return respuesta({ cerrada: true });
    }
    throw new Error("Ruta no esperada: " + ruta);
  };
  try {
    await apiClientes.ingresar({ nombreUsuario: "cliente", contrasena: "segura" });
    await apiClientes.salir();
    assert.deepEqual(llamadas.map(x => x.ruta), [
      "/api/web/clientes/acceso/csrf-token",
      "/api/web/clientes/acceso/login",
      "/api/web/clientes/csrf-token",
      "/api/web/clientes/logout"
    ]);
    assert.equal(llamadas[1].opciones.headers["X-CSRF-TOKEN"], "csrf-anonimo");
    assert.equal(llamadas[3].opciones.headers["X-CSRF-TOKEN"], "csrf-autenticado");
    assert.equal(llamadas[3].opciones.credentials, "same-origin");
    assert.equal(llamadas[2].opciones.credentials, "same-origin");
  } finally {
    globalThis.fetch = anterior;
  }
});

test("pre-reserva y consentimiento usan CSRF autenticado", async () => {
  const anterior = globalThis.fetch;
  const llamadas = [];
  globalThis.fetch = async (ruta, opciones) => {
    llamadas.push({ ruta, opciones });
    if (ruta === "/api/web/clientes/csrf-token") {
      return respuesta({ csrfToken: "csrf-del-cliente" });
    }
    if (ruta === "/api/web/encomiendas/pre-reservas") {
      return respuesta({ id: 17, tokenGestion: "a".repeat(64) });
    }
    if (ruta === "/api/web/encomiendas/pre-reservas/17/consentimiento/iniciar") {
      return respuesta({ estado: "Preparada" });
    }
    throw new Error("Ruta no esperada: " + ruta);
  };
  try {
    await apiClientes.crearPreReserva({
      telefonoDestinatario: "+50499998888", tamanoSolicitado: "Mediano"
    });
    await apiClientes.iniciarConsentimiento(17, "a".repeat(64));
    assert.deepEqual(llamadas.map(x => x.ruta), [
      "/api/web/clientes/csrf-token",
      "/api/web/encomiendas/pre-reservas",
      "/api/web/clientes/csrf-token",
      "/api/web/encomiendas/pre-reservas/17/consentimiento/iniciar"
    ]);
    assert.equal(llamadas[1].opciones.headers["X-CSRF-TOKEN"], "csrf-del-cliente");
    assert.equal(llamadas[3].opciones.headers["X-CSRF-TOKEN"], "csrf-del-cliente");
    assert.equal(llamadas[3].opciones.headers["X-SYNCHRO-RESERVA"], "a".repeat(64));
  } finally {
    globalThis.fetch = anterior;
  }
});

test("si caduca la sesion, no se envia logout con token anonimo", async () => {
  const anterior = globalThis.fetch;
  const llamadas = [];
  globalThis.fetch = async ruta => {
    llamadas.push(ruta);
    return respuesta({ mensaje: "Inicia sesión de nuevo." }, 401);
  };
  try {
    await assert.rejects(apiClientes.salir(), error =>
      error instanceof ErrorApi && error.estado === 401);
    assert.deepEqual(llamadas, ["/api/web/clientes/csrf-token"]);
  } finally {
    globalThis.fetch = anterior;
  }
});
