<script setup>
import { computed, ref } from "vue";
import AppIcon from "./AppIcon.vue";

// Recorrido editorial de una encomienda. No invoca servicios ni atribuye
// disponibilidad a una estación real.
const etapas = Object.freeze([
  {
    nombre: "Solicitud",
    titulo: "Prepara tu envío",
    detalle: "Indica el tamaño de la encomienda y el contacto del destinatario.",
    icono: "reservas"
  },
  {
    nombre: "Consentimiento",
    titulo: "El destinatario autoriza",
    detalle: "La comunicación por WhatsApp requiere su consentimiento.",
    icono: "whatsapp"
  },
  {
    nombre: "Entrega y retiro",
    titulo: "La entrega se verifica",
    detalle: "La estación comprueba la identidad antes de entregar el contenido.",
    icono: "seguridad"
  }
]);

const activa = ref(0);
const etapa = computed(() => etapas[activa.value]);
const puertaAbierta = computed(() => activa.value === 2);
const numeros = Object.freeze(["M01","M02","M03","M04","M05","M06","M07","M08","M09","M10"]);
const fase = computed(() => ["01", "02", "03"][activa.value]);

function elegir(indice) {
  if (indice >= 0 && indice < etapas.length) activa.value = indice;
}
function controlarTeclado(evento) {
  if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(evento.key)) return;
  evento.preventDefault();
  if (evento.key === "Home") elegir(0);
  else if (evento.key === "End") elegir(etapas.length - 1);
  else elegir((activa.value + (evento.key === "ArrowRight" ? 1 : etapas.length - 1)) % etapas.length);
}
</script>

<template>
  <div class="syn-journey" aria-label="Así se gestiona una encomienda en SYNCHRO">
    <div class="syn-journey-stage" :data-step="activa">
      <div class="syn-journey-background" aria-hidden="true"></div>

      <div class="syn-station" aria-hidden="true">
        <div class="syn-station-top">
          <img src="/branding/nombre-azul.svg" alt="" draggable="false" />
          <span class="syn-station-signal"></span>
        </div>
        <div class="syn-station-body">
          <div class="syn-station-lockers">
            <div
              v-for="(codigo, i) in numeros"
              :key="codigo"
              class="syn-station-cell"
              :class="{ 'is-active': codigo === 'M04' && activa === 2 }"
            >
              <span class="syn-station-cavity"></span>
              <span v-if="codigo === 'M04'" class="syn-station-package">
                <AppIcon nombre="encomienda" :tamano="25"/>
              </span>
              <div
                class="syn-station-door"
                :class="{ 'is-open': codigo === 'M04' && puertaAbierta }"
              >
                <span class="syn-station-code">{{ codigo }}</span>
                <span class="syn-station-handle"></span>
              </div>
            </div>
          </div>
          <div class="syn-station-console">
            <div class="syn-console-screen">
              <img src="/branding/isotipo-claro.svg" alt="" />
              <span class="syn-screen-status" :class="{ 'is-ready': activa > 0 }"></span>
              <span class="syn-screen-line"></span>
              <span class="syn-screen-line short"></span>
            </div>
            <div class="syn-console-connector">
              <span></span><span></span>
            </div>
            <div class="syn-console-mark">SYNCHRO</div>
          </div>
        </div>
        <div class="syn-station-base"></div>
      </div>

      <div class="syn-journey-note" :key="'status-' + activa">
        <div class="syn-journey-note-icon"><AppIcon :nombre="etapa.icono" :tamano="23"/></div>
        <span class="syn-journey-note-label">Paso {{ fase }} / 03</span>
        <strong>{{ etapa.nombre }}</strong>
      </div>
    </div>

    <div class="syn-journey-controls">
      <div class="syn-journey-description">
        <span class="syn-journey-overline">De la solicitud a la entrega</span>
        <Transition name="syn-story" mode="out-in">
          <div :key="activa" class="syn-journey-copy" aria-live="polite">
            <h3>{{ etapa.titulo }}</h3>
            <p>{{ etapa.detalle }}</p>
          </div>
        </Transition>
      </div>

      <div class="syn-journey-steps" role="group" aria-label="Etapas del servicio" @keydown="controlarTeclado">
        <button
          v-for="(item, i) in etapas"
          :key="item.nombre"
          type="button"
          class="syn-journey-step"
          :class="{ selected: activa === i }"
          :aria-label="`Ver paso ${i + 1}: ${item.nombre}`"
          :aria-pressed="activa === i"
          @click="elegir(i)"
        >
          <span class="syn-journey-number">0{{ i + 1 }}</span>
          <span class="syn-journey-step-label">{{ item.nombre }}</span>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.syn-journey {
  width: 100%; min-width: 0; border: 1px solid var(--border);
  border-radius: 20px; background: var(--surface);
  box-shadow: 0 20px 55px rgba(5,27,40,.08); overflow: hidden;
}
.syn-journey-stage {
  position: relative; height: 335px; overflow: hidden;
  display: flex; justify-content: center; align-items: flex-end;
  padding: 22px 24px 0; isolation: isolate;
  background: #e5eef1;
}
:global(:root[data-theme="oscuro"]) .syn-journey-stage { background: #233b46; }
.syn-journey-background {
  position: absolute; z-index: -1; inset: 0;
  background:
    linear-gradient(0deg, rgba(23,63,82,.08) 0 1px, transparent 1px 42px),
    linear-gradient(90deg, rgba(23,63,82,.08) 0 1px, transparent 1px 42px);
  background-size: 42px 42px;
  mask-image: linear-gradient(to bottom, transparent, #000 27%, #000 85%);
}
.syn-station {
  position: relative; width: 251px; height: 300px; padding: 13px 12px 11px;
  background: #edf5f7; border: 2px solid #547a8b;
  border-radius: 11px 11px 2px 2px;
  box-shadow: 10px 12px 0 #9ab1ba, 12px 17px 24px rgba(21,48,60,.19);
  transform-origin: center bottom; animation: syn-appear 650ms cubic-bezier(.2,.7,.2,1) both;
}
.syn-station-top { display: flex; align-items: center; justify-content: space-between; height: 24px; margin-bottom: 11px; }
.syn-station-top img { width: 115px; max-height: 22px; object-fit: contain; object-position: left center; }
.syn-station-signal { width: 8px; height: 8px; border-radius: 50%; background: #00b9c7; box-shadow: 0 0 0 3px #d9f2f2; }
.syn-station-body { display: grid; grid-template-columns: 1fr 68px; height: 229px; gap: 9px; }
.syn-station-lockers { display: grid; grid-template-columns: repeat(2,1fr); grid-template-rows: repeat(5,1fr); gap: 4px; perspective: 560px; }
.syn-station-cell { position: relative; border-radius: 2px; background: #adc6d0; overflow: visible; }
.syn-station-cavity { position: absolute; inset: 2px; border-radius: 2px; background: #648190; box-shadow: inset 4px 4px 8px rgba(0,0,0,.25); }
.syn-station-door {
  position: absolute; inset: 0; z-index: 2;
  border: 1px solid #a7c1cb; border-radius: 2px; background: #f7fbfc;
  display: flex; justify-content: space-between; padding: 6px 5px;
  box-shadow: inset 0 -3px 0 rgba(20,75,98,.05);
  transform-origin: left center;
  transition: transform 700ms cubic-bezier(.18,.8,.25,1), box-shadow 400ms ease, border-color 400ms ease;
  backface-visibility: hidden;
}
.syn-station-code { font: 600 8px/1.2 ui-monospace,monospace; color: #426173; }
.syn-station-handle { align-self: center; width: 3px; height: 13px; border-radius: 2px; background: #7aa4b1; }
.syn-station-cell.is-active .syn-station-door { border-color: #00b9c7; }
.syn-station-door.is-open { transform: rotateY(-73deg); box-shadow: 7px 2px 8px rgba(0,0,0,.13); }
.syn-station-package { position: absolute; z-index: 1; top: 6px; left: 10px; }
.syn-station-package :deep(img) { filter: brightness(0) invert(1); }
.syn-station-console { background: #cedfe6; border: 1px solid #aac5d0; border-radius: 5px; padding: 10px 7px 5px; display: flex; flex-direction: column; align-items: center; }
.syn-console-screen { width: 100%; aspect-ratio: 1; background: #01305c; border: 3px solid #375c74; border-radius: 5px; display: flex; flex-direction: column; align-items: center; padding: 6px 4px; }
.syn-console-screen img { width: 17px; height: 19px; object-fit: contain; margin: auto; }
.syn-screen-status { width: 17px; height: 2px; background: #5e8194; border-radius: 3px; transition: background 350ms; }
.syn-screen-status.is-ready { background: #00b9c7; }
.syn-screen-line { margin-top: 4px; width: 77%; height: 2px; border-radius: 3px; background: #62879d; opacity: .65; }
.syn-screen-line.short { width: 45%; margin-top: 3px; }
.syn-console-connector { display: flex; gap: 9px; margin-top: 23px; }
.syn-console-connector span { width: 8px; height: 8px; border-radius: 2px; border: 2px solid #44728c; }
.syn-console-mark { margin-top: auto; color: #466879; letter-spacing: .04em; font-size: 6px; font-weight: 700; }
.syn-station-base { position: absolute; left: 12px; right: 12px; bottom: 3px; height: 4px; background: #6f929f; border-radius: 3px; }
.syn-journey-note {
  position: absolute; top: 38px; right: 22px; width: 145px; padding: 14px;
  display: flex; flex-direction: column; gap: 4px;
  background: #fff; border: 1px solid #cbdde2; border-radius: 12px;
  box-shadow: 0 10px 30px rgba(22,46,60,.09); animation: syn-note 330ms ease-out both;
}
.syn-journey-note-icon { width: 30px; height: 30px; display: flex; align-items: center; }
.syn-journey-note-label { color: #67808d; font-size: 10px; }
.syn-journey-note strong { color: #153b50; font-weight: 600; font-size: 12px; line-height: 1.4; }
.syn-journey-controls { padding: 22px 24px 21px; }
.syn-journey-overline { color: var(--text-muted); font-size: 11px; letter-spacing: .01em; }
.syn-journey-copy { min-height: 80px; }
.syn-journey-copy h3 { margin: 8px 0 5px; color: var(--text-strong); font-size: 19px; font-weight: 600; letter-spacing: -.015em; }
.syn-journey-copy p { margin: 0; color: var(--text-muted); font-size: 12px; line-height: 1.55; max-width: 360px; }
.syn-journey-steps { display: grid; grid-template-columns: 1fr 1.25fr 1.25fr; gap: 7px; margin-top: 18px; }
.syn-journey-step { min-width: 0; display: flex; align-items: center; justify-content: center; gap: 7px; padding: 9px 6px; border: 1px solid var(--border); background: var(--surface-soft); border-radius: 9px; color: var(--text-muted); font-size: 11px; cursor: pointer; }
.syn-journey-step.selected { background: var(--surface-accent); border-color: var(--brand-accent); color: var(--text-strong); }
.syn-journey-number { font: 600 10px/1 ui-monospace,monospace; color: var(--accent-text); }
.syn-story-enter-active, .syn-story-leave-active { transition: opacity 180ms ease, transform 180ms ease; }
.syn-story-enter-from { opacity: 0; transform: translateY(5px); }
.syn-story-leave-to { opacity: 0; transform: translateY(-5px); }
@keyframes syn-appear { from { opacity: 0; transform: translateY(22px); } to { opacity: 1; transform: translateY(0); } }
@keyframes syn-note { from { opacity: 0; transform: translateX(8px); } to { opacity: 1; transform: translateX(0); } }
@media (max-width: 430px) {
  .syn-journey-stage { height: 310px; }
  .syn-station { transform: scale(.92); transform-origin: center bottom; }
  .syn-journey-note { right: 9px; top: 19px; width: 116px; padding: 9px; }
  .syn-journey-steps { grid-template-columns: repeat(3,1fr); }
  .syn-journey-step { flex-direction: column; gap: 4px; min-height: 43px; text-align: center; }
  .syn-journey-step-label { font-size: 9px; }
  .syn-journey-controls { padding: 18px 16px; }
}
@media (prefers-reduced-motion: reduce) {
  .syn-station, .syn-journey-note { animation: none !important; }
  .syn-station-door, .syn-story-enter-active, .syn-story-leave-active { transition: none !important; }
}
</style>
