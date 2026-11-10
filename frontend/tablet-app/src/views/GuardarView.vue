<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import ServiceCard from "../components/ServiceCard.vue";

import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import { useTema } from "../composables/useTema";

// Compartir el tema con el menú
const { oscuro, cambiarTema } = useTema();

// Centro de ayuda reutilizable
const centroAyuda = ref(null);

function abrirAyuda() {
  centroAyuda.value?.abrir();
}
</script>

<template>
  <main class="pantalla-guardar" :class="{ 'tema-oscuro': oscuro }">
    <!-- ENCABEZADO REUTILIZABLE -->
    <AppHeader
      :oscuro="oscuro"
      @cambiar-tema="cambiarTema"
      @abrir-ayuda="abrirAyuda"
    />

    <!-- REGRESAR AL MENÚ -->
    <RouterLink to="/menu" class="regresar">
      <span class="icono-regresar" aria-hidden="true">
        <img
          src="/iconos/complementarios/28_regresar.svg"
          alt=""
          class="regresar-claro"
        />

        <img
          src="/iconos/complementarios/28_regresar-oscuro.svg"
          alt=""
          class="regresar-oscuro"
        />
      </span>

      <span>Volver al menú</span>
    </RouterLink>

    <!-- TÍTULO -->
    <h1>¿Qué deseas guardar?</h1>

    <!-- TARJETAS REUTILIZABLES -->
    <div class="opciones-guardar">
      <ServiceCard
        titulo="Mis pertenencias"
        descripcion="Mochilas, compras, bolsos y objetos personales"
        icono="/iconos/10_mochila_bolso"
        ruta="/guardar/pertenencias"
        :oscuro="oscuro"
      />

      <ServiceCard
        titulo="Depositar encomienda reservada"
        descripcion="Usa la reserva creada desde tu cuenta web"
        icono="/iconos/09_encomienda_paquete"
        ruta="/terminal/encomiendas/reserva"
        :oscuro="oscuro"
      />
    </div>

    <!-- CENTRO DE AYUDA -->
    <HelpDialog ref="centroAyuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
.pantalla-guardar {
  display: flex;
  flex-direction: column;
  gap: 18px;

  width: 100%;
  min-height: 100dvh;
  padding: 20px 26px;

  background: #f2f7fa;
  color: #01305c;
}

/* Navegación táctil de SYNCHRO */

.regresar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  align-self: flex-start;

  min-height: 56px;
  min-width: 175px;
  padding: 10px 20px;

  border: none;
  border-radius: 14px;

  background: #e4f2f6;
  color: #01305c;

  text-decoration: none;
  font-size: 16px;
  font-weight: 600;

  cursor: pointer;
}

.regresar:active {
  background: #cce8ee;
}

.regresar:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

/* Modo oscuro */

.tema-oscuro .regresar {
  background: #204766;
  color: #ffffff;
}

.tema-oscuro .regresar:active {
  background: #315c79;
}

h1 {
  font-size: clamp(24px, 3vw, 34px);
  text-align: center;
}

.opciones-guardar {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 18px;

  width: min(100%, 840px);
  flex: 1;
  margin: 0 auto;
}

.opcion {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;

  min-width: 0;
  padding: 20px;

  background: #ffffff;
  border: 1px solid #dfeaf0;
  border-radius: 20px;

  text-align: center;
}

.opcion h2 {
  font-size: clamp(18px, 2vw, 24px);
}

.opcion p {
  color: #5b7282;
  font-size: clamp(13px, 1.5vw, 16px);
}

/* Iconos claros y oscuros */

.icono-par {
  position: relative;
  display: block;
  width: 84px;
  height: 84px;
  flex-shrink: 0;
}

.icono {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.icono-claro {
  opacity: 1;
}

.icono-oscuro {
  opacity: 0;
}

/* Tema oscuro */

.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .regresar {
  border-color: #42647e;
}

.tema-oscuro .opcion {
  background: #103956;
  border-color: #315573;
}

.tema-oscuro .opcion p {
  color: #c6dfea;
}

.tema-oscuro .icono-claro {
  opacity: 0;
}

.tema-oscuro .icono-oscuro {
  opacity: 1;
}

/* Tablets compactas */

@media (max-height: 550px) {
  .pantalla-guardar {
    gap: 10px;
    padding: 12px 20px;
  }

  .opciones-guardar {
    gap: 12px;
  }

  .icono-par {
    width: 55px;
    height: 55px;
  }

  .opcion {
    gap: 6px;
    padding: 12px;
  }
}

/* Icono original de regresar SYNCHRO */

.icono-regresar {
  position: relative;
  display: block;
  width: 30px;
  height: 30px;
  flex-shrink: 0;
}

.icono-regresar img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
}

/* Modo claro */
.regresar-claro {
  opacity: 1;
}

.regresar-oscuro {
  opacity: 0;
}

/* Modo oscuro */
.tema-oscuro .regresar-claro {
  opacity: 0;
}

.tema-oscuro .regresar-oscuro {
  opacity: 1;
}
</style>
