import { ref } from "vue";

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
    oscuro.value = !oscuro.value;
  }

  return {
    oscuro,
    cambiarTema,
  };
}
