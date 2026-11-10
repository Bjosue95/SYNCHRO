<script setup>
import { ref, onBeforeUnmount } from "vue";
import { RouterLink } from "vue-router";
import IconoSynchro from "../components/IconoSynchro.vue";
import TabletLayout from "../components/TabletLayout.vue";
import { solicitar, esDemo } from "../services/tabletApi";
const codigo = ref(""),
  referencia = ref(""),
  credencial = ref(""),
  pin = ref(""),
  ocupado = ref(false),
  error = ref(""),
  finalizado = ref(false);
const controlador = new AbortController();
async function retirar() {
  if (ocupado.value) return;
  error.value = "";
  ocupado.value = true;
  try {
    if (
      !/^[A-Za-z0-9-]{1,10}$/.test(codigo.value) ||
      !/^[0-9a-f-]{36}$/i.test(referencia.value) ||
      !/^[A-Fa-f0-9]{64}$/.test(credencial.value) ||
      !/^\d{6}$/.test(pin.value)
    )
      throw new Error(
        "Revisa el casillero, la referencia, la credencial y el PIN de seis dígitos.",
      );
    const r = await solicitar(
      `/api/terminales/servicios/${encodeURIComponent(codigo.value)}/reservas/${referencia.value}/retirar`,
      {
        metodo: "POST",
        datos: { pin: pin.value },
        encabezados: { "X-SYNCHRO-SERVICIO": credencial.value },
        csrf: "/api/terminales/servicios/csrf-token",
        signal: controlador.signal,
      },
    );
    if (!r.autorizado)
      throw new Error("No se pudo autorizar el retiro. Revisa tus datos.");
    finalizado.value = true;
    pin.value = "";
    credencial.value = "";
  } catch (e) {
    if (e.name !== "AbortError") error.value = e.message;
  } finally {
    ocupado.value = false;
  }
}
function demo() {
  if (esDemo) {
    codigo.value = "P01";
    referencia.value = "00000000-0000-4000-8000-000000000001";
    credencial.value = "B".repeat(64);
    pin.value = "654321";
  }
}
onBeforeUnmount(() => {
  controlador.abort();
  pin.value = "";
  credencial.value = "";
});
</script>
<template>
  <TabletLayout
    titulo="Recupera tus objetos"
    descripcion="Retira tus pertenencias, tu dispositivo o el contenido de un compartimento térmico."
    ><p v-if="error" role="alert" class="tablet-error">{{ error }}</p>
    <div v-if="!finalizado" class="tablet-two">
      <section class="tablet-panel">
        <h2>Datos de tu reserva</h2>
        <form class="tablet-form" @submit.prevent="retirar">
          <label
            >Código del compartimento<input
              v-model.trim="codigo"
              class="tablet-input"
              placeholder="Ej. P01"
              autocomplete="off"
              required /></label
          ><label
            >Referencia de la reserva<input
              v-model.trim="referencia"
              class="tablet-input"
              autocomplete="off"
              required /></label
          ><label
            >Credencial privada<input
              v-model.trim="credencial"
              type="password"
              class="tablet-input"
              autocomplete="off"
              maxlength="64"
              required /></label
          ><label
            >PIN de retiro<input
              v-model="pin"
              class="tablet-input"
              type="password"
              inputmode="numeric"
              maxlength="6"
              autocomplete="off"
              required /></label
          ><button class="tablet-button" :disabled="ocupado">
            {{ ocupado ? "Comprobando…" : "Confirmar retiro" }}
          </button>
        </form>
        <button v-if="esDemo" type="button" class="demo-fill" @click="demo">
          Rellenar datos de ejemplo
        </button>
      </section>
      <aside class="tablet-panel">
        <h2>Tus objetos siguen a salvo</h2>
        <p class="tablet-muted">
          Puedes retirarlos aunque el tiempo contratado haya terminado. El PIN
          es personal; varios intentos incorrectos pueden bloquearlo.
        </p>
        <p class="tablet-note" style="margin-top: 22px">
          La apertura física del casillero está simulada en este prototipo.
        </p>
      </aside>
    </div>
    <section v-else class="tablet-panel tablet-receipt" role="status">
      <div class="tablet-receipt-icon">
        <IconoSynchro nombre="30_confirmar" :tamano="36" />
      </div>
      <h2>Retiro registrado</h2>
      <p class="tablet-muted">
        La operación terminó y el compartimento vuelve a estar disponible.
      </p>
      <div class="tablet-actions">
        <RouterLink class="tablet-button" to="/menu"
          >Finalizar y volver al inicio</RouterLink
        >
      </div>
    </section></TabletLayout
  >
</template>
<style scoped>
.demo-fill {
  min-height: 48px;
  border: 0;
  background: none;
  text-decoration: underline;
  color: var(--teal);
  font-size: 13px;
}
</style>
