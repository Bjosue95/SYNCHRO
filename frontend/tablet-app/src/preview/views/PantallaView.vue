<script setup>
import { computed } from "vue";
import { RouterLink, useRoute } from "vue-router";
import { useTema } from "../../composables/useTema";
import { buscarPantalla } from "../pantallas.js";
import { casillerosEjemplo } from "../datos/casilleros.js";
import { ordenPagoEjemplo } from "../datos/pago.js";
import { espaciosCargaEjemplo, ordenCargaEjemplo } from "../datos/carga.js";
import { normalizarMetodoPagoDemo } from "../pagoDemo.js";

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

  if (id === "pago") {
    return { ordenEjemplo: ordenPagoEjemplo };
  }

  if (id === "pago-resumen") {
    return {
      ordenEjemplo: ordenPagoEjemplo,
      metodoEjemplo: normalizarMetodoPagoDemo(route.query.metodo) || "NFC"
    };
  }

  // Vistas nuevas reutilizan el componente operativo, con datos locales
  // y un modo que impide peticiones HTTP y cambios de sesiones.
  if (id?.startsWith("carga-espacios-")) {
    return { modoVistaPrevia: true, espaciosEjemplo: espaciosCargaEjemplo };
  }

  if (id === "carga-crear-pin" || id === "carga-confirmar-pin") {
    return {
      modoVistaPrevia: true,
      pasoEjemplo: id === "carga-confirmar-pin" ? "confirmar" : "crear"
    };
  }

  if (id === "carga-pin-confirmado") {
    return { modoVistaPrevia: true };
  }

  if (id === "carga-colocar" || id === "carga-colocacion-confirmada") {
    return {
      modoVistaPrevia: true,
      estadoEjemplo: id === "carga-colocacion-confirmada" ? "confirmado" : "listo"
    };
  }

  if (id === "retiro-carga-rostro") {
    return { modoRetiroEjemplo: true };
  }

  if (id === "retiro-carga-pin") {
    return { modoVistaPrevia: true };
  }

  if (id === "retiro-carga-pago") {
    return { ordenEjemplo: ordenCargaEjemplo, esCargaEjemplo: true };
  }

  if (id?.startsWith("retiro-carga-")) {
    const faseEjemplo = ({
      "retiro-carga-espera": "esperando",
      "retiro-carga-resumen": "resumen",
      "retiro-carga-completado": "completado"
    })[id];
    if (faseEjemplo) {
      return {
        ordenEjemplo: ordenCargaEjemplo,
        faseEjemplo,
        metodoEjemplo: normalizarMetodoPagoDemo(route.query.metodo) || "Efectivo"
      };
    }
  }

  return {};
});

function bloquear(evento) {
  const id = vista.value?.id;
  const permitido =
    ["pago", "pago-resumen", "retiro-carga-pago", "carga-colocar"].includes(id) &&
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
    :class="{ 'pago-interactivo': ['pago', 'pago-resumen', 'retiro-carga-pago', 'carga-colocar'].includes(vista?.id) }"
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