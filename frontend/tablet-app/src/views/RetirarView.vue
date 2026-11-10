<script setup>
import TabletLayout from "../components/TabletLayout.vue";
import ServiceCard from "../components/ServiceCard.vue";
import { useTema } from "../composables/useTema";
const { oscuro } = useTema();
</script>
<template>
  <TabletLayout
    titulo="¿Qué deseas retirar?"
    descripcion="Elige tu servicio y ten a mano los datos de tu reserva."
    ><div class="tablet-two">
      <ServiceCard
        titulo="Pertenencias y dispositivos"
        descripcion="Pertenencias, carga y compartimentos de frío o calor."
        icono="/iconos/10_mochila_bolso"
        ruta="/retirar/servicios"
        :oscuro="oscuro"
      /><ServiceCard
        titulo="Encomiendas"
        descripcion="Verifica tu rostro y utiliza el PIN de WhatsApp."
        icono="/iconos/09_encomienda_paquete"
        ruta="/terminal/encomiendas/retiro"
        :oscuro="oscuro"
      /></div
  ></TabletLayout>
</template>
