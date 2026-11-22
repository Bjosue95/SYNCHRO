let paso = null;

export function registrarPasoCargaDemo(datos) {
  if (!/^(W[12]|C[1-4]|L[12])$/.test(datos.espacio)) {
    return false;
  }

  paso = {
    espacio: datos.espacio,
    dispositivo: datos.dispositivo,
    modalidad: datos.modalidad,
    vence: Date.now() + 10 * 60 * 1000
  };

  return true;
}

export function pasoCargaDemoValido(datos) {
  if (!paso || Date.now() >= paso.vence) {
    paso = null;
    return false;
  }

  return paso.espacio === datos.espacio &&
    paso.dispositivo === datos.dispositivo &&
    paso.modalidad === datos.modalidad;
}

export function limpiarPasoCargaDemo() {
  paso = null;
}