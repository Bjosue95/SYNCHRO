<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import SelectorMetodoPago from "../components/pagos/SelectorMetodoPago.vue";
import {
  obtenerOrdenRetiroPagado,
  limpiarRetiro
} from "../services/retiroSesion";
import {
  finalizarRetiroPersonalPagado
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
const mensaje = ref("");
const casilleroFinal = ref("");
const metodoSeleccionado = ref("");
const pasoMetodo = ref(false);

const instrucciones = {
  Efectivo: {
    titulo: "Pago en efectivo",
    texto: "Introduce el efectivo en el recolector."
  },
  Tarjeta: {
    titulo: "Pago con tarjeta",
    texto: "Inserta o acerca tu tarjeta."
  },
  NFC: {
    titulo: "Pago sin contacto",
    texto: "Acerca tu dispositivo al lector."
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

onMounted(() => {
  if (!orden.value) {
    router.replace("/retirar/rostro");
  }
});

onBeforeUnmount(() => {
  // Ninguna autorizacion queda guardada al abandonar la vista.
  if (!completado.value && !esVistaPrevia) limpiarRetiro();
});

async function comprobarPago() {
  // La vista previa nunca llama al backend.
  if (esVistaPrevia) return;
  if (!orden.value || verificando.value || completado.value) return;

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

function finalizar() {
  if (esVistaPrevia) return;
  limpiarRetiro();
  router.replace("/menu");
}
</script>

<template>
  <main class="pantalla-pago" :class="{ 'tema-oscuro': oscuro, 'vista-previa-pago': esVistaPrevia }">
    <section class="contenido-pago" v-if="orden">
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
          Tu PIN ha sido verificado. El casillero permanece cerrado
          hasta confirmar el pago.
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

      <p v-if="!completado && !esVistaPrevia" class="estado">
        <span class="punto"></span>
        Pago pendiente de confirmación administrativa
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
        @click="esVistaPrevia ? avanzarVistaPrevia() : (completado ? finalizar() : comprobarPago())"
      >
        <span>
          {{
            esVistaPrevia
              ? (pasoMetodo ? "Cambiar método" : "Continuar")
              : completado
                ? "Finalizar"
                : verificando
                  ? "Comprobando..."
                  : "Verificar pago y retirar"
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

      <p v-if="!completado && !esVistaPrevia" class="nota">
        Este prototipo utiliza pagos simulados.
        La tablet no puede confirmar pagos por sí misma.
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
</style>