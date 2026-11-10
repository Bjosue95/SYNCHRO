<script setup>
import TabletLayout from "../components/TabletLayout.vue";
import IconoSynchro from "../components/IconoSynchro.vue";
import ServiceCard from "../components/ServiceCard.vue";
import { useTema } from "../composables/useTema";
const { oscuro } = useTema();
const servicios = [
  {
    titulo: "Guardar",
    descripcion: "Encomiendas y pertenencias",
    icono: "01_guardar_depositar",
    ruta: "/guardar",
  },
  {
    titulo: "Retirar",
    descripcion: "Recoge tus objetos",
    icono: "02_retirar",
    ruta: "/retirar",
  },
  {
    titulo: "Tengo una reserva web",
    descripcion: "Utiliza tu reserva creada desde la web",
    icono: "03_reserva",
    ruta: "/terminal/reserva-web",
  },
  {
    titulo: "Cargar dispositivo",
    descripcion: "Celular o laptop",
    icono: "04_carga_inalambrica",
    ruta: "/servicios/carga",
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
    <div class="tablet-grid menu-original">
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
      <IconoSynchro nombre="41_privacidad" />
      <p>
        Encomiendas, frío y calor se reservan en la web.<span>
          En esta terminal puedes utilizar tu reserva existente.</span
        >
      </p>
    </div>
  </TabletLayout>
</template>
<style scoped>
.menu-original {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  width: min(100%, 840px);
  margin: 0 auto;
}
@media (max-width: 500px) {
  .menu-original {
    grid-template-columns: 1fr;
  }
}

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
