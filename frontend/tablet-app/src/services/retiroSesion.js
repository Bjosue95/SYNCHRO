// SYNCHRO: sesion efimera en memoria. Nunca persistir el token facial en URL/storage.
let autorizacionFacial = null;

// SYNCHRO-TABLET-RETIRO-PAGADO-V1
let ordenRetiroPagado = null;

export function iniciarRetiro() {
  autorizacionFacial = null;
  ordenRetiroPagado = null;
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
  ordenRetiroPagado = null;
}
// Almacenar unicamente en memoria, nunca en URL ni storage.
export function registrarOrdenRetiroPagado(
  autorizacionPago, orden
) {
  if (typeof autorizacionPago !== "string" ||
      !autorizacionPago ||
      typeof orden?.cobroId !== "string" ||
      !orden.cobroId ||
      typeof orden?.codigoCasillero !== "string" ||
      !orden.codigoCasillero ||
      typeof orden?.importeHnl !== "number" ||
      !Number.isFinite(orden.importeHnl) ||
      orden.importeHnl <= 0 ||
      orden.estadoPago !== "Pendiente") {
    throw new Error("La orden de retiro pagado no es valida.");
  }

  ordenRetiroPagado = Object.freeze({
    cobroId: orden.cobroId,
    codigoCasillero: orden.codigoCasillero,
    importeHnl: orden.importeHnl,
    minutosUtilizados: orden.minutosUtilizados,
    descripcionTarifa: orden.descripcionTarifa,
    autorizacionPago
  });

  autorizacionFacial = null;
}

export function obtenerOrdenRetiroPagado() {
  return ordenRetiroPagado;
}
