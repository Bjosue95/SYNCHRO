<script setup>
import { RouterLink } from "vue-router";

defineProps({
  titulo: {
    type: String,
    required: true,
  },

  descripcion: {
    type: String,
    required: true,
  },

  icono: {
    type: String,
    required: true,
  },

  oscuro: {
    type: Boolean,
    default: false,
  },

  ruta: {
    type: String,
    default: "",
  },
});
</script>

<template>
  <component
    :is="ruta ? RouterLink : 'div'"
    :to="ruta || undefined"
    class="tarjeta-servicio"
    :class="{
      'tema-oscuro': oscuro,
      'tarjeta-navegable': !!ruta,
    }"
  >
    <!-- Iconos para los dos temas -->
    <span class="icono-par" aria-hidden="true">
      <img :src="`${icono}.svg`" alt="" class="icono icono-claro" />

      <img :src="`${icono}-oscuro.svg`" alt="" class="icono icono-oscuro" />
    </span>

    <h2>{{ titulo }}</h2>
    <p>{{ descripcion }}</p>
  </component>
</template>

<style scoped>
/* =========================================
   SYNCHRO | TARJETAS REUTILIZABLES
   ========================================= */

.tarjeta-servicio {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  width: 100%;
  height: 100%;
  min-width: 0;
  gap: 9px;
  padding: 18px;

  background: #ffffff;
  border: 1px solid #dfeaf0;
  border-radius: 20px;

  color: #01305c;
  text-align: center;
  text-decoration: none;

  box-shadow: 0 3px 12px rgba(1, 48, 92, 0.04);

  /* Transición original de SYNCHRO */
  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    color 150ms ease;
}

/* Tarjetas con navegación */

.tarjeta-navegable {
  cursor: pointer;
}

.tarjeta-navegable:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

/* =========================================
   ICONOS CLAROS Y OSCUROS
   ========================================= */

.icono-par {
  position: relative;
  display: block;

  width: clamp(58px, 7vw, 82px);
  height: clamp(58px, 7vw, 82px);

  flex-shrink: 0;
}

.icono {
  position: absolute;
  inset: 0;

  display: block;
  width: 100%;
  height: 100%;

  object-fit: contain;

  /* Los iconos cambian suavemente */
  transition: opacity 150ms ease;
}

.icono-claro {
  opacity: 1;
}

.icono-oscuro {
  opacity: 0;
}

/* =========================================
   TIPOGRAFÍA
   ========================================= */

h2 {
  margin: 0;
  font-size: clamp(18px, 2vw, 23px);
  font-weight: 600;
}

p {
  margin: 0;

  color: #5b7282;
  font-size: clamp(13px, 1.5vw, 16px);
  line-height: 1.4;

  transition: color 150ms ease;
}

/* =========================================
   MODO OSCURO
   ========================================= */

.tarjeta-servicio.tema-oscuro {
  background: #103956;
  border-color: #315573;
  color: #ffffff;
}

.tema-oscuro p {
  color: #c6dfea;
}

.tema-oscuro .icono-claro {
  opacity: 0;
}

.tema-oscuro .icono-oscuro {
  opacity: 1;
}

/* =========================================
   TABLETS COMPACTAS
   ========================================= */

@media (max-height: 550px) {
  .tarjeta-servicio {
    gap: 5px;
    padding: 10px;
  }

  .icono-par {
    width: 48px;
    height: 48px;
  }

  h2 {
    font-size: 17px;
  }

  p {
    font-size: 12px;
  }
}

/* =========================================
   ACCESIBILIDAD
   ========================================= */

@media (prefers-reduced-motion: reduce) {
  .tarjeta-servicio,
  .icono,
  p {
    transition: none;
  }
}
</style>
