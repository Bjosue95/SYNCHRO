<script setup>
import { ref, onUnmounted } from "vue";
import { RouterLink } from "vue-router";
import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import { useTema } from "../composables/useTema";
import { solicitarEnvioVisitante } from "../services/encomiendasTerminalApi";

const { oscuro, cambiarTema } = useTema();
const ayuda = ref(null);
const remitente = ref("");
const destinatario = ref("");
const tamano = ref("Mediano");
const enviando = ref(false);
const resultado = ref(null);
const error = ref("");

function limpiar() {
  remitente.value = "";
  destinatario.value = "";
  tamano.value = "Mediano";
  resultado.value = null;
  error.value = "";
}
onUnmounted(limpiar);

async function iniciarSolicitud() {
  if (enviando.value || resultado.value) return;
  error.value = "";
  const regexTelefono = /^\+?[1-9][0-9]{7,14}$/;
  if (!regexTelefono.test(remitente.value.trim()) ||
      !regexTelefono.test(destinatario.value.trim()) ||
      remitente.value.trim() === destinatario.value.trim()) {
    error.value = "Introduce dos teléfonos distintos con formato internacional (solo números y + opcional).";
    return;
  }
  enviando.value = true;
  try {
    resultado.value = await solicitarEnvioVisitante({
      telefonoRemitente: remitente.value.trim(),
      telefonoDestinatario: destinatario.value.trim(),
      tamanoSolicitado: tamano.value,
    });
    remitente.value = "";
    destinatario.value = "";
  } catch (e) {
    error.value = e.message || "No se pudo registrar la solicitud.";
  } finally {
    enviando.value = false;
  }
}
</script>

<template>
  <main class="pantalla" :class="{ 'tema-oscuro': oscuro }">
    <AppHeader :oscuro="oscuro" @cambiar-tema="cambiarTema" @abrir-ayuda="ayuda?.abrir()" />
    <RouterLink class="volver" to="/guardar">← Volver a Guardar</RouterLink>
    <section class="panel">
      <span class="etiqueta">SYNCHRO · TERMINAL</span>
      <h1>Enviar encomienda sin reserva</h1>
      <p>Prepara una solicitud de prueba desde la terminal. El destinatario deberá aceptar las notificaciones por WhatsApp antes de que se habilite el depósito.</p>
      <p class="aviso">WhatsApp todavía no está conectado. Este formulario no envía mensajes ni asigna casilleros. Usa únicamente teléfonos ficticios de prueba.</p>
      <form v-if="!resultado" class="formulario" @submit.prevent="iniciarSolicitud">
        <label for="telefono-remitente">Teléfono del remitente</label>
        <input id="telefono-remitente" v-model.trim="remitente" type="tel" inputmode="tel" autocomplete="off" placeholder="+50400000001" maxlength="20" required />
        <label for="telefono-destinatario">Teléfono del destinatario</label>
        <input id="telefono-destinatario" v-model.trim="destinatario" type="tel" inputmode="tel" autocomplete="off" placeholder="+50400000002" maxlength="20" required />
        <label for="tamano">Tamaño solicitado</label>
        <select id="tamano" v-model="tamano">
          <option value="Pequeno">Pequeño</option>
          <option value="Mediano">Mediano</option>
          <option value="Grande">Grande</option>
        </select>
        <button type="submit" :disabled="enviando">{{ enviando ? 'Registrando...' : 'Guardar solicitud preliminar' }}</button>
      </form>
      <div v-else class="resultado" role="status">
        <h2>Solicitud n.º {{ resultado.solicitudId }}</h2>
        <p>Estado: <strong>{{ resultado.estado }}</strong></p>
        <p class="aviso">{{ resultado.mensaje }}</p>
        <p><strong>Depósito bloqueado:</strong> sin consentimiento real, no se puede colocar la encomienda en un casillero.</p>
        <button type="button" class="secundario" @click="limpiar">Nueva solicitud de prueba</button>
      </div>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
    </section>
    <HelpDialog ref="ayuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
.pantalla{min-height:100dvh;display:flex;flex-direction:column;gap:16px;padding:20px 26px;background:#f2f7fa;color:#01305c;transition:background-color 150ms,color 150ms}
.pantalla.tema-oscuro{background:#061f36;color:white}
.volver{align-self:flex-start;display:inline-flex;align-items:center;min-height:52px;padding:10px 20px;border-radius:14px;background:#e4f2f6;color:#01305c;text-decoration:none;font-weight:600}.tema-oscuro .volver{background:#204766;color:white}
.panel{width:min(100%,760px);margin:12px auto;padding:28px;border:1px solid #dfeaf0;border-radius:22px;background:white;color:#01305c;display:flex;flex-direction:column;gap:16px}.tema-oscuro .panel{background:#103956;color:white;border-color:#315573}
.etiqueta{color:#009ca9;font-size:12px;letter-spacing:1px;font-weight:800}h1{font-size:clamp(26px,3vw,34px);margin:0}h2{margin:0}.formulario,.resultado{display:flex;flex-direction:column;gap:12px}.formulario label{font-weight:600}
input,select{width:100%;min-height:56px;padding:12px 14px;border:1px solid #779aaa;border-radius:12px;font:inherit;background:white;color:#01305c}
button{min-height:56px;padding:12px;border:0;border-radius:14px;background:#00b9c7;color:#01305c;font:inherit;font-weight:700;cursor:pointer}button:disabled{opacity:.5;cursor:default}.secundario{background:#e4f2f6}.aviso{border-left:4px solid #00b9c7;padding:14px;background:#e4f2f6;color:#01305c}.error{color:#a92b2b;font-weight:600}
:focus-visible{outline:3px solid #00b9c7;outline-offset:3px}@media(prefers-reduced-motion:reduce){.pantalla{transition:none}}
</style>
