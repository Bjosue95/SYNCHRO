/* SYNCHRO - Pre-reservas web. Nunca autoriza depositos ni envia WhatsApp. */
const ruta = "/api/web/encomiendas";

async function leerRespuesta(respuesta) {
  const json = await respuesta.json().catch(() => null);
  if (!respuesta.ok) {
    throw new Error(json?.mensaje || `Error HTTP ${respuesta.status}`);
  }
  return json;
}

export async function crearPreReservaEncomienda(datos) {
  const { csrfToken } = await leerRespuesta(await fetch(`${ruta}/csrf-token`, {
    credentials: "same-origin",
    cache: "no-store",
  }));
  if (!csrfToken) throw new Error("No se recibio token CSRF.");

  return leerRespuesta(await fetch(`${ruta}/pre-reservas`, {
    method: "POST",
    credentials: "same-origin",
    cache: "no-store",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrfToken,
    },
    body: JSON.stringify(datos),
  }));
}

export async function consultarPreReservaEncomienda(id, tokenGestion) {
  return leerRespuesta(await fetch(`${ruta}/pre-reservas/${encodeURIComponent(id)}`, {
    method: "GET",
    credentials: "same-origin",
    cache: "no-store",
    headers: { "X-SYNCHRO-RESERVA": tokenGestion },
  }));
}

export async function consultarIntegracionesEncomienda() {
  return leerRespuesta(await fetch(`${ruta}/integraciones`, {
    credentials: "same-origin",
    cache: "no-store",
  }));
}
