import { ref, readonly } from "vue";

const CLAVE = "synchro-usuarios-tema";
const preferencia = typeof window !== "undefined" &&
  window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "oscuro" : "claro";

function temaInicial() {
  try {
    const almacenado = window.localStorage.getItem(CLAVE);
    if (almacenado === "claro" || almacenado === "oscuro") return almacenado;
  } catch {
    // Preferencia local opcional, nunca datos de cuenta.
  }
  return preferencia;
}

const tema = ref(typeof window === "undefined" ? "claro" : temaInicial());

function aplicarTema(nuevo, guardar = false) {
  if (nuevo !== "claro" && nuevo !== "oscuro") return;
  tema.value = nuevo;
  if (typeof document !== "undefined") {
    document.documentElement.dataset.theme = nuevo;
    document.documentElement.style.colorScheme =
      nuevo === "oscuro" ? "dark" : "light";
  }
  if (guardar) {
    try { window.localStorage.setItem(CLAVE, nuevo); } catch { /* opcional */ }
  }
}

if (typeof window !== "undefined") {
  aplicarTema(tema.value);
  const media = window.matchMedia?.("(prefers-color-scheme: dark)");
  media?.addEventListener?.("change", evento => {
    let preferenciaManual = false;
    try { preferenciaManual = window.localStorage.getItem(CLAVE) !== null; }
    catch { /* la preferencia local puede estar deshabilitada */ }
    if (!preferenciaManual) aplicarTema(evento.matches ? "oscuro" : "claro");
  });
}

export function useTheme() {
  return {
    tema: readonly(tema),
    establecerTema: nuevo => aplicarTema(nuevo, true),
    alternarTema: () => aplicarTema(tema.value === "oscuro" ? "claro" : "oscuro", true)
  };
}
