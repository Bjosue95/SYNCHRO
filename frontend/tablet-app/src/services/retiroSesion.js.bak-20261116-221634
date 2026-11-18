// SYNCHRO: sesion efimera en memoria. Nunca persistir el token facial en URL/storage.
let autorizacionFacial = null;

export function iniciarRetiro() {
  autorizacionFacial = null;
}

export function registrarCaptura(token) {
  if (typeof token !== "string" || !token || token.length > 4096) {
    throw new Error("Autorizacion facial invalida.");
  }
  autorizacionFacial = token;
}

export function obtenerRetiro() {
  return { capturaHecha: Boolean(autorizacionFacial), autorizacionFacial };
}

export function limpiarRetiro() {
  autorizacionFacial = null;
}
