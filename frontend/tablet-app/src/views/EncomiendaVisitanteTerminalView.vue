<script setup>
import { RouterLink } from "vue-router";
</script>

<template>
  <main>
    <h1>Encomiendas con reserva web</h1>
    <p>Este servicio es exclusivo para clientes registrados. Crea tu reserva desde tu cuenta en la web; aquí puedes depositar o retirar una encomienda ya reservada.</p>
    <RouterLink to="/terminal/encomiendas/reserva">Consultar mi reserva</RouterLink>
  </main>
</template>
