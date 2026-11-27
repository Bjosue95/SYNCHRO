// SYNCHRO: sesion efimera en memoria. Nunca persistir el token facial en URL/storage.
let autorizacionFacial =
  import.meta.hot?.data?.autorizacionFacial ?? null;

let retiroReservaPersonal = null;

// SYNCHRO-TABLET-RETIRO-PAGADO-V1
let ordenRetiroPagado =
  import.meta.hot?.data?.ordenRetiroPagado ?? null;

let metodoPagoRetiro =
  import.meta.hot?.data?.metodoPagoRetiro ?? null;

// Conserva el estado solamente durante actualizaciones
// de modulos de Vite. No persiste al recargar la pagina.
if (import.meta.hot) {
  import.meta.hot.dispose((datos) => {
    datos.autorizacionFacial = autorizacionFacial;
    datos.ordenRetiroPagado = ordenRetiroPagado;
    datos.metodoPagoRetiro = metodoPagoRetiro;
  });
}

export function iniciarRetiro() {
  autorizacionFacial = null;
  retiroReservaPersonal = null;
  ordenRetiroPagado = null;
  metodoPagoRetiro = null;
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
  retiroReservaPersonal = null;
  ordenRetiroPagado = null;
  metodoPagoRetiro = null;
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
  metodoPagoRetiro = null;
}

export function obtenerOrdenRetiroPagado() {
  return ordenRetiroPagado;
}

export function registrarMetodoPagoRetiro(metodo) {
  if (!ordenRetiroPagado) {
    throw new Error("No existe una orden de cobro activa.");
  }

  if (!["Efectivo", "Tarjeta", "NFC"].includes(metodo)) {
    throw new Error("Metodo de pago no permitido.");
  }

  // La eleccion NO confirma ningun pago.
  metodoPagoRetiro = metodo;
}

export function obtenerMetodoPagoRetiro() {
  return metodoPagoRetiro;
}
export function registrarRetiroReservaPersonal(datos) {
  if(datos?.retiroAutorizado!==true||datos?.retiroConfirmado!==false||
    datos?.origenReserva!=="Web"||datos?.pagoConfirmado!==true||
    typeof datos.autorizacionRetiro!=="string"||datos.autorizacionRetiro.length<32||
    !/^[A-Za-z0-9-]{1,10}$/.test(datos.codigoCasillero||""))
    throw new Error("Autorización de retiro inválida.");
  limpiarRetiro();
  retiroReservaPersonal=Object.freeze({codigoCasillero:datos.codigoCasillero,
    autorizacionRetiro:datos.autorizacionRetiro,vence:Date.now()+Math.min(600,Math.max(1,Number(datos.vigenciaSegundos)||600))*1000});
}
export function obtenerRetiroReservaPersonal(){return retiroReservaPersonal;}
