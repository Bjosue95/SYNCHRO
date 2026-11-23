<script setup>
import { useTheme } from "../../composables/useTheme.js";
import AppIcon from "./AppIcon.vue";
const { tema, alternarTema } = useTheme();
</script>
<template>
  <button type="button" class="boton-tema" :aria-label="tema === 'claro' ? 'Activar modo oscuro' : 'Activar modo claro'"
    :title="tema === 'claro' ? 'Modo oscuro' : 'Modo claro'" @click="alternarTema">
    <AppIcon :nombre="tema === 'claro' ? 'luna' : 'sol'" :tamano="20"/>
  </button>
</template>
<style scoped>
.boton-tema {
  width: 43px; height: 43px; border-radius: 12px; border: 1px solid var(--border);
  display: inline-grid; place-items: center; background: var(--surface);
  transition: background .2s, border-color .2s, transform .2s;
}
.boton-tema:hover { background: var(--surface-hover); border-color: var(--brand-accent); transform: translateY(-1px); }
</style>
