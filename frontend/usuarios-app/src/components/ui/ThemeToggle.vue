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
    <AppIcon :nombre="tema === 'claro' ? 'luna' : 'sol'" />
    <span class="etiqueta-tema">{{ tema === 'claro' ? 'Oscuro' : 'Claro' }}</span>
  </button>
</template>

<style scoped>
.boton-tema {
  width: 96px;
  min-width: 96px;
  height: 44px;
  min-height: 44px;
  padding: 0 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-control);
  background: var(--surface-soft);
  color: var(--text);
  font-size: 12px;
  font-weight: 600;
  line-height: 1;
  white-space: nowrap;
  transition: border-color 160ms var(--ease-ui), background-color 160ms var(--ease-ui);
}
.boton-tema:hover {
  background: var(--surface-hover);
  border-color: var(--ring);
}
.etiqueta-tema {
  width: 40px;
  flex: 0 0 40px;
  text-align: left;
}
@media (max-width: 540px) {
  .boton-tema { width: 44px; min-width: 44px; padding: 0; }
  .etiqueta-tema { display: none; }
}
</style>
