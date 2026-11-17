<script setup>
import { onMounted, ref } from "vue";
import { apiAdmin } from "../services/adminApi";
import AdminIcon from "../components/AdminIcon.vue";

const cobros = ref([]);
const cargando = ref(false);
const procesando = ref("");
const error = ref("");
const aviso = ref("");
const metodos = ref({});
const comprobados = ref({});

function dinero(valor) {
  const numero = Number(valor);
  if (!Number.isFinite(numero)) return "—";
  return new Intl.NumberFormat("es-HN", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  }).format(numero);
}

function fechaLocal(valor) {
  if (!valor) return "—";
  const fecha = new Date(valor);
  return Number.isNaN(fecha.getTime())
    ? "—"
    : fecha.toLocaleString("es-HN");
}

async function cargar() {
  if (procesando.value) return;

  cargando.value = true;
  error.value = "";

  try {
    const datos = await apiAdmin(
      "/api/admin/whatsapp-manual/cobros/retiro/pendientes"
    );

    if (!Array.isArray(datos?.cobros)) {
      throw new Error("El backend no devolvió la lista de cobros.");
    }

    cobros.value = datos.cobros;
  } catch (e) {
    cobros.value = [];
    error.value = e?.message || "No se pudieron consultar los cobros.";
  } finally {
    cargando.value = false;
  }
}

async function confirmar(item) {
  const id = item?.cobroId;
  const metodo = metodos.value[id];

  if (!id || procesando.value || cargando.value) return;

  if (metodo !== "Efectivo" && metodo !== "Tarjeta") {
    error.value = "Selecciona Efectivo o Tarjeta.";
    return;
  }

  if (comprobados.value[id] !== true) {
    error.value = "Primero debes comprobar el pago simulado.";
    return;
  }

  const aceptar = window.confirm(
    `¿Confirmas que comprobaste el pago simulado de L ${dinero(item.importeHnl)} ` +
    `para el casillero ${item.codigoCasillero} mediante ${metodo}?`
  );

  if (!aceptar) return;

  error.value = "";
  aviso.value = "";
  procesando.value = id;

  try {
    const csrf = await apiAdmin("/api/admin/csrf-token");

    if (!csrf?.csrfToken) {
      throw new Error("No se obtuvo el token de seguridad.");
    }

    const respuesta = await apiAdmin(
      "/api/admin/whatsapp-manual/cobros/retiro/confirmar",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken
        },
        body: JSON.stringify({
          cobroId: id,
          metodoPago: metodo,
          pagoComprobado: true
        })
      }
    );

    if (respuesta?.pagoConfirmado !== true ||
        respuesta?.retiroConfirmado !== false) {
      throw new Error(
        "El servidor no confirmó el estado esperado del cobro."
      );
    }

    delete comprobados.value[id];
    delete metodos.value[id];

    aviso.value =
      `Pago simulado confirmado para ${item.codigoCasillero}. ` +
      "El casillero continúa ocupado hasta finalizar el retiro en la tablet.";

    const datos = await apiAdmin(
      "/api/admin/whatsapp-manual/cobros/retiro/pendientes"
    );

    if (!Array.isArray(datos?.cobros)) {
      throw new Error("No se pudo actualizar el listado.");
    }

    cobros.value = datos.cobros;
  } catch (e) {
    error.value = e?.message || "No se pudo confirmar el cobro.";
  } finally {
    procesando.value = "";
  }
}

onMounted(cargar);
</script>

<template>
  <div class="cobros-view">
    <div class="page-heading spread-heading">
      <div>
        <p class="eyebrow">OPERACIONES · SYNCHRO</p>
        <h1>Cobros pendientes</h1>
        <p>
          Comprueba los pagos simulados de resguardo personal.
          El retiro se completa desde la tablet.
        </p>
      </div>

      <button
        type="button"
        class="button-secondary"
        :disabled="cargando || !!procesando"
        @click="cargar"
      >
        <AdminIcon name="refresh" :size="17" />
        {{ cargando ? "Actualizando..." : "Actualizar cobros" }}
      </button>
    </div>

    <p v-if="error" class="inline-error" role="alert">
      {{ error }}
    </p>

    <p v-if="aviso" class="cobros-aviso" role="status">
      {{ aviso }}
    </p>

    <section class="panel cobros-panel">
      <div class="cobros-encabezado">
        <div>
          <h2>Órdenes por confirmar</h2>
          <p>Solamente operaciones pendientes del backend.</p>
        </div>
        <strong class="cobros-total">{{ cobros.length }}</strong>
      </div>

      <div v-if="cargando" class="empty-state" role="status">
        Consultando cobros pendientes...
      </div>

      <div v-else-if="cobros.length === 0" class="empty-state">
        No hay cobros pendientes de confirmación.
      </div>

      <article
        v-for="item in cobros"
        :key="item.cobroId"
        class="cobro-tarjeta"
      >
        <div class="cobro-cabecera">
          <div>
            <span class="cobro-etiqueta">CASILLERO</span>
            <h3>{{ item.codigoCasillero }}</h3>
          </div>
          <span class="cobro-estado">Pendiente</span>
        </div>

        <div class="cobro-datos">
          <div>
            <small>Importe por comprobar</small>
            <strong class="cobro-importe">
              L {{ dinero(item.importeHnl) }}
            </strong>
          </div>
          <div>
            <small>Tiempo utilizado</small>
            <strong>{{ item.minutosUtilizados }} min</strong>
          </div>
          <div>
            <small>Orden creada</small>
            <strong>{{ fechaLocal(item.creadoUtc) }}</strong>
          </div>
        </div>

        <p class="cobro-tarifa">
          {{ item.descripcionTarifa }}
        </p>

        <div class="cobro-confirmacion">
          <label :for="'metodo-' + item.cobroId">
            Método de pago comprobado
          </label>

          <select
            :id="'metodo-' + item.cobroId"
            v-model="metodos[item.cobroId]"
            :disabled="!!procesando"
          >
            <option value="">Seleccionar método</option>
            <option value="Efectivo">Efectivo (simulado)</option>
            <option value="Tarjeta">Tarjeta (simulado)</option>
          </select>

          <label class="cobro-check">
            <input
              v-model="comprobados[item.cobroId]"
              type="checkbox"
              :disabled="!!procesando"
            />
            <span>
              Confirmo que he comprobado el pago simulado
              correspondiente a esta orden.
            </span>
          </label>

          <button
            type="button"
            class="cobro-boton"
            :disabled="
              !!procesando ||
              cargando ||
              !metodos[item.cobroId] ||
              comprobados[item.cobroId] !== true
            "
            @click="confirmar(item)"
          >
            <AdminIcon name="check" :size="18" />
            {{
              procesando === item.cobroId
                ? "Confirmando..."
                : "Confirmar pago simulado"
            }}
          </button>
        </div>
      </article>
    </section>
  </div>
</template>

<style scoped>
.cobros-view {
  color: #01305c;
}

.cobros-panel {
  padding: clamp(18px, 2.5vw, 28px);
}

.cobros-encabezado,
.cobro-cabecera {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.cobros-encabezado {
  padding-bottom: 20px;
  border-bottom: 1px solid #dce8ee;
}

.cobros-encabezado h2 {
  margin: 0 0 6px;
  font-size: 20px;
}

.cobros-encabezado p {
  margin: 0;
  color: #627987;
}

.cobros-total {
  padding: 10px 16px;
  background: #e4f7fa;
  color: #01305c;
  border-radius: 12px;
  font-size: 24px;
}

.cobro-tarjeta {
  margin-top: 18px;
  padding: clamp(18px, 2vw, 26px);
  background: #fff;
  border: 1px solid #dce8ee;
  border-radius: 16px;
  box-shadow: 0 5px 18px rgba(1,48,92,.045);
}

.cobro-etiqueta {
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 1.4px;
  color: #667f8e;
}

.cobro-cabecera h3 {
  margin: 5px 0 0;
  font-size: 25px;
}

.cobro-estado {
  padding: 8px 12px;
  border-radius: 100px;
  background: #fff3dd;
  color: #87590d;
  font-size: 12px;
  font-weight: 800;
}

.cobro-datos {
  display: grid;
  grid-template-columns: repeat(3,minmax(0,1fr));
  gap: 16px;
  padding: 22px 0;
}

.cobro-datos > div {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
}

.cobro-datos small {
  color: #647b89;
  font-size: 12px;
}

.cobro-datos strong {
  font-size: 15px;
  overflow-wrap: anywhere;
}

.cobro-datos .cobro-importe {
  font-size: 26px;
  color: #008b9e;
  font-variant-numeric: tabular-nums;
}

.cobro-tarifa {
  padding: 12px 14px;
  background: #f2f7fa;
  border-radius: 10px;
  color: #526b7a;
  font-size: 14px;
}

.cobro-confirmacion {
  display: grid;
  gap: 13px;
  margin-top: 22px;
}

.cobro-confirmacion > label:first-child {
  font-weight: 700;
}

.cobro-confirmacion select {
  width: 100%;
  min-height: 46px;
  padding: 11px 13px;
  border: 1px solid #b5cbd3;
  border-radius: 10px;
  background: #fff;
  color: #01305c;
  font: inherit;
}

.cobro-check {
  display: flex;
  align-items: flex-start;
  gap: 11px;
  padding: 15px;
  border-radius: 11px;
  background: #f2f7fa;
  font-size: 14px;
  line-height: 1.5;
  cursor: pointer;
}

.cobro-check input {
  width: 18px;
  height: 18px;
  margin-top: 2px;
  flex-shrink: 0;
  accent-color: #008b9e;
}

.cobro-boton {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  gap: 9px;
  min-height: 47px;
  padding: 12px 18px;
  border: 0;
  border-radius: 11px;
  background: #00b9c7;
  color: #01305c;
  font: inherit;
  font-weight: 800;
  cursor: pointer;
}

.cobro-boton:disabled {
  opacity: .5;
  cursor: not-allowed;
}

.cobro-boton:focus-visible,
.cobro-confirmacion select:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

.cobros-aviso {
  padding: 14px 16px;
  border-radius: 11px;
  background: #e2f8ee;
  color: #12673a;
}

@media (max-width: 680px) {
  .cobros-encabezado {
    align-items: flex-start;
  }

  .cobro-datos {
    grid-template-columns: minmax(0,1fr);
  }

  .cobro-boton {
    width: 100%;
  }
}
</style>