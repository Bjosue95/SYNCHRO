<script setup>
import { ref, onBeforeUnmount } from "vue";
import { useRouter } from "vue-router";

const props = defineProps({
  titulo: { type: String, required: true },
  descripcion: { type: String, required: true },
  icono: { type: String, required: true },
  oscuro: { type: Boolean, default: false },
  ruta: { type: String, default: "" },
});

const router = useRouter();
const seleccionada = ref(false);

let temporizador = null;

function abrirServicio() {
  if (!props.ruta || seleccionada.value) return;

  // Confirmacion visual inmediata.
  seleccionada.value = true;

  // Breve pausa para apreciar la pulsacion.
  temporizador = setTimeout(() => {
    router.push(props.ruta)
      .catch(() => {
        seleccionada.value = false;
      })
      .finally(() => {
        seleccionada.value = false;
      });
  }, 150);
}

onBeforeUnmount(() => {
  if (temporizador !== null) {
    clearTimeout(temporizador);
  }
});
</script>

<template>
  <component
    :is="ruta ? 'button' : 'div'"
    :type="ruta ? 'button' : undefined"
    class="tarjeta-servicio"
    :class="{
      'tema-oscuro': oscuro,
      'tarjeta-navegable': !!ruta,
      'tarjeta-seleccionada': seleccionada
    }"
    @click="abrirServicio"
  >
    <span class="icono-par" aria-hidden="true">
      <img
        :src="`${icono}.svg`"
        alt=""
        class="icono icono-claro"
      />

      <img
        :src="`${icono}-oscuro.svg`"
        alt=""
        class="icono icono-oscuro"
      />
    </span>

    <h2>{{ titulo }}</h2>
    <p>{{ descripcion }}</p>
  </component>
</template>

<style scoped>
.tarjeta-servicio {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: clamp(5px, 1.2vh, 12px);
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(8px, 2vh, 18px);
  border: 2px solid #dfeaf0;
  border-radius: 20px;
  background: #ffffff;
  color: #01305c;
  text-align: center;
  font: inherit;
  box-shadow: 0 3px 12px rgba(1,48,92,.05);
  transition: background-color 150ms ease, border-color 150ms ease, color 150ms ease, transform 150ms ease, box-shadow 150ms ease;
}
.tarjeta-navegable { cursor: pointer; touch-action: manipulation; }
.tarjeta-navegable:active, .tarjeta-seleccionada {
  transform: scale(.985);
  background: #e8f9fa;
  border-color: #00b9c7;
  box-shadow: 0 0 0 2px rgba(0,185,199,.13);
}
.icono-par {
  display: block;
  position: relative;
  width: clamp(44px, 8vh, 82px);
  aspect-ratio: 1;
  flex: 0 1 auto;
}
.icono { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; transition: opacity 150ms ease; }
.icono-claro { opacity: 1; }
.icono-oscuro { opacity: 0; }
h2 { margin: 0; font-size: clamp(16px, 1.9vw, 23px); font-weight: 600; line-height: 1.2; }
p { margin: 0; color: #5b7282; font-size: clamp(12px, 1.3vw, 16px); line-height: 1.35; }
.tarjeta-servicio.tema-oscuro { background: #103956; border-color: #315573; color: #ffffff; }
.tema-oscuro p { color: #c6dfea; }
.tema-oscuro .icono-claro { opacity: 0; }
.tema-oscuro .icono-oscuro { opacity: 1; }
.tema-oscuro.tarjeta-seleccionada, .tema-oscuro.tarjeta-navegable:active {
  background: #174b65;
  border-color: #00b9c7;
  box-shadow: 0 0 0 2px rgba(0,185,199,.2);
}
.tarjeta-navegable:focus-visible { outline: 3px solid #00b9c7; outline-offset: 3px; }
@media (max-height: 620px) {
  .tarjeta-servicio { padding: 6px; gap: 4px; }
  .icono-par { width: clamp(32px, 7vh, 52px); }
  h2 { font-size: 15px; }
  p { font-size: 12px; }
}
@media (prefers-reduced-motion: reduce) {
  .tarjeta-servicio, .icono { transition: none; }
  .tarjeta-navegable:active, .tarjeta-seleccionada { transform: none; }
}

/* Tarjetas de menú dimensionadas para tablet */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .tarjeta-servicio {
    gap: clamp(8px, 1.5vh, 18px);
    padding: clamp(12px, 2vh, 25px);
    border-radius: 24px;
  }
  .icono-par { width: clamp(75px, 11vh, 118px); }
  h2 { font-size: clamp(21px, 2.2vw, 28px); }
  p { font-size: clamp(15px, 1.55vw, 19px); }
}
@media (min-width: 768px) and (min-height: 550px) and (max-height: 650px) {
  .icono-par { width: clamp(60px, 10vh, 80px); }
  h2 { font-size: 20px; }
  p { font-size: 14px; }
}

</style>
