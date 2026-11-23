<script setup>
import { computed } from "vue";
import { useTheme } from "../../composables/useTheme.js";
defineProps({ compacto: { type: Boolean, default: false } });
const { tema } = useTheme();
const isotipo = computed(() => tema.value === "oscuro"
  ? "/branding/isotipo-oscuro.png" : "/branding/isotipo-claro.png");
const nombre = computed(() => tema.value === "oscuro"
  ? "/branding/nombre-blanco.png" : "/branding/nombre-azul.png");
</script>
<template>
  <span class="marca-real" :class="{ compacta: compacto }" aria-label="SYNCHRO">
    <img class="marca-icono" :src="isotipo" alt="" draggable="false" />
    <span class="marca-textos">
      <img class="marca-letras" :src="nombre" alt="SYNCHRO" draggable="false" />
      <small v-if="!compacto">El punto donde todo se conecta</small>
    </span>
  </span>
</template>
<style scoped>
.marca-real { display: inline-flex; align-items: center; gap: 11px; text-decoration: none; min-width: 0; }
.marca-icono { width: 36px; height: 42px; object-fit: contain; flex-shrink: 0; }
.marca-textos { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.marca-letras { display: block; width: 146px; height: 32px; object-fit: contain; object-position: left center; }
.marca-textos small { display: block; font-size: 9px; line-height: 1.3; font-weight: 600; color: var(--text-muted); letter-spacing: .015em; }
.compacta .marca-icono { width: 33px; height: 38px; }
.compacta .marca-letras { width: 135px; height: 29px; }
@media (max-width: 440px) {
  .marca-icono { width: 30px; height: 35px; }
  .marca-letras { width: 122px; height: 27px; }
}
</style>
