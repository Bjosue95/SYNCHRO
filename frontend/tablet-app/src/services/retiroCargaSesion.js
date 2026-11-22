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
    venceCobro: 0,
    ordenCobro: null,
    metodoPago: null
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

export function guardarOrdenCobroRetiroCarga(orden) {
  if (!obtenerTokenCobroRetiroCarga() || !sesion ||
      orden?.codigoEspacio !== sesion.codigo ||
      !/^(?:W[12]|C[1-4]|L[12])$/.test(orden.codigoEspacio) ||
      !["Celular", "Laptop"].includes(orden.dispositivo) ||
      !Number.isInteger(orden.minutosUtilizados) ||
      orden.minutosUtilizados < 1 ||
      !Number.isFinite(orden.importeHnl) ||
      orden.importeHnl <= 0 ||
      typeof orden.descripcionTarifa !== "string") {
    throw new Error("El resumen de cobro de carga no es valido.");
  }
  sesion.ordenCobro = Object.freeze({
    codigoEspacio: orden.codigoEspacio,
    dispositivo: orden.dispositivo,
    minutosUtilizados: orden.minutosUtilizados,
    importeHnl: orden.importeHnl,
    descripcionTarifa: orden.descripcionTarifa,
    pagoSimulado: orden.pagoSimulado === true
  });
  sesion.metodoPago = null;
}
export function obtenerOrdenCobroRetiroCarga() {
  if (!obtenerTokenCobroRetiroCarga()) return null;
  return sesion?.ordenCobro ?? null;
}
export function guardarMetodoPagoRetiroCarga(metodo) {
  if (!obtenerOrdenCobroRetiroCarga() ||
      !["Efectivo", "Tarjeta", "NFC"].includes(metodo)) {
    throw new Error("No se puede seleccionar el metodo de pago de carga.");
  }
  sesion.metodoPago = metodo;
}
export function obtenerMetodoPagoRetiroCarga() {
  return obtenerOrdenCobroRetiroCarga() ? sesion?.metodoPago ?? null : null;
}
export function limpiarRetiroCarga() {
  sesion = null;
}