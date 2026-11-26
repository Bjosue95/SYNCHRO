<script setup lang="ts">
import { computed } from 'vue';
import { useTheme } from '../../composables/useTheme.js';
// Exactamente los SVG de la Tablet SYNCHRO, en sus variantes claro/oscuro.
const archivos: Record<string, string> = {
  guardar: '32_guardar_pertenencias_personales',
  encomienda: '09_encomienda_paquete',
  Normal: '01_guardar_depositar',
  Refrigerado: '07_frio',
  Caliente: '08_caliente',
  Pequeno: 'casillero-tamano-S',
  Mediano: 'casillero-tamano-M',
  Grande: 'casillero-tamano-L',
  ubicacion: '53_ubicacion',
  tiempo: '21_tiempo_tarifa',
  whatsapp: '40_whatsapp',
  volver: '28_regresar',
  continuar: '29_continuar'
};
const props = withDefaults(defineProps<{ nombre: string; tamano?: number }>(), { tamano: 58 });
const { tema } = useTheme();
const ruta = computed(() => {
  const archivo = archivos[props.nombre] || archivos.Normal;
  return '/iconos/tablet/' + archivo + (tema.value === 'oscuro' ? '-oscuro' : '') + '.svg';
});
</script>
<template>
  <img class="icono-tablet" :src="ruta" :width="tamano" :height="tamano"
    :style="{ width: tamano + 'px', height: tamano + 'px' }"
    alt="" aria-hidden="true" draggable="false" decoding="async" />
</template>
<style scoped>
.icono-tablet {
  display: block;
  flex: 0 0 auto;
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}
</style>
