<script setup>
import { useReloj } from "../composables/useReloj";

defineProps({
  oscuro: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(["cambiar-tema", "abrir-ayuda"]);

const { hora, fecha } = useReloj();
</script>

<template>
  <header class="encabezado" :class="{ 'tema-oscuro': oscuro }">
    <!-- Identidad visual -->

    <div class="identidad" role="img" aria-label="SYNCHRO">
      <!-- Isotipo: versiones clara y oscura -->
      <span class="capa-logo contenedor-isotipo">
        <img
          src="/assets/isotipo-fondo-claro.png"
          alt=""
          class="imagen-logo version-clara"
        />

        <img
          src="/assets/isotipo-fondo-oscuro.png"
          alt=""
          class="imagen-logo version-oscura"
        />
      </span>

      <!-- Nombre: versiones clara y oscura -->
      <span class="capa-logo contenedor-nombre">
        <img
          src="/assets/nombre-azul.png"
          alt=""
          class="imagen-logo version-clara"
        />

        <img
          src="/assets/nombre-blanco.png"
          alt=""
          class="imagen-logo version-oscura"
        />
      </span>
    </div>

    <!-- Controles -->
    <div class="acciones-header">
      <button type="button" class="boton-ayuda" @click="emit('abrir-ayuda')">
        ? Ayuda
      </button>

      <button
        type="button"
        class="boton-tema"
        :aria-label="oscuro ? 'Activar modo claro' : 'Activar modo oscuro'"
        @click="emit('cambiar-tema')"
      >
        {{ oscuro ? "☀" : "☾" }}
      </button>

      <!-- Reloj -->
      <div class="reloj-header">
        <time class="hora-header">{{ hora }}</time>
        <span class="fecha-header">{{ fecha }}</span>
      </div>
    </div>
  </header>
</template>

<style scoped>
.encabezado {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;

  width: 100%;
  min-height: 84px;
  flex-shrink: 0;
  padding: 12px 20px;

  border-bottom: 1px solid #d9e6ed;
  color: #01305c;

  transition:
    color 260ms ease,
    border-color 260ms ease;

  transition:
    color 150ms ease,
    border-color 150ms ease;
}

/* Isotipo y nombre */

.identidad {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

/* Logotipos superpuestos para ambos temas */

.capa-logo {
  position: relative;
  display: block;
  flex-shrink: 0;
}

.contenedor-isotipo {
  width: clamp(40px, 5vw, 58px);
  height: clamp(40px, 5vw, 58px);
}

.contenedor-nombre {
  width: clamp(120px, 16vw, 180px);
  height: 48px;
}

.imagen-logo {
  position: absolute;
  inset: 0;

  display: block;
  width: 100%;
  height: 100%;

  object-fit: contain;
  object-position: left center;

  transition: opacity 150ms ease;
}

/* Tema claro */

.version-clara {
  opacity: 1;
}

.version-oscura {
  opacity: 0;
}

/* Tema oscuro */

.tema-oscuro .version-clara {
  opacity: 0;
}

.tema-oscuro .version-oscura {
  opacity: 1;
}

/* Controles */

.acciones-header {
  display: flex;
  align-items: center;
  gap: clamp(8px, 1.5vw, 18px);
}

.boton-ayuda,
.boton-tema {
  display: flex;
  align-items: center;
  justify-content: center;

  min-height: 48px;
  border: 1px solid #d9e6ed;
  border-radius: 12px;

  background: #ffffff;
  color: #01305c;
  cursor: pointer;

  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    color 150ms ease;
}

.boton-ayuda {
  padding: 0 16px;
  font-weight: 600;
}

.boton-tema {
  width: 48px;
  font-size: 26px;
}

/* Fecha y hora */

.reloj-header {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 3px;
  min-width: 125px;
}

.hora-header {
  font-size: clamp(18px, 2vw, 25px);
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.fecha-header {
  color: #5b7282;
  font-size: clamp(11px, 1.3vw, 14px);
  text-align: right;
  transition: color 150ms ease;
}

/* Modo oscuro */

.tema-oscuro {
  border-bottom-color: #355269;
  color: #ffffff;
}

.tema-oscuro .boton-ayuda,
.tema-oscuro .boton-tema {
  background: #204766;
  border-color: #42647e;
  color: #ffffff;
}

.tema-oscuro .fecha-header {
  color: #c6dfea;
}

/* Accesibilidad */

button:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

/* Tablets compactas */

@media (max-width: 700px) {
  .encabezado {
    flex-wrap: wrap;
    padding: 12px;
  }

  .acciones-header {
    flex: 1;
    justify-content: flex-end;
  }

  .contenedor-nombre {
    width: 120px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .encabezado,
  .boton-ayuda,
  .boton-tema,
  .fecha-header,
  .imagen-logo {
    transition: none;
  }
}
</style>
