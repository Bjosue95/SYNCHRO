<script setup>
import { useTheme } from "../../composables/useTheme.js";
import AppIcon from "./AppIcon.vue";

const { tema, alternarTema } = useTheme();
</script>

<template>
  <button
    type="button"
    class="boton-tema"
    :data-modo="tema"
    :aria-label="tema === 'claro' ? 'Activar modo oscuro' : 'Activar modo claro'"
    :title="tema === 'claro' ? 'Modo claro activo · Cambiar a oscuro' : 'Modo oscuro activo · Cambiar a claro'"
    @click="alternarTema"
  >
    <span class="tema-indicador" aria-hidden="true"></span>
    <span class="tema-opcion" :class="{ activa: tema === 'claro' }" aria-hidden="true">
      <AppIcon nombre="sol" :tamano="21"/>
    </span>
    <span class="tema-opcion" :class="{ activa: tema === 'oscuro' }" aria-hidden="true">
      <AppIcon nombre="luna" :tamano="21"/>
    </span>
  </button>
</template>

<style scoped>
.boton-tema {
  position: relative;
  isolation: isolate;
  display: inline-grid;
  grid-template-columns: repeat(2, 1fr);
  align-items: center;
  flex: 0 0 auto;
  width: 84px;
  min-width: 84px;
  height: 44px;
  min-height: 44px;
  padding: 3px;
  gap: 0;
  border: 1px solid var(--border-strong);
  border-radius: 999px;
  background: var(--surface-soft);
  cursor: pointer;
  transition: border-color .18s ease, background-color .18s ease;
}
.boton-tema:hover { border-color: var(--brand-accent); background: var(--surface-hover); }
.boton-tema:focus-visible { outline: 3px solid var(--ring); outline-offset: 3px; }
.tema-indicador {
  position: absolute;
  inset: 3px auto 3px 3px;
  width: calc(50% - 3px);
  border-radius: 999px;
  background: var(--surface-accent);
  border: 1px solid var(--brand-accent);
  box-shadow: var(--shadow-sm);
  z-index: -1;
  transition: transform .18s ease;
}
.boton-tema[data-modo="oscuro"] .tema-indicador { transform: translateX(100%); }
.tema-opcion { display: grid; place-items: center; min-width: 0; height: 34px; opacity: .65; transition: opacity .18s ease; }
.tema-opcion.activa { opacity: 1; }
@media (max-width: 540px) {
  .boton-tema { width: 44px; min-width: 44px; grid-template-columns: 1fr; }
  .tema-indicador { width: auto; inset: 3px; }
  .boton-tema[data-modo="oscuro"] .tema-indicador { transform: none; }
  .tema-opcion:not(.activa) { display: none; }
}
@media (prefers-reduced-motion: reduce) {
  .boton-tema, .tema-indicador, .tema-opcion { transition: none; }
}
</style>
