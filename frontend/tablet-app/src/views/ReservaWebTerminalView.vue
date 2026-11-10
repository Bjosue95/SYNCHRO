<script setup>
import TabletLayout from "../components/TabletLayout.vue";
import ServiceCard from "../components/ServiceCard.vue";
import { useTema } from "../composables/useTema";
const { oscuro } = useTema();
</script>
<template>
  <TabletLayout
    titulo="Tengo una reserva web"
    descripcion="Selecciona el tipo de reserva que ya creaste desde tu cuenta de cliente."
  >
    <div class="tablet-grid">
      <ServiceCard
        titulo="Encomienda"
        descripcion="Deposita una encomienda previamente reservada."
        icono="/iconos/09_encomienda_paquete"
        ruta="/terminal/encomiendas/reserva"
        :oscuro="oscuro"
      />
      <ServiceCard
        titulo="Reserva de frío"
        descripcion="Utiliza tu compartimento reservado desde la web."
        icono="/iconos/07_frio"
        ruta="/terminal/reservas/frio"
        :oscuro="oscuro"
      />
      <ServiceCard
        titulo="Reserva de calor"
        descripcion="Utiliza tu compartimento reservado desde la web."
        icono="/iconos/08_caliente"
        ruta="/terminal/reservas/calor"
        :oscuro="oscuro"
      />
    </div>
    <p class="tablet-note" style="margin-top: 22px">
      Frío y calor son exclusivos de reservas web de clientes registrados. Esta
      terminal no crea ni contrata reservas térmicas.
    </p>
  </TabletLayout>
</template>
