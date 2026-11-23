import { nextTick, ref, readonly } from "vue";

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

// El mismo fundido global de la tablet SYNCHRO (220 ms).
// La preferencia sigue guardándose solo cuando el usuario la modifica.
let ultimoCambio = -Infinity;
let transicionActiva = false;

function cambiarTemaConFundido(nuevo) {
  if (nuevo !== "claro" && nuevo !== "oscuro" || nuevo === tema.value) return;

  const ahora = typeof performance !== "undefined" ? performance.now() : Date.now();
  if (transicionActiva || ahora - ultimoCambio < 250) return;
  ultimoCambio = ahora;

  const cambiar = () => aplicarTema(nuevo, true);
  const reducirMovimiento = window.matchMedia?.(
    "(prefers-reduced-motion: reduce)"
  )?.matches;

  if (reducirMovimiento || typeof document.startViewTransition !== "function") {
    cambiar();
    return;
  }

  transicionActiva = true;
  const raiz = document.documentElement;
  raiz.classList.add("synchro-tema-instantaneo");

  let transicion;
  try {
    transicion = document.startViewTransition(async () => {
      cambiar();
      await nextTick();
    });
  } catch {
    raiz.classList.remove("synchro-tema-instantaneo");
    transicionActiva = false;
    if (tema.value !== nuevo) cambiar();
    return;
  }

  const limpiar = () => {
    raiz.classList.remove("synchro-tema-instantaneo");
    transicionActiva = false;
  };
  transicion.finished.then(limpiar, limpiar);
}

export function useTheme() {
  return {
    tema: readonly(tema),
    establecerTema: cambiarTemaConFundido,
    alternarTema: () => cambiarTemaConFundido(
      tema.value === "oscuro" ? "claro" : "oscuro"
    )
  };
}
