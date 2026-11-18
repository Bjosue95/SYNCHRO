import { ref, nextTick } from "vue";

// Estado compartido por todas las pantallas
const oscuro = ref(false);

// Evita pulsaciones excesivamente rápidas
let ultimoCambio = -Infinity;

export function useTema() {
  function cambiarTema() {
    const ahora = performance.now();

    if (ahora - ultimoCambio < 250) {
      return;
    }

    ultimoCambio = ahora;

    const cambiar = () => {
      oscuro.value = !oscuro.value;
    };

    const reducir =
      window.matchMedia?.(
        "(prefers-reduced-motion: reduce)"
      )?.matches;

    if (reducir || typeof document.startViewTransition !== "function") {
      cambiar();
      return;
    }

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
      cambiar();
      return;
    }

    const limpiar = () => {
      raiz.classList.remove("synchro-tema-instantaneo");
    };

    transicion.finished.then(limpiar, limpiar);
  }

  return {
    oscuro,
    cambiarTema,
  };
}
