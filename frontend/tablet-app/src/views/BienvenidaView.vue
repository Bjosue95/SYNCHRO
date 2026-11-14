<script setup>
import { ref } from "vue";
import { useRouter } from "vue-router";
import { useReloj } from "../composables/useReloj";

const router = useRouter();
const { hora, fecha } = useReloj();

const iniciando = ref(false);
const error = ref(false);

// Recursos reales utilizados por el menu.
const recursosMenu = [
  "/assets/isotipo-fondo-claro.png",
  "/assets/isotipo-fondo-oscuro.png",
  "/assets/nombre-azul.png",
  "/assets/nombre-blanco.png",

  ...[
    "01_guardar_depositar",
    "02_retirar",
    "03_reserva",
    "04_carga_inalambrica",
  ].flatMap((nombre) => [
    `/iconos/${nombre}.svg`,
    `/iconos/${nombre}-oscuro.svg`,
  ]),
];

function esperar(ms) {
  return new Promise((resolver) => {
    setTimeout(resolver, ms);
  });
}

// Precarga real de las imagenes del menu.
function precargarImagen(ruta) {
  return new Promise((resolver) => {
    const imagen = new Image();

    imagen.onload = resolver;
    imagen.onerror = resolver;
    imagen.src = ruta;

    if (imagen.complete) {
      resolver();
    }
  });
}

async function comenzar() {
  if (iniciando.value) return;

  iniciando.value = true;
  error.value = false;

  const movimientoReducido = window.matchMedia(
    "(prefers-reduced-motion: reduce)"
  ).matches;

  try {
    // Nunca bloquear indefinidamente por una imagen.
    const precarga = Promise.race([
      Promise.all(recursosMenu.map(precargarImagen)),
      esperar(1800),
    ]);

    // Tiempo breve para apreciar la activacion.
    const presentacion = esperar(
      movimientoReducido ? 0 : 550
    );

    await Promise.all([precarga, presentacion]);

    await router.push("/menu");
  } catch (e) {
    console.error("No se pudo abrir el menu:", e);
    iniciando.value = false;
    error.value = true;
  }
}
</script>

<template>
  <button
    type="button"
    class="bienvenida"
    :class="{ preparando: iniciando }"
    :aria-label="
      iniciando
        ? 'Preparando menú de SYNCHRO'
        : 'Comenzar en SYNCHRO'
    "
    :disabled="iniciando"
    @click="comenzar"
  >
    <!-- Reloj original -->
    <div class="reloj">
      <time class="hora">{{ hora }}</time>
      <span class="fecha">{{ fecha }}</span>
    </div>

    <!-- Identidad original SYNCHRO -->
    <div class="marca">
      <img
        class="isotipo"
        src="/assets/isotipo-fondo-oscuro.png"
        alt=""
      />

      <img
        class="nombre"
        src="/assets/nombre-blanco.png"
        alt="SYNCHRO"
      />

      <p>El punto donde todo se conecta</p>
    </div>

    <!-- Indicaciones de inicio -->
    <div class="pie" role="status" aria-live="polite">
      <template v-if="iniciando">
        <span class="estado-carga">
          Preparando menú...
        </span>

        <span class="barra-carga" aria-hidden="true">
          <span class="progreso"></span>
        </span>
      </template>

      <span v-else-if="error">
        No se pudo abrir el menú. Toca para intentar de nuevo.
      </span>

      <span v-else class="indicacion">
        Toca cualquier parte de la pantalla para comenzar
      </span>
    </div>
  </button>
</template>

<style scoped>
.bienvenida {
  position: relative;
  isolation: isolate;

  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  width: 100%;
  height: 100dvh;
  min-height: 0;
  padding: 24px;

  overflow: hidden;

  border: 0;
  border-radius: 0;
  appearance: none;

  background: radial-gradient(
    ellipse at center,
    #104262 0%,
    #092b49 42%,
    #061f36 80%
  );

  color: #ffffff;
  font-family: Arial, Helvetica, sans-serif;
  text-align: center;
  cursor: pointer;
  touch-action: manipulation;
}

.bienvenida:disabled {
  opacity: 1;
  cursor: progress;
}

/* Iluminacion de encendido */

.bienvenida::before {
  content: "";
  position: absolute;
  inset: 0;
  z-index: -1;

  background: radial-gradient(
    ellipse at center,
    rgba(0, 185, 199, 0.16) 0%,
    transparent 65%
  );

  animation: encender-ambiente 1100ms ease-out both;
  pointer-events: none;
}

/* Reloj */

.reloj {
  position: absolute;
  top: clamp(12px, 3vh, 24px);
  right: clamp(14px, 3vw, 30px);

  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;

  animation: revelar-elemento 650ms 400ms both;
}

.hora {
  font-size: clamp(20px, 2.8vw, 28px);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.fecha {
  color: #c6dfea;
  font-size: clamp(12px, 1.5vw, 15px);
}

/* Activacion de la marca */

.marca {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: clamp(8px, 2vh, 16px);

  width: 100%;
  max-width: 560px;

  animation: activar-marca 950ms
    cubic-bezier(0.22, 1, 0.36, 1) both;
}

.isotipo {
  width: clamp(80px, 14vw, 165px);
  max-height: 24dvh;
  object-fit: contain;
}

.nombre {
  width: clamp(175px, 30vw, 330px);
  max-width: 75vw;
  max-height: 14dvh;
  object-fit: contain;
}

.marca p {
  margin: 0;
  color: #d8edf4;
  font-size: clamp(14px, 1.8vw, 20px);
}

/* Zona inferior */

.pie {
  position: absolute;
  bottom: clamp(12px, 4vh, 32px);
  left: 16px;
  right: 16px;

  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;

  color: #d6eaf2;
  font-size: clamp(13px, 1.6vw, 17px);

  animation: revelar-elemento 600ms 700ms both;
}

.indicacion {
  line-height: 1.5;
}

.estado-carga {
  font-weight: 500;
}

/* Barra de actividad */

.barra-carga {
  display: block;
  position: relative;

  width: min(230px, 65vw);
  height: 4px;

  overflow: hidden;
  border-radius: 999px;

  background: rgba(255, 255, 255, 0.20);
}

.progreso {
  position: absolute;
  inset: 0;
  width: 38%;

  border-radius: inherit;
  background: #00b9c7;

  animation: carga-suave 900ms
    ease-in-out infinite;
}

/* Animaciones de encendido */

@keyframes encender-ambiente {
  from {
    opacity: 0;
    transform: scale(0.85);
  }

  to {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes activar-marca {
  0% {
    opacity: 0;
    transform: scale(0.92);
    filter: blur(6px) brightness(0.55);
  }

  65% {
    opacity: 1;
    filter: blur(0) brightness(1.12);
  }

  100% {
    opacity: 1;
    transform: scale(1);
    filter: blur(0) brightness(1);
  }
}

@keyframes revelar-elemento {
  from {
    opacity: 0;
  }

  to {
    opacity: 1;
  }
}

@keyframes carga-suave {
  from {
    transform: translateX(-110%);
  }

  to {
    transform: translateX(280%);
  }
}

/* Vertical y horizontal */

@media (orientation: portrait) {
  .isotipo {
    width: clamp(90px, 18vw, 150px);
  }

  .nombre {
    width: clamp(180px, 42vw, 300px);
  }
}

@media (max-height: 550px) {
  .marca {
    gap: 7px;
  }

  .isotipo {
    width: 75px;
  }

  .nombre {
    width: 185px;
  }

  .pie {
    bottom: 10px;
  }
}

/* Movimiento reducido */

@media (prefers-reduced-motion: reduce) {
  .bienvenida::before,
  .marca,
  .reloj,
  .pie,
  .progreso {
    animation: none !important;
  }

  .progreso {
    width: 100%;
  }
}
</style>