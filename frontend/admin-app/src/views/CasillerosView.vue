<script setup>
import { computed, onMounted, ref } from "vue";
import { apiAdmin } from "../services/adminApi";
import AdminIcon from "../components/AdminIcon.vue";
import SynButton from "../components/ui/SynButton.vue";

const casilleros = ref([]);
const cargando = ref(true);
const error = ref("");
const espaciosCarga = ref([]);
const cargandoCarga = ref(true);
const errorCarga = ref("");
const buscar = ref("");
const filtro = ref("Todos");
const estados = ["Todos", "Disponible", "Reservado", "Ocupado"];
const filtrados = computed(() => casilleros.value.filter(item => {
  const texto = `${item.codigo} ${item.tamano} ${item.tipo}`.toLocaleLowerCase("es-HN");
  return (filtro.value === "Todos" || item.estado === filtro.value) && texto.includes(buscar.value.trim().toLocaleLowerCase("es-HN"));
}));
const filtradosCarga = computed(() => espaciosCarga.value.filter(item => {
  const texto = `${item.codigo} ${item.modalidad} ${item.dispositivo}`.toLocaleLowerCase("es-HN");
  return (filtro.value === "Todos" || item.estado === filtro.value) &&
    texto.includes(buscar.value.trim().toLocaleLowerCase("es-HN"));
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
async function cargarEspaciosCarga() {
  cargandoCarga.value = true;
  errorCarga.value = "";
  try {
    const datos = await apiAdmin("/api/admin/carga/espacios");
    if (!Array.isArray(datos?.espacios)) {
      throw new Error("El backend no devolvió los espacios de carga.");
    }
    espaciosCarga.value = datos.espacios;
  } catch (e) {
    espaciosCarga.value = [];
    errorCarga.value = e?.message || "No se pudieron consultar los espacios de carga.";
  } finally {
    cargandoCarga.value = false;
  }
}
async function actualizarTodo() {
  await Promise.all([cargar(), cargarEspaciosCarga()]);
}
onMounted(actualizarTodo);
</script>
<template>
  <div>
    <div class="page-heading spread-heading">
      <div><p class="eyebrow">INFRAESTRUCTURA</p><h1>Casilleros</h1><p>Consulta el estado actual de cada módulo. Información de solo lectura.</p></div>
      <SynButton variante="secundario" :cargando="cargando || cargandoCarga" @click="actualizarTodo"><AdminIcon name="refresh" :size="17" />{{ cargando || cargandoCarga ? 'Actualizando...' : 'Actualizar' }}</SynButton>
    </div>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <section class="panel lockers-panel">
      <h2>Casilleros de resguardo</h2>
      <p>Personales, encomiendas y temperatura controlada.</p>
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

    <section class="panel lockers-panel lockers-carga-panel">
      <div class="lockers-carga-titulo">
        <div>
          <h2>Espacios de carga de dispositivos</h2>
          <p>Mismo control de disponibilidad: W1–W2 inalámbrico, C1–C4 por cable y L1–L2 para laptop.</p>
        </div>
        <strong class="lockers-carga-total">{{ espaciosCarga.length }} espacios</strong>
      </div>

      <p v-if="errorCarga" class="inline-error" role="alert">{{ errorCarga }}</p>
      <div class="list-meta"><strong>{{ filtradosCarga.length }}</strong> de {{ espaciosCarga.length }} espacios de carga</div>

      <div v-if="cargandoCarga" class="empty-state" role="status">
        Consultando espacios de carga...
      </div>
      <div v-else-if="!espaciosCarga.length && !errorCarga" class="empty-state">
        No hay espacios de carga registrados en SQL.
      </div>
      <div v-else-if="espaciosCarga.length && !filtradosCarga.length" class="empty-state">
        No encontramos espacios de carga con estos filtros.
      </div>
      <div v-else class="locker-list">
        <div v-for="item in filtradosCarga" :key="item.codigo" class="locker-row">
          <div class="locker-identity">
            <span class="locker-avatar"><AdminIcon name="lockers" :size="19" /></span>
            <span>
              <strong>{{ item.codigo }}</strong>
              <small>
                {{ item.modalidad === 'Inalambrica' ? 'Carga inalámbrica' :
                   item.modalidad === 'Cable' ? 'Carga por cable' : 'Carga de laptop' }}
                · {{ item.dispositivo }}
                <template v-if="item.sesionesActivas > 0">
                  · {{ item.sesionesActivas }} sesión activa
                </template>
              </small>
            </span>
          </div>
          <span class="state-chip" :class="'state-' + String(item.estado).toLowerCase()">
            {{ item.estado }}
          </span>
        </div>
      </div>
    </section>
  </div>
</template>
<style scoped>
.lockers-carga-panel { margin-top: 22px; }
.lockers-carga-titulo {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 15px;
}
.lockers-carga-titulo h2 { margin: 0 0 6px; font-size: 21px; }
.lockers-carga-titulo p { margin: 0; color: #627987; line-height: 1.45; }
.lockers-carga-total { color: #01305c; white-space: nowrap; }
@media (max-width: 680px) {
  .lockers-carga-titulo { align-items: flex-start; flex-direction: column; }
}
</style>
