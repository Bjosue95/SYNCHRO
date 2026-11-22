const DIMENSION = 1024;
const VIGENCIA_MS = 5 * 60 * 1000;

let descriptor = null;
let vence = 0;

export function borrarRostroCargaV3() {
  if (descriptor) descriptor.fill(0);
  descriptor = null;
  vence = 0;
}

export function guardarRostroCargaV3(entrada) {
  borrarRostroCargaV3();

  if (!Array.isArray(entrada) &&
      !ArrayBuffer.isView(entrada)) {
    return false;
  }

  if (entrada.length !== DIMENSION) {
    return false;
  }

  const valores = Array.from(entrada);

  if (valores.some(v =>
    !Number.isFinite(v) || Math.abs(v) > 1000
  )) {
    return false;
  }

  const norma = valores.reduce(
    (suma, v) => suma + v * v, 0
  );

  if (!Number.isFinite(norma) || norma < 1e-8) {
    return false;
  }

  descriptor = Float32Array.from(valores);
  vence = Date.now() + VIGENCIA_MS;

  return true;
}

export function copiarRostroCargaV3() {
  if (!descriptor || Date.now() >= vence) {
    borrarRostroCargaV3();
    return null;
  }

  return Array.from(descriptor);
}