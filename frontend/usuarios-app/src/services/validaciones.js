export const TAMANOS_ENCOMIENDA = Object.freeze([
  { valor: "Pequeno", nombre: "Pequeño", detalle: "Objetos compactos" },
  { valor: "Mediano", nombre: "Mediano", detalle: "Paquetes de tamaño regular" },
  { valor: "Grande", nombre: "Grande", detalle: "Paquetes voluminosos" }
]);

export function usuarioValido(valor) {
  return typeof valor === "string" &&
    /^[\p{L}\p{N}._-]{3,80}$/u.test(valor.trim());
}

export function contrasenaValida(valor) {
  return typeof valor === "string" && valor.length >= 12 && valor.length <= 128;
}

export function telefonoValido(valor) {
  return typeof valor === "string" && /^\+?[1-9][0-9]{7,14}$/.test(valor.trim());
}

export function normalizarTelefono(valor) {
  const telefono = String(valor ?? "").trim();
  return telefonoValido(telefono)
    ? (telefono.startsWith("+") ? telefono : "+" + telefono)
    : null;
}

export function tamanoValido(valor) {
  return TAMANOS_ENCOMIENDA.some(opcion => opcion.valor === valor);
}

export function tokenReservaValido(valor) {
  return typeof valor === "string" && /^[0-9a-fA-F]{64}$/.test(valor.trim());
}

export function referenciaReservaValida(valor) {
  return /^[1-9][0-9]{0,9}$/.test(String(valor ?? "").trim()) &&
    Number.isSafeInteger(Number(valor)) && Number(valor) <= 2147483647;
}
