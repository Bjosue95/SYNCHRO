<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import AppIcon from "./AppIcon.vue";

const DURACION_MS = 6000;
const mensajes = Object.freeze([
  {
    titulo: "Todo conectado.",
    destacado: "A tu alcance.",
    descripcion: "Gestiona tus encomiendas y solicitudes desde tu espacio personal.",
    icono: "seguridad",
    control: "Acceso a SYNCHRO"
  },
  {
    titulo: "Tus solicitudes.",
    destacado: "En un solo lugar.",
    descripcion: "Crea una pre-reserva y consulta su estado desde tu cuenta.",
    icono: "reservas",
    control: "Gestionar solicitudes"
  },
  {
    titulo: "Comunicación.",
    destacado: "Más sencilla.",
    descripcion: "Prepara el consentimiento por WhatsApp para coordinar una encomienda.",
    icono: "whatsapp",
    control: "Comunicación por WhatsApp"
  }
]);

const indice = ref(0);
const progreso = ref(0);
const pausaManual = ref(false);
const sobreCarrusel = ref(false);
const focoCarrusel = ref(false);
const pestanaOculta = ref(false);
const cambioManual = ref(false);

const actual = computed(() => mensajes[indice.value]);
const pausado = computed(() =>
  pausaManual.value || sobreCarrusel.value ||
  focoCarrusel.value || pestanaOculta.value
);

let temporizador = null;
let instanteAnterior = 0;
let mediaReducida = null;

function marcarTiempo() {
  instanteAnterior = performance.now();
}

function avanzarTiempo() {
  const ahora = performance.now();
  const diferencia = Math.min(Math.max(ahora - instanteAnterior, 0), 250);
  instanteAnterior = ahora;

  if (pausado.value) return;
  const nuevo = progreso.value + diferencia / DURACION_MS;
  if (nuevo >= 1) {
    indice.value = (indice.value + 1) % mensajes.length;
    progreso.value = nuevo - 1;
    cambioManual.value = false;
  } else {
    progreso.value = nuevo;
  }
}

function mostrar(numero) {
  if (numero < 0 || numero >= mensajes.length) return;
  indice.value = numero;
  progreso.value = 0;
  cambioManual.value = true;
  pausaManual.value = true;
  marcarTiempo();
}

function alternarReproduccion() {
  pausaManual.value = !pausaManual.value;
  cambioManual.value = false;
  marcarTiempo();
}

function actualizarVisibilidad() {
  pestanaOculta.value = document.visibilityState === "hidden";
  marcarTiempo();
}

function actualizarPreferenciaMovimiento(evento) {
  if (evento.matches) pausaManual.value = true;
}

function cambiarFoco(evento) {
  focoCarrusel.value = evento.currentTarget.contains(evento.relatedTarget);
  marcarTiempo();
}

onMounted(() => {
  mediaReducida = window.matchMedia?.("(prefers-reduced-motion: reduce)");
  if (mediaReducida?.matches) pausaManual.value = true;
  mediaReducida?.addEventListener?.("change", actualizarPreferenciaMovimiento);
  actualizarVisibilidad();
  marcarTiempo();
  temporizador = window.setInterval(avanzarTiempo, 70);
  document.addEventListener("visibilitychange", actualizarVisibilidad);
});

onBeforeUnmount(() => {
  window.clearInterval(temporizador);
  document.removeEventListener("visibilitychange", actualizarVisibilidad);
  mediaReducida?.removeEventListener?.("change", actualizarPreferenciaMovimiento);
});
</script>

<template>
  <aside
    class="acceso-presentacion"
    role="region"
    aria-roledescription="carrusel"
    aria-label="Beneficios de SYNCHRO"
    @mouseenter="sobreCarrusel = true"
    @mouseleave="sobreCarrusel = false; marcarTiempo()"
    @focusin="focoCarrusel = true"
    @focusout="cambiarFoco"
  >
    <div class="acceso-slide-area" :aria-live="cambioManual ? 'polite' : 'off'">
      <Transition name="beneficio" mode="out-in">
        <div :key="indice" class="acceso-slide" role="group"
          aria-roledescription="diapositiva"
          :aria-label="`${indice + 1} de ${mensajes.length}`">
          <span class="acceso-sello">
            <AppIcon :nombre="actual.icono" />
          </span>
          <h2>{{ actual.titulo }}<br /><span>{{ actual.destacado }}</span></h2>
          <p>{{ actual.descripcion }}</p>
        </div>
      </Transition>
    </div>

    <div class="acceso-controles">
      <div class="acceso-paginacion" role="group" aria-label="Elegir mensaje">
        <button v-for="(mensaje, numero) in mensajes" :key="numero" type="button"
          class="acceso-paso"
          :class="{ activo: indice === numero }"
          :aria-label="`Mostrar: ${mensaje.control}`"
          :aria-current="indice === numero ? 'true' : undefined"
          @click="mostrar(numero)">
          <span class="acceso-progreso" aria-hidden="true"
            :style="{ transform: `scaleX(${indice === numero ? Math.max(progreso, .08) : 0})` }"></span>
        </button>
      </div>

      <button class="acceso-pausa" type="button"
        :aria-label="pausaManual ? 'Reanudar los mensajes' : 'Pausar los mensajes'"
        @click="alternarReproduccion">
        <span class="pausa-simbolo" :class="{ reproducir: pausaManual }" aria-hidden="true"></span>
        {{ pausaManual ? "Reanudar" : "Pausar" }}
      </button>
    </div>
  </aside>
</template>

<style scoped>
.acceso-slide-area { min-height: 220px; }
.acceso-slide > p {
  max-width: 36ch;
  margin: 16px 0 0;
  color: var(--text-muted);
  font-size: 14px;
}
.beneficio-enter-active, .beneficio-leave-active { transition: opacity 160ms var(--ease-ui); }
.beneficio-enter-from, .beneficio-leave-to { opacity: 0; }
.acceso-controles, .acceso-paginacion {
  display: flex;
  align-items: center;
  gap: 8px;
}
.acceso-controles { flex-wrap: wrap; gap: 12px; margin-top: 16px; }
.acceso-paso {
  position: relative;
  width: 32px;
  height: 32px;
  padding: 0;
  border: 0;
  border-radius: 4px;
  background: transparent;
}
.acceso-paso::before, .acceso-progreso {
  position: absolute;
  inset: 14px 0;
  border-radius: 2px;
}
.acceso-paso::before { content: ""; background: var(--border-strong); }
.acceso-paso.activo::before, .acceso-paso:hover::before { background: var(--text-subtle); }
.acceso-progreso {
  background: var(--brand-accent);
  transform-origin: left center;
  transition: transform 70ms linear;
}
.acceso-pausa {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 32px;
  padding: 4px;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: var(--text-muted);
  font-size: 13px;
}
.acceso-pausa:hover { color: var(--text-strong); }
.pausa-simbolo {
  width: 12px;
  height: 12px;
  border-inline: 3px solid currentColor;
}
.pausa-simbolo.reproducir {
  width: 12px;
  height: 0;
  border-inline: 9px solid transparent;
  border-inline-start-color: currentColor;
  border-inline-end: 0;
  border-block: 6px solid transparent;
}
@media (prefers-reduced-motion: reduce) {
  .beneficio-enter-active, .beneficio-leave-active, .acceso-progreso { transition: none !important; }
}
</style>
