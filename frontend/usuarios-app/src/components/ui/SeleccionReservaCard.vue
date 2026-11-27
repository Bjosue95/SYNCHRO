<script setup lang="ts">
import AppIcon from './AppIcon.vue';
import IconoTablet from './IconoTablet.vue';
defineProps<{
  titulo: string;
  descripcion?: string;
  icono?: string;
  iconoTablet?: string;
  seleccionado: boolean;
  disponibles?: number;
  disabled?: boolean;
}>();
defineEmits<{ seleccionar: [] }>();
</script>
<template>
  <button type="button" class="seleccion-reserva"
    :class="{ seleccionada: seleccionado }" :aria-pressed="seleccionado"
    :disabled="disabled || disponibles === 0" @click="$emit('seleccionar')">
    <span class="seleccion-reserva__icono" aria-hidden="true">
      <IconoTablet v-if="iconoTablet" :nombre="iconoTablet" :tamano="58" />
      <AppIcon v-else-if="icono" :nombre="icono" :tamano="58" />
    </span>
    <strong class="seleccion-reserva__titulo">{{ titulo }}</strong>
    <span class="seleccion-reserva__descripcion">{{ descripcion || ' ' }}</span>
    <small class="seleccion-reserva__disponibles">
      {{ disponibles == null ? ' ' : disponibles > 0 ? disponibles + ' disponibles' : 'No disponible' }}
    </small>
    <small class="seleccion-reserva__seleccion">
      {{ seleccionado ? 'Seleccionado' : ' ' }}
    </small>
  </button>
</template>
<style scoped>
.seleccion-reserva {
  box-sizing: border-box;
  position: relative;
  display: grid;
  grid-template-rows: 56px 54px 50px 20px 20px;
  align-items: center;
  justify-items: center;
  gap: 6px;
  width: 100%;
  height: 260px;
  min-width: 0;
  padding: 18px 12px;
  text-align: center;
  font: inherit;
  border: 2px solid #dfeaf0;
  border-radius: var(--radius-md);
  background: #ffffff;
  color: #01305c;
  cursor: pointer;
  transition: border-color 150ms ease, background-color 150ms ease, box-shadow 150ms ease;
}
.seleccion-reserva__icono {
  display: grid;
  place-items: center;
  width: 60px;
  height: 56px;
}
.seleccion-reserva__titulo {
  display: grid;
  place-items: center;
  min-width: 0;
  font-size: clamp(14px, 1.6vw, 18px);
  line-height: 1.18;
  overflow-wrap: anywhere;
}
.seleccion-reserva__descripcion {
  display: grid;
  place-items: center;
  align-self: stretch;
  min-width: 0;
  color: #5b7282;
  font-size: 12.5px;
  line-height: 1.25;
  overflow: hidden;
  overflow-wrap: anywhere;
}
.seleccion-reserva__disponibles,
.seleccion-reserva__seleccion {
  display: grid;
  place-items: center;
  align-self: stretch;
  min-width: 0;
  color: #008c99;
  font-size: 12px;
  line-height: 1.2;
  white-space: nowrap;
}
.seleccion-reserva.seleccionada {
  border-color: var(--brand-accent);
  background: #e8f9fa;
  box-shadow: 0 0 0 2px rgb(0 185 199 / 12%);
}
.seleccion-reserva.seleccionada::after {
  content: '✓';
  display: grid;
  place-items: center;
  position: absolute;
  top: 9px;
  right: 9px;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: #00b9c7;
  color: #01305c;
  font-size: 16px;
  font-weight: 800;
  pointer-events: none;
}
.seleccion-reserva:focus-visible {
  outline: 3px solid var(--brand-accent);
  outline-offset: 3px;
}
.seleccion-reserva:disabled { opacity: .55; cursor: not-allowed; }

/* Equivalentes exactos de las tarjetas SYNCHRO Tablet en modo oscuro. */
:global(:root[data-theme="oscuro"] .seleccion-reserva) {
  background:#103956;
  border-color:#315573;
  color:#ffffff;
}
:global(:root[data-theme="oscuro"] .seleccion-reserva .seleccion-reserva__descripcion) {
  color:#c6dfea;
}
:global(:root[data-theme="oscuro"] .seleccion-reserva .seleccion-reserva__disponibles),
:global(:root[data-theme="oscuro"] .seleccion-reserva .seleccion-reserva__seleccion) {
  color:#5ec6e8;
}
:global(:root[data-theme="oscuro"] .seleccion-reserva.seleccionada) {
  background:#174b65;
  border-color:#00b9c7;
  box-shadow:0 0 0 2px rgb(0 185 199 / 20%);
}
:global(:root[data-theme="oscuro"] .seleccion-reserva:disabled) {
  opacity:.75;
}
@media (prefers-reduced-motion: reduce) {
  .seleccion-reserva { transition: none; }
}
</style>
