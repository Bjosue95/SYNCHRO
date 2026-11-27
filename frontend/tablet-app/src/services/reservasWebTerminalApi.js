/*
 * SYNCHRO - Reservas web desde la tablet.
 * No modifica los servicios de uso presencial.
 */

const BASE = "/api/terminales/reservas";
export class SesionTerminalNoAutorizada extends Error {
  constructor() {
    super("Esta estación necesita autorización. Solicita al administrador que verifique la vinculación de la terminal.");
    this.name = "SesionTerminalNoAutorizada";
    this.codigo = "TerminalNoAutorizada";
  }
}

// Una renovación concurrente se comparte; nunca se pide otra autorización
// para una sesión corta vencida si la tablet ya tiene su vínculo aprobado.
let renovacionEnCurso = null;
export async function renovarSesionTerminalReservaWeb() {
  if (!renovacionEnCurso) {
    renovacionEnCurso = (async () => {
      const seguridad = await leer(await fetch("/api/terminales/dispositivo/csrf-token", {
        credentials: "same-origin", cache: "no-store"
      }));
      if (!seguridad?.csrfToken) throw new Error("No se recibió el token de seguridad.");

      return leer(await fetch("/api/terminales/sesion/renovar", {
        method: "POST", credentials: "same-origin", cache: "no-store",
        headers: { "X-CSRF-TOKEN": seguridad.csrfToken, "Content-Type": "application/json" },
        body: "{}"
      }));
    })().finally(() => { renovacionEnCurso = null; });
  }
  return renovacionEnCurso;
}

export async function comprobarSesionTerminalReservaWeb() {
  const consultar = () => fetch("/api/terminales/mi-sesion", {
    credentials: "same-origin", cache: "no-store"
  });
  let respuesta = await consultar();
  if (respuesta.status === 401) {
    await renovarSesionTerminalReservaWeb();
    respuesta = await consultar();
  }
  return leer(respuesta);
}

export async function comprobarAsignacionTerminalReservasWeb() {
  const consultar = () => fetch(`${BASE}/estacion`, {
    credentials: "same-origin", cache: "no-store"
  });
  let respuesta = await consultar();
  if (respuesta.status === 401) {
    await renovarSesionTerminalReservaWeb();
    respuesta = await consultar();
  }
  const datos = await leer(respuesta);
  if (datos?.autorizada !== true) {
    throw new Error("No se pudo confirmar que esta estación atienda reservas web.");
  }
  return datos;
}


async function leer(respuesta) {
  const datos = await respuesta.json().catch(() => null);

  // Un 403 con código de dominio puede significar PIN incorrecto, no cookie vencida.
  if (respuesta.status === 401 || (respuesta.status === 403 && !datos?.codigo && !datos?.mensaje)) {
    throw new SesionTerminalNoAutorizada();
  }

  if (!respuesta.ok) {
    throw new Error(
      datos?.mensaje ||
      datos?.title ||
      `No se pudo completar la operación (${respuesta.status}).`
    );
  }

  return datos;
}

async function enviar(ruta, cuerpo, consentimiento = false) {
  const enviarConSesion = async () => {
    const { csrfToken } = await leer(
      await fetch(`${BASE}/csrf-token`, {
        credentials: "same-origin",
        cache: "no-store",
      })
    );
    if (!csrfToken) throw new Error("No se recibió el token de seguridad.");

    const headers = {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrfToken,
    };
    if (consentimiento) headers["X-SYNCHRO-CONSENTIMIENTO"] = "ACEPTO";
    return leer(await fetch(`${BASE}${ruta}`, {
      method: "POST",
      credentials: "same-origin",
      cache: "no-store",
      headers,
      body: JSON.stringify(cuerpo),
    }));
  };

  try {
    return await enviarConSesion();
  } catch (error) {
    if (!(error instanceof SesionTerminalNoAutorizada)) throw error;
    await renovarSesionTerminalReservaWeb();
    // Un solo reintento; nunca duplicar un depósito por una respuesta ambigua.
    return enviarConSesion();
  }
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
  const consulta = () => fetch(`${BASE}/${id}/pago/estado`, {
    credentials: "same-origin", cache: "no-store"
  });
  let respuesta = await consulta();
  if (respuesta.status === 401) {
    await renovarSesionTerminalReservaWeb();
    respuesta = await consulta();
  }
  return leer(respuesta);
}
