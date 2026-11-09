<script setup>
import { ref, onUnmounted } from "vue";
import { RouterLink } from "vue-router";
import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import { useTema } from "../composables/useTema";
import { consultarReservaDesdeTerminal } from "../services/encomiendasTerminalApi";

const { oscuro, cambiarTema } = useTema();
const ayuda = ref(null);
const referencia = ref("");
const tokenGestion = ref("");
const consultando = ref(false);
const reserva = ref(null);
const error = ref("");

function limpiar() {
  referencia.value = "";
  tokenGestion.value = "";
  error.value = "";
  reserva.value = null;
}
onUnmounted(limpiar);

async function consultar() {
  if (consultando.value) return;
  error.value = "";
  reserva.value = null;
  const id = Number(referencia.value);
  if (!Number.isSafeInteger(id) || id <= 0 || !/^[0-9a-fA-F]{64}$/.test(tokenGestion.value.trim())) {
    error.value = "Introduce una referencia válida y la credencial privada de tu pre-reserva.";
    return;
  }
  consultando.value = true;
  try {
    reserva.value = await consultarReservaDesdeTerminal(id, tokenGestion.value.trim());
    tokenGestion.value = ""; // No conservar la credencial después de consultar.
  } catch (e) {
    error.value = e.message || "No se pudo consultar la reserva.";
  } finally {
    consultando.value = false;
  }
}
</script>

<template>
  <main class="pantalla" :class="{ 'tema-oscuro': oscuro }">
    <AppHeader :oscuro="oscuro" @cambiar-tema="cambiarTema" @abrir-ayuda="ayuda?.abrir()" />
    <RouterLink class="volver" to="/menu">← Volver al menú</RouterLink>
    <section class="panel">
      <span class="etiqueta">SYNCHRO · TERMINAL</span>
      <h1>Tengo una reserva</h1>
      <p>Consulta una reserva creada previamente en la web del cliente. No necesitamos volver a pedir los datos del destinatario.</p>
      <form v-if="!reserva" class="formulario" @submit.prevent="consultar">
        <label for="referencia">Referencia de la pre-reserva</label>
        <input id="referencia" v-model.trim="referencia" type="number" min="1" inputmode="numeric" autocomplete="off" required />
        <label for="credencial">Credencial privada de la pre-reserva</label>
        <input id="credencial" v-model.trim="tokenGestion" type="password" minlength="64" maxlength="64" autocomplete="off" required />
        <p class="nota">Prueba temporal con referencia y credencial. La identificación facial del remitente aún no está integrada.</p>
        <button type="submit" :disabled="consultando">{{ consultando ? 'Consultando...' : 'Consultar reserva' }}</button>
      </form>
      <div v-if="reserva" class="resultado" role="status">
        <h2>Referencia {{ reserva.referencia }}</h2>
        <p>Tamaño solicitado: <strong>{{ reserva.tamanoSolicitado }}</strong></p>
        <p>Estado: <strong>{{ reserva.estado }}</strong></p>
        <p>Vence: <strong>{{ new Date(reserva.fechaExpiracion).toLocaleString() }}</strong></p>
        <p class="aviso">{{ reserva.mensaje }}</p>
        <p><strong>Depósito no disponible todavía.</strong> No se abrirá ni ocupará ningún casillero hasta completar WhatsApp y las verificaciones.</p>
        <button type="button" class="secundario" @click="limpiar">Consultar otra reserva</button>
      </div>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
    </section>
    <HelpDialog ref="ayuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
.pantalla{min-height:100dvh;display:flex;flex-direction:column;gap:16px;padding:20px 26px;background:#f2f7fa;color:#01305c;transition:background-color 150ms,color 150ms}
.pantalla.tema-oscuro{background:#061f36;color:white}
.volver{align-self:flex-start;display:inline-flex;align-items:center;min-height:52px;padding:10px 20px;border-radius:14px;background:#e4f2f6;color:#01305c;text-decoration:none;font-weight:600}
.tema-oscuro .volver{background:#204766;color:white}
.panel{width:min(100%,760px);margin:12px auto;padding:28px;border:1px solid #dfeaf0;border-radius:22px;background:white;color:#01305c;display:flex;flex-direction:column;gap:16px}
.tema-oscuro .panel{background:#103956;color:white;border-color:#315573}
.etiqueta{color:#009ca9;font-size:12px;letter-spacing:1px;font-weight:800}
h1{font-size:clamp(26px,3vw,34px);margin:0}h2{margin:0}.formulario,.resultado{display:flex;flex-direction:column;gap:12px}.formulario label{font-weight:600}
input{min-height:56px;padding:12px 14px;border:1px solid #779aaa;border-radius:12px;font:inherit;background:white;color:#01305c}
button{min-height:56px;padding:12px;border:0;border-radius:14px;background:#00b9c7;color:#01305c;font:inherit;font-weight:700;cursor:pointer}button:disabled{opacity:.5;cursor:default}.secundario{background:#e4f2f6}
.nota{font-size:14px;color:#5b6770}.tema-oscuro .nota{color:#c6dfea}.aviso{border-left:4px solid #00b9c7;padding:14px;background:#e4f2f6;color:#01305c}.error{color:#a92b2b;font-weight:600}
:focus-visible{outline:3px solid #00b9c7;outline-offset:3px}@media(prefers-reduced-motion:reduce){.pantalla{transition:none}}
</style>
