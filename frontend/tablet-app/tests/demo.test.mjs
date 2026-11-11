import { test } from "node:test";
import assert from "node:assert/strict";
import { crearEscenarioDemo } from "../src/services/demo/tabletDemo.js";
test("demo no fabrica un exito para rutas desconocidas o credenciales incorrectas", async () => {
  const api = crearEscenarioDemo();
  await assert.rejects(api('/api/terminales/servicios/F02/reservas',{metodo:'POST',datos:{minutos:30}}), e=>e.estado===403);
  await assert.rejects(api('/api/terminales/servicios/H02/reservas',{metodo:'POST',datos:{minutos:30}}), e=>e.estado===403);
  await assert.rejects(
    api("/api/terminales/encomiendas/visitas", { metodo: "POST", datos: {} }),
  );
  await assert.rejects(
    api("/api/terminales/encomiendas/reservas/consultar", {
      metodo: "POST",
      datos: { referencia: 1, tokenGestion: "C".repeat(64) },
    }),
  );
});
test("demostracion acepta efectivo simulado sin confirmacion duplicada", async () => {
  const api = crearEscenarioDemo();
  const reserva = await api("/api/terminales/servicios/C01/reservas", {
    metodo: "POST",
    datos: { minutos: 30 },
  });
  const ruta = `/api/terminales/servicios/C01/reservas/${reserva.operacion.id}/pagar`;
  const encabezados = { "X-SYNCHRO-SERVICIO": reserva.credencial };
  await assert.rejects(
    api(ruta, { metodo: "POST", encabezados, datos: { idOperacion: crypto.randomUUID(), metodoPago: "Transferencia" } }),
    e => e.estado === 400,
  );
  const idOperacion = crypto.randomUUID();
  const opciones = { metodo: "POST", encabezados, datos: { idOperacion, metodoPago: "Efectivo" } };
  const pago = await api(ruta, opciones);
  assert.equal(pago.pinRetiro, "654321");
  const repetido = await api(ruta, opciones);
  assert.equal(repetido.pinRetiro, null);
  assert.equal(repetido.yaConfirmado, true);
  await assert.rejects(
    api(ruta, { metodo: "POST", encabezados, datos: { idOperacion, metodoPago: "NFC" } }),
    e => e.estado === 409,
  );
});
test("demostracion mantiene pago idempotente, reserva unica y retiro con PIN", async () => {
  const api = crearEscenarioDemo();
  const r = await api("/api/terminales/servicios/C01/reservas", {
    metodo: "POST",
    datos: { minutos: 30 },
  });
  await assert.rejects(
    api("/api/terminales/servicios/C01/reservas", {
      metodo: "POST",
      datos: { minutos: 30 },
    }),
  );
  const base = `/api/terminales/servicios/C01/reservas/${r.operacion.id}`,
    encabezados = { "X-SYNCHRO-SERVICIO": r.credencial };
  const opciones = {
    metodo: "POST",
    encabezados,
    datos: { idOperacion: crypto.randomUUID() },
  };
  const p = await api(`${base}/pagar`, opciones);
  assert.equal(p.pinRetiro, "654321");
  assert.equal((await api(`${base}/pagar`, opciones)).pinRetiro, null);
  await api(`${base}/iniciar`, { metodo: "POST", encabezados, datos: {} });
  await assert.rejects(
    api(`${base}/retirar`, {
      metodo: "POST",
      encabezados,
      datos: { pin: "000000" },
    }),
  );
  assert.equal(
    (
      await api(`${base}/retirar`, {
        metodo: "POST",
        encabezados,
        datos: { pin: "654321" },
      })
    ).autorizado,
    true,
  );
  await assert.rejects(
    api(`${base}/retirar`, {
      metodo: "POST",
      encabezados,
      datos: { pin: "654321" },
    }),
  );
});
