<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref } from "vue";
import CapturaFacialEncomienda from "./CapturaFacialEncomienda.vue";
import SynButton from "./SynButton.vue";
import { encomiendasApi } from "../../services/encomiendasApi.js";
import { fechaHonduras } from "../../services/formatos.js";
import type { CapturaV3 } from "../../services/facialEncomiendaV3";
const props = defineProps<{ config: any }>();
const emit = defineEmits<{ actualizada: [any] }>();
const referencia = ref<any>(null),
  consentido = ref(false),
  revocar = ref(false),
  captura = ref<CapturaV3 | null>(null),
  contrasena = ref(""),
  procesando = ref(false),
  error = ref(""),
  aviso = ref("");
async function cargar() {
  try {
    referencia.value = await encomiendasApi.referencia();
    emit("actualizada", referencia.value);
  } catch (e) {
    error.value = (e as Error).message;
  }
}
async function guardar() {
  if (procesando.value) return;
  error.value = "";
  aviso.value = "";
  if (!consentido.value || !captura.value || !contrasena.value) {
    error.value =
      "Acepta el consentimiento, prepara la captura y confirma tu contraseña actual.";
    return;
  }
  procesando.value = true;
  try {
    await encomiendasApi.registrarReferencia(
      captura.value,
      contrasena.value,
      props.config.versionConsentimiento,
    );
    contrasena.value = "";
    consentido.value = false;
    aviso.value =
      "Referencia registrada por el servidor. Revalídala antes de su vencimiento anual.";
    await cargar();
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    procesando.value = false;
    contrasena.value = "";
  }
}
async function eliminar() {
  if (!revocar.value || procesando.value) return;
  procesando.value = true;
  error.value = "";
  try {
    await encomiendasApi.revocarReferencia();
    aviso.value = "Consentimiento revocado y plantilla activa eliminada.";
    revocar.value = false;
    await cargar();
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    procesando.value = false;
  }
}
onMounted(cargar);
onBeforeUnmount(() => {
  contrasena.value = "";
  captura.value?.descriptor.fill(0);
});
function cerrarRegistro(evento: Event) {
  if (!(evento.target as HTMLDetailsElement).open) {
    consentido.value = false;
    contrasena.value = "";
  }
}
</script>
<template>
  <section class="env-card env-reference" aria-label="Identidad del remitente">
    <div class="env-card-heading">
      <div>
        <h2>Identidad del remitente</h2>
        <p>Tu referencia está vinculada únicamente a tu cuenta.</p>
      </div>
      <span class="env-status">{{
        referencia?.vigente
          ? "Vigente"
          : referencia?.registrada
            ? "Revalidación pendiente"
            : referencia
              ? "Sin referencia"
              : error
                ? "No disponible"
                : "Consultando…"
      }}</span>
    </div>
    <p v-if="referencia?.revalidarUtc">
      Revalidar antes del {{ fechaHonduras(referencia.revalidarUtc) }}.
    </p>
    <p v-if="error" class="alerta" role="alert">{{ error }}</p>
    <p v-if="aviso" class="mensaje-exito" role="status">{{ aviso }}</p>
    <details @toggle="cerrarRegistro">
      <summary>
        {{
          referencia?.registrada
            ? "Revalidar mi referencia"
            : "Registrar mi referencia facial"
        }}
      </summary>
      <form class="formulario" @submit.prevent="guardar">
        <p class="env-help">
          Se guardará un descriptor facial cifrado durante un año para verificar
          tus depósitos. No guardamos la fotografía del remitente. Puedes
          retirar este consentimiento y eliminar la plantilla. Es una
          verificación de prototipo sin prueba de vida certificada.
        </p>
        <label class="env-check"
          ><input v-model="consentido" type="checkbox" :disabled="procesando" />
          Acepto el tratamiento de mi referencia facial para verificar depósitos
          y su revalidación anual.</label
        >
        <CapturaFacialEncomienda
          :consentido="consentido"
          :disabled="procesando"
          @capturada="captura = $event"
        />
        <label class="campo"
          ><span>Confirma tu contraseña actual</span
          ><input
            v-model="contrasena"
            type="password"
            autocomplete="current-password"
            maxlength="128"
            :disabled="procesando"
        /></label>
        <SynButton
          tipo="submit"
          :cargando="procesando"
          :disabled="!consentido || !captura || !contrasena"
          >Guardar referencia</SynButton
        >
      </form>
    </details>
    <details v-if="referencia?.registrada">
      <summary>Retirar el consentimiento biométrico</summary>
      <label class="env-check"
        ><input v-model="revocar" type="checkbox" /> Quiero eliminar mi
        plantilla activa. Deberé registrarla nuevamente para futuros
        depósitos.</label
      ><button
        type="button"
        class="boton boton-secundario"
        :disabled="!revocar || procesando"
        @click="eliminar"
      >
        Eliminar referencia y revocar
      </button>
    </details>
  </section>
</template>
