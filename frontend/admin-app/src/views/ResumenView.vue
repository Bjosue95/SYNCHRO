<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink } from "vue-router";
import { apiAdmin } from "../services/adminApi";
import AdminIcon from "../components/AdminIcon.vue";

const cargando = ref(true);
const error = ref("");
const casilleros = ref(null);
const solicitudes = ref(null);
const ultimaActualizacion = ref("");
const disponibles = computed(() => casilleros.value?.filter(x => x.estado === "Disponible").length ?? null);
const ocupados = computed(() => casilleros.value?.filter(x => x.estado === "Ocupado").length ?? null);
const pendientes = computed(() => solicitudes.value?.filter(x => x.estado === "Pendiente").length ?? null);
const pinPorEnviar = computed(() => solicitudes.value?.filter(x => ["Reservado", "PinEmitido"].includes(x.estado)).length ?? null);
const totalCasilleros = computed(() => casilleros.value?.length ?? null);

async function actualizar() {
  if (cargando.value && ultimaActualizacion.value) return;
  cargando.value = true;
  error.value = "";
  const [c, s] = await Promise.allSettled([
    apiAdmin("/api/terminales/casilleros-prototipo"),
    apiAdmin("/api/admin/whatsapp-manual/pendientes"),
  ]);
  casilleros.value = c.status === "fulfilled" && Array.isArray(c.value) ? c.value : null;
  solicitudes.value = s.status === "fulfilled" && Array.isArray(s.value?.operaciones) ? s.value.operaciones : null;
  if (!casilleros.value || !solicitudes.value) error.value = "Algunos datos no están disponibles. Comprueba la conexión con SYNCHRO e inténtalo otra vez.";
  ultimaActualizacion.value = new Date().toLocaleTimeString("es-HN", { hour: "2-digit", minute: "2-digit" });
  cargando.value = false;
}
onMounted(actualizar);
</script>

<template>
  <div>
    <div class="page-heading spread-heading">
      <div><p class="eyebrow">VISIÓN GENERAL</p><h1>Resumen operativo</h1><p>Supervisa lo que necesita atención y el estado de tus casilleros.</p></div>
      <button type="button" class="button-secondary" :disabled="cargando" @click="actualizar"><AdminIcon name="refresh" :size="17" />{{ cargando ? "Actualizando..." : "Actualizar" }}</button>
    </div>
    <nav class="syn-admin-shortcuts" aria-label="Accesos operativos">
      <RouterLink to="/admin/whatsapp"><AdminIcon name="message" :size="23" /><span><strong>WhatsApp y PIN</strong><small>Autorizaciones</small></span></RouterLink>
      <RouterLink to="/admin/cobros"><AdminIcon name="check" :size="23" /><span><strong>Cobros</strong><small>Gestionar pagos</small></span></RouterLink>
      <RouterLink to="/admin/casilleros"><AdminIcon name="lockers" :size="23" /><span><strong>Casilleros</strong><small>Disponibilidad</small></span></RouterLink>
    </nav>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <div class="section-heading"><h2>Estado actual</h2><span v-if="ultimaActualizacion" class="updated-at">Consultado a las {{ ultimaActualizacion }}</span></div>
    <section class="kpi-grid" aria-label="Indicadores de operación">
      <article class="kpi-card"><div class="kpi-top"><span>Casilleros disponibles</span><span class="kpi-icon"><AdminIcon name="lockers" :size="20" /></span></div><strong>{{ disponibles ?? '—' }}</strong><small>De {{ totalCasilleros ?? '—' }} casilleros</small></article>
      <article class="kpi-card"><div class="kpi-top"><span>Casilleros ocupados</span><span class="kpi-icon"><AdminIcon name="key" :size="20" /></span></div><strong>{{ ocupados ?? '—' }}</strong><small>Estado registrado</small></article>
      <article class="kpi-card"><div class="kpi-top"><span>WhatsApp por verificar</span><span class="kpi-icon kpi-icon-accent"><AdminIcon name="message" :size="20" /></span></div><strong>{{ pendientes ?? '—' }}</strong><small>Requieren revisión manual</small></article>
      <article class="kpi-card"><div class="kpi-top"><span>PIN por enviar</span><span class="kpi-icon kpi-icon-warm"><AdminIcon name="clock" :size="20" /></span></div><strong>{{ pinPorEnviar ?? '—' }}</strong><small>Acción pendiente del operador</small></article>
    </section>

    <div class="dashboard-grid">
      <section class="panel priority-panel">
        <div class="panel-heading"><div><p class="eyebrow">PRIORIDADES</p><h2>Atención de solicitudes</h2></div><AdminIcon name="message" :size="21" /></div>
        <p v-if="solicitudes === null">No se pudo consultar la cola de autorizaciones.</p>
        <p v-else-if="pendientes === 0 && pinPorEnviar === 0">No hay autorizaciones ni envíos de PIN pendientes en este momento.</p>
        <p v-else>Tienes <strong>{{ pendientes }} solicitudes por verificar</strong> y <strong>{{ pinPorEnviar }} PIN pendientes de envío</strong>. Revisa el mensaje recibido y confirma la operación correspondiente.</p>
        <RouterLink to="/admin/whatsapp" class="action-link">Gestionar WhatsApp <AdminIcon name="arrow" :size="17" /></RouterLink>
      </section>
      <section class="panel inventory-panel">
        <div class="panel-heading"><div><p class="eyebrow">INFRAESTRUCTURA</p><h2>Disponibilidad</h2></div><AdminIcon name="lockers" :size="21" /></div>
        <template v-if="casilleros !== null && totalCasilleros > 0">
          <div class="inventory-number"><strong>{{ disponibles }} / {{ totalCasilleros }}</strong><span>Casilleros disponibles</span></div>
          <div class="inventory-track" role="meter" :aria-valuenow="disponibles" aria-valuemin="0" :aria-valuemax="totalCasilleros" aria-label="Casilleros disponibles"><div class="inventory-fill" :style="{ width: (disponibles / totalCasilleros * 100) + '%' }"></div></div>
        </template>
        <p v-else-if="casilleros === null">No se pudo consultar la disponibilidad.</p>
        <p v-else>No hay casilleros registrados.</p>
        <RouterLink to="/admin/casilleros" class="text-link">Ver todos los casilleros <AdminIcon name="chevron" :size="17" /></RouterLink>
      </section>
    </div>
  </div>
</template>
