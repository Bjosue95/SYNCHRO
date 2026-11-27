import test from "node:test";
import assert from "node:assert/strict";

import {
  prevalidarRetiroFacialConPin,
  prepararCobroRetiroPersonal,
  finalizarRetiroPersonalPagado,
  finalizarRetiroReservaPersonal
} from "../src/services/retiroFacialManualApi.js";

import {
  iniciarRetiro,
  registrarCaptura,
  obtenerRetiro,
  registrarOrdenRetiroPagado,
  obtenerOrdenRetiroPagado,
  limpiarRetiro
} from "../src/services/retiroSesion.js";

function simularFetch(respuestaEndpoint) {
  const anterior = globalThis.fetch;
  const llamadas = [];

  globalThis.fetch = async (url, opciones) => {
    llamadas.push({ url, opciones });

    if (url.endsWith("/csrf-token")) {
      return {
        ok: true,
        status: 200,
        json: async () => ({ csrfToken: "csrf-pruebas" })
      };
    }

    return {
      ok: true,
      status: 200,
      json: async () => respuestaEndpoint
    };
  };

  return {
    llamadas,
    restaurar() {
      globalThis.fetch = anterior;
    }
  };
}

test("PIN prevalidado no finaliza retiro", async () => {
  const simulacion = simularFetch({
    pinValidado: true,
    pagoPendiente: true,
    retiroConfirmado: false,
    autorizacionPago: "autorizacion-protegida-de-pruebas"
  });

  try {
    const resultado = await prevalidarRetiroFacialConPin(
      "123456",
      "token-facial-de-pruebas"
    );

    assert.equal(resultado.retiroConfirmado, false);
    assert.equal(
      simulacion.llamadas[1].url,
      "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar"
    );

    assert.equal(
      simulacion.llamadas[1].opciones.headers["X-CSRF-TOKEN"],
      "csrf-pruebas"
    );
  } finally {
    simulacion.restaurar();
  }
});

test("Orden de cobro no confirma el pago", async () => {
  const simulacion = simularFetch({
    cobroId: "00000000-0000-0000-0000-000000000001",
    codigoCasillero: "M01",
    importeHnl: 1,
    estadoPago: "Pendiente",
    pagoConfirmado: false,
    retiroConfirmado: false
  });

  try {
    const resultado = await prepararCobroRetiroPersonal(
      "autorizacion-protegida-de-pruebas"
    );

    assert.equal(resultado.pagoConfirmado, false);

    assert.equal(
      simulacion.llamadas[1].url,
      "/api/terminales/guardar/whatsapp/retiro/cobro/preparar"
    );

    const cuerpo = JSON.parse(
      simulacion.llamadas[1].opciones.body
    );

    assert.deepEqual(
      Object.keys(cuerpo),
      ["autorizacionPago"]
    );
  } finally {
    simulacion.restaurar();
  }
});

test("Finalizar utiliza cobro y autorizacion protegida", async () => {
  const simulacion = simularFetch({
    retiroConfirmado: true,
    pagoAplicado: true,
    estadoCasillero: "Disponible"
  });

  try {
    await finalizarRetiroPersonalPagado(
      "00000000-0000-0000-0000-000000000001",
      "autorizacion-protegida-de-pruebas"
    );

    assert.equal(
      simulacion.llamadas[1].url,
      "/api/terminales/guardar/whatsapp/retiro/cobro/finalizar"
    );

    const cuerpo = JSON.parse(
      simulacion.llamadas[1].opciones.body
    );

    assert.deepEqual(Object.keys(cuerpo).sort(), [
      "autorizacionPago",
      "cobroId"
    ]);
  } finally {
    simulacion.restaurar();
  }
});

test("Sesion de cobro permanece solo en memoria", () => {
  iniciarRetiro();
  registrarCaptura("token-facial-de-pruebas");

  assert.equal(obtenerRetiro().capturaHecha, true);

  registrarOrdenRetiroPagado(
    "autorizacion-protegida-de-pruebas",
    {
      cobroId: "00000000-0000-0000-0000-000000000001",
      codigoCasillero: "M01",
      importeHnl: 1,
      minutosUtilizados: 60,
      descripcionTarifa: "Tarifa de prueba",
      estadoPago: "Pendiente"
    }
  );

  assert.equal(obtenerRetiro().capturaHecha, false);
  assert.equal(obtenerOrdenRetiroPagado().importeHnl, 1);

  limpiarRetiro();

  assert.equal(obtenerOrdenRetiroPagado(), null);
  assert.equal(obtenerRetiro().autorizacionFacial, null);
});
test("reserva web pagada autoriza sin liberar hasta confirmar", async () => {
  const autorizacionRetiro="a".repeat(64);
  const simulacion=simularFetch({pinValidado:true,retiroAutorizado:true,retiroConfirmado:false,origenReserva:"Web",pagoConfirmado:true,pagoPendiente:false,codigoCasillero:"M03",estadoCasillero:"Ocupado",autorizacionRetiro});
  try {const resultado=await prevalidarRetiroFacialConPin("123456","token-facial-de-pruebas");assert.equal(resultado.retiroConfirmado,false);assert.equal(resultado.estadoCasillero,"Ocupado");assert.equal(simulacion.llamadas.length,2);}
  finally {simulacion.restaurar();}
  const cierre=simularFetch({retiroConfirmado:true,codigoCasillero:"M03",estadoCasillero:"Disponible",origenReserva:"Web"});
  try {assert.equal((await finalizarRetiroReservaPersonal(autorizacionRetiro)).retiroConfirmado,true);
    assert.equal(cierre.llamadas[1].url,"/api/terminales/guardar/whatsapp/retiro/reserva/confirmar");
    assert.deepEqual(JSON.parse(cierre.llamadas[1].opciones.body),{autorizacionRetiro,retiroComprobado:true});}
  finally {cierre.restaurar();}
});
