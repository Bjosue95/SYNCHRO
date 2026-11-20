const DIMENSION = 1024;
const VIGENCIA_MS = 2 * 60 * 1000;

let temporal = null;
let vence = 0;

export function borrarDescriptorV3() {
  if (temporal) temporal.fill(0);
  temporal = null;
  vence = 0;
}

export function registrarDescriptorV3(entrada) {
  borrarDescriptorV3();

  if (!Array.isArray(entrada) && !ArrayBuffer.isView(entrada)) {
    return false;
  }

  const valores = Array.from(entrada);

  if (valores.length !== DIMENSION ||
      valores.some(v => !Number.isFinite(v) || Math.abs(v) > 1000)) {
    return false;
  }

  const norma = valores.reduce((suma, v) => suma + v * v, 0);

  if (!Number.isFinite(norma) || norma < 1e-8) {
    return false;
  }

  temporal = Float32Array.from(valores);
  vence = Date.now() + VIGENCIA_MS;
  return true;
}

export function copiarDescriptorV3() {
  if (!temporal || Date.now() >= vence) {
    borrarDescriptorV3();
    return null;
  }

  return Array.from(temporal);
}
export function consumirDescriptorV3() {
  if (!temporal || Date.now() >= vence) {
    borrarDescriptorV3();
    return null;
  }

  const copia = Array.from(temporal);
  borrarDescriptorV3();
  return copia;
}