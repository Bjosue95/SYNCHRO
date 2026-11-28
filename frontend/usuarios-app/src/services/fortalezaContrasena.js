// Estimación orientativa local: no es una prueba de entropía ni de filtraciones.
// No transmite, registra ni persiste contraseñas. La política de alta sigue
// siendo 12–128 unidades UTF-16 (igual que la validación del backend).
const niveles = ["Muy débil", "Poco segura", "Aceptable", "Segura", "Muy segura"];
const comunes = /^(?:password|contrasena|contraseña|clave|admin|administrador|synchro|qwerty|bienvenido|welcome|iloveyou|teamo|123456|abc123)[\d\p{P}\p{S}\s]*$/iu;
const predecibles = /(?:123456|654321|abcdef|fedcba|qwerty|asdfgh|098765|987654)/i;
function compactar(valor) {
  return String(valor ?? "").normalize("NFKD").replace(/\p{M}/gu, "").toLowerCase().replace(/[^\p{L}\p{N}]/gu, "");
}
function repetida(texto) {
  for (let n = 1; n <= Math.floor(texto.length / 2); n++) {
    if (texto.length % n === 0 && texto.slice(0,n).repeat(texto.length/n) === texto) return true;
  }
  return false;
}

export function evaluarFortalezaContrasena(valor, usuario = "", telefono = "") {
  const texto = typeof valor === "string" ? valor : "";
  const longitud = texto.length;
  const reglas = [
    { clave: "longitud", texto: "12 caracteres como mínimo", cumple: longitud >= 12 && longitud <= 128 },
    { clave: "mayuscula", texto: "Una mayúscula", cumple: /\p{Lu}/u.test(texto) },
    { clave: "minuscula", texto: "Una minúscula", cumple: /\p{Ll}/u.test(texto) },
    { clave: "numero", texto: "Un número", cumple: /\p{N}/u.test(texto) },
    { clave: "simbolo", texto: "Un símbolo", cumple: /[\p{P}\p{S}]/u.test(texto) }
  ];
  if (!longitud) return { nivel: 0, etiqueta: "", mensaje: "", reglas };
  const compacta = compactar(texto);
  const nombre = compactar(usuario);
  const numero = String(telefono ?? "").replace(/\D/g,"");
  const datosPersonales = (nombre.length >= 3 && compacta.includes(nombre)) ||
    (numero.length >= 8 && compacta.includes(numero.slice(-8)));
  const patron = comunes.test(texto.trim()) || repetida(compacta || texto) ||
    new Set(texto.toLowerCase()).size <= 3 || /^\d+$/.test(texto);
  const secuencia = predecibles.test(compacta);
  const palabras = texto.trim().split(/\s+/u).filter(p => /\p{L}{2}/u.test(p));
  const frase = palabras.length >= 4 && new Set(palabras.map(compactar)).size >= 4;
  const variedad = reglas.slice(1).filter(r => r.cumple).length;
  let nivel = 1;
  let mensaje = "Usa una contraseña más larga y difícil de adivinar.";
  if (longitud < 12) {
    mensaje = "Necesitas al menos 12 caracteres para crear tu cuenta.";
    nivel = longitud >= 8 && !patron && !datosPersonales ? 2 : 1;
  } else if (longitud > 128) {
    mensaje = "La contraseña admite hasta 128 caracteres.";
  } else if (patron || datosPersonales) {
    mensaje = datosPersonales ? "Evita usar tu nombre de usuario o teléfono." : "Evita claves comunes, números solos y repeticiones.";
  } else if (secuencia) {
    nivel = 2;
    mensaje = "Evita secuencias fáciles de adivinar.";
  } else {
    nivel = 2;
    if (longitud >= 15 || variedad >= 3) nivel = 3;
    if ((longitud >= 16 && variedad >= 3) || (longitud >= 20 && variedad >= 2) || (longitud >= 20 && frase)) nivel = 4;
    if ((longitud >= 20 && variedad >= 4) || (longitud >= 25 && frase) || (longitud >= 26 && variedad >= 3)) nivel = 5;
    mensaje = nivel <= 2 ? "Añade longitud o combina palabras que no tengan relación."
      : nivel === 3 ? "Puedes mejorarla con más longitud o una frase de palabras distintas."
      : "Buena fortaleza estimada. No reutilices esta contraseña en otros sitios.";
  }
  return { nivel, etiqueta: niveles[nivel-1], mensaje, reglas };
}
