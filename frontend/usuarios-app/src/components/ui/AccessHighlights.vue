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
const detenidoPorUsuario = ref(false);
const movimientoReducido = ref(false);
const sobreCarrusel = ref(false);
const focoCarrusel = ref(false);
const pestanaOculta = ref(false);
const cambioManual = ref(false);

const actual = computed(() => mensajes[indice.value]);
const pausado = computed(() =>
  detenidoPorUsuario.value || movimientoReducido.value ||
  sobreCarrusel.value || focoCarrusel.value || pestanaOculta.value
);

let temporizador = null;
let instanteAnterior = 0;
let mediaReducida = null;

function marcarTiempo() {
  instanteAnterior = performance.now();
}

function avanzarTiempo() {
  const ahora = performance.now();
  const diferencia = Math.min(Math.max(ahora - instanteAnterior, 0), 400);
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
  if (numero === indice.value && detenidoPorUsuario.value) {
    detenidoPorUsuario.value = false;
  } else {
    indice.value = numero;
    detenidoPorUsuario.value = true;
  }
  progreso.value = 0;
  cambioManual.value = true;
  marcarTiempo();
}

function actualizarVisibilidad() {
  pestanaOculta.value = document.visibilityState === "hidden";
  marcarTiempo();
}

function actualizarPreferenciaMovimiento(evento) {
  movimientoReducido.value = evento.matches;
}

function cambiarFoco(evento) {
  focoCarrusel.value = evento.currentTarget.contains(evento.relatedTarget);
  marcarTiempo();
}

onMounted(() => {
  mediaReducida = window.matchMedia?.("(prefers-reduced-motion: reduce)");
  movimientoReducido.value = Boolean(mediaReducida?.matches);
  mediaReducida?.addEventListener?.("change", actualizarPreferenciaMovimiento);
  actualizarVisibilidad();
  marcarTiempo();
  temporizador = window.setInterval(avanzarTiempo, 100);
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
            <AppIcon :nombre="actual.icono" :tamano="35" />
          </span>
          <h2>{{ actual.titulo }}<br /><span>{{ actual.destacado }}</span></h2>
          <p>{{ actual.descripcion }}</p>
        </div>
      </Transition>
    </div>

    <div class="acceso-controles">
      <div class="acceso-paginacion" role="group" aria-label="Elegir mensaje. Seleccionar un mensaje detiene la reproducción automática.">
        <button v-for="(mensaje, numero) in mensajes" :key="numero" type="button"
          class="acceso-paso"
          :class="{ activo: indice === numero }"
          :aria-label="indice === numero && detenidoPorUsuario ? 'Reanudar recorrido automático' : `Mostrar: ${mensaje.control}`"
          :aria-current="indice === numero ? 'true' : undefined"
          @click="mostrar(numero)">
          <span class="acceso-progreso" aria-hidden="true"
            :style="{ width: indice === numero ? `${Math.max(progreso * 100, 6)}%` : '0%' }"></span>
        </button>
      </div>


    </div>
  </aside>
</template>

<style scoped>
.acceso-slide-area {
  width: 100%;
  min-height: 285px;
  display: grid;
  align-items: start;
}
.acceso-slide {
  grid-area: 1 / 1;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  width: 100%;
  min-width: 0;
}
.acceso-slide > p {
  margin: 20px 0 0;
  max-width: 330px;
  color: var(--text-muted);
  font-size: 14px;
  line-height: 1.7;
}
.beneficio-enter-active,
.beneficio-leave-active {
  transition: opacity 330ms ease, transform 330ms ease;
}
.beneficio-enter-from {
  opacity: 0;
  transform: translateY(8px);
}
.beneficio-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}
.acceso-controles {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 17px;
  margin-top: 11px;
}
.acceso-paginacion {
  display: flex;
  gap: 9px;
  align-items: center;
}
.acceso-paso {
  position: relative;
  display: block;
  width: 39px;
  height: 44px;
  min-width: 44px;
  padding: 0;
  background: transparent;
  border: 0;
  border-radius: 10px;
}
.acceso-paso::before {
  content: "";
  position: absolute;
  inset: 20px 0;
  border-radius: 99px;
  background: var(--border-strong);
}
.acceso-paso.activo { width: 64px; }
.acceso-paso.activo::before { background: var(--surface-hover); }
.acceso-progreso {
  position: absolute;
  left: 0;
  top: 20px;
  height: 4px;
  border-radius: 99px;
  background: var(--brand-accent);
  z-index: 1;
  transition: width 100ms linear;
}
.acceso-paso:hover::before { background: var(--text-subtle); }
@media (max-height: 740px) {
  .acceso-slide-area { min-height: 250px; }
}
@media (prefers-reduced-motion: reduce) {
  .beneficio-enter-active,
  .beneficio-leave-active,
  .acceso-progreso { transition: none !important; }
}
</style>
