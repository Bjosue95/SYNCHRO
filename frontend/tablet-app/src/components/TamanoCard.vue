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
    <!-- Ilustraciones originales creadas para SYNCHRO -->
    <span class="marco-icono" aria-hidden="true">
      <img
        :src="`/iconos/casillero-tamano-${tamano}.svg`"
        alt=""
        class="imagen-casillero icono-claro"
      />

      <img
        :src="`/iconos/casillero-tamano-${tamano}-oscuro.svg`"
        alt=""
        class="imagen-casillero icono-oscuro"
      />
    </span>

    <span class="titulo">{{ titulo }}</span>

    <span class="descripcion">
      {{ descripcion }}
    </span>

    <span class="disponibilidad" aria-live="polite">
      {{
        disponibles === null
          ? "Consultando..."
          : disponibles === 0
            ? "No disponible"
            : `${disponibles} disponibles`
      }}
    </span>

    <span
      class="indicador"
      :class="{ 'indicador-oculto': !seleccionado }"
    >
      Seleccionado
    </span>
  </button>
</template>

<style scoped>
/* Una sola fuente para el tamaño interno de las tarjetas. */
.tarjeta-tamano {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: clamp(4px, 1vh, 9px);
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(10px, 1.6vh, 16px);
  border: 2px solid #dfeaf0;
  border-radius: 18px;
  background: #ffffff;
  color: #01305c;
  text-align: center;
  font: inherit;
  cursor: pointer;
  box-shadow: 0 3px 12px rgba(1,48,92,.05);
  transition: background-color 150ms ease, border-color 150ms ease, color 150ms ease, transform 150ms ease, box-shadow 150ms ease;
}
.marco-icono {
  position: relative;
  display: block;
  width: clamp(58px, 8vh, 82px);
  aspect-ratio: 76 / 92;
  flex: 0 1 auto;
  min-height: 0;
}
.imagen-casillero {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
  transition: opacity 150ms ease;
}
.icono-claro { opacity: 1; }
.icono-oscuro { opacity: 0; }
.titulo { font-size: clamp(16px, 1.9vw, 19px); font-weight: 700; line-height: 1.2; }
.descripcion { color: #5b7282; font-size: clamp(12px, 1.4vw, 14px); line-height: 1.3; }
.disponibilidad { color: #008c99; font-size: clamp(12px, 1.4vw, 14px); font-weight: 600; line-height: 1.25; }
.indicador { color: #008c99; font-size: 12px; font-weight: 700; line-height: 1.2; }
.indicador-oculto { visibility: hidden; }
.tarjeta-tamano.seleccionada {
  border-color: #00b9c7;
  background: #e8f9fa;
  box-shadow: 0 0 0 2px rgba(0,185,199,.12);
}
.tarjeta-tamano:active:not(:disabled) { transform: scale(.985); }
.tarjeta-tamano:disabled { cursor: not-allowed; }
.tarjeta-tamano.sin-disponibilidad { opacity: .7; }
.tarjeta-tamano:focus-visible { outline: 3px solid #00b9c7; outline-offset: 3px; }
.tarjeta-tamano.tema-oscuro { background: #103956; border-color: #315573; color: #ffffff; }
.tema-oscuro .descripcion { color: #c6dfea; }
.tema-oscuro .disponibilidad, .tema-oscuro .indicador { color: #5ec6e8; }
.tema-oscuro .icono-claro { opacity: 0; }
.tema-oscuro .icono-oscuro { opacity: 1; }
.tema-oscuro.seleccionada { background: #174b65; border-color: #00b9c7; box-shadow: 0 0 0 2px rgba(0,185,199,.2); }

/* En vertical, la ilustración va a la izquierda y el texto a la derecha. */
@media (orientation: portrait) {
  .tarjeta-tamano {
    display: grid;
    grid-template-columns: clamp(62px, 10vw, 88px) minmax(0, 1fr);
    grid-template-rows: repeat(4, auto);
    align-content: center;
    align-items: center;
    justify-items: start;
    column-gap: clamp(9px, 2vw, 17px);
    row-gap: clamp(2px, .55vh, 6px);
    padding: clamp(8px, 1.5vh, 16px);
    text-align: left;
  }
  .marco-icono { grid-column: 1; grid-row: 1 / 5; width: 100%; max-width: 82px; align-self: center; }
  .titulo, .descripcion, .disponibilidad, .indicador { grid-column: 2; min-width: 0; text-align: left; }
  .titulo { grid-row: 1; }
  .descripcion { grid-row: 2; }
  .disponibilidad { grid-row: 3; }
  .indicador { grid-row: 4; }
}
@media (max-height: 620px) {
  .tarjeta-tamano { gap: 3px; padding: 7px; }
  .marco-icono { width: clamp(42px, 8vh, 60px); }
  .titulo { font-size: 15px; }
  .descripcion, .disponibilidad { font-size: 11px; }
  .indicador { font-size: 11px; }
}
@media (prefers-reduced-motion: reduce) {
  .tarjeta-tamano, .imagen-casillero { transition: none; }
  .tarjeta-tamano:active:not(:disabled) { transform: none; }
}

/* Mayor legibilidad y superficie táctil en tablets */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .tarjeta-tamano {
    padding: clamp(14px, 2vh, 26px);
    gap: clamp(8px, 1.4vh, 15px);
    border-radius: 24px;
  }
  .marco-icono { width: clamp(96px, 14vh, 142px); }
  .titulo { font-size: clamp(21px, 2vw, 27px); }
  .descripcion { font-size: clamp(15px, 1.5vw, 18px); }
  .disponibilidad { font-size: clamp(16px, 1.5vw, 18px); }
  .indicador { font-size: clamp(14px, 1.3vw, 16px); }
}
@media (min-width: 600px) and (min-height: 700px) and (orientation: portrait) {
  .tarjeta-tamano {
    grid-template-columns: clamp(92px, 13vw, 128px) minmax(0, 1fr);
    column-gap: clamp(14px, 2vw, 24px);
    row-gap: clamp(6px, .8vh, 10px);
    padding: clamp(14px, 2vh, 24px);
  }
  .marco-icono { max-width: 128px; }
}
@media (min-width: 768px) and (min-height: 550px) and (max-height: 650px) {
  .tarjeta-tamano { padding: 9px; gap: 5px; }
  .marco-icono { width: clamp(66px, 12vh, 88px); }
  .titulo { font-size: 20px; }
  .descripcion { font-size: 14px; }
  .disponibilidad { font-size: 14px; }
  .indicador { font-size: 12px; }
}


/* SYNCHRO-CHECK-Y-CONTINUAR-UNIFICADOS-V1 */

/* Misma marca de seleccion que Guardar */

.tarjeta-tamano {
  position: relative;
}

.tarjeta-tamano.seleccionada::after {
  content: "\2713";

  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 2;

  display: grid;
  place-items: center;

  width: clamp(27px, 3vw, 38px);
  height: clamp(27px, 3vw, 38px);

  border-radius: 50%;

  background: #00b9c7;
  color: #01305c;

  font-size: clamp(18px, 2vw, 23px);
  font-weight: 800;
  line-height: 1;

  box-shadow: 0 2px 8px rgba(1, 48, 92, 0.12);

  pointer-events: none;
}

/* Mantener la marca dentro de la tarjeta */

@media (max-height: 620px) {
  .tarjeta-tamano.seleccionada::after {
    top: 5px;
    right: 5px;
    width: 25px;
    height: 25px;
    font-size: 17px;
  }
}
</style>
