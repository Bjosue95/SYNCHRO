<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import AppIcon from "../components/ui/AppIcon.vue";
import { fechaHonduras, nombreEstadoReserva } from "../services/formatos.js";
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
  <section class="pagina-interna">
    <div class="encabezado-pagina">
      <RouterLink to="/mi-cuenta" class="enlace-texto enlace-volver"><AppIcon nombre="volver"/> Mi cuenta</RouterLink>
      <h1>Nueva pre-reserva</h1>
      <p>Solicita una pre-reserva de encomienda. La asignación del casillero requiere verificaciones posteriores.</p>
    </div>

    <div class="reserva-grid">
      <div class="formulario-tarjeta" :class="{ 'detalle-reserva': creada }">
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
                class="opcion-tamano" :data-tamano="opcion.valor" :class="{ elegido: tamano === opcion.valor }">
                <input v-model="tamano" type="radio" name="tamano" :value="opcion.valor" />
                <span class="puerta-tamano" aria-hidden="true"></span>
                <span class="letra-tamano">{{ opcion.nombre }}</span>
                <small>{{ opcion.detalle }}</small>
              </label>
            </fieldset>
            <p v-if="error" class="alerta" role="alert">{{ error }}</p>
            <button class="boton boton-primario boton-ancho" type="submit"
              :disabled="preparando" :aria-busy="preparando">
              {{ preparando ? "Creando solicitud..." : "Crear pre-reserva" }}
              <AppIcon nombre="flecha"/>
            </button>
          </form>
        </template>

        <template v-else>
          <h2 class="titulo-confirmacion"><AppIcon nombre="check"/> Pre-reserva creada</h2>
          <p class="texto-suave">Tu solicitud quedó registrada, pero aún no tienes un casillero asignado.</p>
          <div class="resumen-reserva">
            <div class="fila-referencia"><span>Referencia</span><strong class="referencia-reserva">#{{ creada.id }}</strong></div>
            <div><span>Estado</span><strong>{{ nombreEstadoReserva(creada.estado) }}</strong></div>
            <div><span>Válida hasta</span><strong>{{ fechaHonduras(creada.fechaExpiracion) }}</strong></div>
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

          <h3 class="separador-suave">Consentimiento del destinatario</h3>
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
            <button v-if="enlaceWhatsAppValido(consentimiento.enlaceParaDestinatario)"
              type="button" class="boton boton-secundario boton-ancho"
              @click="copiar(consentimiento.enlaceParaDestinatario)">
              Copiar enlace para el destinatario
            </button>
            <small class="texto-suave">El mensaje debe ser enviado por el destinatario desde su propio número al WhatsApp de SYNCHRO.</small>
          </template>
          <RouterLink to="/consultar" class="enlace-texto enlace-destacado">
            Consultar una solicitud <AppIcon nombre="flecha"/>
          </RouterLink>
        </template>
      </div>

      <aside class="panel-pasos">
        <h2>El recorrido de tu encomienda</h2>
        <ol class="pasos-lista">
          <li><b aria-hidden="true">01</b><div><strong>Crear la solicitud</strong><span>Se registra una pre-reserva de encomienda a tu cuenta.</span></div></li>
          <li><b aria-hidden="true">02</b><div><strong>Consentimiento</strong><span>El destinatario autoriza las comunicaciones por WhatsApp.</span></div></li>
          <li><b aria-hidden="true">03</b><div><strong>Verificación</strong><span>El depósito requiere las comprobaciones de identidad correspondientes.</span></div></li>
        </ol>
        <div class="nota-seguridad"><strong>Importante</strong><p>La pre-reserva vence a las 24 horas. No constituye un depósito ni asigna un casillero.</p></div>
      </aside>
    </div>
  </section>
</template>
