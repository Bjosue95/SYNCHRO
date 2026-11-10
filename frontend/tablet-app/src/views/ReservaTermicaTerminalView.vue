<script setup>
import { computed, ref, onBeforeUnmount } from "vue";
import { useRoute, RouterLink } from "vue-router";
import TabletLayout from "../components/TabletLayout.vue";
import IconoSynchro from "../components/IconoSynchro.vue";
import { solicitar, esDemo } from "../services/tabletApi";
const route = useRoute();
const frio = computed(() => route.params.tipo === "frio");
const codigo = ref(""),
  referencia = ref(""),
  credencial = ref(""),
  reserva = ref(null),
  ocupado = ref(false),
  error = ref(""),
  confirmado = ref(false),
  finalizado = ref(false);
const controlador = new AbortController();
async function ejecutar(fn) {
  if (ocupado.value) return;
  ocupado.value = true;
  error.value = "";
  try {
    await fn();
  } catch (e) {
    if (e.name !== "AbortError") error.value = e.message;
  } finally {
    ocupado.value = false;
  }
}
function encabezados() {
  return { "X-SYNCHRO-SERVICIO": credencial.value.trim() };
}
function base() {
  return `/api/servicios/${encodeURIComponent(codigo.value.trim())}/reservas/${referencia.value.trim()}`;
}
async function consultar() {
  await ejecutar(async () => {
    if (
      !/^[A-Za-z0-9-]{1,10}$/.test(codigo.value.trim()) ||
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
        referencia.value.trim(),
      ) ||
      !/^[A-Fa-f0-9]{64}$/.test(credencial.value.trim())
    )
      throw new Error(
        "Revisa el compartimento, la referencia y la credencial de tu reserva web.",
      );
    const r = await solicitar(base(), {
      encabezados: encabezados(),
      signal: controlador.signal,
    });
    if (r.clase !== (frio.value ? "Frio" : "Calor"))
      throw new Error("La reserva corresponde a otro tipo de servicio.");
    if (r.estado === "Reservada")
      throw new Error(
        "Completa el pago de esta reserva desde tu cuenta web antes de utilizar el compartimento.",
      );
    if (!["Pagada", "Activa", "Agotada"].includes(r.estado))
      throw new Error("Esta reserva ya no está disponible para utilizarse.");
    reserva.value = r;
    if (["Activa", "Agotada"].includes(r.estado)) {
      finalizado.value = true;
      credencial.value = "";
    }
  });
}
async function iniciar() {
  await ejecutar(async () => {
    if (!confirmado.value)
      throw new Error(
        "Confirma que guardaste tus objetos y cerraste la puerta.",
      );
    await solicitar(
      `/api/terminales/servicios/${encodeURIComponent(codigo.value.trim())}/reservas/${referencia.value.trim()}/iniciar`,
      {
        metodo: "POST",
        datos: { equipoCerrado: true },
        encabezados: encabezados(),
        csrf: "/api/terminales/servicios/csrf-token",
        signal: controlador.signal,
      },
    );
    finalizado.value = true;
    credencial.value = "";
  });
}
function ejemplo() {
  if (esDemo) {
    codigo.value = frio.value ? "F01" : "H01";
    referencia.value = frio.value
      ? "00000000-0000-4000-8000-000000000002"
      : "00000000-0000-4000-8000-000000000003";
    credencial.value = "B".repeat(64);
  }
}
onBeforeUnmount(() => {
  controlador.abort();
  credencial.value = "";
});
</script>
<template>
  <TabletLayout
    :titulo="
      frio ? 'Utiliza tu reserva de frío' : 'Utiliza tu reserva de calor'
    "
    descripcion="Solo necesitas los datos de una reserva ya creada y pagada desde tu cuenta web."
    volver="/terminal/reserva-web"
  >
    <p v-if="error" role="alert" class="tablet-error">{{ error }}</p>
    <div v-if="!reserva" class="tablet-two">
      <section class="tablet-panel">
        <h2>Consulta tu reserva web</h2>
        <form class="tablet-form" @submit.prevent="consultar">
          <label
            >Compartimento reservado<input
              v-model.trim="codigo"
              class="tablet-input"
              :placeholder="frio ? 'Ej. F01' : 'Ej. H01'"
              autocomplete="off"
              required /></label
          ><label
            >Referencia de tu reserva web<input
              v-model.trim="referencia"
              class="tablet-input"
              autocomplete="off"
              required /></label
          ><label
            >Credencial privada<input
              v-model.trim="credencial"
              class="tablet-input"
              type="password"
              autocomplete="off"
              maxlength="64"
              required /></label
          ><button class="tablet-button" :disabled="ocupado">
            {{ ocupado ? "Consultando…" : "Consultar reserva"
            }}<IconoSynchro nombre="29_continuar" tema="claro" />
          </button>
        </form>
        <button v-if="esDemo" type="button" class="demo-fill" @click="ejemplo">
          Usar reserva web de ejemplo
        </button>
      </section>
      <aside class="tablet-panel">
        <h2>Exclusivo para clientes con reserva</h2>
        <ul class="tablet-checks">
          <li>La reserva y su pago se realizan en la web.</li>
          <li>En esta terminal solo utilizas una reserva existente.</li>
          <li>Conserva el PIN que recibiste al confirmar el pago.</li>
        </ul>
        <p class="tablet-note" style="margin-top: 22px">
          La temperatura y la apertura física están simuladas en el prototipo.
        </p>
      </aside>
    </div>
    <section v-else-if="!finalizado" class="tablet-panel tablet-receipt">
      <span class="tablet-badge">Reserva web comprobada</span>
      <h2 style="margin-top: 20px">
        Tu compartimento {{ reserva.codigo }} está reservado
      </h2>
      <p class="tablet-muted">
        El pago de {{ reserva.minutos }} minutos ya está confirmado. No se cobra
        ni se crea otra reserva aquí.
      </p>
      <label class="confirmar"
        ><input v-model="confirmado" type="checkbox" />Guardé mis objetos y
        cerré la puerta.</label
      ><button
        class="tablet-button"
        :disabled="ocupado || !confirmado"
        @click="iniciar"
      >
        {{ ocupado ? "Confirmando…" : "Comenzar uso de mi reserva" }}
      </button>
    </section>
    <section v-else class="tablet-panel tablet-receipt" role="status">
      <div class="tablet-receipt-icon">
        <IconoSynchro nombre="30_confirmar" :tamano="36" />
      </div>
      <h2>Tu reserva está en uso</h2>
      <p class="tablet-muted">
        Compartimento {{ reserva.codigo }}. Para retirar tus objetos, utiliza
        los datos de esta reserva y su PIN.
      </p>
      <p class="tablet-note" style="margin-top: 22px">
        La temperatura y apertura física son simuladas. No se creó una reserva
        desde la terminal.
      </p>
      <div class="tablet-actions">
        <RouterLink to="/menu" class="tablet-button">Finalizar</RouterLink>
      </div>
    </section>
  </TabletLayout>
</template>
<style scoped>
.demo-fill {
  min-height: 48px;
  border: 0;
  background: none;
  color: var(--teal);
  text-decoration: underline;
  font-size: 13px;
}
.confirmar {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  margin: 24px 0;
  font-size: 14px;
}
.confirmar input {
  width: 24px;
  height: 24px;
  accent-color: #007c89;
}
</style>
