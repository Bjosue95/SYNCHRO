<script setup>
import { RouterView } from "vue-router";
</script>

<template>
  <RouterView v-slot="{ Component, route }">
    <Transition name="navegacion" mode="out-in">
      <component :is="Component" :key="route.path" />
    </Transition>
  </RouterView>
</template>

<style>
html,
body,
#app {
  width: 100%;
  min-height: 100dvh;
  margin: 0;
  padding: 0;
}

body {
  background: #061f36;
  font-family: Arial, Helvetica, sans-serif;
}

#app {
  background: #061f36;
}
</style>
