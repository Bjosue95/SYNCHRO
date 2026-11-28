<script setup>
import { computed, onMounted, onBeforeUnmount, ref } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import {
  obtenerTokenCobroRetiroCarga,
  obtenerOrdenCobroRetiroCarga,
  obtenerMetodoPagoRetiroCarga,
  obtenerVencimientoCobroRetiroCarga,
  limpiarRetiroCarga
} from "../services/retiroCargaSesion.js";
import {
  seleccionarMetodoRetiroCarga,
  consultarPagoRetiroCarga,
  confirmarRecogidaRetiroCarga
} from "../services/retiroCargaApi.js";

import { registrarRecogidaRetiro } from "../services/recogidaRetiroSesion.js";
const router = useRouter();
const { oscuro } = useTema();
const propsVistaPrevia = defineProps({
  ordenEjemplo: { type: Object, default: null },
  metodoEjemplo: { type: String, default: "Efectivo" },
  faseEjemplo: { type: String, default: "resumen" }
});
const esVistaPrevia = import.meta.env.DEV && propsVistaPrevia.ordenEjemplo !== null;

// Este recorrido reutiliza exactamente la identidad visual de RetiroPagoView.
// El negocio y la autorizacion del cobro siguen siendo exclusivos de carga.
const orden = ref(esVistaPrevia
  ? propsVistaPrevia.ordenEjemplo
  : obtenerOrdenCobroRetiroCarga());
const metodoVisual = esVistaPrevia
  ? propsVistaPrevia.metodoEjemplo
  : obtenerMetodoPagoRetiroCarga();
const fase = ref(esVistaPrevia
  ? (["esperando", "resumen", "completado"].includes(propsVistaPrevia.faseEjemplo)
    ? propsVistaPrevia.faseEjemplo : "resumen")
  : orden.value?.pagoSimulado ? "resumen" : "iniciando");
const procesando = ref(false);
const consultando = ref(false);
const mensaje = ref("");
const codigoFinal = ref(esVistaPrevia && propsVistaPrevia.faseEjemplo === "completado"
  ? String(orden.value?.codigoEspacio || "")
  : "");
let comprobador = null;
let temporizadorDemo = null;

const mostrarAnimacionPago = computed(() =>
  fase.value === "iniciando" || fase.value === "esperando"
);
const mostrarPantallaFinal = computed(() => fase.value === "completado");
const importe = computed(() => {
  const valor = orden.value?.importeHnl;
  return typeof valor === "number" && Number.isFinite(valor)
    ? new Intl.NumberFormat("es-HN", {
        style: "currency", currency: "HNL"
      }).format(valor)
    : "No disponible";
});
const nombreMetodo = computed(() => ({
  Efectivo: "Efectivo",
  Tarjeta: "Tarjeta",
  NFC: "NFC del celular"
})[metodoVisual] || "No disponible");

function detenerConsultas() {
  if (comprobador !== null) {
    window.clearInterval(comprobador);
    comprobador = null;
  }
}

function reiniciarVerificacion() {
  detenerConsultas();
  limpiarRetiroCarga();
  router.replace("/retirar/dispositivo");
}

async function comprobarPago() {
  if (esVistaPrevia) return;
  if (fase.value !== "esperando" || consultando.value) return;
  if (!obtenerTokenCobroRetiroCarga()) {
    fase.value = "vencido";
    detenerConsultas();
    mensaje.value = "La autorización venció. Repite rostro y PIN sin iniciar otra carga.";
    return;
  }
  consultando.value = true;
  try {
    const estado = await consultarPagoRetiroCarga();
    if (estado?.codigoEspacio !== orden.value?.codigoEspacio ||
        estado?.metodoPago !== metodoVisual) {
      throw new Error("La orden confirmada no corresponde a esta sesión.");
    }
    if (estado.pagoConfirmado === true) {
      fase.value = "resumen";
      detenerConsultas();
      mensaje.value = "";
    }
  } catch (error) {
    mensaje.value = error?.message || "No se pudo consultar el pago.";
    if (!obtenerTokenCobroRetiroCarga()) {
      fase.value = "vencido";
      detenerConsultas();
    }
  } finally {
    consultando.value = false;
  }
}

onMounted(async () => {
  if (esVistaPrevia) {
    if (fase.value === "esperando") {
      // La animacion de efectivo/tarjeta/NFC permanece visible antes
      // de mostrar la confirmacion ficticia de administracion.
      temporizadorDemo = window.setTimeout(() => {
        fase.value = "resumen";
      }, 4800);
    }
    return;
  }
  if (!orden.value || !metodoVisual || !obtenerTokenCobroRetiroCarga()) {
    router.replace("/retirar/dispositivo");
    return;
  }
  if (fase.value === "resumen") return;
  try {
    const seleccionado = await seleccionarMetodoRetiroCarga(metodoVisual);
    if (seleccionado?.codigoEspacio !== orden.value.codigoEspacio) {
      throw new Error("El espacio seleccionado no coincide con tu sesión.");
    }
    fase.value = seleccionado.pagoConfirmado ? "resumen" : "esperando";
    if (fase.value === "esperando") {
      await comprobarPago();
      if (fase.value === "esperando") {
        comprobador = window.setInterval(() => {
          void comprobarPago();
        }, 10000);
      }
    }
  } catch (error) {
    mensaje.value = error?.message || "No se pudo registrar el método de pago.";
    fase.value = "vencido";
  }
});

onBeforeUnmount(() => {
  detenerConsultas();
  window.clearTimeout(temporizadorDemo);
});

async function finalizarRetiro() {
  if (esVistaPrevia) {
    if (fase.value !== "resumen" || procesando.value) return;
    procesando.value = true;
    temporizadorDemo = window.setTimeout(() => {
      codigoFinal.value = String(orden.value?.codigoEspacio || "");
      fase.value = "completado";
      procesando.value = false;
    }, 900);
    return;
  }
  if (fase.value !== "resumen" || procesando.value) return;
  mensaje.value = "";
  procesando.value = true;
  try {
    const codigo=orden.value.codigoEspacio;
    registrarRecogidaRetiro({codigo,tipo:"carga",vence:obtenerVencimientoCobroRetiroCarga(),volver:"/retirar/dispositivo",
      confirmar:async()=>{const resultado=await confirmarRecogidaRetiroCarga();return {...resultado,codigoCasillero:resultado.codigoEspacio};},
      limpiar:limpiarRetiroCarga});
    await router.replace("/retirar/recoger");
  } catch (error) {
    mensaje.value = error?.message || "No se pudo finalizar la recogida.";
  } finally {
    procesando.value = false;
  }
}

function finalizar() {
  if (esVistaPrevia) {
    router.replace("/vista-previa");
    return;
  }
  if (fase.value !== "completado") return;
  limpiarRetiroCarga();
  router.replace("/menu");
}
</script>

<template>
  <main
    class="pantalla-pago"
    :class="{
      'tema-oscuro': oscuro,
      'retiro-para-finalizar': fase === 'resumen',
      'retiro-terminado': mostrarPantallaFinal,
      'pago-preview-animado': esVistaPrevia
    }"
  >
    <!-- Mismas pantallas, tarjetas, iconos y animaciones que RetiroPagoView. -->

    <section
      v-if="mostrarPantallaFinal"
      class="contenido-pago retiro-final"
      aria-live="polite"
    >
      <header>
        <span class="etiqueta">SYNCHRO · RETIRO DE DISPOSITIVO</span>
        <h1>Retiro completado</h1>
        <p>Tu retiro se registró correctamente.</p>
      </header>

      <div class="tarjeta-resumen tarjeta-final">
        <div class="sello-final" aria-hidden="true">
          <img
            src="/iconos/complementarios/30_confirmar.svg"
            class="icono-claro"
            alt=""
          />
          <img
            src="/iconos/complementarios/30_confirmar.svg"
            class="icono-oscuro"
            alt=""
          />
        </div>

        <div class="dato">
          <span>Servicio</span>
          <strong>Carga de dispositivos</strong>
        </div>

        <div class="dato">
          <span>Espacio</span>
          <strong>{{ codigoFinal }}</strong>
        </div>

        <div class="dato">
          <span>Método de pago</span>
          <strong>{{ nombreMetodo }}</strong>
        </div>

        <div class="dato">
          <span>Estado del espacio</span>
          <strong>Disponible</strong>
        </div>

        <div class="dato total">
          <span>Total del consumo</span>
          <strong>{{ importe }}</strong>
        </div>
      </div>

      <button class="boton-principal" type="button" data-preview-permitido @click="finalizar">
        <span>Finalizar</span>
        <span class="icono-continuar" aria-hidden="true">
          <img
            src="/iconos/complementarios/30_confirmar.svg"
            class="icono-claro" alt=""
          />
          <img
            src="/iconos/complementarios/30_confirmar.svg"
            class="icono-oscuro" alt=""
          />
        </span>
      </button>
    </section>

    <section
      v-else-if="mostrarAnimacionPago"
      class="contenido-pago espera-animada"
      aria-live="polite"
    >
      <header>
        <span class="etiqueta">SYNCHRO · PAGO</span>
        <h1>
          {{
            metodoVisual === "Efectivo"
              ? "Introduce el efectivo"
              : metodoVisual === "Tarjeta"
                ? "Coloca tu tarjeta"
                : "Acerca tu teléfono"
          }}
        </h1>
        <p>
          {{
            metodoVisual === "Efectivo"
              ? "Deposita el importe en el recolector."
              : metodoVisual === "Tarjeta"
                ? "Acerca tu tarjeta a la pantalla."
                : "Acerca el teléfono al lector sin contacto."
          }}
        </p>
      </header>

      <div class="escenario-cobro escena-pago-synchro" aria-hidden="true">
        <template v-if="metodoVisual === 'Efectivo'">
          <div class="escena-efectivo-synchro">
            <img
              :src="'/iconos/pagos/efectivo' + (oscuro ? '-oscuro' : '') + '.svg'"
              class="icono-pago-v3 icono-efectivo-v3" alt=""
            />
            <div class="depositador-efectivo">
              <div class="ranura-deposito"></div>
              <div class="panel-deposito"></div>
            </div>
          </div>
        </template>

        <template v-else-if="metodoVisual === 'Tarjeta'">
          <div class="escena-tarjeta-synchro">
            <div class="pantalla-cobro-v3">
              <div class="panel-cobro-v3"></div>
            </div>
            <img
              :src="'/iconos/pagos/tarjeta' + (oscuro ? '-oscuro' : '') + '.svg'"
              class="icono-pago-v3 icono-tarjeta-v3" alt=""
            />
          </div>
        </template>

        <template v-else>
          <div class="escena-nfc-synchro">
            <div class="pantalla-cobro-v3">
              <div class="panel-cobro-v3"></div>
            </div>
            <img
              :src="'/iconos/pagos/nfc' + (oscuro ? '-oscuro' : '') + '.svg'"
              class="icono-pago-v3 icono-nfc-v3" alt=""
            />
            <div class="ondas-nfc-v3">
              <span></span>
              <span></span>
              <span></span>
            </div>
          </div>
        </template>
      </div>

      <div class="esperando-operador" role="status">
        <span class="pulso-espera"></span>
        {{ fase === "iniciando" ? "Registrando método de pago..." : "Esperando confirmación del administrador" }}
      </div>

      <p v-if="mensaje" class="mensaje" role="alert">{{ mensaje }}</p>
    </section>

    <section v-else-if="fase === 'vencido'" class="contenido-pago" role="alert">
      <header>
        <span class="etiqueta">SYNCHRO · RETIRO DE DISPOSITIVO</span>
        <h1>Verificación necesaria</h1>
        <p>{{ mensaje }}</p>
      </header>
      <button class="boton-principal" type="button"
        @click="reiniciarVerificacion">
        Repetir rostro y PIN
      </button>
    </section>

    <section v-else-if="orden" class="contenido-pago">
      <header>
        <span class="etiqueta">SYNCHRO · RETIRO DE DISPOSITIVO</span>
        <h1>Resumen de tu consumo</h1>
        <p>
          La administración confirmó el cobro.
          Revisa tu consumo antes de finalizar el retiro.
        </p>
      </header>

      <div class="tarjeta-resumen resumen-alto-v5">
        <div class="resumen-intro-v5">
          <span>DETALLE DE TU CONSUMO</span>
          <strong>Carga de dispositivos</strong>
        </div>

        <div class="dato">
          <span>Servicio</span>
          <strong>{{ orden.dispositivo }}</strong>
        </div>

        <div class="dato dato-casillero">
          <span>Espacio utilizado</span>
          <strong>{{ orden.codigoEspacio }}</strong>
        </div>

        <div class="dato">
          <span>Tiempo de uso</span>
          <strong>{{ orden.minutosUtilizados }} min</strong>
        </div>

        <div class="dato">
          <span>Modalidad</span>
          <strong>Pago al finalizar</strong>
        </div>

        <div class="dato">
          <span>Método de pago</span>
          <strong>{{ nombreMetodo }}</strong>
        </div>

        <p v-if="orden.descripcionTarifa" class="descripcion">
          {{ orden.descripcionTarifa }}
        </p>

        <div class="dato total">
          <span>Total del consumo</span>
          <strong>{{ importe }}</strong>
        </div>
      </div>

      <p v-if="mensaje" class="mensaje" role="alert">{{ mensaje }}</p>

      <button
        class="boton-principal" type="button"
        :disabled="procesando"
        data-preview-permitido
        @click="finalizarRetiro"
      >
        <span>{{ procesando ? "Comprobando..." : esVistaPrevia ? "Finalizar retiro" : "Continuar" }}</span>
        <span class="icono-continuar" aria-hidden="true">
          <img
            src="/iconos/complementarios/29_continuar.svg"
            class="icono-claro" alt=""
          />
          <img
            src="/iconos/complementarios/29_continuar-oscuro.svg"
            class="icono-oscuro" alt=""
          />
        </span>
      </button>

      <p class="nota">
        {{ esVistaPrevia ? "Retira tu dispositivo antes de finalizar." : "Continúa para recoger tu dispositivo en el espacio asignado." }}
      </p>
    </section>
  </main>
</template>

<style scoped>
/* SYNCHRO - RESUMEN DE PAGO
   Identidad visual unificada con Introducir PIN.
   No altera la logica del retiro. */

.pantalla-pago,
.pantalla-pago * {
  box-sizing: border-box;
}

.pantalla-pago {
  display: flex;
  align-items: center;
  justify-content: center;

  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;

  padding:
    clamp(16px, 2.5dvh, 28px)
    clamp(14px, 3vw, 32px);

  overflow-x: hidden;
  overflow-y: auto;

  background: #f2f7fa;
  color: #01305c;

  font-family: Arial, Helvetica, sans-serif;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

/* Distribucion principal */

.contenido-pago {
  display: flex;
  flex-direction: column;
  align-items: stretch;

  width: min(100%, 650px);
  min-width: 0;

  margin: auto;

  gap: clamp(14px, 2.2dvh, 22px);
}

/* Encabezado */

header {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;

  text-align: center;
}

.etiqueta {
  display: inline-block;

  color: #008b9e;
  font-size: clamp(12px, 1.5vw, 15px);
  font-weight: 800;
  letter-spacing: 1.3px;
}

h1 {
  margin: 0;

  color: inherit;
  font-size: clamp(27px, 3.3vw, 38px);
  font-weight: 750;
  line-height: 1.18;
  letter-spacing: -0.025em;
}

header p {
  max-width: 530px;
  margin: 0;

  color: #5b7282;
  font-size: clamp(14px, 1.5vw, 17px);
  line-height: 1.5;
}

/* Tarjeta principal */

.tarjeta-resumen {
  width: 100%;

  padding:
    clamp(20px, 3vw, 32px)
    clamp(22px, 3.4vw, 38px);

  border: 2px solid #c9e3ea;
  border-radius: 22px;

  background: #ffffff;

  box-shadow:
    0 10px 28px rgba(1, 48, 92, .08);
}

/* Filas de informacion */

.dato {
  display: flex;
  justify-content: space-between;
  align-items: center;

  gap: 16px;
  min-height: 65px;

  padding: 14px 0;

  border-bottom: 1px solid #e0ebef;
}

.dato:first-child {
  padding-top: 4px;
}

.dato span {
  min-width: 0;

  color: #526b7c;
  font-size: clamp(15px, 1.6vw, 18px);
  font-weight: 500;
  line-height: 1.35;
}

.dato strong {
  min-width: 0;

  color: #01305c;
  font-size: clamp(19px, 2.2vw, 25px);
  font-weight: 500;
  line-height: 1.2;

  text-align: right;
  overflow-wrap: anywhere;
  font-variant-numeric: tabular-nums;
}

/* Codigo del casillero */

.dato:first-child strong {
  font-size: clamp(26px, 3vw, 34px);
  letter-spacing: .02em;
}

/* Importe */

.total {
  min-height: 82px;
  border-bottom: 0;
}

.total span {
  color: #01305c;
  font-size: clamp(17px, 1.8vw, 20px);
  font-weight: 500;
}

.total strong {
  color: #008b9e;
  font-size: clamp(30px, 3.7vw, 43px);
  font-weight: 700;
  letter-spacing: -.025em;
}

.descripcion {
  margin: 4px 0 0;
  padding-top: 15px;

  border-top: 1px dashed #c9dce4;

  color: #607988;
  font-size: clamp(13px, 1.4vw, 15px);
  line-height: 1.5;
  text-align: center;
}

/* Estado del pago */

.estado {
  display: flex;
  justify-content: center;
  align-items: center;

  gap: 12px;
  align-self: center;

  width: fit-content;
  max-width: 100%;
  min-height: 48px;

  margin: 0;
  padding: 11px 18px;

  border: 1px solid #ebd5a9;
  border-radius: 13px;

  background: #fff8ea;
  color: #775214;

  font-size: clamp(14px, 1.5vw, 17px);
  font-weight: 750;
  line-height: 1.35;
  text-align: center;
}

.punto {
  width: 11px;
  height: 11px;
  flex: 0 0 11px;

  border-radius: 50%;
  background: #d59c27;
}

.estado.completado {
  border-color: #afdcc8;
  background: #e9f8f0;
  color: #176d49;
}

.completado .punto {
  background: #16a47c;
}

/* Mensajes del servidor */

.mensaje {
  width: 100%;
  margin: 0;
  padding: 14px 17px;

  border: 1px solid #b9dce6;
  border-radius: 12px;

  background: #e5f3f7;
  color: #01305c;

  font-size: 15px;
  line-height: 1.5;
  text-align: center;
}

/* Boton principal:
   mismo estilo del boton Verificar PIN */

.boton-principal {
  display: inline-flex;
  align-items: center;
  justify-content: center;

  align-self: center;
  gap: 12px;

  width: max-content;
  max-width: 100%;
  min-width: min(100%, 305px);
  min-height: 60px;

  padding: 10px 22px;

  border: 2px solid #00b9c7;
  border-radius: 15px;

  background: #f4feff;
  color: #01305c;

  font: inherit;
  font-size: clamp(16px, 1.8vw, 20px);
  font-weight: 800;
  line-height: 1.25;

  box-shadow:
    0 4px 0 #00a5b2,
    0 7px 16px rgba(1, 48, 92, .10);

  cursor: pointer;
  touch-action: manipulation;

  transition:
    background-color 150ms ease,
    transform 150ms ease,
    box-shadow 150ms ease;
}

.boton-principal:active:not(:disabled) {
  transform: translateY(3px);
  box-shadow: 0 1px 0 #00a5b2;
}

.boton-principal:disabled {
  background: #edf3f5;
  color: #647784;
  border-color: #b5cad3;
  box-shadow: none;
  cursor: not-allowed;
}

.boton-principal:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 4px;
}

/* Icono Continuar original de SYNCHRO */

.icono-continuar {
  position: relative;
  display: block;

  width: 38px;
  height: 38px;
  flex: 0 0 38px;
}

.icono-continuar img {
  position: absolute;
  inset: 0;

  display: block;
  width: 100%;
  height: 100%;

  object-fit: contain;

  transition: opacity 150ms ease;
}

.icono-claro {
  opacity: 1;
}

.icono-oscuro {
  opacity: 0;
}

/* Nota de pago simulado */

.nota {
  max-width: 520px;
  align-self: center;

  margin: -4px 0 0;

  color: #627b8a;
  font-size: clamp(12px, 1.3vw, 14px);
  line-height: 1.5;
  text-align: center;
}

/* El modo oscuro se define una sola vez en SYNCHRO-PAGO-TEMA-UNIFICADO-V1. */

/* Pantallas estrechas */

@media (max-width: 480px) {
  .contenido-pago {
    gap: 15px;
  }

  .tarjeta-resumen {
    padding: 19px 20px;
    border-radius: 18px;
  }

  .dato {
    gap: 10px;
    min-height: 58px;
  }

  .dato span {
    font-size: 14px;
  }

  .dato strong {
    font-size: 18px;
  }

  .dato:first-child strong {
    font-size: 27px;
  }

  .total strong {
    font-size: 31px;
  }

  .boton-principal {
    min-width: min(100%, 290px);
  }

  .icono-continuar {
    width: 32px;
    height: 32px;
    flex-basis: 32px;
  }
}

/* Pantallas de poca altura */

@media (max-height: 650px) {
  .pantalla-pago {
    padding-top: 12px;
    padding-bottom: 12px;
  }

  .contenido-pago {
    gap: 11px;
  }

  header {
    gap: 6px;
  }

  h1 {
    font-size: clamp(24px, 3vw, 31px);
  }

  .tarjeta-resumen {
    padding-top: 14px;
    padding-bottom: 14px;
  }

  .dato {
    min-height: 48px;
    padding: 9px 0;
  }

  .total {
    min-height: 60px;
  }

  .estado {
    min-height: 40px;
    padding: 8px 13px;
  }

  .boton-principal {
    min-height: 52px;
    padding: 8px 17px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .pantalla-pago,
  .boton-principal,
  .icono-continuar img {
    transition: none;
  }

  .boton-principal:active:not(:disabled) {
    transform: none;
  }
}

/* SYNCHRO-PAGO-TEMA-UNIFICADO-V1
   Misma paleta y velocidad del modo oscuro de Introducir PIN.
   Exclusivo de la pantalla de pago.
*/

.pantalla-pago .tarjeta-resumen,
.pantalla-pago .dato,
.pantalla-pago .dato span,
.pantalla-pago .dato strong,
.pantalla-pago .descripcion,
.pantalla-pago .estado,
.pantalla-pago .mensaje,
.pantalla-pago .nota,
.pantalla-pago header p,
.pantalla-pago .etiqueta,
.pantalla-pago .boton-principal {
  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    color 150ms ease,
    box-shadow 150ms ease;
}

/* Fondo y superficie oscuros */

#app .synchro-app .pantalla-pago.tema-oscuro {
  background-color: #061f36;
  color: #ffffff;
}

.pantalla-pago.tema-oscuro .tarjeta-resumen {
  background-color: #103956;
  border-color: #42647e;
  color: #ffffff;
  box-shadow: 0 10px 28px rgba(0, 0, 0, .18);
}

.pantalla-pago.tema-oscuro .dato {
  border-bottom-color: #42647e;
}

.pantalla-pago.tema-oscuro .total {
  border-bottom: 0;
}

/* Jerarquia de texto */

.pantalla-pago.tema-oscuro h1,
.pantalla-pago.tema-oscuro .dato strong,
.pantalla-pago.tema-oscuro .total span {
  color: #ffffff;
}

.pantalla-pago.tema-oscuro .dato span,
.pantalla-pago.tema-oscuro header p,
.pantalla-pago.tema-oscuro .descripcion,
.pantalla-pago.tema-oscuro .nota {
  color: #d4e2ea;
}

.pantalla-pago.tema-oscuro .etiqueta,
.pantalla-pago.tema-oscuro .total strong {
  color: #5ec6e8;
}

/* Separacion del resumen */

.pantalla-pago.tema-oscuro .descripcion {
  border-top-color: #42647e;
}

/* Estado de pago pendiente */

.pantalla-pago.tema-oscuro .estado:not(.completado) {
  background-color: #42351f;
  border-color: #967a45;
  color: #fff0ca;
}

.pantalla-pago.tema-oscuro .estado.completado {
  background-color: #16483e;
  border-color: #448b7a;
  color: #e4fff2;
}

.pantalla-pago.tema-oscuro .mensaje {
  background-color: #204766;
  border-color: #426984;
  color: #ffffff;
}

/* Boton igual al de Introducir PIN */

.pantalla-pago.tema-oscuro
.boton-principal:not(:disabled) {
  background-color: #123d5b;
  border-color: #5ec6e8;
  color: #ffffff;
  box-shadow:
    0 4px 0 #087d93,
    0 7px 16px rgba(0, 0, 0, .22);
}

.pantalla-pago.tema-oscuro
.boton-principal:disabled {
  background-color: #254054;
  border-color: #426984;
  color: #c0d0da;
  box-shadow: none;
}

/* El SVG del boton cambia con el tema */

.pantalla-pago.tema-oscuro .icono-claro {
  opacity: 0;
}

.pantalla-pago.tema-oscuro .icono-oscuro {
  opacity: 1;
}

@media (prefers-reduced-motion: reduce) {
  .pantalla-pago,
  .pantalla-pago .tarjeta-resumen,
  .pantalla-pago .dato,
  .pantalla-pago .dato span,
  .pantalla-pago .dato strong,
  .pantalla-pago .descripcion,
  .pantalla-pago .estado,
  .pantalla-pago .mensaje,
  .pantalla-pago .nota,
  .pantalla-pago header p,
  .pantalla-pago .etiqueta,
  .pantalla-pago .boton-principal {
    transition: none !important;
  }
}

/* Vista previa del siguiente paso. No afecta el flujo real. */
.paso-pago-preview {
  display: grid;
  place-items: center;
  min-height: 142px;
  padding: 22px;
  border: 2px solid #00b9c7;
  border-radius: 18px;
  background: #e9fbfd;
  text-align: center;
}

.paso-pago-preview p {
  margin: 0;
  font-size: clamp(18px, 2vw, 23px);
  font-weight: 750;
  line-height: 1.4;
}

.pantalla-pago.tema-oscuro .paso-pago-preview {
  background: #103956;
  border-color: #5ec6e8;
  color: #ffffff;
}

/* Tablet vertical: el resumen comienza a 46 px del inicio util,
   como el titulo del menu, sin margen vertical automatico. */
@media (orientation: portrait) and (min-width: 400px) and (min-height: 680px) {
  .pantalla-pago {
    align-items: flex-start;
    padding-top: 46px;
  }

  .pantalla-pago .contenido-pago {
    margin: 0 auto;
  }
}

/* SYNCHRO-PAGO-PROPORCIONES-V2
   Ajustes exclusivos de la vista previa. */

.pantalla-pago.vista-previa-pago .contenido-pago {
  width: min(100%, 780px);
  gap: clamp(18px, 2.6dvh, 30px);
}

.pantalla-pago.vista-previa-pago h1 {
  font-size: clamp(30px, 4vw, 44px);
}

.pantalla-pago.vista-previa-pago .tarjeta-resumen {
  padding: clamp(22px, 3.4vw, 36px);
}

.pantalla-pago.vista-previa-pago .dato {
  min-height: 75px;
  padding-block: 15px;
}

.pantalla-pago.vista-previa-pago .dato span {
  font-size: clamp(18px, 2vw, 22px);
}

.pantalla-pago.vista-previa-pago .dato strong {
  font-size: clamp(23px, 2.8vw, 32px);
}

.pantalla-pago.vista-previa-pago .total {
  min-height: 105px;
}

.pantalla-pago.vista-previa-pago .total strong {
  font-size: clamp(46px, 5.8vw, 66px);
  color: #008b9e;
}

.pantalla-pago.vista-previa-pago.tema-oscuro .total strong {
  color: #5ec6e8;
}

.pantalla-pago.vista-previa-pago .boton-principal {
  min-width: min(100%, 355px);
  min-height: 74px;
  font-size: clamp(19px, 2vw, 23px);
}

@media (max-width: 500px) {
  .pantalla-pago.vista-previa-pago .contenido-pago {
    gap: 14px;
  }

  .pantalla-pago.vista-previa-pago .tarjeta-resumen {
    padding: 18px;
  }

  .pantalla-pago.vista-previa-pago .dato {
    min-height: 54px;
    padding-block: 8px;
  }

  .pantalla-pago.vista-previa-pago .dato span {
    font-size: 15px;
  }

  .pantalla-pago.vista-previa-pago .dato strong {
    font-size: 20px;
  }

  .pantalla-pago.vista-previa-pago .total strong {
    font-size: 43px;
  }
}

@media (max-height: 650px) and (orientation: landscape) {
  .pantalla-pago.vista-previa-pago .contenido-pago {
    gap: 8px;
  }

  .pantalla-pago.vista-previa-pago .dato {
    min-height: 44px;
    padding-block: 6px;
  }

  .pantalla-pago.vista-previa-pago .total {
    min-height: 60px;
  }
}

/* SYNCHRO - ANIMACION DEL PAGO SIMULADO */
.espera-animada {
  align-items: center;
  text-align: center;
  gap: clamp(18px, 3dvh, 32px);
  padding-top: clamp(10px, 2dvh, 26px);
}

.escenario-cobro {
  position: relative;
  width: min(100%, 470px);
  height: clamp(250px, 35dvh, 350px);
  overflow: hidden;
  border: 2px solid #c9e3ea;
  border-radius: 28px;
  background: linear-gradient(160deg, #fff, #dff4f8);
}

.escena-efectivo,
.escena-tarjeta,
.escena-nfc {
  position: absolute;
  inset: 0;
}

.billete-animado {
  position: absolute;
  top: 13%;
  left: calc(50% - 65px);
  display: grid;
  place-items: center;
  width: 130px;
  height: 72px;
  border: 5px double #328f82;
  border-radius: 12px;
  background: #b9f1cf;
  color: #246f58;
  font-size: 37px;
  font-weight: 800;
  animation: entrar-billete 2.6s ease-in-out infinite;
}

.recolector-animado {
  position: absolute;
  bottom: 18%;
  left: calc(50% - 105px);
  width: 210px;
  height: 88px;
  border-radius: 16px;
  background: #01305c;
  box-shadow: 0 12px 25px #01305c33;
}

.ranura-animada {
  position: absolute;
  top: 15px;
  left: 20px;
  right: 20px;
  height: 13px;
  border-radius: 8px;
  background: #031b31;
  border: 2px solid #5ec6e8;
}

.lector-animado,
.lector-nfc {
  position: absolute;
  bottom: 17%;
  left: calc(50% - 95px);
  width: 190px;
  height: 125px;
  border: 7px solid #01305c;
  border-radius: 22px;
  background: #24627b;
  box-shadow: 0 12px 25px #01305c33;
}

.pantalla-lector {
  position: absolute;
  top: 12px;
  left: 17px;
  right: 17px;
  height: 50px;
  border-radius: 9px;
  background: #a5e9ee;
}

.tarjeta-animada {
  position: absolute;
  top: 11%;
  left: calc(50% - 90px);
  width: 180px;
  height: 105px;
  border-radius: 13px;
  background: linear-gradient(135deg, #01305c, #00b9c7);
  box-shadow: 0 8px 20px #01305c33;
  animation: acercar-tarjeta 2.7s ease-in-out infinite;
}

.chip-animado {
  position: absolute;
  top: 38px;
  left: 20px;
  width: 36px;
  height: 29px;
  background: #f5d77c;
  border-radius: 6px;
}

.telefono-animado {
  position: absolute;
  top: 8%;
  left: calc(50% - 45px);
  width: 90px;
  height: 155px;
  border: 7px solid #01305c;
  border-radius: 19px;
  background: #c8eff6;
  animation: acercar-telefono 2.7s ease-in-out infinite;
}

.pantalla-telefono {
  position: absolute;
  inset: 12px 7px;
  border-radius: 8px;
  background: linear-gradient(150deg, #5ec6e8, #00b9c7);
}

.ondas-nfc {
  position: absolute;
  top: 42%;
  left: 58%;
  color: #008b9e;
  font-size: 45px;
  font-weight: 800;
  letter-spacing: -9px;
  animation: pulsar-nfc 1.5s ease-in-out infinite;
}

.esperando-operador {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  font-size: clamp(16px, 2vw, 20px);
  font-weight: 700;
}

.pulso-espera {
  width: 13px;
  height: 13px;
  border-radius: 50%;
  background: #00b9c7;
  animation: pulsar-nfc 1.5s ease-in-out infinite;
}

.pantalla-pago.tema-oscuro .escenario-cobro {
  background: linear-gradient(160deg, #103956, #061f36);
  border-color: #42647e;
}

@keyframes entrar-billete {
  0%, 15% { transform: translateY(0); opacity: 1; }
  75%, 90% { transform: translateY(135px) scale(.85); opacity: 0; }
  100% { transform: translateY(0); opacity: 0; }
}

@keyframes acercar-tarjeta {
  0%, 15% { transform: translateY(0); }
  65%, 85% { transform: translateY(105px) scale(.9); }
  100% { transform: translateY(0); }
}

@keyframes acercar-telefono {
  0%, 15% { transform: translateY(0); }
  65%, 85% { transform: translateY(85px); }
  100% { transform: translateY(0); }
}

@keyframes pulsar-nfc {
  0%, 100% { opacity: .3; }
  50% { opacity: 1; }
}

@media (prefers-reduced-motion: reduce) {
  .billete-animado, .tarjeta-animada,
  .telefono-animado, .ondas-nfc, .pulso-espera {
    animation: none;
  }
}

/* SYNCHRO - ICONOS DE PAGO ORIGINALES V2 */

.escena-iconos-synchro {
  display: grid;
  place-items: center;
  isolation: isolate;
}

.circulo-lector {
  position: absolute;
  left: 50%;
  top: 50%;
  width: clamp(170px, 48%, 240px);
  aspect-ratio: 1;
  border: 3px solid #5ec6e8;
  border-radius: 50%;
  background: rgba(0,185,199,.07);
  transform: translate(-50%, -50%);
  animation: synchro-respirar 2.7s ease-in-out infinite;
}

.circulo-interior {
  position: absolute;
  inset: 16%;
  border: 2px dashed #00b9c7;
  border-radius: 50%;
  opacity: .55;
}

.icono-cobro-synchro {
  position: relative;
  z-index: 2;
  width: clamp(125px, 35%, 195px);
  max-height: 190px;
  object-fit: contain;
  filter: drop-shadow(0 12px 14px rgba(1,48,92,.16));
}

.icono-cobro-synchro.efectivo {
  animation: synchro-efectivo 2.8s ease-in-out infinite;
}

.icono-cobro-synchro.tarjeta,
.icono-cobro-synchro.nfc {
  animation: synchro-acercar 2.8s ease-in-out infinite;
}

.onda-lector {
  position: absolute;
  bottom: 15%;
  left: calc(50% - 14px);
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: #00b9c7;
  box-shadow: 0 0 0 12px rgba(0,185,199,.15);
  animation: synchro-onda 1.8s ease-in-out infinite;
}

@keyframes synchro-efectivo {
  0%, 12%, 100% {
    transform: translateY(-38px);
    opacity: 1;
  }
  70%, 90% {
    transform: translateY(40px) scale(.85);
    opacity: .35;
  }
}

@keyframes synchro-acercar {
  0%, 12%, 100% {
    transform: translateY(-30px) scale(.86);
  }
  65%, 85% {
    transform: translateY(20px) scale(1.08);
  }
}

@keyframes synchro-respirar {
  0%, 100% { opacity: .5; }
  50% { opacity: 1; }
}

@keyframes synchro-onda {
  0%, 100% {
    opacity: .25;
    transform: scale(.75);
  }
  50% {
    opacity: 1;
    transform: scale(1.15);
  }
}

@media (prefers-reduced-motion: reduce) {
  .circulo-lector,
  .icono-cobro-synchro,
  .onda-lector {
    animation: none !important;
  }
}

/* SYNCHRO-PAGO-V3 */

.escena-pago-synchro {
  display: grid;
  place-items: center;
  isolation: isolate;
}

.escena-efectivo-synchro,
.escena-tarjeta-synchro,
.escena-nfc-synchro {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: clamp(240px, 34dvh, 330px);
}

.icono-pago-v3 {
  position: absolute;
  z-index: 3;
  width: clamp(120px, 24vw, 180px);
  max-height: 180px;
  object-fit: contain;
  filter: drop-shadow(0 12px 14px rgba(1,48,92,.16));
}

/* EFECTIVO */
.icono-efectivo-v3 {
  left: 50%;
  top: 12%;
  transform: translateX(-50%);
  animation: synchro-efectivo-deposito 2.8s ease-in-out infinite;
}

.depositador-efectivo {
  position: absolute;
  left: 50%;
  bottom: 16%;
  transform: translateX(-50%);
  width: clamp(180px, 34vw, 260px);
  height: clamp(95px, 14dvh, 130px);
  border-radius: 22px;
  background: linear-gradient(180deg, #0a4276 0%, #01305c 100%);
  box-shadow: 0 14px 28px rgba(1,48,92,.20);
}

.ranura-deposito {
  position: absolute;
  top: 16px;
  left: 16px;
  right: 16px;
  height: 14px;
  border-radius: 12px;
  background: #061b31;
  border: 2px solid #5ec6e8;
}

.panel-deposito {
  position: absolute;
  left: 50%;
  bottom: 16px;
  transform: translateX(-50%);
  width: 58%;
  height: 34px;
  border-radius: 12px;
  background: rgba(94,198,232,.18);
  border: 2px solid rgba(94,198,232,.28);
}

/* TARJETA Y NFC */
.pantalla-cobro-v3 {
  position: absolute;
  left: 50%;
  bottom: 16%;
  transform: translateX(-50%);
  width: clamp(170px, 32vw, 230px);
  height: clamp(140px, 22dvh, 190px);
  border-radius: 26px;
  background: linear-gradient(180deg, #124c7f 0%, #01305c 100%);
  box-shadow: 0 14px 28px rgba(1,48,92,.20);
}

.panel-cobro-v3 {
  position: absolute;
  top: 16px;
  left: 16px;
  right: 16px;
  bottom: 16px;
  border-radius: 18px;
  background: linear-gradient(180deg, rgba(94,198,232,.38) 0%, rgba(0,185,199,.20) 100%);
  border: 2px solid rgba(94,198,232,.30);
}

.icono-tarjeta-v3,
.icono-nfc-v3 {
  left: 50%;
  top: 10%;
  transform: translateX(-50%);
  animation: synchro-acercar-pantalla 2.8s ease-in-out infinite;
}

.ondas-nfc-v3 {
  position: absolute;
  left: 50%;
  bottom: 22%;
  transform: translateX(55px);
  z-index: 4;
  display: flex;
  gap: 6px;
  align-items: center;
}

.ondas-nfc-v3 span {
  display: block;
  width: 10px;
  height: 10px;
  border: 3px solid #00b9c7;
  border-left-color: transparent;
  border-bottom-color: transparent;
  border-radius: 50%;
  transform: rotate(45deg);
  animation: synchro-onda-nfc 1.4s ease-in-out infinite;
}

.ondas-nfc-v3 span:nth-child(2) {
  animation-delay: .18s;
}

.ondas-nfc-v3 span:nth-child(3) {
  animation-delay: .36s;
}

@keyframes synchro-efectivo-deposito {
  0%, 12%, 100% {
    transform: translateX(-50%) translateY(0) scale(.96);
    opacity: 1;
  }
  70%, 90% {
    transform: translateX(-50%) translateY(120px) scale(.78);
    opacity: .35;
  }
}

@keyframes synchro-acercar-pantalla {
  0%, 12%, 100% {
    transform: translateX(-50%) translateY(0) scale(.88);
    opacity: .92;
  }
  65%, 85% {
    transform: translateX(-50%) translateY(92px) scale(1.05);
    opacity: 1;
  }
}

@keyframes synchro-onda-nfc {
  0%, 100% {
    opacity: .25;
    transform: rotate(45deg) scale(.82);
  }
  50% {
    opacity: 1;
    transform: rotate(45deg) scale(1.12);
  }
}

.pantalla-pago.tema-oscuro .depositador-efectivo,
.pantalla-pago.tema-oscuro .pantalla-cobro-v3 {
  box-shadow: 0 14px 28px rgba(0,0,0,.28);
}

.pantalla-pago.tema-oscuro .icono-pago-v3 {
  filter: drop-shadow(0 12px 14px rgba(0,0,0,.24));
}

@media (prefers-reduced-motion: reduce) {
  .icono-efectivo-v3,
  .icono-tarjeta-v3,
  .icono-nfc-v3,
  .ondas-nfc-v3 span {
    animation: none !important;
  }
}

/* SYNCHRO-RETIRO-FINAL-V4
   Ajuste visual del resumen confirmado y retiro completado.
   No modifica la logica de cobro ni las animaciones. */

.pantalla-pago.retiro-para-finalizar,
.pantalla-pago.retiro-terminado {
  display: flex;
  align-items: center;
  justify-content: center;
  padding:
    clamp(12px, 2dvh, 22px)
    clamp(12px, 2.4vw, 28px);
}

.pantalla-pago.retiro-para-finalizar .contenido-pago,
.pantalla-pago.retiro-terminado .contenido-pago {
  width: min(100%, 820px);
  max-width: none;
  margin: auto;
  gap: clamp(10px, 1.6dvh, 19px);
}

.retiro-para-finalizar .contenido-pago > header,
.retiro-terminado .contenido-pago > header {
  gap: 7px;
  text-align: center;
}

.retiro-para-finalizar .tarjeta-resumen,
.retiro-terminado .tarjeta-resumen {
  width: 100%;
  padding:
    clamp(15px, 2dvh, 24px)
    clamp(20px, 3vw, 36px);
  border-radius: 23px;
}

.retiro-para-finalizar .dato,
.retiro-terminado .dato {
  min-height: clamp(48px, 6.5dvh, 68px);
  padding: 9px 0;
}

.retiro-para-finalizar .total,
.retiro-terminado .total {
  min-height: clamp(60px, 8dvh, 82px);
}

.retiro-para-finalizar .estado,
.retiro-terminado .estado {
  min-height: 43px;
  padding: 9px 15px;
}

.retiro-para-finalizar .nota,
.retiro-terminado .nota {
  margin: 0;
}

.retiro-para-finalizar .boton-principal,
.retiro-terminado .boton-principal {
  min-height: 54px;
}

/* Confirmacion final: tarjeta amplia y equilibrada. */
.retiro-terminado .tarjeta-resumen {
  display: flex;
  flex-direction: column;
  justify-content: center;
  min-height: clamp(190px, 30dvh, 290px);
}

.retiro-terminado .tarjeta-resumen::before {
  content: "\2713";
  display: grid;
  place-items: center;
  align-self: center;
  flex-shrink: 0;

  width: 70px;
  height: 70px;
  margin-bottom: 12px;

  border-radius: 50%;
  background: #e0f7ef;
  color: #078b70;
  font-size: 42px;
  font-weight: 800;
}

.retiro-terminado .total strong {
  font-size: clamp(23px, 3vw, 34px);
}

.retiro-terminado.tema-oscuro
.tarjeta-resumen::before {
  background: #16483e;
  color: #a8f7d1;
}

/* Ajuste para pantallas de poca altura. */
@media (max-height: 700px) {
  .pantalla-pago.retiro-para-finalizar,
  .pantalla-pago.retiro-terminado {
    padding: 10px 12px;
  }

  .retiro-para-finalizar .contenido-pago,
  .retiro-terminado .contenido-pago {
    gap: 8px;
  }

  .retiro-para-finalizar .tarjeta-resumen,
  .retiro-terminado .tarjeta-resumen {
    padding: 10px 18px;
  }

  .retiro-para-finalizar .dato,
  .retiro-terminado .dato {
    min-height: 43px;
    padding: 7px 0;
  }

  .retiro-para-finalizar .total,
  .retiro-terminado .total {
    min-height: 54px;
  }

  .retiro-terminado .tarjeta-resumen {
    min-height: 165px;
  }

  .retiro-terminado .tarjeta-resumen::before {
    width: 52px;
    height: 52px;
    margin-bottom: 5px;
    font-size: 32px;
  }

  .retiro-para-finalizar .boton-principal,
  .retiro-terminado .boton-principal {
    min-height: 48px;
  }
}


/* SYNCHRO-RETIRO-TITULO-V5
   Titulo a la misma altura que "Coloca tus objetos".
   Solo cambia la distribucion visual en tablet vertical. */

@media (orientation: portrait)
and (min-width: 600px)
and (min-height: 720px) {

  .pantalla-pago.retiro-para-finalizar,
  .pantalla-pago.retiro-terminado {
    align-items: stretch;
    justify-content: flex-start;
    padding-top: 46px;
  }

  .pantalla-pago.retiro-para-finalizar .contenido-pago,
  .pantalla-pago.retiro-terminado .contenido-pago {
    position: relative;
    display: flex;
    flex-direction: column;
    justify-content: center;

    width: min(100%, 820px);
    height: 100%;
    margin: 0 auto;

    padding-top: clamp(80px, 10dvh, 115px);
  }

  .pantalla-pago.retiro-para-finalizar
  .contenido-pago > header,
  .pantalla-pago.retiro-terminado
  .contenido-pago > header {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    width: 100%;
    gap: 7px;
    text-align: center;
  }

  /* Las otras pantallas no llevan una etiqueta
     encima del titulo principal. */
  .retiro-para-finalizar .contenido-pago > header .etiqueta,
  .retiro-terminado .contenido-pago > header .etiqueta {
    display: none;
  }

  .retiro-para-finalizar .contenido-pago > header h1,
  .retiro-terminado .contenido-pago > header h1 {
    margin: 0;
    font-size: clamp(29px, 3vw, 41px);
    font-weight: 750;
    line-height: 1.18;
    letter-spacing: -0.025em;
  }
}


/* SYNCHRO-RESUMEN-CONSUMO-V4 */

.pantalla-pago .tarjeta-resumen.detalle-consumo-v4 {
  padding: clamp(22px, 2.7vw, 34px);
}

.consumo-encabezado {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px 16px;
  margin-bottom: 18px;
  padding-bottom: 15px;
  border-bottom: 1px solid #e0ebef;
}

.consumo-encabezado span {
  color: #008b9e;
  font-size: clamp(12px, 1.4vw, 15px);
  font-weight: 500;
  letter-spacing: 1px;
}

.consumo-encabezado strong {
  color: #01305c;
  font-size: clamp(15px, 1.7vw, 19px);
  font-weight: 600;
}

.consumo-cuadricula {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: clamp(9px, 1.4vw, 14px);
}

.consumo-dato {
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 7px;
  min-width: 0;
  min-height: 86px;
  padding: 13px 15px;
  border: 1px solid #dcebf0;
  border-radius: 13px;
  background: #f5fafc;
}

.consumo-dato span,
.consumo-tarifa span {
  color: #526b7c;
  font-size: clamp(13px, 1.5vw, 16px);
  font-weight: 600;
  line-height: 1.3;
}

.consumo-dato strong,
.consumo-tarifa strong {
  color: #01305c;
  font-size: clamp(17px, 2vw, 23px);
  font-weight: 500;
  line-height: 1.2;
  overflow-wrap: anywhere;
}

.consumo-tarifa {
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px 16px;
  margin-top: 15px;
  padding: 14px 0;
  border-bottom: 1px solid #e0ebef;
}

.pantalla-pago .detalle-consumo-v4 .dato.total {
  margin-top: 12px;
  min-height: 90px;
  border-bottom: 0;
}

.pantalla-pago.tema-oscuro .consumo-encabezado,
.pantalla-pago.tema-oscuro .consumo-tarifa {
  border-color: #42647e;
}

.pantalla-pago.tema-oscuro .consumo-dato {
  background: #163f5d;
  border-color: #42647e;
}

.pantalla-pago.tema-oscuro .consumo-dato strong,
.pantalla-pago.tema-oscuro .consumo-tarifa strong,
.pantalla-pago.tema-oscuro .consumo-encabezado strong {
  color: #ffffff;
}

.pantalla-pago.tema-oscuro .consumo-dato span,
.pantalla-pago.tema-oscuro .consumo-tarifa span {
  color: #d4e2ea;
}

.pantalla-pago.tema-oscuro .consumo-encabezado span {
  color: #5ec6e8;
}

.synchro-app .pantalla-pago.vista-synchro {
  overflow-x: hidden;
  overflow-y: auto;
}

@media (max-width: 480px) {
  .pantalla-pago .tarjeta-resumen.detalle-consumo-v4 {
    padding: 16px;
  }

  .consumo-dato {
    min-height: 78px;
    padding: 10px;
  }

  .consumo-dato strong {
    font-size: 16px;
  }
}

@media (max-height: 650px) and (orientation: landscape) {
  .consumo-encabezado {
    margin-bottom: 10px;
    padding-bottom: 10px;
  }

  .consumo-dato {
    min-height: 65px;
    padding: 9px 12px;
    gap: 4px;
  }

  .pantalla-pago .detalle-consumo-v4 .dato.total {
    min-height: 70px;
  }
}


/* SYNCHRO-RESUMEN-ALTO-V5 */

.pantalla-pago .tarjeta-resumen.resumen-alto-v5 {
  display: flex;
  flex-direction: column;
  width: 100%;
  min-height: clamp(490px, 62dvh, 630px);
  padding: clamp(20px, 2.8vw, 32px)
           clamp(22px, 3.4vw, 38px);
}

.pantalla-pago .resumen-alto-v5 .resumen-intro-v5 {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px 14px;
  min-height: 45px;
  border-bottom: 1px solid #dce8ed;
}

.resumen-intro-v5 span {
  color: #008b9e;
  font-size: clamp(12px, 1.4vw, 15px);
  font-weight: 500;
  letter-spacing: .7px;
}

.resumen-intro-v5 strong {
  color: #01305c;
  font-size: clamp(14px, 1.5vw, 18px);
  font-weight: 600;
}

.pantalla-pago .resumen-alto-v5 .dato {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex: 1 0 auto;
  gap: 12px;
  min-height: clamp(52px, 6dvh, 74px);
  padding: 11px 0;
  border-bottom: 1px solid #e0ebef;
}

.pantalla-pago .resumen-alto-v5 .dato span {
  font-size: clamp(14px, 1.7vw, 18px);
}

.pantalla-pago .resumen-alto-v5 .dato strong {
  font-size: clamp(17px, 2vw, 24px);
  text-align: right;
}

.pantalla-pago .resumen-alto-v5 .dato-casillero strong {
  font-size: clamp(24px, 2.8vw, 32px);
}

.pantalla-pago .resumen-alto-v5 .dato.total {
  flex: 0 0 auto;
  min-height: 85px;
  border-bottom: 0;
}

.pantalla-pago .resumen-alto-v5 .total span {
  font-size: clamp(16px, 1.8vw, 21px);
}

.pantalla-pago .resumen-alto-v5 .total strong {
  color: #008b9e;
  font-size: clamp(31px, 4vw, 46px);
}

.pantalla-pago.tema-oscuro .resumen-intro-v5 {
  border-bottom-color: #42647e;
}

.pantalla-pago.tema-oscuro .resumen-intro-v5 span,
.pantalla-pago.tema-oscuro .resumen-alto-v5 .total strong {
  color: #5ec6e8;
}

.pantalla-pago.tema-oscuro .resumen-intro-v5 strong {
  color: #ffffff;
}

@media (max-width: 480px) {
  .pantalla-pago .tarjeta-resumen.resumen-alto-v5 {
    padding: 18px 20px;
  }

  .pantalla-pago .resumen-alto-v5 .dato strong {
    font-size: 17px;
  }

  .pantalla-pago .resumen-alto-v5 .total strong {
    font-size: 34px;
  }
}

@media (max-height: 650px) and (orientation: landscape) {
  .pantalla-pago .tarjeta-resumen.resumen-alto-v5 {
    min-height: 0;
  }

  .pantalla-pago .resumen-alto-v5 .dato {
    min-height: 45px;
    padding: 8px 0;
  }
}




/* SYNCHRO - tarjeta final de retiro */

.pantalla-pago .retiro-final {
  width: min(100%, 780px);
  gap: clamp(12px, 2dvh, 22px);
}

.pantalla-pago .retiro-final .tarjeta-final {
  display: flex;
  flex-direction: column;
  justify-content: space-around;
  gap: 6px;
  min-height: clamp(490px, 60dvh, 640px);
  padding: clamp(22px, 3vw, 38px);
}

.pantalla-pago .retiro-final .tarjeta-final .dato {
  flex: 1 1 auto;
  min-height: 62px;
  padding: 12px 0;
}

.pantalla-pago .retiro-final .tarjeta-final .dato.total {
  min-height: 82px;
  border-bottom: 0;
}

.pantalla-pago .retiro-final .tarjeta-final .total strong {
  color: #008b9e;
  font-size: clamp(30px, 3.6vw, 44px);
}

.sello-final {
  position: relative;
  align-self: center;
  flex: 0 0 auto;
  width: clamp(86px, 12dvh, 125px);
  height: clamp(86px, 12dvh, 125px);
  border: 2px solid #a9dcd3;
  border-radius: 50%;
  background: #e9f8f2;
}

.sello-final img {
  position: absolute;
  inset: 12%;
  width: 76%;
  height: 76%;
  object-fit: contain;
}

.pantalla-pago.tema-oscuro .sello-final {
  background: #18494c;
  border-color: #5ec6e8;
}

.pantalla-pago.tema-oscuro .retiro-final .total strong {
  color: #5ec6e8;
}

@media (max-height: 650px) {
  .pantalla-pago .retiro-final .tarjeta-final {
    min-height: 350px;
    gap: 3px;
  }

  .pantalla-pago .retiro-final .tarjeta-final .dato {
    min-height: 48px;
    padding: 8px 0;
  }

  .sello-final {
    width: 68px;
    height: 68px;
  }
}



/* Animacion solo en vista previa, sin alterar el cobro operativo. */
.pago-preview-animado .contenido-pago {
  animation: synchro-paso-preview .55s cubic-bezier(.2,.65,.2,1) both;
}
@keyframes synchro-paso-preview {
  from { opacity: 0; transform: translateY(9px); }
  to { opacity: 1; transform: translateY(0); }
}
@media (prefers-reduced-motion: reduce) {
  .pago-preview-animado .contenido-pago { animation: none; }
}
</style>
