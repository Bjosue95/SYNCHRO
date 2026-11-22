<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";

import {
  obtenerEspacioRetiroCarga,
  obtenerTokenFacialRetiroCarga,
  obtenerTokenCobroRetiroCarga,
  guardarOrdenCobroRetiroCarga,
  guardarMetodoPagoRetiroCarga
} from "../services/retiroCargaSesion.js";

import {
  verificarPinRetiroCarga,
  prepararCobroRetiroCarga
} from "../services/retiroCargaApi.js";

const router = useRouter();
const { oscuro } = useTema();
const propsVistaPrevia = defineProps({
  modoVistaPrevia: { type: Boolean, default: false }
});
const esVistaPrevia = import.meta.env.DEV && propsVistaPrevia.modoVistaPrevia;

const pin = ref("");
const mensaje = ref("");
const procesando = ref(false);
// La autorizacion PIN se reutiliza solo ante fallo al preparar la orden.
// No se presenta una pantalla intermedia tras verificarlo.
const pinComprobado = ref(false);
let temporizadorDemo = null;

const completo = computed(() =>
  (pin.value.length === 6 || pinComprobado.value) &&
  !procesando.value
);

const teclas = [
  "1", "2", "3",
  "4", "5", "6",
  "7", "8", "9",
  "limpiar", "0", "borrar"
];

function pulsar(valor) {
  if (procesando.value || pinComprobado.value) return;

  mensaje.value = "";

  if (valor === "limpiar") {
    pin.value = "";
    return;
  }

  if (valor === "borrar") {
    pin.value = pin.value.slice(0, -1);
    return;
  }

  if (/^[0-9]$/.test(valor) && pin.value.length < 6) {
    pin.value += valor;
  }
}

function tecladoFisico(evento) {
  if (evento.ctrlKey || evento.altKey ||
      evento.metaKey || evento.repeat) return;

  if (
    evento.target?.isContentEditable ||
    ["INPUT", "TEXTAREA"].includes(evento.target?.tagName)
  ) return;

  if (/^[0-9]$/.test(evento.key)) {
    evento.preventDefault();
    pulsar(evento.key);
  } else if (evento.key === "Backspace") {
    evento.preventDefault();
    pulsar("borrar");
  } else if (evento.key === "Delete") {
    evento.preventDefault();
    pulsar("limpiar");
  }
}

async function verificar() {
  if (esVistaPrevia) {
    if (!completo.value || procesando.value) return;
    procesando.value = true;
    temporizadorDemo = window.setTimeout(() => {
      pin.value = "";
      procesando.value = false;
      router.push("/vista-previa/retiro-carga-pago");
    }, 750);
    return;
  }
  if (!completo.value || procesando.value) return;
  if (!pinComprobado.value && !obtenerTokenFacialRetiroCarga()) {
    mensaje.value = "La autorizacion facial vencio. Repite el escaneo.";
    pin.value = "";
    return;
  }

  procesando.value = true;
  mensaje.value = "";
  try {
    // Si falla la preparacion se reintenta sin volver a consumir el PIN.
    if (!pinComprobado.value) {
      await verificarPinRetiroCarga(pin.value);
      pinComprobado.value = true;
    }
    const orden = await prepararCobroRetiroCarga();
    guardarOrdenCobroRetiroCarga(orden);
    if (orden.pagoSimulado === true) {
      if (!["Efectivo", "Tarjeta", "NFC"].includes(orden.metodoPago)) {
        throw new Error("El cobro confirmado no tiene un metodo valido.");
      }
      guardarMetodoPagoRetiroCarga(orden.metodoPago);
      await router.replace("/retirar/dispositivo/pago/resumen");
    } else {
      await router.replace("/retirar/dispositivo/pago");
    }
  } catch (e) {
    mensaje.value = e?.message || "No se pudo verificar el PIN.";
    if (pinComprobado.value && !obtenerTokenCobroRetiroCarga()) {
      await router.replace("/retirar/dispositivo");
    }
  } finally {
    pin.value = "";
    procesando.value = false;
  }
}

onMounted(() => {
  if (esVistaPrevia) return;
  if (!obtenerTokenFacialRetiroCarga() && !obtenerTokenCobroRetiroCarga()) {
    router.replace("/retirar/dispositivo");
    return;
  }

  window.addEventListener("keydown", tecladoFisico);
});

onBeforeUnmount(() => {
  window.clearTimeout(temporizadorDemo);
  window.removeEventListener("keydown", tecladoFisico);
  pin.value = "";
});
</script>

<template>
  <main
    ref="pantallaRef"
    class="pantalla-pin"
    :class="{ 'tema-oscuro': oscuro }"
  >
    <section class="pin-contenido">

      <header ref="encabezadoRef" class="pin-encabezado">
        <h1>Introduce tu PIN</h1>
        <p v-if="!pinComprobado">
          Ingresa el PIN de 6 dígitos que creaste al iniciar la carga.
        </p>
      </header>

      <div
        v-if="!pinComprobado || procesando"
        class="indicadores"
        role="group"
        :aria-label="`${pin.length} de 6 digitos ingresados`"
      >
        <span
          v-for="n in 6"
          :key="n"
          class="indicador"
          :class="{
            lleno: pin.length >= n,
            actual: pin.length === n - 1
          }"
          aria-hidden="true"
        ></span>
      </div>

      <div
        v-if="!pinComprobado || procesando"
        class="teclado"
        role="group"
        aria-label="Teclado numerico"
      >
        <button
          v-for="tecla in teclas"
          :key="tecla"
          type="button"
          class="tecla"
          data-preview-permitido
          :class="{
            utilidad:
              tecla === 'limpiar' ||
              tecla === 'borrar'
          }"
          :aria-label="
            tecla === 'limpiar'
              ? 'Limpiar todos los digitos'
              : tecla === 'borrar'
                ? 'Borrar ultimo digito'
                : tecla
          "
          @click="pulsar(tecla)"
        >
          <span
            v-if="tecla === 'limpiar'"
            class="contenido-limpiar"
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M4 7h16" />
              <path d="M9 4h6" />
              <path d="M6 7l1 13h10l1-13" />
              <path d="M10 11v6" />
              <path d="M14 11v6" />
            </svg>

            <span>Limpiar</span>
          </span>

          <svg
            v-else-if="tecla === 'borrar'"
            class="icono-borrar"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M21 4H9L2 12l7 8h12V4Z" />
            <path d="m12 9 6 6m0-6-6 6" />
          </svg>

          <span v-else class="numero">
            {{ tecla }}
          </span>
        </button>
      </div>

      <footer class="acciones">
        <button
          type="button"
          class="boton-ir-casilleros"
          :disabled="!completo"
          data-preview-permitido
          @click="verificar"
        >
          <span>{{ procesando ? "Continuando..." : pinComprobado ? "Reintentar cobro" : "Verificar PIN" }}</span>

          <span class="icono-ir" aria-hidden="true">
            <img
              src="/iconos/complementarios/29_continuar.svg"
              class="ir-claro"
              alt=""
            />
            <img
              src="/iconos/complementarios/29_continuar-oscuro.svg"
              class="ir-oscuro"
              alt=""
            />
          </span>
        </button>

        <p
          v-if="mensaje"
          class="mensaje"
          role="status"
        >
          {{ mensaje }}
        </p>
      </footer>
    </section>

    <span v-if="!pinComprobado || procesando" class="solo-lectura" aria-live="polite">
      {{ pin.length }} de 6 d&iacute;gitos ingresados.
    </span>
  </main>
</template>

<style scoped>
/* SYNCHRO-PIN-CSS-LIMPIO-V8
   Un solo sistema de dimensiones, con suelo y techo mas
   flexibles. --alto-cabecera se mide en JS (ResizeObserver)
   para que el calculo del teclado refleje el alto real de
   la cabecera en cada orientacion.
*/

.pantalla-pin,
.pantalla-pin * {
  box-sizing: border-box;
}

/* Pantalla */

.pantalla-pin {
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;

  /* Fallback si JS no llega a medir (por ejemplo en SSR). */
  --alto-cabecera: 112px;

  padding: clamp(16px, 3dvh, 40px)
           clamp(12px, 2.5vw, 28px)
           18px;

  background: #f2f7fa;
  color: #01305c;
}

.pantalla-pin.vista-synchro {
  overflow-x: hidden;
  overflow-y: auto;
  overscroll-behavior: contain;
}

/* Contenedor central */

.pin-contenido {
  display: flex;
  flex-direction: column;
  align-items: center;

  width: min(100%, 650px);
  margin: 0 auto;
  padding: 0 0 12px;
}

/* Titulo */

.pin-encabezado {
  display: flex;
  flex-direction: column;
  align-items: center;

  width: 100%;
  gap: 6px;
  text-align: center;
}

.pin-encabezado h1 {
  margin: 0;

  font-size: clamp(30px, 5vw, 42px);
  font-weight: 700;
  line-height: 1.18;
  color: inherit;
}

.pin-encabezado p {
  margin: 0;

  font-size: clamp(15px, 2.2vw, 20px);
  line-height: 1.35;
  color: #5b7282;

  text-align: center;
}

/* Los seis indicadores del PIN */

.indicadores {
  display: flex;
  align-items: center;
  justify-content: center;

  gap: clamp(10px, 2.5vw, 18px);
  margin-top: clamp(12px, 2dvh, 20px);
}

.indicador {
  display: block;
  flex-shrink: 0;

  width: clamp(22px, 3vw, 28px);
  height: clamp(22px, 3vw, 28px);

  border: 2px solid #86a8b8;
  border-radius: 50%;
  background: transparent;

  transition:
    background-color 150ms ease,
    border-color 150ms ease;
}

.indicador.actual {
  border-color: #00b9c7;
}

.indicador.lleno {
  background: #01305c;
  border-color: #01305c;
}

/* TECLADO
   UNICA DECLARACION DE DIAMETRO (portrait).

   Suelo 48px (antes 68px) y techo 132px (antes 144px).
   La cabecera se descuenta con su altura real medida en JS.
   La reserva inferior cubre indicadores + margen del teclado
   + 3 separaciones + acciones + paddings.
*/

.teclado {
  --diametro: clamp(
    48px,
    min(
      31vw,
      calc(
        (
          100dvh -
          var(--alto-cabecera, 112px) -
          clamp(220px, 28dvh, 320px)
        ) / 4
      )
    ),
    132px
  );

  --separacion: clamp(6px, 1.2vw, 12px);

  display: grid;
  grid-template-columns: repeat(3, var(--diametro));
  grid-template-rows: repeat(4, var(--diametro));

  gap: var(--separacion);

  width: max-content;
  max-width: 100%;

  margin-top: clamp(12px, 2dvh, 22px);
}

/* Botones circulares */

.tecla {
  display: grid;
  place-items: center;

  width: var(--diametro);
  height: var(--diametro);

  padding: 0;
  overflow: hidden;

  container-type: inline-size;

  border: 2px solid #a1cede;
  border-radius: 50%;

  background: #ffffff;
  color: #01305c;

  font: inherit;
  font-weight: 500;

  box-shadow: 0 5px 12px rgba(1, 48, 92, .07);

  cursor: pointer;
  touch-action: manipulation;
  user-select: none;

  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    transform 120ms ease;
}

/* Numeros proporcionales al diametro.
   Minimo 18px (antes 24px) para que quepan en botones de 48px. */

.numero {
  display: block;

  font-size: clamp(18px, 32cqw, 52px);
  font-weight: 500;
  font-variant-numeric: tabular-nums;
  line-height: 1;
  white-space: nowrap;
}

.tecla:active {
  background: #c9eef3;
  transform: scale(.96);
}

/* Limpiar */

.tecla.utilidad {
  background: #f8fbfd;
  color: #0990a3;
}

.contenido-limpiar {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  width: 100%;
  min-width: 0;
  gap: 2px;

  font-size: clamp(8px, 11cqw, 15px);
  font-weight: 700;
  line-height: 1.1;
  white-space: nowrap;
}

.contenido-limpiar svg {
  display: block;
  flex-shrink: 0;

  width: clamp(12px, 19cqw, 28px);
  height: clamp(12px, 19cqw, 28px);
}

/* Borrar */

.icono-borrar {
  display: block;

  width: clamp(13px, 27cqw, 38px);
  height: clamp(13px, 27cqw, 38px);
}

/* Acciones */

.acciones {
  display: flex;
  flex-direction: column;
  align-items: center;

  width: 100%;
  gap: 9px;

  margin-top: clamp(12px, 2dvh, 20px);
}

.boton-ir-casilleros {
  display: inline-flex;
  align-items: center;
  justify-content: center;

  gap: 12px;

  width: max-content;
  max-width: 100%;
  min-width: clamp(190px, 23vw, 250px);
  min-height: clamp(52px, 7vh, 64px);

  padding: 9px 22px;

  border: 2px solid #00b9c7;
  border-radius: 15px;

  background: #f4feff;
  color: #01305c;

  font: inherit;
  font-size: clamp(16px, 1.7vw, 21px);
  font-weight: 800;
  line-height: 1.2;
  text-align: center;

  box-shadow:
    0 4px 0 #00a5b2,
    0 7px 16px rgba(1, 48, 92, .10);

  cursor: pointer;
  touch-action: manipulation;
}

.boton-ir-casilleros:disabled {
  background: #edf3f5;
  color: #647784;
  border-color: #b5cad3;
  box-shadow: none;
  cursor: not-allowed;
}

.boton-ir-casilleros:active:not(:disabled) {
  transform: translateY(3px) scale(.99);
}

/* Icono original del boton */

.icono-ir {
  position: relative;
  display: block;

  width: 40px;
  height: 40px;
  flex: 0 0 40px;
}

.icono-ir img {
  position: absolute;
  inset: 0;

  display: block;
  width: 100%;
  height: 100%;

  object-fit: contain;
}

.ir-claro {
  opacity: 1;
}

.ir-oscuro {
  opacity: 0;
}

/* Mensajes */

.mensaje {
  width: min(100%, 380px);

  margin: 0;
  padding: 10px;

  border-radius: 9px;

  background: #e5f3f7;
  color: #01305c;

  font-size: 13px;
  line-height: 1.4;
  text-align: center;
  overflow-wrap: anywhere;
}

/* Modo oscuro */

.pantalla-pin.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .pin-encabezado p {
  color: #d4e2ea;
}

.tema-oscuro .indicador {
  border-color: #9bb7c9;
}

.tema-oscuro .indicador.actual {
  border-color: #5ec6e8;
}

.tema-oscuro .indicador.lleno {
  background: #ffffff;
  border-color: #ffffff;
}

.tema-oscuro .tecla {
  background: #103956;
  color: #ffffff;
  border-color: #42647e;
}

.tema-oscuro .tecla.utilidad {
  background: #103956;
  color: #5ec6e8;
}

.tema-oscuro .tecla:active {
  background: #245a77;
}

.tema-oscuro .boton-ir-casilleros:not(:disabled) {
  background: #123d5b;
  color: #ffffff;
  border-color: #5ec6e8;

  box-shadow:
    0 4px 0 #087d93,
    0 7px 16px rgba(0, 0, 0, .22);
}

.tema-oscuro .boton-ir-casilleros:disabled {
  background: #254054;
  color: #c0d0da;
  border-color: #426984;
  box-shadow: none;
}

.tema-oscuro .ir-claro {
  opacity: 0;
}

.tema-oscuro .ir-oscuro {
  opacity: 1;
}

.tema-oscuro .mensaje {
  background: #204766;
  color: #ffffff;
}

/* Tablet vertical alta */

@media (orientation: portrait) and (min-width: 600px) and (min-height: 900px) {
  .pantalla-pin {
    padding-top: 46px;
  }
}

/* Horizontal: se redefine SOLO el diametro y la reserva.
   Suelo 44px (mas bajo que en portrait) y techo 118px.
   El resto de tamanos tipograficos y margenes se reducen
   como antes. */

@media (orientation: landscape) {
  .pantalla-pin {
    padding: 10px 12px 12px;
  }

  .pin-encabezado {
    gap: 3px;
  }

  .pin-encabezado h1 {
    font-size: clamp(24px, 2.8vw, 36px);
  }

  .pin-encabezado p {
    font-size: clamp(12px, 1.4vw, 16px);
  }

  .indicadores {
    margin-top: 8px;
    gap: clamp(7px, 1.5vw, 14px);
  }

  .indicador {
    width: clamp(16px, 2vw, 21px);
    height: clamp(16px, 2vw, 21px);
  }

  .teclado {
    --diametro: clamp(
      44px,
      min(
        28vw,
        calc(
          (
            100dvh -
            var(--alto-cabecera, 60px) -
            clamp(140px, 19dvh, 200px)
          ) / 4
        )
      ),
      118px
    );

    margin-top: 10px;
    --separacion: clamp(5px, 1dvh, 9px);
  }

  .acciones {
    margin-top: 10px;
    gap: 5px;
  }

  .boton-ir-casilleros {
    min-height: 46px;
    padding: 6px 14px;
    font-size: clamp(14px, 1.7vw, 18px);
  }

  .icono-ir {
    width: 28px;
    height: 28px;
    flex-basis: 28px;
  }
}

/* Pantallas extremadamente bajas */

@media (max-height: 550px) {
  .pantalla-pin {
    padding-top: 8px;
  }

  .teclado {
    margin-top: 7px;
    --separacion: 5px;
  }

  .acciones {
    margin-top: 7px;
  }
}

/* Interaccion */

@media (hover: hover) {
  .tecla:hover {
    background: #e8f9fa;
    border-color: #00b9c7;
  }

  .tema-oscuro .tecla:hover {
    background: #194b68;
    border-color: #5ec6e8;
  }
}

.tecla:focus-visible,
.boton-ir-casilleros:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

@media (prefers-reduced-motion: reduce) {
  .pantalla-pin *,
  .pantalla-pin {
    transition: none;
  }

  .tecla:active,
  .boton-ir-casilleros:active {
    transform: none;
  }
}

/* Accesibilidad */

.solo-lectura {
  position: absolute;

  width: 1px;
  height: 1px;

  margin: -1px;
  padding: 0;

  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
</style>