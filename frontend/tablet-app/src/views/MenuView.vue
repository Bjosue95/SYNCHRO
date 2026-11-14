<script setup>

import ServiceCard from "../components/ServiceCard.vue";

import { useTema } from "../composables/useTema";

// Tema compartido en toda la aplicación
const { oscuro } = useTema();

// Centro de ayuda

// Servicios de SYNCHRO
const servicios = [
  {
    titulo: "Guardar",
    descripcion: "Pertenencias personales",
    icono: "01_guardar_depositar",
    ruta: "/guardar",
  },
  {
    titulo: "Retirar",
    descripcion: "Recoge tus objetos",
    icono: "02_retirar",
  },
  {
    titulo: "Tengo una reserva",
    descripcion: "Continúa tu reserva web",
    icono: "03_reserva",
  },
  {
    titulo: "Cargar dispositivo",
    descripcion: "Celular o laptop",
    icono: "04_carga_inalambrica",
  },
];


</script>

<template>
  <main class="pantalla-menu" :class="{ 'tema-oscuro': oscuro }">
    <div class="contenido-menu">
      <!-- Título -->
      <h1 class="titulo-menu">¿Qué deseas hacer?</h1>
    
      <!-- Tarjetas reutilizables -->
      <div class="servicios">
        <ServiceCard
          v-for="servicio in servicios"
          :key="servicio.titulo"
          :titulo="servicio.titulo"
          :descripcion="servicio.descripcion"
          :icono="`/iconos/${servicio.icono}`"
          :ruta="servicio.ruta || ''"
          :oscuro="oscuro"
        />
      </div>
    </div>
  </main>
</template>

<style scoped>
/* SYNCHRO | MENU. El alto disponible ya excluye el encabezado. */
.pantalla-menu {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: minmax(0, 1fr);
  place-items: center;
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(12px, 2vh, 22px) clamp(12px, 2.4vw, 28px);
  background: #f2f7fa;
  color: #01305c;
  transition: background-color 150ms ease, color 150ms ease;
}
.contenido-menu {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);
  gap: clamp(10px, 1.8vh, 20px);
  width: min(100%, 800px);
  height: min(100%, 610px);
  min-width: 0;
  min-height: 0;
}
.titulo-menu {
  margin: 0;
  font-size: clamp(22px, 2.8vw, 34px);
  line-height: 1.18;
  font-weight: 700;
  text-align: center;
}
.servicios {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  grid-template-rows: repeat(2, minmax(0, 1fr));
  gap: clamp(10px, 1.8vh, 18px);
  width: 100%;
  height: min(100%, 530px);
  min-width: 0;
  min-height: 0;
  margin: 0 auto;
  place-self: center;
}
.pantalla-menu.tema-oscuro { background: #061f36; color: #ffffff; }
@media (orientation: portrait) {
  .contenido-menu { width: min(100%, 630px); height: min(100%, 670px); }
  .servicios { height: min(100%, 600px); }
}
@media (max-height: 620px) {
  .pantalla-menu { padding: 8px 14px; }
  .contenido-menu { gap: 8px; }
  .titulo-menu { font-size: clamp(19px, 2.5vw, 27px); }
  .servicios { gap: 8px; }
}
@media (max-height: 450px) {
  .pantalla-menu { min-height: 300px; }
  .contenido-menu { min-height: 250px; }
  .servicios { min-height: 200px; }
}
@media (prefers-reduced-motion: reduce) {
  .pantalla-menu { transition: none; }
}

/* Tablet: aprovechar el espacio útil sin tocar el diseño de teléfono */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .contenido-menu {
    width: min(100%, 1110px);
    height: min(100%, 760px);
    gap: clamp(12px, 2vh, 25px);
  }
  .titulo-menu { font-size: clamp(29px, 3vw, 41px); }
  .servicios {
    height: 100%;
    gap: clamp(12px, 1.7vw, 24px);
  }
}
@media (min-width: 600px) and (min-height: 700px) and (orientation: portrait) {
  .contenido-menu {
    width: min(100%, 820px);
    height: min(100%, 1000px);
  }
}

</style>

