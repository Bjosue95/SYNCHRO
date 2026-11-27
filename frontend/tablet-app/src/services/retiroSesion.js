// Sesión efímera de retiro. No se guarda en URL, localStorage ni sessionStorage.
// Vite conserva la misma referencia de estado entre versiones del módulo durante HMR.
// Una recarga completa elimina la autorización y exige verificar nuevamente.
function crearEstadoRetiro() {
  return { autorizacionFacial:null, retiroReservaPersonal:null, ordenRetiroPagado:null, metodoPagoRetiro:null };
}
const estado = import.meta.hot
  ? (import.meta.hot.data.estadoRetiro ??= crearEstadoRetiro())
  : crearEstadoRetiro();

export function iniciarRetiro() {
  estado.autorizacionFacial = null;
  estado.retiroReservaPersonal = null;
  estado.ordenRetiroPagado = null;
  estado.metodoPagoRetiro = null;
}

export function registrarCaptura(token) {
  if (typeof token !== "string" || !token || token.length > 4096) {
    throw new Error("Autorizacion facial invalida.");
  }
  estado.autorizacionFacial = token;
}

export function obtenerRetiro() {
  return { capturaHecha: Boolean(estado.autorizacionFacial), autorizacionFacial: estado.autorizacionFacial };
}

export function limpiarRetiro(autorizacionEsperada) {
  if (typeof autorizacionEsperada === "string" && estado.ordenRetiroPagado?.autorizacionPago !== autorizacionEsperada) return;
  estado.autorizacionFacial = null;
  estado.retiroReservaPersonal = null;
  estado.ordenRetiroPagado = null;
  estado.metodoPagoRetiro = null;
}
// Almacenar unicamente en memoria, nunca en URL ni storage.
export function registrarOrdenRetiroPagado(
  autorizacionPago, orden, venceAutorizacion=Date.now()+5*60*1000
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

  estado.ordenRetiroPagado = Object.freeze({
    cobroId: orden.cobroId,
    codigoCasillero: orden.codigoCasillero,
    importeHnl: orden.importeHnl,
    minutosUtilizados: orden.minutosUtilizados,
    descripcionTarifa: orden.descripcionTarifa,
    venceAutorizacion,
    autorizacionPago
  });

  estado.autorizacionFacial = null;
  estado.metodoPagoRetiro = null;
  return estado.ordenRetiroPagado;
}

export function obtenerOrdenRetiroPagado() {
  return estado.ordenRetiroPagado;
}

export function registrarMetodoPagoRetiro(metodo) {
  if (!estado.ordenRetiroPagado) {
    throw new Error("No existe una orden de cobro activa.");
  }

  if (!["Efectivo", "Tarjeta", "NFC"].includes(metodo)) {
    throw new Error("Metodo de pago no permitido.");
  }

  // La eleccion NO confirma ningun pago.
  estado.metodoPagoRetiro = metodo;
}

export function obtenerMetodoPagoRetiro() {
  return estado.metodoPagoRetiro;
}
export function registrarRetiroReservaPersonal(datos) {
  if(datos?.retiroAutorizado!==true||datos?.retiroConfirmado!==false||
    datos?.origenReserva!=="Web"||datos?.pagoConfirmado!==true||
    typeof datos.autorizacionRetiro!=="string"||datos.autorizacionRetiro.length<32||
    !/^[A-Za-z0-9-]{1,10}$/.test(datos.codigoCasillero||""))
    throw new Error("Autorización de retiro inválida.");
  limpiarRetiro();
  estado.retiroReservaPersonal=Object.freeze({codigoCasillero:datos.codigoCasillero,
    autorizacionRetiro:datos.autorizacionRetiro,vence:Date.now()+Math.min(600,Math.max(1,Number(datos.vigenciaSegundos)||600))*1000});
}
export function obtenerRetiroReservaPersonal(){return estado.retiroReservaPersonal;}
