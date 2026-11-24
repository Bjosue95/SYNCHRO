<script setup lang="ts">
import { onMounted, ref } from "vue";
import { RouterLink, useRouter } from "vue-router";
import { encomiendasApi } from "../services/encomiendasApi.js";
import {
  TAMANOS_ENCOMIENDA,
  normalizarTelefono,
} from "../services/validaciones.js";
import ReferenciaFacialEncomienda from "../components/ui/ReferenciaFacialEncomienda.vue";
import SynButton from "../components/ui/SynButton.vue";
const router = useRouter(),
  paso = ref(0),
  config = ref<any>(null),
  facial = ref<any>(null),
  telefono = ref(""),
  tamano = ref("Mediano"),
  enviando = ref(false),
  error = ref("");
const pasos = ["Tu identidad", "Destinatario", "Revisar y crear"];
onMounted(async () => {
  try {
    [config.value, facial.value] = await Promise.all([
      encomiendasApi.configuracion(),
      encomiendasApi.referencia(),
    ]);
  } catch (e) {
    error.value = (e as Error).message;
  }
});
function siguiente() {
  error.value = "";
  if (paso.value === 0 && !facial.value?.vigente) {
    error.value = "Registra una referencia facial vigente antes de continuar.";
    return;
  }
  if (paso.value === 1 && !normalizarTelefono(telefono.value)) {
    error.value =
      "Escribe el WhatsApp del destinatario con código de país, por ejemplo +504XXXXXXXX.";
    return;
  }
  paso.value++;
}
async function crear() {
  if (enviando.value) return;
  enviando.value = true;
  error.value = "";
  try {
    const d = await encomiendasApi.crear({
      telefonoDestinatario: normalizarTelefono(telefono.value),
      tamanoSolicitado: tamano.value,
    });
    await router.replace("/encomiendas/" + d.id);
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    enviando.value = false;
  }
}
</script>
<template>
  <section class="env-page">
    <RouterLink to="/mi-cuenta" class="enlace-volver"
      >Volver a mis encomiendas</RouterLink
    >
    <header class="env-heading">
      <div>
        <p class="env-kicker">PREPARACIÓN DEL ENVÍO</p>
        <h1>Nueva encomienda</h1>
        <p>
          Identifica a los participantes y prepara las autorizaciones para tu
          depósito.
        </p>
      </div>
    </header>
    <ol class="env-steps" aria-label="Pasos de creación">
      <li
        v-for="(p, i) in pasos"
        :key="p"
        :aria-current="i === paso ? 'step' : undefined"
      >
        <span>{{ i + 1 }}</span
        >{{ p }}
      </li>
    </ol>
    <p v-if="error" class="alerta" role="alert">{{ error }}</p>
    <ReferenciaFacialEncomienda
      v-if="paso === 0 && config"
      :config="config"
      @actualizada="facial = $event"
    />
    <form
      v-if="paso === 1"
      class="env-card formulario"
      @submit.prevent="siguiente"
    >
      <h2>¿Quién recibe la encomienda?</h2>
      <label class="campo"
        ><span>WhatsApp del destinatario</span
        ><input
          v-model="telefono"
          type="tel"
          inputmode="tel"
          maxlength="16"
          autocomplete="off"
          placeholder="+504XXXXXXXX"
          required
        /><small
          >Debe ser distinto al del remitente. Su aceptación se comprobará por
          separado.</small
        ></label
      >
      <fieldset class="grupo-tamanos">
        <legend>Tamaño de la encomienda</legend>
        <label
          v-for="t in TAMANOS_ENCOMIENDA"
          :key="t.valor"
          class="opcion-tamano"
          :class="{ elegido: tamano === t.valor }"
          ><input
            v-model="tamano"
            type="radio"
            :value="t.valor"
            name="tamano"
          /><strong>{{ t.nombre }}</strong
          ><small>{{ t.detalle }}</small></label
        >
      </fieldset>
      <button type="submit" class="boton boton-primario">
        Revisar encomienda
      </button>
    </form>
    <section v-if="paso === 2" class="env-card">
      <h2>Revisa los datos</h2>
      <dl class="env-facts">
        <div>
          <dt>Destinatario</dt>
          <dd>{{ normalizarTelefono(telefono) }}</dd>
        </div>
        <div>
          <dt>Tamaño</dt>
          <dd>{{ tamano }}</dd>
        </div>
        <div>
          <dt>Pago</dt>
          <dd>Lo realiza el remitente. Aún no está confirmado.</dd>
        </div>
      </dl>
      <p class="env-help">
        Al crear la encomienda se preparan dos solicitudes de consentimiento. El
        administrador comprobará los mensajes y el pago antes de emitir el PIN
        de depósito. No se enviarán mensajes automáticamente.
      </p>
      <SynButton tipo="button" :cargando="enviando" @click="crear"
        >Crear encomienda</SynButton
      >
    </section>
    <div class="env-actions">
      <button
        v-if="paso > 0"
        type="button"
        class="boton boton-secundario"
        :disabled="enviando"
        @click="paso--"
      >
        Anterior</button
      ><button
        v-if="paso === 0"
        type="button"
        class="boton boton-primario"
        :disabled="!facial?.vigente"
        @click="siguiente"
      >
        Continuar con el destinatario
      </button>
    </div>
  </section>
</template>
