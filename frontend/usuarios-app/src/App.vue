<script setup>
import { onMounted, computed } from "vue";
import { RouterView, useRoute } from "vue-router";
import { comprobarSesion } from "./services/sesion.js";
import AppShell from "./components/layout/AppShell.vue";
import PublicLayout from "./components/layout/PublicLayout.vue";

const route = useRoute();
const layout = computed(() => route.meta.privada ? AppShell : PublicLayout);

// Restaurar la identidad a partir de la cookie del backend, nunca desde localStorage.
onMounted(() => {
  void comprobarSesion().catch(() => {
    // Las páginas privadas comprueban la sesión desde su guardia.
  });
});
</script>

<template>
  <component :is="layout">
    <RouterView v-slot="{ Component, route: actual }">
      <Transition name="vista-pagina" mode="out-in">
        <component :is="Component" :key="actual.path" />
      </Transition>
    </RouterView>
  </component>
</template>
