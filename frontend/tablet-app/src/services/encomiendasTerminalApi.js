/* SYNCHRO: operaciones de encomiendas exclusivas de la tablet.
 * Estas solicitudes NO habilitan depósitos ni sustituyen WhatsApp o verificación facial.
 */
async function leer(respuesta) {
  const datos = await respuesta.json().catch(() => null);
  if (!respuesta.ok) {
    if (respuesta.status === 401) throw new Error("La sesión de la terminal venció. Vuelve a vincularla.");
    if (respuesta.status === 429) throw new Error("Demasiadas solicitudes. Intenta más tarde.");
    throw new Error(datos?.mensaje || `Error HTTP ${respuesta.status}`);
  }
  return datos;
}

async function enviarProtegido(ruta, datos) {
  const { csrfToken } = await leer(await fetch("/api/terminales/csrf-token", {
    credentials: "same-origin", cache: "no-store",
  }));
  if (!csrfToken) throw new Error("No se recibió el token CSRF.");
  return leer(await fetch(ruta, {
    method: "POST", credentials: "same-origin", cache: "no-store",
    headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrfToken },
    body: JSON.stringify(datos),
  }));
}

export async function consultarReservaDesdeTerminal(referencia, tokenGestion) {
  return enviarProtegido("/api/terminales/encomiendas/reservas/consultar", {
    referencia, tokenGestion,
  });
}

export async function solicitarEnvioVisitante(datos) {
  return enviarProtegido("/api/terminales/encomiendas/visitantes/solicitar", datos);
}
