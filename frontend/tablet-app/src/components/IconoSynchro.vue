<script setup>
import { computed } from "vue";
import { useTema } from "../composables/useTema";
const props = defineProps({
  nombre: { type: String, required: true },
  tema: { type: String, default: "auto" },
  tamano: { type: Number, default: 24 },
});
const { oscuro } = useTema();
const ruta = computed(
  () =>
    `/iconos/complementarios/${props.nombre}${(props.tema === "auto" ? oscuro.value : props.tema === "oscuro") ? "-oscuro" : ""}.svg`,
);
</script>
<template>
  <img
    :src="ruta"
    alt=""
    aria-hidden="true"
    :width="tamano"
    :height="tamano"
    class="icono-synchro"
  />
</template>
<style scoped>
.icono-synchro {
  display: inline-block;
  object-fit: contain;
  flex-shrink: 0;
  vertical-align: middle;
}
</style>
