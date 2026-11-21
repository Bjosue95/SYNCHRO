<script setup>
import { computed } from "vue";
import { RouterLink, useRoute } from "vue-router";
import { useTema } from "../../composables/useTema";
import { buscarPantalla } from "../pantallas.js";
import { casillerosEjemplo } from "../datos/casilleros.js";
import { ordenPagoEjemplo } from "../datos/pago.js";

const route = useRoute();
const { oscuro } = useTema();

const vista = computed(() =>
  buscarPantalla(route.params.pantalla)
);

const propiedades = computed(() => {
  const id = vista.value?.id;

  if (id === "tamano" || id === "casillero") {
    return { casillerosEjemplo };
  }

  if (["whatsapp", "deposito", "pin"].includes(id)) {
    return { modoVistaPrevia: true };
  }

  if (id === "rostro-retirar") {
    return { modoRetiroEjemplo: true };
  }

  if (id === "pago" || id === "pago-resumen") {
    return { ordenEjemplo: ordenPagoEjemplo };
  }

  return {};
});

function bloquear(evento) {
  const permitido =
    (vista.value?.id === "pago" || vista.value?.id === "pago-resumen") &&
    ["click", "keydown"].includes(evento.type) &&
    evento.target?.closest?.("[data-preview-permitido]");

  if (permitido) return;

  evento.preventDefault();
  evento.stopPropagation();
}
</script>

<template>
  <div
    class="visor"
    :class="{
      'visor-bienvenida': vista?.id === 'bienvenida',
      'tema-oscuro': oscuro
    }"
  >
    <div
      class="pantalla-real"
    :class="{ 'pago-interactivo': vista?.id === 'pago' || vista?.id === 'pago-resumen' }"
      @click.capture="bloquear"
      @submit.capture="bloquear"
      @change.capture="bloquear"
      @keydown.capture="bloquear"
    >
      <component
        v-if="vista"
        :is="vista.componente"
        v-bind="propiedades"
      />
    </div>

    <RouterLink class="volver" to="/vista-previa">
      <span aria-hidden="true">‹</span>
      <span>Vistas previas</span>
    </RouterLink>
  </div>
</template>

<style scoped>
.visor {
  position: relative;
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
}

.pantalla-real {
  width: 100%;
  height: 100%;
  pointer-events: none;
}

.pantalla-real.pago-interactivo {
  pointer-events: auto;
}

.pantalla-real > :first-child {
  width: 100%;
  height: 100%;
}

.volver {
  position: absolute;
  right: 16px;
  bottom: 12px;
  z-index: 9;

  display: flex;
  align-items: center;
  gap: 8px;

  padding: 9px 15px;

  border: 1px solid #82b9c8;
  border-radius: 999px;

  background: #f4feff;
  color: #01305c;

  text-decoration: none;
  font-size: 13px;
  font-weight: 750;

  box-shadow: 0 3px 10px rgba(1, 48, 92, .18);
}

.volver span:first-child {
  font-size: 22px;
  line-height: .7;
}

.volver:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

.tema-oscuro .volver {
  background: #123d5b;
  color: #ffffff;
  border-color: #5ec6e8;
}

/* Bienvenida conserva su superficie de pantalla completa. */
#app .synchro-app .visor-bienvenida.vista-synchro {
  position: fixed;
  inset: 0;
  top: 0;
  width: 100%;
  height: 100dvh;
  z-index: 70;
}
</style>