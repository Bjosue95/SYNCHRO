<script setup>
import { computed } from "vue";

const props = defineProps({
  codigo: {
    type: String,
    required: true,
  },
  tamano: {
    type: String,
    required: true,
  },
  tipo: {
    type: String,
    default: "Normal",
  },
  estado: {
    type: String,
    required: true,
  },
  oscuro: {
    type: Boolean,
    default: false,
  },
  seleccionado: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(["seleccionar"]);

// Solo se pueden elegir casilleros normales disponibles
const habilitado = computed(() => {
  return props.tipo === "Normal" && props.estado === "Disponible";
});

function seleccionar() {
  if (!habilitado.value) return;

  emit("seleccionar", props.codigo);
}
</script>

<template>
  <button
    type="button"
    class="tarjeta-casillero"
    :class="{
      'tema-oscuro': oscuro,
      seleccionada: seleccionado,
      'no-disponible': !habilitado,
    }"
    :disabled="!habilitado"
    :aria-pressed="seleccionado"
    @click="seleccionar"
  >
    <span class="representacion" aria-hidden="true">
      <span class="cerradura"></span>
    </span>

    <span class="codigo">
      {{ codigo }}
    </span>

    <span class="tamano">
      {{ tamano }}
    </span>

    <span class="estado">
      {{
        !habilitado
          ? "No disponible"
          : seleccionado
            ? "Seleccionado"
            : "Disponible"
      }}
    </span>
  </button>
</template>

<style scoped>
/* SYNCHRO | CASILLERO INDIVIDUAL */

.tarjeta-casillero {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;

  width: 100%;
  min-height: 205px;
  padding: 20px;

  border: 2px solid #dfeaf0;
  border-radius: 20px;

  background: #ffffff;
  color: #01305c;

  font: inherit;
  text-align: center;
  cursor: pointer;

  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    color 150ms ease;
}

/* Representación de la puerta */

.representacion {
  position: relative;
  display: block;

  width: 58px;
  height: 68px;

  border: 2px solid #01305c;
  border-radius: 8px;
  background: #e4f2f6;

  transition:
    background-color 150ms ease,
    border-color 150ms ease;
}

.cerradura {
  position: absolute;
  right: 8px;
  top: 50%;

  width: 6px;
  height: 13px;

  border-radius: 3px;
  background: #01305c;
  transform: translateY(-50%);
}

/* Información */

.codigo {
  font-size: 23px;
  font-weight: 700;
}

.tamano {
  font-size: 14px;
}

.estado {
  min-height: 20px;
  color: #008c99;
  font-size: 14px;
  font-weight: 600;
}

/* Selección */

.tarjeta-casillero.seleccionada {
  border-color: #00b9c7;
}

/* No disponible */

.tarjeta-casillero.no-disponible {
  opacity: 0.65;
  cursor: not-allowed;
}

.no-disponible .estado {
  color: #5b7282;
}

/* Tema oscuro */

.tarjeta-casillero.tema-oscuro {
  background: #103956;
  border-color: #315573;
  color: #ffffff;
}

.tema-oscuro .representacion {
  background: #204766;
  border-color: #5ec6e8;
}

.tema-oscuro .cerradura {
  background: #5ec6e8;
}

.tema-oscuro .estado {
  color: #5ec6e8;
}

.tema-oscuro.seleccionada {
  border-color: #00b9c7;
}

/* Accesibilidad */

.tarjeta-casillero:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

@media (prefers-reduced-motion: reduce) {
  .tarjeta-casillero,
  .representacion {
    transition: none;
  }
}
</style>
