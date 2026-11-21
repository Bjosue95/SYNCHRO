<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import SelectorMetodoPago from "../components/pagos/SelectorMetodoPago.vue";
import {
  obtenerOrdenRetiroPagado,
  obtenerMetodoPagoRetiro,
  limpiarRetiro
} from "../services/retiroSesion";
import {
  finalizarRetiroPersonalPagado,
  consultarEstadoCobroRetiroPersonal
} from "../services/retiroFacialManualApi";

const router = useRouter();
const { oscuro } = useTema();

// Datos de muestra solo para visualizar el diseño.
// No hay cobro ni autorizacion de retiro.
const props = defineProps({
  ordenEjemplo: {
    type: Object,
    default: null
  }
});

const esVistaPrevia =
  import.meta.env.DEV && props.ordenEjemplo !== null;

const orden = ref(
  esVistaPrevia
    ? props.ordenEjemplo
    : obtenerOrdenRetiroPagado()
);
const verificando = ref(false);
const completado = ref(false);
const pagoConfirmado = ref(false);
const mensaje = ref("");
const casilleroFinal = ref("");
const metodoSeleccionado = ref("");
const pasoMetodo = ref(false);
const cambiandoMetodo = ref(false);

// Preferencia visual; solo el backend y el operador confirman pagos.
const metodoRetiro = esVistaPrevia ? "" : obtenerMetodoPagoRetiro();
const indicacionesRetiro = {
  Efectivo: "Pago en efectivo confirmado.",
  Tarjeta: "Pago con tarjeta confirmado.",
  NFC: "El pago NFC aún no está habilitado. Elige otro método."
};
const indicacionRetiro = indicacionesRetiro[metodoRetiro] ||
  "Solicita asistencia al operador para continuar.";

const instrucciones = {
  Efectivo: {
    titulo: "Pago en efectivo",
    texto: "Introduce el efectivo en el recolector."
  },
  Tarjeta: {
    titulo: "Pago con tarjeta",
    texto: "Acerca tu tarjeta a la pantalla."
  },
  NFC: {
    titulo: "Pago sin contacto",
    texto: "Acerca tu teléfono a la pantalla."
  }
};

const instruccionActual = computed(
  () => instrucciones[metodoSeleccionado.value] || null
);

function avanzarVistaPrevia() {
  if (!esVistaPrevia || !metodoSeleccionado.value) return;
  pasoMetodo.value = !pasoMetodo.value;
}

const importe = computed(() => {
  const valor = orden.value?.importeHnl;
  return Number.isFinite(valor)
    ? new Intl.NumberFormat("es-HN", {
        style: "currency",
        currency: "HNL"
      }).format(valor)
    : "No disponible";
});

let temporizadorPago = null;
let consultaEnCurso = false;
let vistaDesmontada = false;

async function actualizarEstadoPago() {
  if (
    esVistaPrevia ||
    !orden.value ||
    pagoConfirmado.value ||
    consultaEnCurso ||
    vistaDesmontada
  ) return;

  consultaEnCurso = true;

  try {
    const datos = await consultarEstadoCobroRetiroPersonal(
      orden.value.autorizacionPago
    );

    if (vistaDesmontada) return;

    mensaje.value = "";

    if (datos.pagoConfirmado === true) {
      pagoConfirmado.value = true;
      window.clearInterval(temporizadorPago);
      temporizadorPago = null;
    }
  } catch (error) {
    if (vistaDesmontada) return;

    mensaje.value = error instanceof Error
      ? error.message
      : "No se pudo consultar el pago.";

    if (/vencida|expirad/i.test(mensaje.value)) {
      mensaje.value += " Repite la verificacion facial y del PIN.";
      window.clearInterval(temporizadorPago);
      temporizadorPago = null;
    }
  } finally {
    consultaEnCurso = false;
  }
}
onMounted(() => {
  if (!orden.value) {
    router.replace("/retirar/rostro");
    return;
  }

  if (esVistaPrevia) return;

  if (!metodoRetiro) {
    router.replace("/retirar/pago");
    return;
  }

  void actualizarEstadoPago();

  temporizadorPago = window.setInterval(() => {
    void actualizarEstadoPago();
  }, 6000);
});

onBeforeUnmount(() => {
  vistaDesmontada = true;

  if (temporizadorPago !== null) {
    window.clearInterval(temporizadorPago);
    temporizadorPago = null;
  }

  // Ninguna autorizacion queda guardada al abandonar la vista.
  if (!completado.value && !esVistaPrevia && !cambiandoMetodo.value) {
    limpiarRetiro();
  }
});

async function comprobarPago() {
  // La vista previa nunca llama al backend.
  if (esVistaPrevia) return;
  if (!pagoConfirmado.value || !orden.value || verificando.value || completado.value) return;

  verificando.value = true;
  mensaje.value = "";

  try {
    // El servidor exige un pago previamente confirmado
    // por un operador autorizado.
    const resultado = await finalizarRetiroPersonalPagado(
      orden.value.cobroId,
      orden.value.autorizacionPago
    );

    casilleroFinal.value = resultado.codigoCasillero;
    completado.value = true;

    limpiarRetiro();

    mensaje.value =
      "Retiro confirmado. En este prototipo, abre manualmente " +
      "el casillero asignado.";
  } catch (error) {
    const detalle = error instanceof Error
      ? error.message
      : "No se pudo comprobar el pago.";

    mensaje.value =
      `${detalle} Si el pago aun no ha sido confirmado, ` +
      "solicita ayuda al operador. La autorizacion temporal " +
      "puede vencer y requerir una nueva verificacion.";
  } finally {
    verificando.value = false;
  }
}

async function cambiarMetodo() {
  if (esVistaPrevia || completado.value || verificando.value) return;
  cambiandoMetodo.value = true;
  try {
    await router.push("/retirar/pago");
  } catch (error) {
    cambiandoMetodo.value = false;
    mensaje.value = "No fue posible volver a elegir el método de pago.";
  }
}

function finalizar() {
  if (esVistaPrevia) return;
  limpiarRetiro();
  router.replace("/menu");
}
</script>

<template>
  <main class="pantalla-pago" :class="{ 'tema-oscuro': oscuro, 'vista-previa-pago': esVistaPrevia }">
    <!-- Solo mostrar el resultado si el backend confirmó el retiro pagado. -->
    <section v-if="completado && !esVistaPrevia" class="contenido-pago" aria-live="polite">
      <header>
        <span class="etiqueta">SYNCHRO · RETIRO PERSONAL</span>
        <h1>Retiro completado</h1>
        <p>El pago fue confirmado y el retiro quedó registrado.</p>
      </header>
      <div class="tarjeta-resumen">
        <div class="dato">
          <span>Casillero</span>
          <strong>{{ casilleroFinal }}</strong>
        </div>
        <div class="dato total">
          <span>Estado</span>
          <strong>Disponible</strong>
        </div>
      </div>
      <p class="estado completado"><span class="punto"></span>Retiro autorizado por SYNCHRO</p>
      <p class="nota">En este prototipo, abre manualmente el casillero asignado para recoger tus objetos.</p>
      <button class="boton-principal" type="button" @click="finalizar">
        <span>Finalizar</span>
        <span class="icono-continuar" aria-hidden="true">
          <img src="/iconos/complementarios/29_continuar.svg" class="icono-claro" alt="" />
          <img src="/iconos/complementarios/29_continuar-oscuro.svg" class="icono-oscuro" alt="" />
        </span>
      </button>
    </section>

    <!-- SYNCHRO: animacion de pago antes de la confirmacion -->
    <section
      v-else-if="orden && !esVistaPrevia && !pagoConfirmado"
      class="contenido-pago espera-animada"
      aria-live="polite"
    >
      <header>
        <span class="etiqueta">SYNCHRO · PAGO</span>
        <h1>
          {{
            metodoRetiro === "Efectivo"
              ? "Introduce el efectivo"
              : metodoRetiro === "Tarjeta"
                ? "Coloca tu tarjeta"
                : "Acerca tu teléfono"
          }}
        </h1>
        <p>
          {{
            metodoRetiro === "Efectivo"
              ? "Deposita el importe en el recolector."
              : metodoRetiro === "Tarjeta"
                ? "Acerca tu tarjeta a la pantalla."
                : "Acerca el teléfono al lector sin contacto."
          }}
        </p>
      </header>

      <!-- SYNCHRO-PAGO-V3 -->
      <div class="escenario-cobro escena-pago-synchro" aria-hidden="true">
        <template v-if="metodoRetiro === 'Efectivo'">
          <div class="escena-efectivo-synchro">
            <img
              :src="`/iconos/pagos/efectivo${oscuro ? '-oscuro' : ''}.svg`"
              class="icono-pago-v3 icono-efectivo-v3"
              alt=""
            />

            <div class="depositador-efectivo">
              <div class="ranura-deposito"></div>
              <div class="panel-deposito"></div>
            </div>
          </div>
        </template>

        <template v-else-if="metodoRetiro === 'Tarjeta'">
          <div class="escena-tarjeta-synchro">
            <div class="pantalla-cobro-v3">
              <div class="panel-cobro-v3"></div>
            </div>

            <img
              :src="`/iconos/pagos/tarjeta${oscuro ? '-oscuro' : ''}.svg`"
              class="icono-pago-v3 icono-tarjeta-v3"
              alt=""
            />
          </div>
        </template>

        <template v-else>
          <div class="escena-nfc-synchro">
            <div class="pantalla-cobro-v3">
              <div class="panel-cobro-v3"></div>
            </div>

            <img
              :src="`/iconos/pagos/nfc${oscuro ? '-oscuro' : ''}.svg`"
              class="icono-pago-v3 icono-nfc-v3"
              alt=""
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
        Esperando confirmación del pago
      </div>

      <p v-if="mensaje" class="mensaje" role="alert">
        {{ mensaje }}
      </p>

      <p v-if="metodoRetiro === 'NFC'" class="nota">
        NFC requiere habilitación administrativa adicional.
      </p>

      <p class="nota">
        El casillero permanecerá ocupado
        hasta que SYNCHRO autorice el retiro.
      </p>
    </section>

    <section v-else-if="orden" class="contenido-pago">
      <header>
        <span v-if="!esVistaPrevia" class="etiqueta">SYNCHRO · RETIRO PERSONAL</span>
        <h1>
          {{
            esVistaPrevia
              ? (pasoMetodo ? instruccionActual?.titulo : "Selecciona cómo pagar")
              : (completado ? "Retiro completado" : "Resumen de tu consumo")
          }}
        </h1>
        <p v-if="!completado && !esVistaPrevia">
          El pago fue confirmado.
          Revisa tu consumo antes de finalizar el retiro.
        </p>
      </header>

      <div class="tarjeta-resumen">
        <div class="dato">
          <span>Casillero</span>
          <strong>{{ orden.codigoCasillero }}</strong>
        </div>
        <div class="dato">
          <span>{{ esVistaPrevia ? "Tiempo" : "Tiempo registrado" }}</span>
          <strong>{{ orden.minutosUtilizados }} {{ esVistaPrevia ? "min" : "minuto(s)" }}</strong>
        </div>
        <div class="dato total">
          <span>{{ esVistaPrevia ? "Total" : "Importe" }}</span>
          <strong>{{ importe }}</strong>
        </div>
        <p v-if="orden.descripcionTarifa" class="descripcion">
          {{ orden.descripcionTarifa }}
        </p>
      </div>

      <SelectorMetodoPago
        v-if="esVistaPrevia && !pasoMetodo"
        v-model="metodoSeleccionado"
        :oscuro="oscuro"
      />

      <section
        v-if="esVistaPrevia && pasoMetodo && instruccionActual"
        class="paso-pago-preview"
        aria-live="polite"
      >
        <p>{{ instruccionActual.texto }}</p>
      </section>

      <p v-if="!esVistaPrevia" class="estado">
        <span class="punto"></span>
        Pago confirmado por el administrador
      </p>

      <p v-if="completado" class="estado completado">
        <span class="punto"></span>
        Casillero {{ casilleroFinal }} disponible
      </p>

      <p v-if="mensaje" class="mensaje" role="status">
        {{ mensaje }}
      </p>

      <button
        class="boton-principal"
        data-preview-permitido
        type="button"
        :disabled="verificando || (esVistaPrevia && !metodoSeleccionado)"
        @click="esVistaPrevia ? avanzarVistaPrevia() : (metodoRetiro === 'NFC' ? cambiarMetodo() : comprobarPago())"
      >
        <span>
          {{
            esVistaPrevia
              ? (pasoMetodo ? "Cambiar método" : "Continuar")
              : metodoRetiro === "NFC"
                ? "Cambiar método"
                : verificando
                  ? "Comprobando..."
                  : "Finalizar retiro"
          }}
        </span>

        <span class="icono-continuar" aria-hidden="true">
          <img
            src="/iconos/complementarios/29_continuar.svg"
            class="icono-claro"
            alt=""
          />
          <img
            src="/iconos/complementarios/29_continuar-oscuro.svg"
            class="icono-oscuro"
            alt=""
          />
        </span>
      </button>

      <p v-if="!esVistaPrevia" class="nota">
        Método: <strong>{{ metodoRetiro }}</strong>.
        {{ indicacionRetiro }}
        <template v-if="metodoRetiro !== 'NFC'">
          Revisa el resumen y pulsa «Finalizar retiro».
        </template>
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
  font-weight: 600;
  line-height: 1.35;
}

.dato strong {
  min-width: 0;

  color: #01305c;
  font-size: clamp(19px, 2.2vw, 25px);
  font-weight: 800;
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
  font-weight: 800;
}

.total strong {
  color: #008b9e;
  font-size: clamp(30px, 3.7vw, 43px);
  font-weight: 800;
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
</style>