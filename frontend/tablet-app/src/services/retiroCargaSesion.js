/*
 * SYNCHRO: retiro de dispositivos.
 * Tokens temporales exclusivamente en memoria.
 * Nunca URL, localStorage ni sessionStorage.
 */

let sesion = null;

const CODIGO = /^(?:W[12]|C[1-4]|L[12])$/;

export function iniciarRetiroCarga() {
  limpiarRetiroCarga();
  sesion = {
    codigo: null,
    tokenFacial: null,
    venceFacial: 0,
    tokenCobro: null,
    venceCobro: 0
  };
}
export function obtenerEspacioRetiroCarga() {
  return sesion?.codigo ?? null;
}

export function guardarRostroRetiroCarga(token) {
  if (!sesion || typeof token !== "string" || !token) {
    throw new Error("Autorizacion facial invalida.");
  }
  sesion.tokenFacial = token;
  sesion.venceFacial = Date.now() + 90 * 1000;
}
export function obtenerTokenFacialRetiroCarga() {
  if (!sesion?.tokenFacial ||
      Date.now() >= sesion.venceFacial) {
    return null;
  }

  return sesion.tokenFacial;
}

export function guardarPinValidadoRetiroCarga(token, codigo) {
  if (!sesion || typeof token !== "string" || !token || !CODIGO.test(codigo)) {
    throw new Error("Autorizacion de cobro invalida.");
  }
  sesion.codigo = codigo;
  sesion.tokenFacial = null;
  sesion.venceFacial = 0;
  sesion.tokenCobro = token;
  sesion.venceCobro = Date.now() + 4 * 60 * 1000;
}
export function obtenerTokenCobroRetiroCarga() {
  if (!sesion?.tokenCobro ||
      Date.now() >= sesion.venceCobro) {
    return null;
  }

  return sesion.tokenCobro;
}

export function limpiarRetiroCarga() {
  sesion = null;
}