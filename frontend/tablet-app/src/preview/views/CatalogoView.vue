<script setup>
import { computed } from "vue";
import { RouterLink } from "vue-router";
import { useTema } from "../../composables/useTema";
import { pantallas } from "../pantallas.js";

const { oscuro } = useTema();

const secciones = computed(() =>
  ["Inicio", "Depósito", "Carga", "Retiro", "Retiro de dispositivos"].map(nombre => ({
    nombre,
    vistas: pantallas.filter(p => p.grupo === nombre)
  }))
);
</script>

<template>
  <main class="catalogo" :class="{ 'tema-oscuro': oscuro }">
    <div class="contenido">
      <header>
        <h1>Vistas previas</h1>
        <p>Selecciona una pantalla para revisar su diseño.</p>
      </header>

      <section
        v-for="seccion in secciones"
        :key="seccion.nombre"
        class="seccion"
      >
        <h2>{{ seccion.nombre }}</h2>

        <div class="cuadricula">
          <RouterLink
            v-for="pantalla in seccion.vistas"
            :key="pantalla.id"
            class="tarjeta"
            :to="`/vista-previa/${pantalla.id}`"
          >
            <span>{{ pantalla.titulo }}</span>
            <span aria-hidden="true">↗</span>
          </RouterLink>
        </div>
      </section>
    </div>
  </main>
</template>

<style scoped>
.catalogo,
.catalogo * {
  box-sizing: border-box;
}

.catalogo {
  width: 100%;
  height: 100%;
  overflow-y: auto;
  background: #f2f7fa;
  color: #01305c;
  transition: background-color 150ms ease, color 150ms ease;
}

.contenido {
  width: min(100%, 1060px);
  margin: 0 auto;
  padding: clamp(20px, 3vw, 38px);
}

header {
  text-align: center;
  margin-bottom: 26px;
}

h1 {
  margin: 0;
  font-size: clamp(27px, 3.3vw, 38px);
}

header p {
  color: #5b7282;
  font-size: 15px;
}

.seccion {
  margin-bottom: 25px;
}

h2 {
  margin: 0 0 13px;
  font-size: 20px;
}

.cuadricula {
  display: grid;
  grid-template-columns: repeat(
    auto-fit, minmax(min(100%, 245px), 1fr)
  );
  gap: 13px;
}

.tarjeta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;

  min-height: 82px;
  padding: 18px;

  border: 2px solid #c9e3ea;
  border-radius: 17px;

  background: #ffffff;
  color: #01305c;

  text-decoration: none;
  font-weight: 750;
  font-size: 16px;

  box-shadow: 0 5px 14px rgba(1, 48, 92, .05);
  transition: background-color 150ms ease, border-color 150ms ease;
}

.tarjeta span:last-child {
  color: #008b9e;
  font-size: 24px;
}

.tarjeta:hover {
  border-color: #00b9c7;
  background: #ecfbfd;
}

.tarjeta:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro header p {
  color: #d4e2ea;
}

.tema-oscuro .tarjeta {
  background: #103956;
  border-color: #42647e;
  color: #ffffff;
}

.tema-oscuro .tarjeta:hover {
  background: #194b68;
  border-color: #5ec6e8;
}

.tema-oscuro .tarjeta span:last-child {
  color: #5ec6e8;
}

@media (prefers-reduced-motion: reduce) {
  .catalogo,
  .tarjeta {
    transition: none;
  }
}
</style>