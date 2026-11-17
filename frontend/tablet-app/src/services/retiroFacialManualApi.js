// SYNCHRO / simulacion visual manual / solo HTTPS y base de pruebas.
async function solicitarCsrf() {
  const respuesta = await fetch("/api/terminales/guardar/whatsapp/csrf-token", {
    credentials: "same-origin", cache: "no-store",
  });
  if (!respuesta.ok) throw new Error("No se pudo obtener el token de seguridad del terminal.");
  const datos = await respuesta.json();
  if (!datos?.csrfToken) throw new Error("No se recibio el token CSRF.");
  return datos.csrfToken;
}

async function respuestaSegura(respuesta) {
  const datos = await respuesta.json().catch(() => null);
  if (!respuesta.ok) {
    throw new Error(datos?.mensaje || datos?.title ||
      `La verificacion no pudo completarse (${respuesta.status}).`);
  }
  return datos;
}

export async function validarRostroSimuladoRetiro(imagenDataUrl) {
  if (typeof imagenDataUrl !== "string" ||
    !imagenDataUrl.startsWith("data:image/png;base64,")) {
    throw new Error("No se recibio una imagen PNG valida.");
  }
  // El PNG existe solo en memoria: no se guarda en sessionStorage.
  const png = await (await fetch(imagenDataUrl)).blob();
  if (png.size === 0 || png.size > 1024 * 1024) {
    throw new Error("El tamano de la captura no es valido.");
  }
  const csrfToken = await solicitarCsrf();
  const respuesta = await fetch("/api/terminales/guardar/whatsapp/retiro/rostro", {
    method: "POST", credentials: "same-origin", cache: "no-store",
    headers: {
      "Content-Type": "application/octet-stream",
      "X-CSRF-TOKEN": csrfToken,
      "X-SYNCHRO-CONSENTIMIENTO": "ACEPTO",
    },
    body: png,
  });
  const datos = await respuestaSegura(respuesta);
  if (datos?.verificacionSimulada !== true || !datos?.tokenFacial) {
    throw new Error("No se obtuvo autorizacion facial del servidor.");
  }
  return datos.tokenFacial;
}

export async function confirmarRetiroFacialConPin(pin, tokenFacial) {
  if (typeof pin !== "string" || !/^\d{6}$/.test(pin) ||
      typeof tokenFacial !== "string" || !tokenFacial) {
    throw new Error("Verificacion facial o PIN incompletos.");
  }
  const csrfToken = await solicitarCsrf();
  const respuesta = await fetch("/api/terminales/guardar/whatsapp/retiro/pin", {
    method: "POST", credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrfToken },
    body: JSON.stringify({ pin, tokenFacial }),
  });
  const datos = await respuestaSegura(respuesta);
  if (datos?.retiroConfirmado !== true) {
    throw new Error("El servidor no confirmo el retiro.");
  }
  return datos;
}
// SYNCHRO-TABLET-RETIRO-PAGADO-V1
// Verifica rostro y PIN sin finalizar ni liberar el casillero.
export async function prevalidarRetiroFacialConPin(pin, tokenFacial) {
  if (typeof pin !== "string" || !/^\d{6}$/.test(pin) ||
      typeof tokenFacial !== "string" || !tokenFacial) {
    throw new Error("Verificacion facial o PIN incompletos.");
  }

  const csrfToken = await solicitarCsrf();

  const respuesta = await fetch(
    "/api/terminales/guardar/whatsapp/retiro/pin/prevalidar",
    {
      method: "POST",
      credentials: "same-origin",
      cache: "no-store",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrfToken
      },
      body: JSON.stringify({ pin, tokenFacial })
    }
  );

  const datos = await respuestaSegura(respuesta);

  if (datos?.pinValidado !== true ||
      datos?.pagoPendiente !== true ||
      datos?.retiroConfirmado !== false ||
      typeof datos?.autorizacionPago !== "string" ||
      !datos.autorizacionPago) {
    throw new Error("El servidor no autorizo la preparacion del cobro.");
  }

  return datos;
}

// Obtiene la orden real calculada por SQL Server.
// No recibe importes calculados por la tablet.
export async function prepararCobroRetiroPersonal(autorizacionPago) {
  if (typeof autorizacionPago !== "string" ||
      !autorizacionPago) {
    throw new Error("Falta autorizacion temporal para el cobro.");
  }

  const csrfToken = await solicitarCsrf();

  const respuesta = await fetch(
    "/api/terminales/guardar/whatsapp/retiro/cobro/preparar",
    {
      method: "POST",
      credentials: "same-origin",
      cache: "no-store",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrfToken
      },
      body: JSON.stringify({ autorizacionPago })
    }
  );

  const datos = await respuestaSegura(respuesta);

  if (typeof datos?.cobroId !== "string" ||
      !datos.cobroId ||
      typeof datos?.importeHnl !== "number" ||
      !Number.isFinite(datos.importeHnl) ||
      datos.importeHnl <= 0 ||
      datos?.estadoPago !== "Pendiente" ||
      datos?.pagoConfirmado !== false ||
      datos?.retiroConfirmado !== false) {
    throw new Error("La orden de cobro recibida no es valida.");
  }

  return datos;
}

// Solo se invocara despues de comprobar en el servidor
// que el operador confirmo el pago simulado.
export async function finalizarRetiroPersonalPagado(
  cobroId, autorizacionPago
) {
  if (typeof cobroId !== "string" || !cobroId ||
      typeof autorizacionPago !== "string" ||
      !autorizacionPago) {
    throw new Error("Falta la autorizacion del retiro pagado.");
  }

  const csrfToken = await solicitarCsrf();

  const respuesta = await fetch(
    "/api/terminales/guardar/whatsapp/retiro/cobro/finalizar",
    {
      method: "POST",
      credentials: "same-origin",
      cache: "no-store",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrfToken
      },
      body: JSON.stringify({ cobroId, autorizacionPago })
    }
  );

  const datos = await respuestaSegura(respuesta);

  if (datos?.retiroConfirmado !== true ||
      datos?.pagoAplicado !== true ||
      datos?.estadoCasillero !== "Disponible") {
    throw new Error("El servidor no confirmo el retiro pagado.");
  }

  return datos;
}
