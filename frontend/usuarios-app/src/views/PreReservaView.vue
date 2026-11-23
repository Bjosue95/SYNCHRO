<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import { apiClientes } from "../services/clientesApi.js";
import {
  TAMANOS_ENCOMIENDA, normalizarTelefono, tamanoValido
} from "../services/validaciones.js";

const destinatario = ref("");
const tamano = ref("Mediano");
const preparando = ref(false);
const error = ref("");
const creada = ref(null);
const consentimiento = ref(null);
const solicitandoConsentimiento = ref(false);
const avisoCopia = ref("");

function fechaLocal(valor) {
  if (!valor || Number.isNaN(Date.parse(valor))) return "No disponible";
  return new Intl.DateTimeFormat("es-HN", {
    dateStyle: "medium", timeStyle: "short", timeZone: "America/Tegucigalpa"
  }).format(new Date(valor));
}

async function crear() {
  if (preparando.value) return;
  error.value = "";
  const telefonoDestinatario = normalizarTelefono(destinatario.value);
  if (!telefonoDestinatario || !tamanoValido(tamano.value)) {
    error.value = "Verifica el teléfono del destinatario y el tamaño elegido.";
    return;
  }
  preparando.value = true;
  try {
    creada.value = await apiClientes.crearPreReserva({
      telefonoDestinatario, tamanoSolicitado: tamano.value
    });
    consentimiento.value = null;
    avisoCopia.value = "";
  } catch (e) {
    error.value = e?.message || "No se pudo preparar la pre-reserva.";
  } finally {
    preparando.value = false;
  }
}

async function copiar(valor) {
  try {
    await navigator.clipboard.writeText(String(valor));
    avisoCopia.value = "Copiado. Guarda la referencia y la clave en un lugar privado.";
  } catch {
    avisoCopia.value = "Selecciona la clave y cópiala manualmente.";
  }
}

async function iniciarConsentimiento() {
  if (!creada.value || solicitandoConsentimiento.value) return;
  solicitandoConsentimiento.value = true;
  error.value = "";
  try {
    consentimiento.value = await apiClientes.iniciarConsentimiento(
      creada.value.id, creada.value.tokenGestion
    );
  } catch (e) {
    error.value = e?.message || "No se pudo iniciar el consentimiento.";
  } finally {
    solicitandoConsentimiento.value = false;
  }
}

const enlaceWhatsAppValido = valor =>
  typeof valor === "string" &&
  /^https:\/\/wa\.me\/[1-9][0-9]{7,14}(\?text=.+)?$/.test(valor);
</script>

<template>
  <section class="contenedor pagina-interna">
    <div class="encabezado-pagina">
      <RouterLink to="/mi-cuenta" class="enlace-volver">← Mi cuenta</RouterLink>
      <span class="eyebrow">Encomiendas · Solicitud web</span>
      <h1>Nueva pre-reserva</h1>
      <p>Registra una solicitud de encomienda. El casillero se asignará solamente mediante un proceso posterior autorizado.</p>
    </div>

    <div class="reserva-grid">
      <div class="formulario-tarjeta">
        <template v-if="!creada">
          <h2>Datos de la encomienda</h2>
          <p class="texto-suave">Indica el teléfono del destinatario y el tamaño aproximado del paquete.</p>
          <form class="formulario" @submit.prevent="crear">
            <label class="campo">
              <span>Teléfono del destinatario (WhatsApp)</span>
              <input v-model="destinatario" type="tel" required maxlength="16"
                placeholder="+504XXXXXXXX" autocomplete="off" inputmode="tel" />
              <small>El destinatario deberá autorizar las comunicaciones por WhatsApp.</small>
            </label>
            <fieldset class="grupo-tamanos">
              <legend>Tamaño solicitado</legend>
              <label v-for="opcion in TAMANOS_ENCOMIENDA" :key="opcion.valor"
                class="opcion-tamano" :class="{ elegido: tamano === opcion.valor }">
                <input v-model="tamano" type="radio" name="tamano" :value="opcion.valor" />
                <span class="letra-tamano">{{ opcion.nombre }}</span>
                <small>{{ opcion.detalle }}</small>
              </label>
            </fieldset>
            <p v-if="error" class="alerta" role="alert">{{ error }}</p>
            <button class="boton boton-primario boton-ancho" type="submit"
              :disabled="preparando" :aria-busy="preparando">
              {{ preparando ? "Creando solicitud..." : "Crear pre-reserva" }}
              <span aria-hidden="true">→</span>
            </button>
          </form>
        </template>

        <template v-else>
          <div class="confirmacion-icono" aria-hidden="true">✓</div>
          <span class="eyebrow">Solicitud registrada</span>
          <h2>Pre-reserva creada</h2>
          <p class="texto-suave">Tu solicitud quedó registrada, pero aún no tienes un casillero asignado.</p>
          <div class="resumen-reserva">
            <div><span>Referencia</span><strong>#{{ creada.id }}</strong></div>
            <div><span>Estado</span><strong>{{ creada.estado }}</strong></div>
            <div><span>Válida hasta</span><strong>{{ fechaLocal(creada.fechaExpiracion) }}</strong></div>
          </div>
          <label class="campo">
            <span>Clave privada de consulta</span>
            <textarea class="clave-privada" :value="creada.tokenGestion" readonly
              rows="2" aria-label="Clave privada de consulta"></textarea>
            <small>Se entrega una sola vez. No la compartas públicamente ni la pierdas.</small>
          </label>
          <button type="button" class="boton boton-secundario boton-ancho"
            @click="copiar(creada.tokenGestion)">Copiar clave privada</button>
          <p v-if="avisoCopia" class="mensaje-exito" role="status">{{ avisoCopia }}</p>

          <div class="separador-suave"></div>
          <h3>Consentimiento del destinatario</h3>
          <p class="texto-suave">La solicitud de consentimiento es un paso aparte. No se envía automáticamente ningún WhatsApp desde esta pantalla.</p>
          <p v-if="error" class="alerta" role="alert">{{ error }}</p>
          <button type="button" class="boton boton-primario boton-ancho"
            :disabled="solicitandoConsentimiento || !!consentimiento"
            @click="iniciarConsentimiento">
            {{ solicitandoConsentimiento ? "Preparando..." : consentimiento
              ? "Solicitud de consentimiento preparada" : "Preparar consentimiento WhatsApp" }}
          </button>
          <template v-if="consentimiento">
            <p class="mensaje-exito" role="status">
              Solicitud preparada para el teléfono terminado en
              {{ consentimiento.destinatarioUltimos4 }}.
              Aún debe confirmarla el destinatario.
            </p>
            <label class="campo">
              <span>Mensaje para compartir con el destinatario</span>
              <textarea :value="consentimiento.mensajeParaCompartirConDestinatario"
                rows="4" readonly></textarea>
            </label>
            <button type="button" class="boton boton-secundario boton-ancho"
              @click="copiar(consentimiento.mensajeParaCompartirConDestinatario)">
              Copiar mensaje
            </button>
            <a v-if="enlaceWhatsAppValido(consentimiento.enlaceParaDestinatario)"
              :href="consentimiento.enlaceParaDestinatario" target="_blank"
              rel="noopener noreferrer" class="enlace-texto enlace-destacado">
              Abrir enlace de WhatsApp preparado por SYNCHRO ↗
            </a>
            <small class="texto-suave">El mensaje debe ser enviado por el destinatario desde su propio número al WhatsApp de SYNCHRO.</small>
          </template>
          <RouterLink to="/consultar" class="enlace-texto enlace-destacado">
            Consultar una solicitud →
          </RouterLink>
        </template>
      </div>

      <aside class="panel-pasos">
        <span class="eyebrow">Cómo funciona</span>
        <h2>Antes de utilizar un casillero</h2>
        <ol class="pasos-lista">
          <li><b>1</b><div><strong>Crear la solicitud</strong><span>Se registra una pre-reserva de encomienda a tu cuenta.</span></div></li>
          <li><b>2</b><div><strong>Consentimiento</strong><span>El destinatario autoriza las comunicaciones por WhatsApp.</span></div></li>
          <li><b>3</b><div><strong>Verificación</strong><span>El depósito requiere las comprobaciones de identidad correspondientes.</span></div></li>
        </ol>
        <div class="nota-seguridad"><strong>Importante</strong><p>La pre-reserva vence a las 24 horas. No constituye un depósito ni asigna un casillero.</p></div>
      </aside>
    </div>
  </section>
</template>
