<script setup>
import {
  copiarRostroCargaV3,
  borrarRostroCargaV3
} from "../services/rostroCargaTemporal.js";
import { computed, onMounted, onBeforeUnmount, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import {
  prepararCargaSegura
} from "../services/cargaSesionSeguraApi.js";
import {
  copiarDescriptorV3,
  borrarDescriptorV3
} from "../services/descriptorFacialV3Temporal.js";
import { registrarPasoCargaDemo } from "../services/pasoCargaDemostracion.js";
import {
  verificacionCargaValida,
  limpiarVerificacionCarga
} from "../services/verificacionCargaTemporal.js";

const route = useRoute();
const router = useRouter();
const { oscuro } = useTema();
const propsVistaPrevia = defineProps({
  modoVistaPrevia: { type: Boolean, default: false },
  pasoEjemplo: { type: String, default: "crear" }
});
const esVistaPrevia = import.meta.env.DEV && propsVistaPrevia.modoVistaPrevia;

const paso = ref("crear");
const pin = ref("");
const primerPin = ref("");
const error = ref("");

const datos = computed(() => ({
  espacio: String(route.query.espacio ?? ""),
  dispositivo: String(route.query.dispositivo ?? ""),
  modalidad: String(
    route.query.modalidad ??
    (route.query.dispositivo === "Laptop" ? "Laptop" : "")
  )
}));

const teclas = [
  "1", "2", "3",
  "4", "5", "6",
  "7", "8", "9",
  "limpiar", "0", "borrar"
];

const registrandoCarga = ref(false);
let temporizadorVistaPrevia = null;

const titulo = computed(() => {
  if (paso.value === "crear") return "Crea tu PIN de retiro";
  if (paso.value === "confirmar") return "Confirma tu PIN";
  return "PIN comprobado";
});

function pulsar(valor) {
  if (paso.value === "listo" || registrandoCarga.value) return;

  error.value = "";

  if (valor === "limpiar") {
    pin.value = "";
  } else if (valor === "borrar") {
    pin.value = pin.value.slice(0, -1);
  } else if (/^[0-9]$/.test(valor) && pin.value.length < 6) {
    pin.value += valor;
  }
}

function avanzar() {
  if (pin.value.length !== 6) return;

  if (paso.value === "crear") {
    primerPin.value = pin.value;
    pin.value = "";
    paso.value = "confirmar";
    return;
  }

  if (pin.value !== primerPin.value) {
    pin.value = "";
    primerPin.value = "";
    paso.value = "crear";
    error.value = "Los PIN no coinciden. Crea uno nuevamente.";
    return;
  }

  if (esVistaPrevia) {
    pin.value = "";
    primerPin.value = "";
    paso.value = "listo";
    temporizadorVistaPrevia = window.setTimeout(() => {
      router.push({
        path: "/vista-previa/carga-pin-confirmado",
        query: { ...route.query, recorrido: "1" }
      });
    }, 550);
    return;
  }

  // Conservar el PIN solo durante la peticion HTTPS.
  const pinElegido = primerPin.value;

  pin.value = "";
  primerPin.value = "";

  void registrarCargaReal(pinElegido);
}

async function registrarCargaReal(pinElegido) {
  if (registrandoCarga.value) return;

  registrandoCarga.value = true;
  error.value = "";

  const descriptor = copiarRostroCargaV3();

  try {
    if (!descriptor) {
      throw new Error(
        "La captura facial V3 vencio o no fue autorizada. " +
        "Regresa a Registro facial y repite el escaneo."
      );
    }

    const espacio = datos.value.espacio;

    if (!/^(W[12]|C[1-4]|L[12])$/.test(espacio)) {
      throw new Error("El espacio seleccionado no es valido.");
    }

    const dispositivo = /^L/.test(espacio)
      ? "Laptop"
      : "Celular";

    const modalidad = /^W/.test(espacio)
      ? "Inalambrica"
      : /^C/.test(espacio)
        ? "Cable"
        : "Laptop";

    // El backend guarda el PIN protegido y el rostro cifrado.
    // La tablet no guarda ninguno de los dos.
    await prepararCargaSegura({
      codigoEspacio: espacio,
      dispositivo,
      modalidad,
      pin: pinElegido,
      descriptor
    });

    // Marcador de navegacion, no autorizacion de seguridad.
    if (!registrarPasoCargaDemo(datos.value)) {
      throw new Error(
        "No se pudo preparar la siguiente pantalla."
      );
    }

    borrarDescriptorV3();
    borrarRostroCargaV3();

    paso.value = "listo";
    irCondiciones();
  } catch (e) {
    paso.value = "crear";
    error.value = e?.message ||
      "No se pudo registrar la sesion de carga.";
  } finally {
    if (descriptor) descriptor.fill(0);
    pinElegido = "";
    registrandoCarga.value = false;
  }
}
function irCondiciones() {
  if (paso.value !== "listo" || esVistaPrevia) return;

  // El PIN no se transmite ni se almacena desde esta vista.
  // Su registro seguro requiere el endpoint de carga.
  limpiarVerificacionCarga();

  router.push({
    path: "/cargar/pin-confirmado",
    query: datos.value
  });
}

function regresar() {
  limpiarVerificacionCarga();

  router.replace({
    path: "/cargar/rostro",
    query: datos.value
  });
}

onMounted(() => {
  if (esVistaPrevia) {
    paso.value = propsVistaPrevia.pasoEjemplo === "confirmar"
      ? "confirmar" : "crear";
    return;
  }
  if (!verificacionCargaValida(datos.value)) {
    regresar();
  }
});

onBeforeUnmount(() => {
  window.clearTimeout(temporizadorVistaPrevia);
  pin.value = "";
  primerPin.value = "";
});
</script>

<template>
  <main class="pantalla-pin" :class="{ 'tema-oscuro': oscuro }">


    <section class="pin-contenido">
      <header class="pin-encabezado">
        <h1>{{ titulo }}</h1>
        <p v-if="paso === 'crear'">
          Elige un PIN de 6 d&iacute;gitos para retirar tu dispositivo.
        </p>
        <p v-else-if="paso === 'confirmar'">
          Introduce nuevamente los mismos 6 d&iacute;gitos.
        </p>
        <p v-else>
          Ambos PIN coinciden.
        </p>
      </header>

      <div
        v-if="paso !== 'listo'"
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
        v-if="paso !== 'listo'"
        class="teclado"
        aria-label="Teclado numerico"
      >
        <button
          v-for="tecla in teclas"
          :key="tecla"
          type="button"
          class="tecla"
          data-preview-permitido
          :class="{ utilidad: tecla === 'limpiar' || tecla === 'borrar' }"
          @click="pulsar(tecla)"
        >
          <span v-if="tecla === 'limpiar'" class="contenido-limpiar">
            <svg viewBox="0 0 24 24" fill="none"
              stroke="currentColor" stroke-width="2"
              stroke-linecap="round" stroke-linejoin="round"
              aria-hidden="true">
              <path d="M4 7h16M9 4h6M6 7l1 13h10l1-13M10 11v6M14 11v6" />
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
            aria-label="Borrar"
          >
            <path d="M21 4H9L2 12l7 8h12V4Z" />
            <path d="m12 9 6 6m0-6-6 6" />
          </svg>

          <span v-else class="numero">{{ tecla }}</span>
        </button>
      </div>

      <footer class="acciones">
        <button
          class="boton-ir-casilleros"
          type="button"
          :disabled="paso !== 'listo' && pin.length !== 6"
          data-preview-permitido
          @click="paso === 'listo' ? irCondiciones() : avanzar()"
        >
          <span>
            {{ paso === 'listo' ? 'Continuar' : 'Continuar' }}
          </span>

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

        <p v-if="error" class="mensaje" role="alert">
          {{ error }}
        </p>


      </footer>
    </section>
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

<style scoped>
.regresar-carga {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 48px;
  padding: 8px 14px;
  border: 1.5px solid #c7e2e9;
  border-radius: 13px;
  background: #e4f2f6;
  color: #01305c;
  font: inherit;
  font-size: 15px;
  font-weight: 700;
  cursor: pointer;
  margin-bottom: 12px;
}
.icono-regreso {
  width: 22px;
  height: 22px;
}
.tema-oscuro .regresar-carga {
  background: #204766;
  border-color: #42647e;
  color: #fff;
}
</style>