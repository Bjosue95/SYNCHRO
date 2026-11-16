<script setup>
import { computed, onMounted, ref } from "vue";
import { apiAdmin } from "../services/adminApi";
import AdminIcon from "../components/AdminIcon.vue";

const casilleros = ref([]);
const cargando = ref(true);
const error = ref("");
const buscar = ref("");
const filtro = ref("Todos");
const estados = ["Todos", "Disponible", "Reservado", "Ocupado"];
const filtrados = computed(() => casilleros.value.filter(item => {
  const texto = `${item.codigo} ${item.tamano} ${item.tipo}`.toLocaleLowerCase("es-HN");
  return (filtro.value === "Todos" || item.estado === filtro.value) && texto.includes(buscar.value.trim().toLocaleLowerCase("es-HN"));
}));
async function cargar() {
  cargando.value = true;
  error.value = "";
  try {
    const lista = await apiAdmin("/api/terminales/casilleros-prototipo");
    if (!Array.isArray(lista)) throw new Error("El servidor no devolvió la información esperada.");
    casilleros.value = lista;
  } catch (e) { error.value = e?.message || "No se pudieron cargar los casilleros."; }
  finally { cargando.value = false; }
}
onMounted(cargar);
</script>
<template>
  <div>
    <div class="page-heading spread-heading">
      <div><p class="eyebrow">INFRAESTRUCTURA</p><h1>Casilleros</h1><p>Consulta el estado actual de cada módulo. Información de solo lectura.</p></div>
      <button type="button" class="button-secondary" :disabled="cargando" @click="cargar"><AdminIcon name="refresh" :size="17" />{{ cargando ? 'Actualizando...' : 'Actualizar' }}</button>
    </div>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <section class="panel lockers-panel">
      <div class="lockers-toolbar">
        <label class="search-control"><span class="sr-only">Buscar casillero por código, tamaño o tipo</span><AdminIcon name="search" :size="18" /><input v-model="buscar" type="search" placeholder="Buscar por código, tamaño o tipo" /></label>
        <div class="filter-group" role="group" aria-label="Filtrar casilleros por estado">
          <button v-for="estado in estados" :key="estado" type="button" class="filter-chip" :class="{ selected: filtro === estado }" :aria-pressed="filtro === estado" @click="filtro = estado">{{ estado }}</button>
        </div>
      </div>
      <div class="list-meta"><strong>{{ filtrados.length }}</strong> de {{ casilleros.length }} casilleros</div>
      <div v-if="cargando" class="empty-state" role="status">Consultando casilleros...</div>
      <div v-else-if="!casilleros.length" class="empty-state">No hay casilleros para mostrar.</div>
      <div v-else-if="!filtrados.length" class="empty-state">No encontramos casilleros con esos filtros. Cambia la búsqueda o el estado.</div>
      <div v-else class="locker-list">
        <div v-for="item in filtrados" :key="item.codigo" class="locker-row">
          <div class="locker-identity"><span class="locker-avatar"><AdminIcon name="lockers" :size="19" /></span><span><strong>{{ item.codigo }}</strong><small>{{ item.tamano }} · {{ item.tipo }}</small></span></div>
          <span class="state-chip" :class="'state-' + String(item.estado).toLowerCase()">{{ item.estado }}</span>
        </div>
      </div>
    </section>
  </div>
</template>
