<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import { useTema } from "../composables/useTema";
import { crearPreReservaEncomienda } from "../services/encomiendasWebApi";

const { oscuro, cambiarTema } = useTema();
const centroAyuda = ref(null);
const telefonoRemitente = ref("");
const telefonoDestinatario = ref("");
const tamanoSolicitado = ref("Pequeno");
const procesando = ref(false);
const error = ref("");
const reserva = ref(null);
const copiado = ref(false);

async function registrar() {
  if (procesando.value || reserva.value) return;
  procesando.value = true;
  error.value = "";
  try {
    reserva.value = await crearPreReservaEncomienda({
      telefonoRemitente: telefonoRemitente.value,
      telefonoDestinatario: telefonoDestinatario.value,
      tamanoSolicitado: tamanoSolicitado.value,
    });
    telefonoRemitente.value = "";
    telefonoDestinatario.value = "";
  } catch (e) {
    error.value = e.message || "No se pudo registrar la pre-reserva.";
  } finally {
    procesando.value = false;
  }
}

async function copiarCodigo() {
  if (!reserva.value) return;
  try {
    await navigator.clipboard.writeText(`${reserva.value.id} ${reserva.value.tokenGestion}`);
    copiado.value = true;
  } catch {
    copiado.value = false;
    error.value = "No se pudo copiar. Anota la referencia y token de manera privada.";
  }
}
</script>

<template>
  <main class="pantalla" :class="{ 'tema-oscuro': oscuro }">
    <AppHeader :oscuro="oscuro" @cambiar-tema="cambiarTema" @abrir-ayuda="centroAyuda?.abrir()" />
    <RouterLink to="/guardar" class="volver">← Volver a Guardar</RouterLink>
    <section class="tarjeta">
      <p class="etiqueta">SYNCHRO · ENCOMIENDAS</p>
      <h1>Pre-reserva web de encomienda</h1>
      <p>Registra una solicitud de prueba. Todavía no reserva físicamente un casillero ni autoriza el depósito.</p>
      <p class="alerta">Solo para pruebas: usa números ficticios. La verificación real por WhatsApp y facial aún no está conectada.</p>

      <form v-if="!reserva" @submit.prevent="registrar" class="formulario">
        <label for="remitente">Teléfono del remitente</label>
        <input id="remitente" v-model.trim="telefonoRemitente" type="tel" autocomplete="off" placeholder="+504XXXXXXXX" required minlength="8" maxlength="20" />
        <label for="destinatario">Teléfono del destinatario</label>
        <input id="destinatario" v-model.trim="telefonoDestinatario" type="tel" autocomplete="off" placeholder="+504XXXXXXXX" required minlength="8" maxlength="20" />
        <label for="tamano">Tamaño solicitado</label>
        <select id="tamano" v-model="tamanoSolicitado">
          <option value="Pequeno">Pequeño</option>
          <option value="Mediano">Mediano</option>
          <option value="Grande">Grande</option>
        </select>
        <button type="submit" :disabled="procesando">{{ procesando ? "Registrando..." : "Crear pre-reserva de prueba" }}</button>
      </form>

      <div v-else class="resultado" role="status">
        <h2>Pre-reserva creada</h2>
        <p>Referencia: <strong>{{ reserva.id }}</strong></p>
        <p>Estado: <strong>Pendiente de consentimiento por WhatsApp</strong></p>
        <p>Token privado de gestión (se muestra solo ahora):</p>
        <output class="token">{{ reserva.tokenGestion }}</output>
        <button type="button" @click="copiarCodigo">{{ copiado ? "Copiado" : "Copiar referencia y token" }}</button>
        <p>Guarda ambos datos en privado. Si recargas esta página, no podrás recuperar el token original.</p>
        <RouterLink class="volver" to="/web/encomiendas/consultar">Consultar estado</RouterLink>
      </div>
      <p v-if="error" role="alert" class="error">{{ error }}</p>
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
.etiqueta{font-size:13px;font-weight:700;letter-spacing:1px;color:#009ca9}h1{font-size:clamp(24px,3vw,32px);margin:0}h2{margin:0}
.alerta{border-left:4px solid #00b9c7;padding:10px 14px;background:#e4f2f6;color:#01305c}
.formulario{display:flex;flex-direction:column;gap:10px}.formulario label{font-weight:600}
input,select{min-height:48px;border:1px solid #779aaa;border-radius:10px;padding:10px 12px;font:inherit;background:white;color:#01305c}
button{min-height:56px;padding:13px;background:#00b9c7;color:#01305c;border:0;border-radius:12px;font:inherit;font-weight:700;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}
.resultado{display:flex;flex-direction:column;gap:12px}.token{word-break:break-all;padding:14px;border-radius:10px;background:#e4f2f6;color:#01305c;font-family:monospace}.error{color:#a92b2b}
:focus-visible{outline:3px solid #00b9c7;outline-offset:3px}
@media(prefers-reduced-motion:reduce){.pantalla{transition:none}}
</style>
