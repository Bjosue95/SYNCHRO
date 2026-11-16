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
