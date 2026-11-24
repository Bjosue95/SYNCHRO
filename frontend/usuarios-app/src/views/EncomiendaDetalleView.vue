<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref } from "vue";
import { RouterLink, useRoute } from "vue-router";
import { encomiendasApi } from "../services/encomiendasApi.js";
import { fechaHonduras, nombreEstadoReserva } from "../services/formatos.js";
import CapturaFacialEncomienda from "../components/ui/CapturaFacialEncomienda.vue";
import SynButton from "../components/ui/SynButton.vue";
import type { CapturaV3 } from "../services/facialEncomiendaV3";
const route = useRoute(),
  detalle = ref<any>(null),
  config = ref<any>(null),
  error = ref(""),
  aviso = ref(""),
  cargando = ref(false),
  procesando = ref(false),
  consentido = ref(false),
  pin = ref(""),
  captura = ref<CapturaV3 | null>(null);
let reloj: ReturnType<typeof setInterval> | undefined,
  montada = true;
const solicitud = computed(() =>
  typeof route.query.solicitud === "string" &&
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
    route.query.solicitud,
  )
    ? route.query.solicitud
    : "",
);
const pinEntregado = computed(() =>
  detalle.value?.pines.some(
    (p: any) => p.finalidad === "Deposito" && p.estado === "Entregado",
  ),
);
const puedePreparar = computed(
  () =>
    detalle.value &&
    ["Preparacion", "ListaParaDeposito"].includes(detalle.value.estado) &&
    detalle.value.condiciones.consentimientosConfirmados &&
    detalle.value.condiciones.pagoConfirmado &&
    detalle.value.facial.vigente &&
    pinEntregado.value,
);
async function cargar() {
  if (cargando.value) return;
  cargando.value = true;
  try {
    const [d, c] = await Promise.all([
      encomiendasApi.detalle(route.params.id),
      encomiendasApi.configuracion(),
    ]);
    if (montada) {
      detalle.value = d;
      config.value = c;
      error.value = "";
    }
  } catch (e) {
    if (montada) error.value = (e as Error).message;
  } finally {
    cargando.value = false;
  }
}
function enlaceValido(valor: unknown): valor is string {
  return (
    typeof valor === "string" &&
    /^https:\/\/wa\.me\/[1-9][0-9]{7,14}\?text=/.test(valor)
  );
}
async function copiarEnlace() {
  try {
    await navigator.clipboard.writeText(
      detalle.value.destinatario.enlaceWhatsApp,
    );
    aviso.value =
      "Enlace copiado. Compártelo con el destinatario para que envíe su aceptación al WhatsApp oficial.";
  } catch {
    error.value =
      "No se pudo copiar. Puedes seleccionar el enlace del destinatario y compartirlo manualmente.";
  }
}
async function autorizar() {
  if (procesando.value) return;
  error.value = "";
  if (
    !solicitud.value ||
    !consentido.value ||
    !captura.value ||
    !/^\d{6}$/.test(pin.value)
  ) {
    error.value =
      "Comprueba la solicitud de estación, el consentimiento, la captura y los seis dígitos del PIN.";
    return;
  }
  procesando.value = true;
  try {
    const d = await encomiendasApi.autorizarDeposito(
      detalle.value.id,
      solicitud.value,
      pin.value,
      captura.value,
      config.value.versionConsentimiento,
    );
    if (d.autorizada === true) {
      aviso.value =
        "Identidad y PIN verificados por el servidor. Completa el depósito en la estación antes de " +
        fechaHonduras(d.venceUtc) +
        ". Esta autorización no abre el casillero desde la web.";
      consentido.value = false;
      await cargar();
    }
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    procesando.value = false;
    pin.value = "";
  }
}
onMounted(async () => {
  await cargar();
  reloj = setInterval(() => {
    if (!document.hidden && !procesando.value) void cargar();
  }, 15000);
});
onBeforeUnmount(() => {
  montada = false;
  clearInterval(reloj);
  pin.value = "";
  captura.value?.descriptor.fill(0);
});
</script>
<template>
  <section class="env-page">
    <RouterLink to="/mi-cuenta" class="enlace-volver"
      >Mis encomiendas</RouterLink
    >
    <p v-if="error" class="alerta" role="alert">{{ error }}</p>
    <p v-if="aviso" class="mensaje-exito" role="status">{{ aviso }}</p>
    <p v-if="!detalle && cargando" role="status">Consultando la encomienda…</p>
    <template v-if="detalle">
      <header class="env-heading">
        <div>
          <p class="env-kicker">SEGUIMIENTO DEL ENVÍO</p>
          <h1>Encomienda #{{ detalle.id }}</h1>
          <p>
            {{ detalle.tamano }} ·
            <span class="env-status">{{
              nombreEstadoReserva(detalle.estado)
            }}</span>
          </p>
        </div>
        <button
          type="button"
          class="boton boton-secundario"
          :disabled="cargando"
          @click="cargar"
        >
          Actualizar estado
        </button>
      </header>
      <div class="env-two-columns">
        <section
          v-for="rol in ['remitente', 'destinatario']"
          :key="rol"
          class="env-card"
        >
          <div class="env-card-heading">
            <h2>{{ rol === "remitente" ? "Remitente" : "Destinatario" }}</h2>
            <span class="env-status">{{
              nombreEstadoReserva(detalle[rol].consentimiento)
            }}</span>
          </div>
          <p class="env-phone">{{ detalle[rol].telefono }}</p>
          <p>
            Autorización de mensajes:
            {{ nombreEstadoReserva(detalle[rol].consentimiento) }}.
          </p>
          <p v-if="rol === 'destinatario'">
            Aceptación de la encomienda:
            {{
              detalle.destinatario.aceptaEncomienda
                ? "Comprobada por el administrador"
                : "Pendiente de comprobar"
            }}.
          </p>
          <template v-if="detalle[rol].consentimiento === 'Pendiente'"
            ><p class="env-help">
              {{
                rol === "remitente"
                  ? "Envía tu autorización desde tu número de WhatsApp."
                  : "Comparte este enlace para que el destinatario envíe su aceptación desde su propio número."
              }}
              Abrir el enlace no confirma el consentimiento.
            </p>
            <a
              v-if="enlaceValido(detalle[rol].enlaceWhatsApp)"
              :href="detalle[rol].enlaceWhatsApp"
              class="boton boton-secundario"
              target="_blank"
              rel="noopener noreferrer"
              >{{
                rol === "remitente"
                  ? "Autorizar por WhatsApp"
                  : "Enlace de aceptación del destinatario"
              }}</a
            ><button
              v-if="
                rol === 'destinatario' &&
                enlaceValido(detalle[rol].enlaceWhatsApp)
              "
              type="button"
              class="boton boton-secundario"
              @click="copiarEnlace"
            >
              Copiar enlace para compartir
            </button>
            <p v-if="!detalle[rol].enlaceWhatsApp" class="env-help">
              El número oficial todavía no está configurado en el servidor.
            </p></template
          >
          <p v-if="detalle[rol].verificadoUtc" class="env-help">
            Comprobado el {{ fechaHonduras(detalle[rol].verificadoUtc) }}.
          </p>
        </section>
      </div>
      <section class="env-card">
        <div class="env-card-heading">
          <h2>Pago del remitente</h2>
          <span class="env-status">{{
            nombreEstadoReserva(detalle.pago.estado)
          }}</span>
        </div>
        <dl class="env-facts">
          <div>
            <dt>Importe</dt>
            <dd>
              {{
                detalle.pago.importeHnl == null
                  ? "Pendiente de importe comprobado"
                  : "L " + Number(detalle.pago.importeHnl).toFixed(2)
              }}
            </dd>
          </div>
          <div>
            <dt>Método</dt>
            <dd>{{ detalle.pago.metodo || "Pendiente" }}</dd>
          </div>
          <div>
            <dt>Confirmación</dt>
            <dd>
              {{
                detalle.pago.fechaUtc
                  ? fechaHonduras(detalle.pago.fechaUtc)
                  : "Pendiente de verificación manual"
              }}
            </dd>
          </div>
        </dl>
        <p class="env-help">
          El administrador registra el pago efectivamente comprobado. No hay
          pasarela conectada. El destinatario no paga al retirar una encomienda
          ya pagada.
        </p>
      </section>
      <section class="env-card">
        <h2>Preparar el depósito</h2>
        <ol class="env-checklist">
          <li>
            {{
              detalle.condiciones.consentimientosConfirmados
                ? "Consentimientos de ambos participantes comprobados."
                : "Espera la comprobación manual de los dos consentimientos."
            }}
          </li>
          <li>
            {{
              detalle.condiciones.pagoConfirmado
                ? "Pago manual confirmado."
                : "El pago del remitente está pendiente de confirmación."
            }}
          </li>
          <li>
            {{
              detalle.facial.vigente
                ? "Referencia facial vigente."
                : "Registra o revalida tu referencia facial desde Mi espacio."
            }}
          </li>
          <li>
            {{
              pinEntregado
                ? "PIN de depósito entregado al número autorizado."
                : "Espera la entrega manual de tu PIN de depósito."
            }}
          </li>
        </ol>
        <p class="env-help">
          La conexión de la tablet se revisará en una etapa posterior. La web
          solo autoriza una solicitud válida de estación; no permite aperturas
          remotas.
        </p>
        <p v-if="puedePreparar && !solicitud">
          Cuando la estación esté conectada, abre su enlace de solicitud para
          verificar tu rostro y utilizar el PIN de depósito.
        </p>
        <form
          v-if="puedePreparar && solicitud && config"
          class="formulario"
          @submit.prevent="autorizar"
        >
          <h3>Verificar mi depósito</h3>
          <label class="env-check"
            ><input
              v-model="consentido"
              type="checkbox"
              :disabled="procesando"
            />
            Acepto utilizar mi captura para compararla con la referencia facial
            de mi cuenta y autorizar este depósito.</label
          ><CapturaFacialEncomienda
            :consentido="consentido"
            :disabled="procesando"
            @capturada="captura = $event"
          /><label class="campo"
            ><span>PIN exclusivo de depósito</span
            ><input
              v-model="pin"
              type="password"
              inputmode="numeric"
              autocomplete="off"
              maxlength="6"
              pattern="[0-9]{6}"
              :disabled="procesando" /></label
          ><SynButton
            tipo="submit"
            :cargando="procesando"
            :disabled="!consentido || !captura || pin.length !== 6"
            >Verificar identidad y PIN</SynButton
          >
        </form>
      </section>
      <section class="env-card">
        <h2>Depósito y retiro</h2>
        <p v-if="detalle.estado === 'Depositada'">
          La encomienda está depositada. El administrador puede emitir y
          entregar el PIN de retiro al destinatario autorizado.
        </p>
        <p v-else-if="detalle.estado === 'Retirada'">
          La entrega está registrada y el PIN de retiro fue consumido.
        </p>
        <p v-else>
          El depósito y la entrega aparecerán aquí cuando sean confirmados por
          el servidor.
        </p>
        <ul>
          <li v-for="p in detalle.pines" :key="p.id">
            PIN de {{ p.finalidad.toLowerCase() }}:
            {{ nombreEstadoReserva(p.estado) }} · Vigencia hasta
            {{ fechaHonduras(p.venceUtc) }}.
          </li>
        </ul>
        <p class="env-help">
          Los valores de los PIN no se muestran en este historial. La fotografía
          del destinatario constituye evidencia de retiro, sin afirmar una
          coincidencia biométrica.
        </p>
      </section>
      <section class="env-card">
        <h2>Historial de operaciones</h2>
        <ol class="env-timeline">
          <li v-for="h in detalle.historial" :key="h.id">
            <time>{{ fechaHonduras(h.fechaUtc) }}</time>
            <div>
              <strong>{{ nombreEstadoReserva(h.accion) }}</strong>
              <p>{{ h.detalle }}</p>
              <small>{{ h.actor }} · {{ nombreEstadoReserva(h.estado) }}</small>
            </div>
          </li>
        </ol>
        <p class="env-help">Últimas 300 operaciones de esta encomienda.</p>
      </section>
    </template>
  </section>
</template>
