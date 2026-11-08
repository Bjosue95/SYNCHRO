<script setup>
import { useRouter } from "vue-router";
import { useReloj } from "../composables/useReloj";

const router = useRouter();
const { hora, fecha } = useReloj();

function comenzar() {
  router.push("/menu");
}
</script>

<template>
  <button
    type="button"
    class="bienvenida"
    aria-label="Comenzar en SYNCHRO"
    @click="comenzar"
  >
    <!-- Fecha y hora -->
    <div class="reloj" aria-hidden="true">
      <time class="hora">{{ hora }}</time>
      <span class="fecha">{{ fecha }}</span>
    </div>

    <!-- Identidad original SYNCHRO -->
    <div class="marca">
      <img class="isotipo" src="/assets/isotipo-fondo-oscuro.png" alt="" />

      <img class="nombre" src="/assets/nombre-blanco.png" alt="SYNCHRO" />

      <p>El punto donde todo se conecta</p>
    </div>

    <!-- Indicación inferior -->
    <span class="indicacion">
      Toca cualquier parte de la pantalla para comenzar
    </span>
  </button>
</template>

<style scoped>
.bienvenida {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  width: 100%;
  min-height: 100vh;
  min-height: 100dvh;
  padding: 32px;

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
  overflow: hidden;

  -webkit-tap-highlight-color: transparent;
}

/* Reloj */

.reloj {
  position: absolute;
  top: 24px;
  right: 30px;

  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
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

/* Marca */

.marca {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;

  width: 100%;
  animation: aparecer 1.2s ease-out both;
}

.isotipo {
  width: clamp(100px, 14vw, 165px);
  max-height: 180px;
  object-fit: contain;

  animation: elevar 3.5s ease-in-out infinite;
}

.nombre {
  width: clamp(185px, 30vw, 330px);
  max-width: 75vw;
  max-height: 90px;
  object-fit: contain;
}

.marca p {
  margin: 0;
  color: #d8edf4;
  font-size: clamp(14px, 1.8vw, 20px);
  letter-spacing: 0.5px;
}

/* Indicación */

.indicacion {
  position: absolute;
  bottom: 30px;

  color: #d6eaf2;
  font-size: clamp(13px, 1.6vw, 17px);
  line-height: 1.5;

  animation: respirar 2.5s ease-in-out infinite;
}

/* Animaciones */

@keyframes aparecer {
  from {
    opacity: 0;
    transform: translateY(16px);
  }

  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes elevar {
  0%,
  100% {
    transform: translateY(0);
  }

  50% {
    transform: translateY(-7px);
  }
}

@keyframes respirar {
  0%,
  100% {
    opacity: 0.65;
  }
  50% {
    opacity: 1;
  }
}

/* Accesibilidad y tablets compactas */

.bienvenida:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: -6px;
}

@media (max-height: 550px) {
  .isotipo {
    width: 85px;
  }
  .nombre {
    width: 190px;
  }
  .marca {
    gap: 8px;
  }
  .reloj {
    top: 12px;
  }
  .indicacion {
    bottom: 12px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .marca,
  .isotipo,
  .indicacion {
    animation: none;
  }
}
</style>
