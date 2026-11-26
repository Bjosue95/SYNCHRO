let actual = null;

export function iniciarReservaWebDeposito(reserva, pin) {
  if (
    !Number.isInteger(reserva?.reservaId) ||
    reserva.reservaId <= 0 ||
    typeof reserva.casillero !== "string" ||
    !/^\d{6}$/.test(pin)
  ) {
    throw new Error("Los datos de la reserva no son válidos.");
  }

  actual = {
    reservaId: reserva.reservaId,
    casillero: reserva.casillero,
    servicio: reserva.servicio,
    tipoCasillero: reserva.tipoCasillero,
    pin,
    vence: Date.now() + 5 * 60 * 1000,
  };
}

export function obtenerReservaWebDeposito() {
  if (!actual) return null;
  if (Date.now() >= actual.vence) {
    actual = null;
    return null;
  }
  return actual;
}

export function limpiarReservaWebDeposito() {
  actual = null;
}
