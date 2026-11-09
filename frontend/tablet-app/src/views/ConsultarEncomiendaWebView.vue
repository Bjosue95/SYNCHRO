<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import { useTema } from "../composables/useTema";
import { consultarPreReservaEncomienda, consultarIntegracionesEncomienda } from "../services/encomiendasWebApi";

const { oscuro, cambiarTema } = useTema();
const centroAyuda = ref(null);
const id = ref("");
const token = ref("");
const consultando = ref(false);
const resultado = ref(null);
const integraciones = ref(null);
const error = ref("");

async function consultar() {
  if (consultando.value) return;
  resultado.value = null;
  error.value = "";
  const numero = Number(id.value);
  if (!Number.isSafeInteger(numero) || numero <= 0 || !/^[a-fA-F0-9]{64}$/.test(token.value.trim())) {
    error.value = "Introduce una referencia valida y su token privado de 64 caracteres.";
    return;
  }
  consultando.value = true;
  try {
    const [res, estado] = await Promise.all([
      consultarPreReservaEncomienda(numero, token.value.trim()),
      consultarIntegracionesEncomienda(),
    ]);
    resultado.value = res;
    integraciones.value = estado;
  } catch (e) {
    error.value = e.message || "No se pudo consultar.";
  } finally {
    consultando.value = false;
  }
}
</script>

<template>
  <main class="pantalla" :class="{ 'tema-oscuro': oscuro }">
    <AppHeader :oscuro="oscuro" @cambiar-tema="cambiarTema" @abrir-ayuda="centroAyuda?.abrir()" />
    <RouterLink to="/menu" class="volver">← Volver al menú</RouterLink>
    <section class="tarjeta">
      <p class="etiqueta">SYNCHRO · RESERVAS WEB</p>
      <h1>Consultar pre-reserva</h1>
      <p>Ingresa los datos recibidos al registrar la pre-reserva. No son el PIN de retiro.</p>
      <form @submit.prevent="consultar" class="formulario">
        <label for="referencia">Número de referencia</label>
        <input id="referencia" v-model.trim="id" type="number" inputmode="numeric" min="1" required />
        <label for="token">Token privado de gestión</label>
        <input id="token" v-model.trim="token" type="password" autocomplete="off" minlength="64" maxlength="64" required />
        <button type="submit" :disabled="consultando">{{ consultando ? "Consultando..." : "Consultar estado" }}</button>
      </form>
      <div v-if="resultado" class="resultado" role="status">
        <h2>Referencia {{ resultado.id }}</h2>
        <p>Tamaño solicitado: <strong>{{ resultado.tamanoSolicitado }}</strong></p>
        <p>Estado: <strong>{{ resultado.estado }}</strong></p>
        <p>Vence: {{ new Date(resultado.fechaExpiracion).toLocaleString() }}</p>
        <p class="alerta">{{ integraciones?.mensaje || "No puedes depositar hasta confirmar WhatsApp y los requisitos de seguridad." }}</p>
        <p>Por seguridad, esta pre-reserva no permite abrir ni ocupar un casillero.</p>
      </div>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
      <RouterLink to="/web/encomiendas" class="volver">Crear otra pre-reserva</RouterLink>
    </section>
    <HelpDialog ref="centroAyuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
.pantalla{min-height:100dvh;padding:20px 26px;display:flex;flex-direction:column;gap:18px;background:#f2f7fa;color:#01305c;transition:background-color 150ms,color 150ms}
.pantalla.tema-oscuro{background:#061f36;color:white}
.volver{align-self:flex-start;min-height:48px;display:inline-flex;align-items:center;padding:10px 15px;border-radius:12px;background:#e4f2f6;color:#01305c;text-decoration:none;font-weight:600}
.tarjeta{width:min(100%,720px);margin:12px auto;padding:26px;border:1px solid #dfeaf0;border-radius:20px;background:white;color:#01305c;display:flex;flex-direction:column;gap:15px}
.tema-oscuro .tarjeta{background:#103956;color:white;border-color:#315573}
.etiqueta{font-size:13px;font-weight:700;color:#009ca9;letter-spacing:1px}h1{font-size:clamp(24px,3vw,32px);margin:0}h2{margin:0}.formulario{display:flex;flex-direction:column;gap:10px}.formulario label{font-weight:600}
input{min-height:48px;border:1px solid #779aaa;border-radius:10px;padding:10px 12px;font:inherit;background:white;color:#01305c}
button{min-height:56px;padding:13px;background:#00b9c7;color:#01305c;border:0;border-radius:12px;font:inherit;font-weight:700;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}
.resultado{display:flex;flex-direction:column;gap:10px}.alerta{border-left:4px solid #00b9c7;padding:10px 14px;background:#e4f2f6;color:#01305c}.error{color:#a92b2b}
:focus-visible{outline:3px solid #00b9c7;outline-offset:3px}
@media(prefers-reduced-motion:reduce){.pantalla{transition:none}}
</style>
