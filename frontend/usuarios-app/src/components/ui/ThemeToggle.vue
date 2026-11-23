<script setup>
import { useTheme } from "../../composables/useTheme.js";
import AppIcon from "./AppIcon.vue";

const { tema, alternarTema } = useTheme();
</script>

<template>
  <button
    type="button"
    class="boton-tema"
    :aria-label="tema === 'claro' ? 'Activar modo oscuro' : 'Activar modo claro'"
    :title="tema === 'claro' ? 'Cambiar a modo oscuro' : 'Cambiar a modo claro'"
    @click="alternarTema"
  >
    <AppIcon :nombre="tema === 'claro' ? 'luna' : 'sol'" :tamano="22" />
    <span class="etiqueta-tema">{{ tema === 'claro' ? 'Oscuro' : 'Claro' }}</span>
  </button>
</template>

<style scoped>
.boton-tema {
  min-height: 44px;
  padding: 0 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid var(--border-strong);
  border-radius: 999px;
  background: var(--surface-soft);
  color: var(--text);
  font-size: 12px;
  font-weight: 720;
  line-height: 1;
  white-space: nowrap;
  transition: border-color .18s ease, background-color .18s ease;
}
.boton-tema:hover {
  background: var(--surface-hover);
  border-color: var(--brand-accent);
}
.etiqueta-tema { letter-spacing: .005em; }
@media (max-width: 540px) {
  .boton-tema { width: 44px; padding: 0; }
  .etiqueta-tema { display: none; }
}
</style>
