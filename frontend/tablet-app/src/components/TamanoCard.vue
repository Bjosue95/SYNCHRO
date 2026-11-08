<script setup>
defineProps({
  tamano: {
    type: String,
    required: true,
  },

  titulo: {
    type: String,
    required: true,
  },

  descripcion: {
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

  disponibles: {
    type: Number,
    default: null,
  },
});

const emit = defineEmits(["seleccionar"]);
</script>

<template>
  <button
    type="button"
    class="tarjeta-tamano"
    :class="{
      'tema-oscuro': oscuro,
      seleccionada: seleccionado,
      'sin-disponibilidad': disponibles === 0,
    }"
    :aria-pressed="seleccionado"
    :disabled="disponibles === null || disponibles === 0"
    @click="emit('seleccionar', tamano)"
  >
    <!-- Representación del tamaño -->
    <span class="casillero" :class="`tamano-${tamano}`">
      <span>{{ tamano }}</span>
    </span>

    <span class="titulo">{{ titulo }}</span>

    <span class="descripcion">
      {{ descripcion }}
    </span>

    <span
      class="disponibilidad"
      :class="{ 'sin-datos': disponibles === null }"
      aria-live="polite"
    >
      {{
        disponibles === null
          ? "Pendiente"
          : disponibles === 0
            ? "No disponible"
            : `${disponibles} disponibles`
      }}
    </span>

    <span class="indicador" :class="{ 'indicador-oculto': !seleccionado }">
      Seleccionado
    </span>
  </button>
</template>

<style scoped>
/* SYNCHRO | SELECCIÓN DE TAMAÑO */

.tarjeta-tamano {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;

  width: 100%;
  min-height: 220px;
  padding: 20px;

  background: #ffffff;
  border: 2px solid #dfeaf0;
  border-radius: 20px;

  color: #01305c;
  font: inherit;
  text-align: center;
  cursor: pointer;

  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    color 150ms ease;
}

.tarjeta-tamano:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

/* Ilustración del casillero */

.casillero {
  display: flex;
  align-items: center;
  justify-content: center;

  width: 62px;
  background: #e4f2f6;
  border: 2px solid #01305c;
  border-radius: 8px;

  color: #01305c;
  font-size: 23px;
  font-weight: 700;

  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    color 150ms ease;
}

.tamano-S {
  height: 58px;
}

.tamano-M {
  height: 76px;
}

.tamano-L {
  height: 96px;
}

/* Textos */

.titulo {
  font-size: 20px;
  font-weight: 700;
}

.descripcion {
  color: #5b7282;
  font-size: 14px;
  line-height: 1.4;

  transition: color 150ms ease;
}

/* Selección */

.tarjeta-tamano.seleccionada {
  border-color: #00b9c7;
}

.indicador {
  color: #008c99;
  font-size: 13px;
  font-weight: 700;
}

/* Reservar espacio para evitar movimientos */
.indicador-oculto {
  visibility: hidden;
}

/* Modo oscuro */

.tarjeta-tamano.tema-oscuro {
  background: #103956;
  border-color: #315573;
  color: #ffffff;
}

.tema-oscuro .casillero {
  background: #204766;
  border-color: #5ec6e8;
  color: #ffffff;
}

.tema-oscuro .descripcion {
  color: #c6dfea;
}

.tema-oscuro .indicador {
  color: #5ec6e8;
}

.tema-oscuro.seleccionada {
  border-color: #00b9c7;
}

/* Accesibilidad */

@media (prefers-reduced-motion: reduce) {
  .tarjeta-tamano,
  .casillero,
  .descripcion {
    transition: none;
  }
}

/* Estado de disponibilidad de casilleros */

.disponibilidad {
  display: block;
  min-height: 20px;
  color: #008c99;
  font-size: 14px;
  font-weight: 600;
}

.disponibilidad.sin-datos {
  visibility: hidden;
}

.tarjeta-tamano.sin-disponibilidad {
  cursor: not-allowed;
}

.sin-disponibilidad .disponibilidad {
  color: #5b7282;
}

/* Disponibilidad en modo oscuro */

.tema-oscuro .disponibilidad {
  color: #5ec6e8;
}

.tema-oscuro.sin-disponibilidad .disponibilidad {
  color: #c6dfea;
}
</style>
