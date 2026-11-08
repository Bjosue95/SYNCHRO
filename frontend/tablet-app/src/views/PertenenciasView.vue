<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";

import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import { useTema } from "../composables/useTema";

const { oscuro, cambiarTema } = useTema();
const centroAyuda = ref(null);

function abrirAyuda() {
  centroAyuda.value?.abrir();
}
</script>

<template>
  <main class="pantalla-pertenencias" :class="{ 'tema-oscuro': oscuro }">
    <AppHeader
      :oscuro="oscuro"
      @cambiar-tema="cambiarTema"
      @abrir-ayuda="abrirAyuda"
    />

    <RouterLink to="/guardar" class="regresar">
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
      Volver a Guardar
    </RouterLink>

    <section class="contenido">
      <h1>Mis pertenencias</h1>

      <p class="descripcion">
        Guarda temporalmente mochilas, compras, bolsos y otros objetos
        personales.
      </p>

      <div class="informacion">
        <h2>¿Cómo funcionará?</h2>

        <ol>
          <li>Seleccionar el tamaño de casillero.</li>
          <li>Verificarse y completar el depósito.</li>
          <li>Retirar las pertenencias de forma segura.</li>
        </ol>

        <p class="nota">
          Estamos preparando este servicio. Todavía no se realizan depósitos
          desde esta pantalla.
        </p>
      </div>

      <RouterLink to="/guardar/pertenencias/tamano" class="boton-continuar">
        Seleccionar tamaño de casillero
      </RouterLink>
    </section>

    <HelpDialog ref="centroAyuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
.pantalla-pertenencias {
  display: flex;
  flex-direction: column;
  gap: 18px;
  min-height: 100dvh;
  padding: 20px 26px;
  background: #f2f7fa;
  color: #01305c;
}

.regresar {
  display: inline-flex;
  align-items: center;
  align-self: flex-start;
  gap: 12px;
  min-height: 56px;
  padding: 10px 20px;
  border-radius: 14px;
  background: #e4f2f6;
  color: inherit;
  text-decoration: none;
  font-weight: 600;
}

.icono-regresar {
  position: relative;
  display: block;
  width: 30px;
  height: 30px;
}

.icono-regresar img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.regresar-oscuro {
  opacity: 0;
}

.contenido {
  width: min(100%, 760px);
  margin: 0 auto;
  text-align: center;
}

h1 {
  font-size: clamp(24px, 3vw, 34px);
}

.descripcion {
  margin-top: 12px;
  color: #5b7282;
}

.informacion {
  margin-top: 24px;
  padding: 28px;
  border: 1px solid #dfeaf0;
  border-radius: 20px;
  background: #ffffff;
  text-align: left;
}

.informacion h2 {
  font-size: 22px;
  margin-bottom: 16px;
}

.informacion ol {
  padding-left: 24px;
  list-style: decimal;
}

.informacion li {
  margin-bottom: 14px;
}

.nota {
  margin-top: 20px;
  color: #5b7282;
  font-size: 14px;
}

/* Modo oscuro */
.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .regresar {
  background: #204766;
}

.tema-oscuro .regresar-claro {
  opacity: 0;
}

.tema-oscuro .regresar-oscuro {
  opacity: 1;
}

.tema-oscuro .informacion {
  background: #103956;
  border-color: #315573;
}

.tema-oscuro .descripcion,
.tema-oscuro .nota {
  color: #c6dfea;
}

.regresar:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

/* Acción principal: selección de tamaño */

.boton-continuar {
  display: inline-flex;
  align-items: center;
  justify-content: center;

  min-height: 56px;
  margin-top: 22px;
  padding: 12px 26px;

  border-radius: 14px;
  background: #00b9c7;
  color: #01305c;

  text-align: center;
  text-decoration: none;
  font-size: 17px;
  font-weight: 700;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

.boton-continuar:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 4px;
}

@media (prefers-reduced-motion: reduce) {
  .boton-continuar {
    transition: none;
  }
}
</style>
