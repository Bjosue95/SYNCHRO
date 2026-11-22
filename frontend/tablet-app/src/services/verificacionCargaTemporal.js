let verificacion = null;

export function registrarVerificacionCarga(datos) {
  verificacion = {
    espacio: datos.espacio,
    dispositivo: datos.dispositivo,
    modalidad: datos.modalidad,
    vence: Date.now() + 180000
  };
}

export function verificacionCargaValida(datos) {
  if (!verificacion || Date.now() > verificacion.vence) {
    verificacion = null;
    return false;
  }

  return verificacion.espacio === datos.espacio &&
    verificacion.dispositivo === datos.dispositivo &&
    verificacion.modalidad === datos.modalidad;
}

export function limpiarVerificacionCarga() {
  verificacion = null;
}