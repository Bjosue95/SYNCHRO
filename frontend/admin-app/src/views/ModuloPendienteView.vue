<script setup>
import { RouterLink } from "vue-router";
import AdminIcon from "../components/AdminIcon.vue";
defineProps({ titulo: { type: String, required: true }, descripcion: { type: String, required: true } });
</script>
<template>
  <div>
    <div class="page-heading"><p class="eyebrow">ADMINISTRACIÓN</p><h1>{{ titulo }}</h1></div>
    <section class="panel empty-module">
      <div class="empty-module-icon"><AdminIcon name="info" :size="26" /></div>
      <h2>Módulo en preparación</h2>
      <p>{{ descripcion }}</p>
      <p>Aún no presenta datos ni permite realizar cambios.</p>
      <RouterLink class="action-link" to="/admin">Volver al resumen <AdminIcon name="arrow" :size="17" /></RouterLink>
    </section>
  </div>
</template>
