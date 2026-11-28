// Preferencia local, sin identificadores personales ni llamadas externas.
export const CLAVE_COOKIES = "synchro-preferencias-cookies";
export const VERSION_COOKIES = 1;
export const DURACION_COOKIES = 180 * 24 * 60 * 60 * 1000;

export function leerPreferencias(almacen, ahora = Date.now()) {
  try {
    const datos = JSON.parse(almacen.getItem(CLAVE_COOKIES));
    if (!datos || datos.version !== VERSION_COOKIES ||
        typeof datos.mapa !== "boolean" ||
        !Number.isFinite(datos.vence) || datos.vence <= ahora) return null;
    return datos;
  } catch { return null; }
}

export function crearPreferencias(mapa, ahora = Date.now()) {
  return { version: VERSION_COOKIES, mapa: mapa === true, vence: ahora + DURACION_COOKIES };
}

export function persistirPreferencias(almacen, datos) {
  try { almacen.setItem(CLAVE_COOKIES, JSON.stringify(datos)); return true; }
  catch { return false; }
}
