<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import AppHeader from "./AppHeader.vue";
import HelpDialog from "./HelpDialog.vue";
import { useTema } from "../composables/useTema";
import { esDemo } from "../services/tabletApi";
defineProps({
  titulo: String,
  descripcion: String,
  volver: { type: String, default: "/menu" },
  paso: Number,
  pasos: { type: Array, default: () => [] },
});
const { oscuro, cambiarTema } = useTema();
const ayuda = ref(null);
</script>
<template>
  <main class="tablet-layout" :class="{ 'tema-oscuro': oscuro }">
    <AppHeader
      :oscuro="oscuro"
      @cambiar-tema="cambiarTema"
      @abrir-ayuda="ayuda?.abrir()"
    />
    <div class="tablet-content">
      <div class="tablet-context">
        <RouterLink v-if="volver" :to="volver" class="tablet-back"
          >← Volver</RouterLink
        ><span class="tablet-station"
          ><span class="status-dot"></span
          >{{
            esDemo
              ? "Demostración · datos ficticios"
              : "Terminal de autoservicio"
          }}</span
        >
      </div>
      <header class="tablet-heading">
        <div>
          <p class="eyebrow">A TU RITMO, EN UN SOLO LUGAR</p>
          <h1>{{ titulo }}</h1>
          <p v-if="descripcion" class="tablet-description">{{ descripcion }}</p>
        </div>
        <slot name="heading" />
      </header>
      <ol v-if="pasos.length" class="tablet-steps" aria-label="Progreso">
        <li
          v-for="(nombre, i) in pasos"
          :key="nombre"
          :class="{ actual: i === paso, completo: i < paso }"
          :aria-current="i === paso ? 'step' : undefined"
        >
          <span>{{ i < paso ? "✓" : i + 1 }}</span
          >{{ nombre }}
        </li>
      </ol>
      <slot />
    </div>
    <footer class="tablet-footer">
      <span>El punto donde todo se conecta.</span
      ><span>{{
        esDemo
          ? "Vista de prueba · no abre casilleros ni envía mensajes"
          : "Tus datos se usan únicamente para esta operación"
      }}</span>
    </footer>
    <HelpDialog ref="ayuda" :oscuro="oscuro" />
  </main>
</template>
