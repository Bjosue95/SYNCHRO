<script setup>
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import SelectorMetodoPago from "../components/pagos/SelectorMetodoPago.vue";
import {
  obtenerTokenCobroRetiroCarga,
  obtenerEspacioRetiroCarga,
  limpiarRetiroCarga
} from "../services/retiroCargaSesion.js";
import {
  prepararCobroRetiroCarga,
  confirmarPagoSimuladoRetiroCarga,
  confirmarRecogidaRetiroCarga
} from "../services/retiroCargaApi.js";

const router = useRouter();
const { oscuro } = useTema();
const fase = ref("cargando");
const orden = ref(null);
const metodo = ref("");
const error = ref("");
const procesando = ref(false);
const haRetirado = ref(false);
const codigoFinal = ref("");

const importe = computed(() => {
  const n = orden.value?.importeHnl;
  return typeof n === "number" && Number.isFinite(n)
    ? new Intl.NumberFormat("es-HN", {
        style: "currency", currency: "HNL"
      }).format(n)
    : "Pendiente";
});

onMounted(async () => {
  if (!obtenerTokenCobroRetiroCarga() || !obtenerEspacioRetiroCarga()) {
    router.replace("/retirar/dispositivo");
    return;
  }
  try {
    orden.value = await prepararCobroRetiroCarga();
    fase.value = orden.value?.pagoSimulado ? "recogida" : "pago";
  } catch (e) {
    fase.value = "error";
    error.value = e?.message || "No se pudo preparar el cobro.";
  }
});

async function confirmarPago() {
  if (!metodo.value || procesando.value || fase.value !== "pago") return;
  procesando.value = true;
  error.value = "";
  try {
    await confirmarPagoSimuladoRetiroCarga(metodo.value);
    fase.value = "recogida";
  } catch (e) {
    error.value = e?.message || "No se pudo confirmar el pago simulado.";
  } finally {
    procesando.value = false;
  }
}

async function finalizarRetiro() {
  if (!haRetirado.value || procesando.value || fase.value !== "recogida") return;
  procesando.value = true;
  error.value = "";
  try {
    const resultado = await confirmarRecogidaRetiroCarga();
    codigoFinal.value = resultado.codigoEspacio;
    fase.value = "completado";
  } catch (e) {
    error.value = e?.message || "No se pudo confirmar la recogida.";
  } finally {
    procesando.value = false;
  }
}

function volverAlMenu() {
  limpiarRetiroCarga();
  router.replace("/menu");
}
function repetirIdentificacion() {
  limpiarRetiroCarga();
  router.replace("/retirar/dispositivo");
}
</script>

<template>
  <main class="pantalla" :class="{ 'tema-oscuro': oscuro }">
    <section class="contenido">
      <header class="cabecera">
        <h1 v-if="fase === 'completado'">Retiro completado</h1>
        <h1 v-else>Retirar mi dispositivo</h1>
        <p v-if="fase !== 'completado'">
          El cobro se realiza después del uso del espacio.
        </p>
      </header>

      <section v-if="fase === 'cargando'" class="panel" role="status">
        Preparando el resumen del consumo...
      </section>

      <section v-else-if="fase === 'error'" class="panel" role="alert">
        <p>{{ error }}</p>
        <button class="boton-secundario" type="button"
          @click="repetirIdentificacion">
          Volver a verificar rostro y PIN
        </button>
      </section>

      <template v-else-if="orden && fase !== 'completado'">
        <section class="panel resumen" aria-label="Resumen de consumo">
          <h2>Resumen de tu consumo</h2>
          <div class="fila"><span>Espacio</span><strong>{{ orden.codigoEspacio }}</strong></div>
          <div class="fila"><span>Dispositivo</span><strong>{{ orden.dispositivo }}</strong></div>
          <div class="fila"><span>Tiempo facturado</span>
            <strong>{{ orden.minutosUtilizados }} min</strong>
          </div>
          <div class="fila total"><span>Total</span><strong>{{ importe }}</strong></div>
          <p class="nota">{{ orden.descripcionTarifa }}</p>
        </section>

        <section v-if="fase === 'pago'" class="panel" aria-label="Pago de prueba">
          <h2>¿Cómo deseas pagar?</h2>
          <SelectorMetodoPago v-model="metodo" :oscuro="oscuro" />
          <p class="aviso">
            Este es un pago simulado para el prototipo. No se transfiere dinero real.
          </p>
        </section>

        <section v-else-if="fase === 'recogida'" class="panel" role="status">
          <h2>Pago simulado confirmado</h2>
          <p>
            Ya puedes retirar físicamente tu dispositivo del espacio
            <strong>{{ orden.codigoEspacio }}</strong>.
            La apertura del prototipo es manual.
          </p>
          <label class="confirmacion">
            <input v-model="haRetirado" type="checkbox" />
            Confirmo que ya retiré mi dispositivo.
          </label>
          <p class="aviso">
            El espacio permanecerá ocupado hasta que confirmes la recogida.
          </p>
        </section>
      </template>

      <section v-else-if="fase === 'completado'" class="panel final" role="status">
        <span class="icono-exito" aria-hidden="true">✓</span>
        <h2>Dispositivo retirado</h2>
        <p>Espacio {{ codigoFinal }} liberado correctamente.</p>
        <p>El pago fue simulado. No se procesó dinero real.</p>
      </section>

      <p v-if="error && fase !== 'error'" class="mensaje-error" role="alert">
        {{ error }}
      </p>

      <footer class="acciones">
        <button
          v-if="fase === 'pago'"
          class="boton-principal" type="button"
          :disabled="!metodo || procesando"
          @click="confirmarPago">
          {{ procesando ? "Verificando..." : "Confirmar pago simulado" }}
        </button>
        <button
          v-if="fase === 'recogida'"
          class="boton-principal" type="button"
          :disabled="!haRetirado || procesando"
          @click="finalizarRetiro">
          {{ procesando ? "Finalizando..." : "Confirmar recogida" }}
        </button>
        <button
          v-if="fase === 'completado'"
          class="boton-principal" type="button" @click="volverAlMenu">
          Finalizar
        </button>
      </footer>
    </section>
  </main>
</template>

<style scoped>
.pantalla, .pantalla * { box-sizing: border-box; }
.pantalla {
  width: 100%; height: 100%; overflow-y: auto;
  background: #f2f7fa; color: #01305c;
  padding: clamp(16px, 2vh, 28px);
}
.contenido {
  max-width: 830px; margin: 0 auto; min-height: 100%;
  display: flex; flex-direction: column;
  gap: clamp(12px, 1.5vh, 20px);
}
.cabecera { text-align: center; }
.cabecera h1 {
  font-size: clamp(1.55rem, 3.2vw, 2.35rem);
  margin: 0 0 6px;
}
.cabecera p { margin: 0; color: #5b6770; }
.panel {
  border: 1px solid #c9e2ec;
  border-radius: 18px;
  background: #fff;
  padding: clamp(14px, 2vw, 24px);
  display: flex; flex-direction: column; gap: 14px;
}
.panel h2 {
  font-size: clamp(1.12rem, 2.5vw, 1.5rem);
  text-align: center; margin: 0;
}
.panel p { margin: 0; line-height: 1.5; }
.fila {
  display: flex; justify-content: space-between; align-items: center;
  gap: 16px; padding: 6px 0;
  border-bottom: 1px solid #dcebf1;
}
.fila strong { text-align: right; }
.total { font-size: 1.3rem; border-bottom: none; }
.total strong { color: #008c97; }
.nota, .aviso { font-size: .94rem; color: #5b6770; }
.confirmacion {
  display: flex; gap: 12px; align-items: center;
  font-weight: 700; min-height: 46px;
}
.confirmacion input { width: 25px; height: 25px; accent-color: #00b9c7; }
.acciones { margin-top: auto; padding: 12px 0; }
.boton-principal, .boton-secundario {
  width: 100%; min-height: 58px; border-radius: 15px;
  font-weight: 750; font-size: 1.08rem; cursor: pointer;
}
.boton-principal {
  border: 0; color: #fff; background: #01305c;
}
.boton-principal:disabled { cursor: not-allowed; opacity: .45; }
.boton-secundario { border: 1px solid #01305c; background: transparent; color: #01305c; }
.mensaje-error { color: #aa3333; font-weight: 600; }
.final { align-items: center; text-align: center; }
.icono-exito {
  display: grid; place-items: center; width: 70px; height: 70px;
  border-radius: 50%; background: #d5f4f3; color: #007b83; font-size: 2.3rem;
}
.tema-oscuro { background: #061f36; color: #f5fafc; }
.tema-oscuro .panel { background: #0c3150; border-color: #266079; }
.tema-oscuro .cabecera p, .tema-oscuro .nota,
.tema-oscuro .aviso { color: #bdd4e1; }
.tema-oscuro .fila { border-color: #2a5570; }
.tema-oscuro .boton-principal { background: #00b9c7; color: #06253d; }
.tema-oscuro .boton-secundario { border-color: #5ec6e8; color: #f1fbff; }
@media (max-height: 700px) {
  .panel { gap: 9px; }
  .contenido { gap: 10px; }
}
</style>
