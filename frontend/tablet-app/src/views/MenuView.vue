<script setup>
import { ref } from "vue";

import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import ServiceCard from "../components/ServiceCard.vue";

import { useTema } from "../composables/useTema";

// Tema compartido en toda la aplicación
const { oscuro, cambiarTema } = useTema();

// Centro de ayuda
const centroAyuda = ref(null);

// Servicios de SYNCHRO
const servicios = [
  {
    titulo: "Guardar",
    descripcion: "Encomiendas y pertenencias",
    icono: "01_guardar_depositar",
    ruta: "/guardar",
  },
  {
    titulo: "Retirar",
    descripcion: "Recoge tus objetos",
    icono: "02_retirar",
    ruta: "/retirar",
  },
  {
    titulo: "Tengo una reserva",
    descripcion: "Deposita una encomienda ya reservada",
    icono: "03_reserva",
    ruta: "/terminal/encomiendas/reserva",
  },
  {
    titulo: "Cargar dispositivo",
    descripcion: "Celular o laptop",
    icono: "04_carga_inalambrica",
  },
];

function abrirAyuda() {
  centroAyuda.value?.abrir();
}
</script>

<template>
  <main class="pantalla-menu" :class="{ 'tema-oscuro': oscuro }">
    <!-- Encabezado reutilizable -->
    <AppHeader
      :oscuro="oscuro"
      @cambiar-tema="cambiarTema"
      @abrir-ayuda="abrirAyuda"
    />

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

    <!-- Centro de ayuda -->
    <HelpDialog ref="centroAyuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
/* SYNCHRO | MENÚ PRINCIPAL */

.pantalla-menu {
  display: flex;
  flex-direction: column;
  gap: 22px;

  width: 100%;
  height: 100dvh;
  min-height: 0;
  padding: 20px 26px;
  overflow: hidden;

  background: #f2f7fa;
  color: #01305c;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

/* Título */

.titulo-menu {
  margin: 0;
  font-size: clamp(24px, 3vw, 34px);
  font-weight: 700;
  text-align: center;
}

/* Distribución de las tarjetas */

.servicios {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  grid-template-rows: repeat(2, minmax(0, 1fr));
  gap: 18px;

  width: min(100%, 840px);
  min-height: 0;
  flex: 1;
  margin: 0 auto;
}

/* Tema oscuro */

.pantalla-menu.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

/* Tablets compactas */

@media (max-height: 550px) {
  .pantalla-menu {
    gap: 12px;
    padding: 12px 20px;
  }

  .servicios {
    gap: 12px;
  }
}

/* Movimiento reducido */

@media (prefers-reduced-motion: reduce) {
  .pantalla-menu {
    transition: none;
  }
}
</style>
