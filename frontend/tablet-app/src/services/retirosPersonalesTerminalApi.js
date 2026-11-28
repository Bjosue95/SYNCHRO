import { obtenerCsrfTerminal } from "./recuperarSesionTerminal.js";

/* SYNCHRO - Retiro personal protegido (misma sesion y CSRF que el deposito). */
export async function retirarPertenenciasPersonales(usoId, pin) {
  if (!Number.isInteger(usoId) || usoId <= 0) {
    throw new Error("Número de reserva inválido.");
  }
  if (typeof pin !== "string" || !/^\d{6}$/.test(pin)) {
    throw new Error("El PIN debe tener seis dígitos.");
  }

  const respuestaCsrf = await obtenerCsrfTerminal("/api/terminales/csrf-token");
  if (!respuestaCsrf.ok) {
    throw new Error(respuestaCsrf.status === 401
      ? "Servicio temporalmente no disponible. Intenta nuevamente o solicita ayuda al operador."
      : "No se pudo obtener el token de seguridad.");
  }
  const { csrfToken } = await respuestaCsrf.json();
  if (!csrfToken) throw new Error("No se recibió el token CSRF.");

  const respuesta = await fetch("/api/terminales/retiros-personales", {
    method: "POST",
    credentials: "same-origin",
    cache: "no-store",
    headers: {
      "Content-Type": "application/json",
      "X-CSRF-TOKEN": csrfToken,
    },
    body: JSON.stringify({ usoId, pin }),
  });

  if (!respuesta.ok) {
    const datos = await respuesta.json().catch(() => null);
    throw new Error(datos?.mensaje ||
      (respuesta.status === 401
        ? "Servicio temporalmente no disponible. Intenta nuevamente o solicita ayuda al operador."
        : `No se pudo registrar el retiro (${respuesta.status}).`));
  }
  return await respuesta.json();
}
