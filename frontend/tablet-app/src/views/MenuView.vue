<script setup>
import TabletLayout from "../components/TabletLayout.vue";
import ServiceCard from "../components/ServiceCard.vue";
import { useTema } from "../composables/useTema";
const { oscuro } = useTema();
const servicios = [
  {
    titulo: "Guardar pertenencias",
    descripcion: "Un espacio para tus objetos personales.",
    icono: "10_mochila_bolso",
    ruta: "/servicios/personal",
  },
  {
    titulo: "Retirar",
    descripcion: "Recoge tus objetos o una encomienda.",
    icono: "02_retirar",
    ruta: "/retirar",
  },
  {
    titulo: "Tengo una reserva",
    descripcion: "Deposita tu encomienda reservada en la web.",
    icono: "03_reserva",
    ruta: "/terminal/encomiendas/reserva",
  },
  {
    titulo: "Cargar dispositivo",
    descripcion: "Energía para tu celular o tu laptop.",
    icono: "05_carga_por_cable",
    ruta: "/servicios/carga",
  },
  {
    titulo: "Mantener en frío",
    descripcion: "Resguardo en un compartimento frío.",
    icono: "07_frio",
    ruta: "/servicios/frio",
  },
  {
    titulo: "Mantener caliente",
    descripcion: "Un espacio para conservar el calor.",
    icono: "08_caliente",
    ruta: "/servicios/calor",
  },
];
</script>
<template>
  <TabletLayout
    titulo="¿Qué deseas hacer?"
    descripcion="Elige un servicio. Nosotros te acompañamos paso a paso."
    volver="/"
  >
    <template #heading
      ><span class="tablet-badge">Fácil · Seguro · A tu ritmo</span></template
    >
    <div class="tablet-grid">
      <ServiceCard
        v-for="s in servicios"
        :key="s.ruta"
        :titulo="s.titulo"
        :descripcion="s.descripcion"
        :icono="`/iconos/${s.icono}`"
        :ruta="s.ruta"
        :oscuro="oscuro"
      />
    </div>
    <div class="menu-reminder">
      <img :src="`/iconos/complementarios/41_privacidad${oscuro ? '-oscuro' : ''}.svg`" alt="" />
      <p>
        Tu encomienda comienza en la web.<span>
          Reserva desde tu cuenta; en esta terminal puedes depositarla o
          retirarla.</span
        >
      </p>
    </div>
  </TabletLayout>
</template>
<style scoped>
.menu-reminder {
  display: flex;
  align-items: center;
  gap: 13px;
  color: var(--muted);
  font-size: 13px;
  margin-top: 22px;
  padding: 16px 20px;
  border: 1px solid var(--line);
  border-radius: 14px;
}
.menu-reminder img {
  width: 25px;
  height: 25px;
}
.menu-reminder p {
  font-weight: 600;
}
.menu-reminder span {
  font-weight: 400;
}
</style>
