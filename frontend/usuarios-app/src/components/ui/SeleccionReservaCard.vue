<script setup lang="ts">
import AppIcon from './AppIcon.vue';
defineProps<{titulo:string;descripcion?:string;icono?:string;seleccionado:boolean;disponibles?:number;disabled?:boolean}>();
defineEmits<{seleccionar:[]}>();
</script>
<template>
  <button type="button" class="seleccion-reserva" :class="{seleccionada:seleccionado}" :aria-pressed="seleccionado" :disabled="disabled || disponibles===0" @click="$emit('seleccionar')">
    <AppIcon v-if="icono" :nombre="icono" :tamano="54" /><strong>{{ titulo }}</strong><span v-if="descripcion">{{ descripcion }}</span>
    <small v-if="disponibles!=null">{{ disponibles ? disponibles+' disponibles':'No disponible' }}</small><small v-if="seleccionado">Seleccionado</small>
  </button>
</template>
<style scoped>
.seleccion-reserva{display:flex;flex-direction:column;align-items:center;justify-content:center;gap:12px;min-height:150px;width:100%;padding:22px 16px;text-align:center;border:2px solid var(--border);border-radius:var(--radius-md);background:var(--surface);color:var(--text-strong)}.seleccion-reserva.seleccionada{border-color:var(--brand-accent);background:var(--surface-accent)}.seleccion-reserva strong{font-size:18px}.seleccion-reserva span,.seleccion-reserva small{color:var(--text-muted);font-size:14px}.seleccion-reserva:disabled{opacity:.55;cursor:not-allowed}
</style>
