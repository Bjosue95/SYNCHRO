<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import {
  confirmarRetiroFacialConPin,
  prevalidarRetiroFacialConPin,
  prepararCobroRetiroPersonal
} from "../services/retiroFacialManualApi";
import {
  obtenerRetiro,
  limpiarRetiro,
  registrarOrdenRetiroPagado
} from "../services/retiroSesion";

const router = useRouter();
const { oscuro } = useTema();

const propsVistaPrevia = defineProps({
  modoVistaPrevia: {
    type: Boolean,
    default: false
  }
});

const pin = ref("");
const mensaje = ref("");
const procesando = ref(false);
const terminado = ref(false);

// Autorizacion de cobro temporal: solo memoria, nunca URL o storage.
const autorizacionPagoPendiente = ref(null);
let autorizacionGeneradaEn = 0;

const completo = computed(() =>
  (pin.value.length === 6 || Boolean(autorizacionPagoPendiente.value)) &&
  !procesando.value &&
  !terminado.value
);

const teclas = [
  "1", "2", "3",
  "4", "5", "6",
  "7", "8", "9",
  "limpiar", "0", "borrar"
];

function pulsar(valor) {
  if (procesando.value || terminado.value) return;
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
  if (
    evento.ctrlKey ||
    evento.altKey ||
    evento.metaKey ||
    evento.repeat
  ) return;

  const elemento = evento.target;

  if (
    elemento?.isContentEditable ||
    ["INPUT", "TEXTAREA"].includes(elemento?.tagName)
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
  if (!completo.value || procesando.value || terminado.value) return;

  // El servidor autoriza este pago durante cinco minutos.
  // Usamos un margen local de cuatro minutos para reintentos.
  if (
    autorizacionPagoPendiente.value &&
    Date.now() - autorizacionGeneradaEn >= 4 * 60 * 1000
  ) {
    autorizacionPagoPendiente.value = null;
    autorizacionGeneradaEn = 0;
    limpiarRetiro();
    pin.value = "";
    mensaje.value =
      "La autorizacion temporal del pago expiro. " +
      "Es necesario iniciar nuevamente la verificacion.";
    return;
  }

  const tokenFacial = obtenerRetiro().autorizacionFacial;

  if (!tokenFacial && !autorizacionPagoPendiente.value) {
    pin.value = "";
    mensaje.value =
      "No se encontro una autorizacion facial en esta sesion. " +
      "Inicia nuevamente el proceso de retiro.";
    limpiarRetiro();
    return;
  }

  procesando.value = true;
  mensaje.value = "";

  try {
    // SYNCHRO-TABLET-PAGO-OPTATIVO-V1
    // Activar solamente cuando backend, tarifas y operador
    // esten preparados. No se modifica el diseno del PIN.
    if (import.meta.env.VITE_RETIRO_PAGO_HABILITADO === "true") {
      // Validar rostro y PIN una sola vez.
      // Si el cobro falla, reutilizar solo la autorizacion
      // de pago temporal que el servidor ya emitio.
      let autorizacionPago = autorizacionPagoPendiente.value;

      if (!autorizacionPago) {
        const respuesta = await prevalidarRetiroFacialConPin(
          pin.value,
          tokenFacial
        );

        if (
          typeof respuesta?.autorizacionPago !== "string" ||
          !respuesta.autorizacionPago
        ) {
          throw new Error(
            "El servidor no entrego la autorizacion del cobro."
          );
        }

        autorizacionPago = respuesta.autorizacionPago;
        autorizacionPagoPendiente.value = autorizacionPago;
        autorizacionGeneradaEn = Date.now();
      }

      try {
        const orden = await prepararCobroRetiroPersonal(
          autorizacionPago
        );

        registrarOrdenRetiroPagado(
          autorizacionPago,
          orden
        );

        autorizacionPagoPendiente.value = null;
        autorizacionGeneradaEn = 0;

        await router.replace("/retirar/pago");
        return;
      } catch (error) {
        const detalle = error instanceof Error
          ? error.message
          : "No se pudo preparar el cobro.";

        throw new Error(
          `${detalle} El PIN ya fue validado. ` +
          "Puedes reintentar el cobro sin repetir el escaneo " +
          "mientras la autorizacion siga vigente."
        );
      }
    }

    // Compatibilidad temporal con el modo sin cobro.
    const resultado = await confirmarRetiroFacialConPin(
      pin.value,
      tokenFacial
    );

    terminado.value = true;
    limpiarRetiro();

    mensaje.value =
      `Retiro registrado correctamente. Casillero: ${resultado.codigoCasillero}. ` +
      "En este prototipo, abre manualmente el casillero asignado.";
  } catch (error) {
    mensaje.value = error instanceof Error
      ? error.message
      : "No se pudo verificar el PIN.";
  } finally {
    pin.value = "";
    procesando.value = false;
  }
}

onMounted(() => {
  if (import.meta.env.DEV && propsVistaPrevia.modoVistaPrevia) return;
  if (!obtenerRetiro().capturaHecha) {
    router.replace("/retirar/rostro");
    return;
  }

  window.addEventListener("keydown", tecladoFisico);
});

onBeforeUnmount(() => {
  window.removeEventListener("keydown", tecladoFisico);
  pin.value = "";
});
</script>

<template>
  <main
    class="pantalla-pin"
    :class="{ 'tema-oscuro': oscuro }"
  >
    <section class="pin-contenido">

      <header class="pin-encabezado">
        <h1>{{ terminado ? "Retiro completado" : "Introduce tu PIN" }}</h1>
        <p v-if="!terminado">
          Ingresa los 6 d&iacute;gitos recibidos por WhatsApp.
        </p>
      </header>

      <div
        v-if="!terminado"
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
        v-if="!terminado"
        class="teclado"
        role="group"
        aria-label="Teclado numerico"
      >
        <button
          v-for="tecla in teclas"
          :key="tecla"
          type="button"
          class="tecla"
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
          :disabled="!terminado && !completo"
          @click="terminado ? router.replace('/menu') : verificar()"
        >
          <span>{{ terminado ? "Finalizar" : autorizacionPagoPendiente ? "Reintentar cobro" : "Verificar PIN" }}</span>

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
          v-if="mensaje" :style="{ order: terminado ? -1 : 0 }"
          class="mensaje"
          role="status"
        >
          {{ mensaje }}
        </p>
      </footer>
    </section>

    <span v-if="!terminado" class="solo-lectura" aria-live="polite">
      {{ pin.length }} de 6 d&iacute;gitos ingresados.
    </span>
  </main>
</template>

<style scoped>
.pantalla-pin,
.pantalla-pin * {
  box-sizing: border-box;
}

/* Superficie */

.pantalla-pin {
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;

  overflow-x: hidden;
  overflow-y: auto;

  padding:
    clamp(16px, 2.6dvh, 26px)
    clamp(12px, 2.4vw, 28px)
    18px;

  background: #f2f7fa;
  color: #01305c;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

/* Contenido central */

.pin-contenido {
  display: flex;
  flex-direction: column;
  align-items: center;

  width: min(100%, 480px);
  margin: 0 auto;
  padding: 0;
}

/* Titulo */

.pin-encabezado {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;

  width: 100%;
  text-align: center;
}

.pin-encabezado h1 {
  margin: 0;

  color: inherit;
  font-size: clamp(24px, 3vw, 34px);
  font-weight: 700;
  line-height: 1.18;
}

.pin-encabezado p {
  margin: 0;

  color: #5b7282;
  font-size: clamp(12px, 1.5vw, 16px);
  line-height: 1.4;

  transition: color 150ms ease;
}

/* Seis puntos de PIN */

.indicadores {
  display: flex;
  align-items: center;
  justify-content: center;

  gap: clamp(13px, 3vw, 20px);

  margin-top: clamp(14px, 2.4dvh, 20px);
}

.indicador {
  display: block;
  flex-shrink: 0;

  width: 15px;
  height: 15px;

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

/* Teclado circular
   La anchura depende del viewport.
   La altura disponible limita el diametro.
   Los botones nunca se estiran verticalmente. */

.teclado {
  --diametro: clamp(
    78px,
    min(24vw, calc((100dvh - 334px) / 4)),
    160px
  );

  --separacion: clamp(7px, 1.8vw, 13px);

  display: grid;
  grid-template-columns: repeat(3, var(--diametro));
  grid-template-rows: repeat(4, var(--diametro));

  gap: var(--separacion);

  width: max-content;
  max-width: 100%;

  margin-top: clamp(18px, 2.8dvh, 24px);
}

.tecla {
  display: grid;
  place-items: center;

  width: var(--diametro);
  height: var(--diametro);

  padding: 0;

  border: 2px solid #a1cede;
  border-radius: 50%;

  background: #ffffff;
  color: #01305c;

  font: inherit;
  font-size: clamp(32px, 5vw, 48px);
  font-weight: 500;

  box-shadow:
    0 5px 12px rgba(1,48,92,.07);

  cursor: pointer;
  touch-action: manipulation;
  user-select: none;

  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    color 150ms ease,
    transform 120ms ease;
}

.numero {
  line-height: 1;
}

.tecla:active {
  background: #c9eef3;
  transform: scale(.96);
}

/* Limpiar y Borrar circulares */

.tecla.utilidad {
  background: #f8fbfd;
  color: #0990a3;
}

.contenido-limpiar {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  gap: 4px;

  font-size: clamp(13px, 2vw, 19px);
  font-weight: 700;
  line-height: 1.1;
}

.contenido-limpiar svg {
  display: block;

  width: clamp(25px, 5vw, 39px);
  height: clamp(25px, 5vw, 39px);
}

.icono-borrar {
  display: block;

  width: clamp(32px, 6vw, 45px);
  height: clamp(32px, 6vw, 45px);
}

@media (hover: hover) {
  .tecla:hover {
    background: #e8f9fa;
    border-color: #00b9c7;
  }
}

/* Verificar PIN:
   apariencia aprobada de SeleccionTamanoView.vue */

.acciones {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 9px;

  width: 100%;
  margin-top: clamp(16px, 2.3dvh, 23px);
}

.boton-ir-casilleros {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 12px;

  width: max-content;
  max-width: 100%;
  min-width: clamp(190px, 23vw, 250px);
  min-height: clamp(54px, 7vh, 64px);

  padding: 10px 24px;

  border: 2px solid #00b9c7;
  border-radius: 15px;

  background: #f4feff;
  color: #01305c;

  font: inherit;
  font-size: clamp(17px, 1.7vw, 21px);
  font-weight: 800;
  line-height: 1.2;

  box-shadow:
    0 4px 0 #00a5b2,
    0 7px 16px rgba(1,48,92,.10);

  cursor: pointer;
  touch-action: manipulation;

  transition:
    background-color 150ms ease,
    color 150ms ease,
    border-color 150ms ease,
    box-shadow 150ms ease,
    transform 150ms ease;
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
  box-shadow: 0 1px 0 #00a5b2;
}

/* Icono Continuar original */

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
  transition: opacity 150ms ease;
}

.ir-claro { opacity: 1; }
.ir-oscuro { opacity: 0; }

/* Mensajes */

.mensaje {
  width: min(100%, 380px);
  margin: 0;
  padding: 10px;

  border-radius: 9px;

  background: #e5f3f7;
  color: #01305c;

  font-size: 13px;
  text-align: center;
}

.tecla:focus-visible,
.boton-ir-casilleros:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 4px;
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

  box-shadow:
    0 5px 12px rgba(0,0,0,.13);
}

.tema-oscuro .tecla.utilidad {
  background: #103956;
  color: #5ec6e8;
}

.tema-oscuro .tecla:active {
  background: #245a77;
}

@media (hover: hover) {
  .tema-oscuro .tecla:hover {
    background: #194b68;
    border-color: #5ec6e8;
  }
}

/* Boton original en oscuro */

.tema-oscuro .boton-ir-casilleros:not(:disabled) {
  background: #123d5b;
  color: #ffffff;
  border-color: #5ec6e8;

  box-shadow:
    0 4px 0 #087d93,
    0 7px 16px rgba(0,0,0,.22);
}

.tema-oscuro .boton-ir-casilleros:disabled {
  background: #254054;
  color: #c0d0da;
  border-color: #426984;
  box-shadow: none;
}

.tema-oscuro .ir-claro { opacity: 0; }
.tema-oscuro .ir-oscuro { opacity: 1; }

.tema-oscuro .mensaje {
  background: #204766;
  color: #ffffff;
}

/* Ventanas de poca altura */

@media (max-height: 700px) {
  .pantalla-pin {
    padding-top: 10px;
  }

  .teclado {
    --diametro: clamp(
      62px,
      min(23vw, calc((100dvh - 290px) / 4)),
      104px
    );

    --separacion: 7px;

    margin-top: 12px;
  }

  .indicadores {
    margin-top: 10px;
  }

  .acciones {
    margin-top: 11px;
  }

  .icono-ir {
    width: 30px;
    height: 30px;
    flex-basis: 30px;
  }

  .boton-ir-casilleros {
    min-height: 48px;
    padding: 5px 14px;
  }
}

/* Tablet vertical alta: titulo alineado con el menu, sin reducir
   el tamaño de los botones del teclado. Se recupera la altura extra
   en las separaciones entre encabezado, indicadores, teclado y acciones. */
@media (orientation: portrait) and (min-width: 600px) and (min-height: 900px) {
  .pantalla-pin {
    padding-top: 46px;
  }

  .indicadores {
    margin-top: 12px;
  }

  .teclado {
    margin-top: 16px;
  }

  .acciones {
    margin-top: 19px;
  }
}

/* Movimiento reducido */

@media (prefers-reduced-motion: reduce) {
  .pantalla-pin,
  .pin-encabezado p,
  .indicador,
  .tecla,
  .boton-ir-casilleros,
  .icono-ir img {
    transition: none !important;
  }

  .tecla:active,
  .boton-ir-casilleros:active {
    transform: none;
  }
}

/* Lectores de pantalla */

.solo-lectura {
  position: absolute;
  width: 1px;
  height: 1px;

  padding: 0;
  margin: -1px;

  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}
</style>