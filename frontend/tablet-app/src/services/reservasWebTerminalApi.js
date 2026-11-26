/*
 * SYNCHRO - Reservas web desde la tablet.
 * No modifica los servicios de uso presencial.
 */

const BASE = "/api/terminales/reservas";

async function leer(respuesta) {
  const datos = await respuesta.json().catch(() => null);

  if (!respuesta.ok) {
    throw new Error(
      datos?.mensaje ||
      datos?.title ||
      (respuesta.status === 401
        ? "La sesión de la terminal ha vencido."
        : `No se pudo completar la operación (${respuesta.status}).`)
    );
  }

  return datos;
}

async function enviar(ruta, cuerpo, consentimiento = false) {
  const { csrfToken } = await leer(
    await fetch(`${BASE}/csrf-token`, {
      credentials: "same-origin",
      cache: "no-store",
    })
  );

  if (!csrfToken) {
    throw new Error("No se recibió el token de seguridad.");
  }

  const headers = {
    "Content-Type": "application/json",
    "X-CSRF-TOKEN": csrfToken,
  };

  if (consentimiento) {
    headers["X-SYNCHRO-CONSENTIMIENTO"] = "ACEPTO";
  }

  return leer(
    await fetch(`${BASE}${ruta}`, {
      method: "POST",
      credentials: "same-origin",
      cache: "no-store",
      headers,
      body: JSON.stringify(cuerpo),
    })
  );
}

function validarId(id) {
  if (!Number.isInteger(id) || id <= 0) {
    throw new Error("Reserva inválida.");
  }
}

function validarPin(pin) {
  if (typeof pin !== "string" || !/^[0-9]{6}$/.test(pin)) {
    throw new Error("El PIN debe tener seis dígitos.");
  }
}

// Depósito: requiere el ID obtenido de manera segura del backend.
export async function depositarReservaWeb(id, datos) {
  validarId(id);
  validarPin(datos?.pin);

  if (!datos?.depositoComprobado || !datos?.captura) {
    throw new Error("Falta confirmar el depósito o su captura.");
  }

  return enviar(`/${id}/depositar`, {
    pin: datos.pin,
    captura: datos.captura,
    depositoComprobado: true,
    codigoCasillero: datos.codigoCasillero ?? null,
  }, true);
}

// Retiro: el backend identifica la reserva mediante el PIN.
export async function identificarRetiroReservaWeb(pin) {
  validarPin(pin);
  return enviar("/retiros/identificar", { pin });
}

// Captura independiente de la persona que retira.
export async function capturarRetiroReservaWeb(id, captura) {
  validarId(id);
  if (!captura) throw new Error("Falta la captura de retiro.");

  return enviar(`/${id}/captura-retiro`, captura, true);
}

// Confirmación definitiva: PIN y evidencia.
export async function confirmarRetiroReservaWeb(id, datos) {
  validarId(id);
  validarPin(datos?.pin);

  if (!datos?.evidenciaId || !datos?.entregaComprobada) {
    throw new Error("Falta confirmar la entrega.");
  }

  return enviar(`/${id}/retirar`, {
    evidenciaId: datos.evidenciaId,
    pin: datos.pin,
    entregaComprobada: true,
  });
}

// Identificar una reserva web mediante su PIN de depósito.
export async function identificarDepositoReservaWeb(pin) {
  validarPin(pin);

  return enviar("/depositos/identificar", { pin });
}

export async function solicitarPagoReservaWebTablet(id, pin, metodo) {
  validarId(id);
  validarPin(pin);
  if (!["Efectivo","Tarjeta","NFC"].includes(metodo)) {
    throw new Error("Selecciona un método de pago válido.");
  }
  // La petición contiene un PIN de retiro y un método permitido.
  // Nunca datos de tarjeta, vencimiento o código de seguridad.
  return enviar(`/${id}/pago/solicitar`, { pin, metodo });
}

export async function consultarPagoReservaWebTablet(id) {
  validarId(id);
  const respuesta = await fetch(`${BASE}/${id}/pago/estado`, {
    credentials: "same-origin",
    cache: "no-store"
  });
  return leer(respuesta);
}
