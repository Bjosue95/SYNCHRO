<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { apiAdmin } from '../services/adminApi';
import AdminIcon from '../components/AdminIcon.vue';

type Movimiento = {
  id: string;
  codigo: string;
  servicio: 'Resguardo' | 'Encomienda' | 'Carga';
  estado: string;
  inicioUtc: string;
  finUtc: string | null;
  detalle: string;
  usoId: number | null;
  encomiendaId: number | null;
  sesionId: string | null;
  duracionMinutos: number | null;
};

const cargando = ref(false);
const error = ref('');
const actualizado = ref('');
const movimientos = ref<Movimiento[]>([]);
const servicio = ref('Todos');
const codigo = ref('');
const buscar = ref('');
const pagina = ref(1);
const total = ref(0);
const totalPaginas = ref(1);
const hayRegistros = computed(() => total.value > 0);

function fecha(valor: string | null) {
  if (!valor) return 'En curso';
  const f = new Date(valor);
  if (Number.isNaN(f.getTime())) return 'Sin fecha';
  return new Intl.DateTimeFormat('es-HN', {
    timeZone: 'America/Tegucigalpa', day: '2-digit', month: '2-digit',
    year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: true
  }).format(f);
}
function duracion(min: number | null) {
  if (min === null) return 'En curso';
  if (min < 60) return min + ' min';
  return Math.floor(min / 60) + ' h ' + (min % 60) + ' min';
}
function estadoVisible(estado: string) {
  return ({
    Ocupado: 'En uso', Activa: 'En uso', Retirada: 'Finalizado',
    Finalizado: 'Finalizado', Cancelado: 'Cancelado',
    Cancelada: 'Cancelado', Reservado: 'Reservado'
  } as Record<string, string>)[estado] ?? estado;
}
function servicioVisible(tipo: string) {
  return tipo === 'Carga' ? 'Carga de dispositivos' :
    tipo === 'Resguardo' ? 'Resguardo personal' : 'Encomienda';
}
function referencia(item: Movimiento) {
  if (item.encomiendaId) return 'Encomienda #' + item.encomiendaId;
  if (item.usoId) return 'Uso #' + item.usoId;
  return item.sesionId ? 'Sesión ' + item.sesionId.slice(0, 8) : '—';
}
async function cargar() {
  if (cargando.value) return;
  cargando.value = true;
  error.value = '';
  try {
    const params = new URLSearchParams({
      pagina: String(pagina.value), tamanoPagina: '20',
      servicio: servicio.value
    });
    if (buscar.value) params.set('codigo', buscar.value);
    const d = await apiAdmin('/api/admin/movimientos?' + params);
    if (!Array.isArray(d?.movimientos) || typeof d?.total !== 'number')
      throw new Error('El servidor no devolvió el historial esperado.');
    movimientos.value = d.movimientos;
    total.value = d.total;
    totalPaginas.value = Math.max(1, d.totalPaginas || 1);
    actualizado.value = new Date().toLocaleTimeString('es-HN', {
      hour: '2-digit', minute: '2-digit'
    });
  } catch (e) {
    movimientos.value = [];
    total.value = 0;
    totalPaginas.value = 1;
    actualizado.value = '';
    error.value = (e as Error).message || 'No fue posible consultar el historial.';
  } finally {
    cargando.value = false;
  }
}
function aplicarFiltros() {
  pagina.value = 1;
  buscar.value = codigo.value.trim();
  void cargar();
}
function cambiarPagina(nueva: number) {
  if (nueva < 1 || nueva > totalPaginas.value || cargando.value) return;
  pagina.value = nueva;
  void cargar();
}
onMounted(() => { void cargar(); });
</script>

<template>
  <div>
    <header class="page-heading spread-heading">
      <div>
        <p class="eyebrow">REGISTRO DE OPERACIONES</p>
        <h1>Movimientos</h1>
        <p>Historial de uso de casilleros y espacios de carga de SYNCHRO.</p>
      </div>
      <button type="button" class="button-secondary"
        :disabled="cargando" @click="cargar">
        <AdminIcon name="refresh" :size="17" />
        {{ cargando ? 'Actualizando...' : 'Actualizar' }}
      </button>
    </header>

    <section class="panel movimientos-panel" aria-label="Historial de utilización">
      <form class="movimientos-filtros" @submit.prevent="aplicarFiltros">
        <label>Servicio
          <select v-model="servicio">
            <option value="Todos">Todos los servicios</option>
            <option value="Resguardo">Resguardo personal</option>
            <option value="Encomienda">Encomiendas</option>
            <option value="Carga">Carga de dispositivos</option>
          </select>
        </label>
        <label>Casillero o espacio
          <input v-model="codigo" type="search" maxlength="20"
            placeholder="Ej.: M03, W1, C2, L1" />
        </label>
        <button type="submit" class="mov-filtrar" :disabled="cargando">
          <AdminIcon name="search" :size="17" /> Filtrar
        </button>
      </form>

      <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
      <div class="movimientos-meta" aria-live="polite">
        <strong>{{ error ? 'Movimientos no disponibles' : total + ' movimientos' }}</strong>
        <span v-if="actualizado">Actualizado a las {{ actualizado }}</span>
        <span v-if="hayRegistros">Página {{ pagina }} de {{ totalPaginas }}</span>
      </div>

      <p v-if="cargando && !movimientos.length" class="movimientos-vacio"
        role="status">Consultando movimientos...</p>
      <p v-else-if="!cargando && !hayRegistros && !error"
        class="movimientos-vacio">No hay movimientos registrados con estos filtros.</p>

      <div v-if="movimientos.length" class="movimientos-tabla-contenedor">
        <table class="movimientos-tabla">
          <thead>
            <tr>
              <th>Espacio</th>
              <th>Servicio</th>
              <th>Inicio</th>
              <th>Finalización</th>
              <th>Duración</th>
              <th>Estado</th>
              <th>Referencia</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="m in movimientos" :key="m.id">
              <td><strong>{{ m.codigo }}</strong><small>{{ m.detalle }}</small></td>
              <td>{{ servicioVisible(m.servicio) }}</td>
              <td>{{ fecha(m.inicioUtc) }}</td>
              <td>{{ fecha(m.finUtc) }}</td>
              <td>{{ duracion(m.duracionMinutos) }}</td>
              <td><span class="movimientos-estado" :class="{
                activo: m.estado === 'Ocupado' || m.estado === 'Activa',
                completado: ['Retirada','Finalizado'].includes(m.estado)
              }">{{ estadoVisible(m.estado) }}</span></td>
              <td>
                <RouterLink v-if="m.encomiendaId"
                  :to="{ path:'/admin/encomiendas', query:{ encomienda:m.encomiendaId } }"
                  class="mov-enlace">{{ referencia(m) }}</RouterLink>
                <span v-else>{{ referencia(m) }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <nav v-if="totalPaginas > 1" class="movimientos-paginacion"
        aria-label="Páginas de movimientos">
        <button type="button" :disabled="pagina === 1 || cargando"
          @click="cambiarPagina(pagina - 1)">Anterior</button>
        <span>Página {{ pagina }} de {{ totalPaginas }}</span>
        <button type="button" :disabled="pagina >= totalPaginas || cargando"
          @click="cambiarPagina(pagina + 1)">Siguiente</button>
      </nav>
      <p class="movimientos-aclaracion">
        Se muestran usos iniciados realmente. Las solicitudes todavía sin depósito
        y los PIN pendientes se gestionan en WhatsApp y PIN.
      </p>
    </section>
  </div>
</template>

<style scoped>
.movimientos-panel {
  min-width: 0;
  background: var(--white, #fff);
  color: var(--ink, #01305c);
}
.movimientos-filtros {
  display: flex; align-items: end; flex-wrap: wrap; gap: 14px;
  margin: 0 0 19px;
}
.movimientos-filtros label {
  display: grid; gap: 7px; min-width: 185px; flex: 1;
  font-size: 13px; font-weight: 700;
}
.movimientos-filtros input,.movimientos-filtros select {
  min-height: 44px; padding: 10px 12px; border-radius: 10px;
  border: 1px solid var(--border, #d5e5ec);
  background: var(--white, #fff); color: var(--ink, #01305c);
  font: inherit;
}
.mov-filtrar,.movimientos-paginacion button {
  min-height: 44px; padding: 10px 17px; border-radius: 10px;
  border: 1px solid var(--border, #d5e5ec);
  color: var(--ink,#01305c); background: var(--canvas,#eef5f8);
  display: inline-flex; align-items: center; justify-content: center;
  gap: 7px; font-weight: 700; cursor: pointer;
}
.mov-filtrar:disabled,.movimientos-paginacion button:disabled {
  opacity: .5; cursor: not-allowed;
}
.movimientos-meta {
  display: flex; flex-wrap: wrap; align-items: center;
  gap: 10px 20px; margin: 0 0 15px;
  color: var(--muted, #617887); font-size: 13px;
}
.movimientos-meta strong { color: var(--ink,#01305c); }
.movimientos-tabla-contenedor { overflow-x: auto; max-width: 100%; }
.movimientos-tabla {
  width: 100%; min-width: 920px;
  border-collapse: collapse; font-size: 13px;
  font-variant-numeric: tabular-nums;
}
.movimientos-tabla th {
  text-align: left; font-size: 12px; letter-spacing: .02em;
  color: var(--muted,#617887); font-weight: 800;
  border-bottom: 1px solid var(--border,#d5e5ec);
  padding: 14px 11px; white-space: nowrap;
}
.movimientos-tabla td {
  border-bottom: 1px solid var(--border,#d5e5ec);
  padding: 16px 11px; vertical-align: middle;
}
.movimientos-tabla td:first-child strong { display: block; font-size: 15px; }
.movimientos-tabla small {
  display: block; margin-top: 4px; color: var(--muted,#617887);
  font-size: 11px; white-space: nowrap;
}
.movimientos-estado {
  display: inline-flex; padding: 5px 10px; border-radius: 99px;
  font-weight: 700; background: var(--canvas,#eef5f8);
  white-space: nowrap;
}
.movimientos-estado.activo { background: #fff3dd; color: #805500; }
.movimientos-estado.completado { background: #dcf5e9; color: #116b49; }
.mov-enlace { color: #008b9e; font-weight: 700; }
.movimientos-paginacion {
  display: flex; align-items: center; justify-content: flex-end;
  gap: 14px; padding-top: 18px; font-size: 13px;
}
.movimientos-vacio {
  padding: 28px 14px; text-align: center;
  color: var(--muted,#617887);
}
.movimientos-aclaracion {
  margin: 22px 0 0; font-size: 12px; line-height: 1.5;
  color: var(--muted,#617887);
}
@media(max-width:590px) {
  .movimientos-filtros label { min-width: 100%; }
  .movimientos-filtros .mov-filtrar { width: 100%; }
  .movimientos-paginacion { justify-content: space-between; }
}
</style>
