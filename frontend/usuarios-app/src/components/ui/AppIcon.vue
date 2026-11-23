<script setup>
import { computed } from "vue";
import { useTheme } from "../../composables/useTheme.js";
const props = defineProps({
  nombre: { type: String, required: true },
  tamano: { type: Number, default: 22 },
  etiqueta: { type: String, default: "" }
});
const { tema } = useTheme();
const conocidos = new Set([
  "inicio","encomienda","reservas","casillero","temperatura","carga",
  "usuario","seguridad","buscar","whatsapp","sol","luna","salir","menu",
  "flecha","calendario","reloj","ubicacion","check","volver"
]);
const icono = computed(() => conocidos.has(props.nombre) ? props.nombre : "casillero");
const ruta = computed(() => `/iconos/${tema.value}/${icono.value}.svg`);
</script>
<template>
  <img class="icono-ui" :src="ruta" :width="tamano" :height="tamano"
    :alt="etiqueta" :aria-hidden="etiqueta ? undefined : 'true'"
    decoding="async" draggable="false" />
</template>
<style scoped>
.icono-ui { display: block; flex: 0 0 auto; object-fit: contain; }
</style>
