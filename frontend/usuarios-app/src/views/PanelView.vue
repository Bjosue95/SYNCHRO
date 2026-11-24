<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { RouterLink } from "vue-router";
import { encomiendasApi } from "../services/encomiendasApi.js";
import { fechaHonduras, nombreEstadoReserva } from "../services/formatos.js";
import ReferenciaFacialEncomienda from "../components/ui/ReferenciaFacialEncomienda.vue";
const items = ref<any[]>([]),
  config = ref<any>(null),
  cargando = ref(true),
  error = ref(""),
  filtro = ref("Todas");
const visibles = computed(() =>
  items.value.filter(
    (e) => filtro.value === "Todas" || e.estado === filtro.value,
  ),
);
const resumen = computed(() => ({
  preparacion: items.value.filter((e) =>
    ["Preparacion", "ListaParaDeposito"].includes(e.estado),
  ).length,
  depositadas: items.value.filter((e) => e.estado === "Depositada").length,
  entregadas: items.value.filter((e) => e.estado === "Retirada").length,
}));
async function cargar() {
  cargando.value = true;
  error.value = "";
  try {
    const [datos, cfg] = await Promise.all([
      encomiendasApi.listar(),
      encomiendasApi.configuracion(),
    ]);
    items.value = datos.encomiendas;
    config.value = cfg;
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    cargando.value = false;
  }
}
onMounted(cargar);
</script>
<template>
  <section class="env-page">
    <header class="env-heading">
      <div>
        <p class="env-kicker">TU ESPACIO SYNCHRO</p>
        <h1>Mis encomiendas</h1>
        <p>
          Desde la preparación hasta la entrega, con el estado de cada
          operación.
        </p>
      </div>
      <RouterLink to="/reservar/encomienda" class="boton boton-primario"
        >Nueva encomienda</RouterLink
      >
    </header>
    <p v-if="error" class="alerta" role="alert">
      {{ error }}
      <button type="button" class="boton boton-secundario" @click="cargar">
        Reintentar
      </button>
    </p>
    <p v-if="cargando" role="status">Consultando tus encomiendas…</p>
    <template v-if="!cargando && !error">
      <dl class="env-stats">
        <div>
          <dt>En preparación</dt>
          <dd>{{ resumen.preparacion }}</dd>
        </div>
        <div>
          <dt>Depositadas</dt>
          <dd>{{ resumen.depositadas }}</dd>
        </div>
        <div>
          <dt>Entregadas</dt>
          <dd>{{ resumen.entregadas }}</dd>
        </div>
      </dl>
      <section class="env-card">
        <div class="env-card-heading">
          <h2>Seguimiento</h2>
          <label class="env-filter"
            >Estado
            <select v-model="filtro">
              <option>Todas</option>
              <option value="Preparacion">En preparación</option>
              <option value="Depositada">Depositadas</option>
              <option value="Retirada">Entregadas</option>
              <option value="Vencida">Vencidas</option>
              <option value="Rechazada">Rechazadas</option>
            </select></label
          ><button type="button" class="boton boton-secundario" @click="cargar">
            Actualizar
          </button>
        </div>
        <div v-if="visibles.length" class="env-table-wrap" tabindex="0" role="region" aria-label="Tabla de encomiendas, desplazable horizontalmente">
          <table class="env-table">
            <caption class="env-sr-only">
              Encomiendas de tu cuenta
            </caption>
            <thead>
              <tr>
                <th>Encomienda</th>
                <th>Estado</th>
                <th>Pago del remitente</th>
                <th>Última operación</th>
                <th><span class="env-sr-only">Acciones</span></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="e in visibles" :key="e.id">
                <td>
                  <strong>#{{ e.id }}</strong
                  ><small
                    >{{ e.tamano
                    }}<template v-if="e.casillero">
                      · {{ e.casillero }}</template
                    ></small
                  >
                </td>
                <td>
                  <span class="env-status">{{
                    nombreEstadoReserva(e.estado)
                  }}</span>
                </td>
                <td>
                  {{ nombreEstadoReserva(e.pagoEstado)
                  }}<small v-if="e.importeHnl != null"
                    >L {{ Number(e.importeHnl).toFixed(2) }}</small
                  >
                </td>
                <td>
                  {{
                    fechaHonduras(e.entregaUtc || e.depositoUtc || e.creadaUtc)
                  }}
                </td>
                <td>
                  <RouterLink :to="'/encomiendas/' + e.id" class="enlace-texto"
                    >Ver detalle</RouterLink
                  >
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-else class="env-empty">
          {{
            items.length
              ? "No hay encomiendas en este estado."
              : "Todavía no tienes encomiendas operativas. Crea una para comenzar."
          }}
        </p>
        <p class="env-help">
          Se muestran las últimas 200 encomiendas operativas de tu cuenta.
          <RouterLink to="/consultar"
            >Consultar una pre-reserva histórica</RouterLink
          >.
        </p>
      </section>
      <ReferenciaFacialEncomienda v-if="config" :config="config" />
    </template>
  </section>
</template>
