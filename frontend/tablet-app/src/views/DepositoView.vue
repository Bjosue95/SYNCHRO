<script setup>
import { computed, onMounted, onBeforeUnmount, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { obtenerReservaWebDeposito,limpiarReservaWebDeposito } from "../services/reservaWebSesion.js";
import { depositarReservaWeb } from "../services/reservasWebTerminalApi.js";
import {
  MINUTO_DEPOSITO_MS,
  segundosRestantesDeposito,
  extenderLimiteDeposito,
  formatearTiempoDeposito
} from "../utils/tiempoDeposito.js";

const route = useRoute();
const router = useRouter();
const { oscuro } = useTema();
const esReservaWeb = computed(()=>route.path==="/guardar/reserva/deposito");

const propsVistaPrevia = defineProps({
  modoVistaPrevia: {
    type: Boolean,
    default: false
  }
});

const claveIntento = "synchroWhatsappManualActivoV1";
const claveTiempo = "synchroDepositoInicioV1";

const casillero = computed(() => {
  const valor = esReservaWeb.value ? obtenerReservaWebDeposito()?.casillero : route.query.casillero;
  return typeof valor === "string" &&
    /^[A-Za-z0-9-]{1,10}$/.test(valor)
    ? valor
    : "";
});

const estado = ref("verificando");
const segundos = ref(60);
const ventanaSegundos = ref(60);

// SYNCHRO-RELOJ-RESPONSIVO
const avisoMinuto = ref(false);
const recargaReloj = ref(0);
const escalaTiempo = computed(() =>
  formatearTiempoDeposito(ventanaSegundos.value)
);


const textoTiempo = computed(() =>
  formatearTiempoDeposito(segundos.value)
);

const avanceReloj = computed(() =>
  452.39 * (
    1 - Math.min(
      1,
      segundos.value / Math.max(60, ventanaSegundos.value)
    )
  )
);
const confirmando = ref(false);
const error = ref("");

let intento = null;
let vivo = true;
let reloj = null;
let redireccion = null;
let reintento = null;
let verificando = false;
let limiteMs = 0;
let avisoTimer = null;

function limpiarRelojes() {
  clearInterval(reloj);
  clearTimeout(redireccion);
  clearTimeout(reintento);
  clearTimeout(avisoTimer);
  reloj = null;
  redireccion = null;
  reintento = null;
}

function limpiarSesion() {
  if(esReservaWeb.value){limpiarReservaWebDeposito();return;}
  sessionStorage.removeItem(claveIntento);
  sessionStorage.removeItem(claveTiempo);
}

async function solicitar(url, opciones = {}) {
  const respuesta = await fetch(url, {
    credentials: "same-origin",
    cache: "no-store",
    ...opciones,
  });

  const datos = await respuesta.json().catch(() => null);

  if (!respuesta.ok) {
    const fallo = new Error(
      datos?.mensaje ||
      `El servidor rechazó la solicitud (${respuesta.status}).`
    );
    fallo.status = respuesta.status;
    throw fallo;
  }

  return datos;
}

async function consultarEstado() {
  return solicitar(
    "/api/terminales/guardar/whatsapp/estado/" +
      encodeURIComponent(intento.intentoId),
    {
      headers: {
        "X-SYNCHRO-INTENTO": intento.tokenConsulta,
      },
    }
  );
}

function comprobarTiempo() {
  if (estado.value !== "listo") return;

  segundos.value = segundosRestantesDeposito(
    limiteMs,
    Date.now()
  );
}

function comenzarReloj() {
  if(esReservaWeb.value){
    limiteMs=Date.now()+MINUTO_DEPOSITO_MS;
    ventanaSegundos.value=60;segundos.value=60;
    clearInterval(reloj);
    reloj=setInterval(comprobarTiempo,250);
    return;
  }
  let guardado = null;

  try {
    guardado = JSON.parse(
      sessionStorage.getItem(claveTiempo) || "null"
    );
  } catch {
    sessionStorage.removeItem(claveTiempo);
  }

  const ahora = Date.now();

  if (
    guardado?.intentoId === intento.intentoId &&
    Number.isFinite(guardado?.limiteMs)
  ) {
    limiteMs = guardado.limiteMs;
  } else if (
    guardado?.intentoId === intento.intentoId &&
    Number.isFinite(guardado?.inicioMs) &&
    guardado.inicioMs <= ahora
  ) {
    // Compatibilidad con el temporizador anterior.
    limiteMs = guardado.inicioMs + MINUTO_DEPOSITO_MS;
  } else {
    limiteMs = ahora + MINUTO_DEPOSITO_MS;
  }

  sessionStorage.setItem(claveTiempo, JSON.stringify({
    intentoId: intento.intentoId,
    limiteMs
  }));

  ventanaSegundos.value = Math.max(
    60,
    segundosRestantesDeposito(limiteMs, ahora)
  );

  comprobarTiempo();
  clearInterval(reloj);
  reloj = setInterval(comprobarTiempo, 250);
}

function agregarMinuto() {
  if (
    !vivo ||
    estado.value !== "listo" ||
    confirmando.value ||
    !intento
  ) return;

  const ahora = Date.now();

  limiteMs = extenderLimiteDeposito(limiteMs, ahora);

  ventanaSegundos.value = segundosRestantesDeposito(
    limiteMs,
    ahora
  );

  if(!esReservaWeb.value)sessionStorage.setItem(claveTiempo, JSON.stringify({
    intentoId: intento.intentoId,
    limiteMs
  }));

  comprobarTiempo();

  recargaReloj.value += 1;
  avisoMinuto.value = true;

  clearTimeout(avisoTimer);
  avisoTimer = setTimeout(() => {
    if (vivo) avisoMinuto.value = false;
  }, 1500);
}

function finalizar() {
  if (!vivo || estado.value === "confirmado") return;

  limpiarRelojes();
  limpiarSesion();

  estado.value = "confirmado";
  error.value = "";

  redireccion = setTimeout(() => {
    if (vivo) router.replace("/menu");
  }, 2200);
}

async function verificarOperacion() {
  if (!vivo || verificando || confirmando.value) return;

  verificando = true;

  try {
    const datos = await consultarEstado();

    if (!vivo) return;

    if (datos?.estado === "Confirmado") {
      finalizar();
      return;
    }

    if (
      ["Vencido", "Cancelado", "Fallido"].includes(datos?.estado)
    ) {
      limpiarRelojes();
      estado.value = "error";
      error.value =
        "La operación ya no está disponible. Consulta al operador.";
      return;
    }

    if (
      datos?.reservaConfirmada !== true ||
      datos?.pinEntregado !== true
    ) {
      limpiarRelojes();
      estado.value = "error";
      error.value =
        "Todavía no se ha confirmado la autorización y el PIN.";
      return;
    }

    if (estado.value !== "listo") {
      estado.value = "listo";
      comenzarReloj();
    }

    error.value = "";
  } catch (e) {
    if (!vivo) return;

    if (e?.status === 429) {
      estado.value = "verificando";
      error.value = "";
      aviso.value =
        "El servidor está regulando las consultas. Comprobaremos la operación automáticamente.";

      clearTimeout(reintento);
      reintento = setTimeout(() => {
        if (vivo) verificarOperacion();
      }, 65000);

      return;
    }

    if (estado.value === "verificando") {
      estado.value = "error";
    }

    error.value =
      e?.message || "No se pudo consultar la operación.";
  } finally {
    verificando = false;
  }
}

async function cargarOperacion() {
  try {
    const guardado = JSON.parse(
      sessionStorage.getItem(claveIntento) || "null"
    );

    if (
      !guardado ||
      guardado.casillero !== casillero.value ||
      !/^[0-9a-fA-F-]{36}$/.test(guardado.intentoId || "") ||
      !/^[0-9a-fA-F]{64}$/.test(guardado.tokenConsulta || "") ||
      !Number.isFinite(guardado.creadoMs) ||
      guardado.creadoMs > Date.now() ||
      Date.now() - guardado.creadoMs > 45 * 60 * 1000
    ) {
      throw new Error(
        "No se encontró una operación válida para este casillero."
      );
    }

    intento = guardado;

    await verificarOperacion();

    // La confirmación POST vuelve a validar el estado en el servidor.
    // No se necesitan consultas continuas durante el depósito.
  } catch (e) {
    if (!vivo) return;

    estado.value = "error";
    error.value =
      e?.message || "No fue posible recuperar el depósito.";
  }
}

async function confirmarDeposito() {
  if (
    !vivo ||
    estado.value !== "listo" ||
    confirmando.value ||
    !intento
  ) return;

  confirmando.value = true;
  error.value = "";

  try {
    if(esReservaWeb.value){
      const sesion=obtenerReservaWebDeposito();
      if(!sesion||sesion.reservaId!==intento.reservaId)throw new Error("La reserva ha vencido. Verifica nuevamente tu PIN.");
      const datos=await depositarReservaWeb(sesion.reservaId,{
        pin:sesion.pin,
        codigoCasillero:sesion.casillero,
        depositoComprobado:true,
        captura:{imagenPng:sesion.foto,descriptor:sesion.descriptor,
           modelo:sesion.servicio==="Personal"?"SYNCHRO-HUMANJS-EMBEDDING-1024-v3":"SYNCHRO-CAPTURA-DEMOSTRATIVA",
           versionConsentimiento:sesion.servicio==="Personal"?"SYNCHRO-FACIAL-V3-2026-11":"SYNCHRO-CAPTURA-DEMOSTRATIVA-2026-11"}
      });
      if(datos?.depositoConfirmado!==true)throw new Error("El servidor no confirmó el depósito.");
      finalizar();
      return;
    }
    const datosCsrf = await solicitar(
      "/api/terminales/guardar/whatsapp/csrf-token"
    );

    if (!datosCsrf?.csrfToken) {
      throw new Error("No se obtuvo el token de seguridad.");
    }

    const datos = await solicitar(
      "/api/terminales/guardar/whatsapp/deposito/confirmar",
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": datosCsrf.csrfToken,
          "X-SYNCHRO-INTENTO": intento.tokenConsulta,
        },
        body: JSON.stringify({
          intentoId: intento.intentoId,
        }),
      }
    );

    if (
      datos?.depositoConfirmado !== true ||
      datos?.estadoCasillero !== "Ocupado"
    ) {
      throw new Error(
        "El servidor no confirmó el depósito."
      );
    }

    finalizar();
  } catch (e) {
    if (vivo) {
      error.value =
        e?.message || "No se pudo confirmar el depósito.";
    }
  } finally {
    confirmando.value = false;
  }
}

onMounted(() => {
  if (import.meta.env.DEV && propsVistaPrevia.modoVistaPrevia) {
    estado.value =
      route.query.vista === "confirmado"
        ? "confirmado"
        : "listo";

    segundos.value = 60;
    return;
  }

  if(esReservaWeb.value){
    intento=obtenerReservaWebDeposito();
    if(!intento){estado.value="error";error.value="Verifica la reserva y el PIN antes de depositar.";return;}
    estado.value="listo";
    comenzarReloj();
    return;
  }
  cargarOperacion();
});

onBeforeUnmount(() => {
  vivo = false;
  if(esReservaWeb.value&&estado.value!=="confirmado")limpiarReservaWebDeposito();
  limpiarRelojes();
});
</script>

<template>
  <main
    class="deposito-pantalla"
    :class="{ 'reserva-web': esReservaWeb, 'tema-oscuro': oscuro, 'deposito-en-curso': estado === 'listo', 'deposito-finalizado': estado === 'confirmado' }"
  >
    <div class="deposito-contenedor">

      <header class="deposito-encabezado">

        <h1>
          {{
            estado === "confirmado"
              ? "¡Depósito completado!"
              : "Coloca tus objetos"
          }}
        </h1>

        <div class="deposito-identificador">
          <span class="deposito-punto"></span>
          Casillero {{ casillero || "—" }}
        </div>
      </header>

      <section class="deposito-tarjeta" :class="{ 'en-deposito': estado === 'listo' }">

        <template v-if="estado === 'verificando'">
          <div class="deposito-cargando" aria-hidden="true"></div>

          <h2>Verificando tu operación</h2>

          <p class="deposito-descripcion" role="status">
            SYNCHRO está comprobando la autorización
            de tu casillero.
          </p>
        </template>

        <template v-else-if="estado === 'listo'">
          <div class="deposito-instruccion">
            <h2>Guarda tus objetos y cierra la puerta</h2>
          </div>

          <div class="deposito-reloj">
            <svg
              class="deposito-circulo"
              viewBox="0 0 176 176"
              aria-hidden="true"
            >
              <defs>
                <linearGradient
                  id="synchro-timer-gradient-v3"
                  x1="0%"
                  y1="0%"
                  x2="100%"
                  y2="100%"
                >
                  <stop offset="0%" stop-color="#5EC6E8" />
                  <stop offset="55%" stop-color="#00B9C7" />
                  <stop offset="100%" stop-color="#008C99" />
                </linearGradient>
              </defs>

              <circle
                cx="88"
                cy="88"
                r="84"
                fill="none"
                stroke="#5EC6E8"
                stroke-opacity="0.30"
                stroke-width="1.6"
                stroke-dasharray="2 7"
              />
              <defs>
                <linearGradient
                  id="synchro-gradiente-reloj"
                  x1="0%" y1="0%"
                  x2="100%" y2="100%"
                >
                  <stop offset="0%" stop-color="#5EC6E8" />
                  <stop offset="55%" stop-color="#00B9C7" />
                  <stop offset="100%" stop-color="#008C99" />
                </linearGradient>
              </defs>

              <circle
                class="deposito-halo"
                cx="88" cy="88" r="84"
                fill="none"
                stroke-width="1.5"
              />

              <circle
                class="deposito-pista"
                cx="88"
                cy="88"
                r="72"
                fill="none"
                stroke-width="10"
              />

              <circle
                class="deposito-avance"
                :class="{ 'deposito-final': segundos === 0 }"
                cx="88"
                cy="88"
                r="72"
                fill="none"
                stroke-width="10"
                stroke-linecap="round"
                :stroke-dasharray="452.39"
                :stroke-dashoffset="avanceReloj"
              />
            </svg>

            <Transition name="deposito-minuto">
              <span
                v-if="avisoMinuto"
                class="deposito-aviso-minuto"
                role="status"
              >
                +1:00
              </span>
            </Transition>

            <div
              class="deposito-tiempo"
              role="timer"
              aria-live="off"
            >
              <span :key="recargaReloj" class="deposito-digitos">
                {{ textoTiempo }}
              </span>

              <span class="deposito-unidad">
                tiempo restante
              </span>

              <span class="deposito-referencia">
                escala {{ escalaTiempo }}
              </span>
            </div>
          </div>


          <div class="deposito-acciones">
            <button
              class="deposito-boton-principal"
              type="button"
              :disabled="confirmando"
              :aria-busy="confirmando"
              @click="confirmarDeposito"
            >
              {{
                confirmando
                  ? "Confirmando depósito..."
                  : "Ya deposité"
              }}
            </button>

            <button
              class="deposito-boton-secundario"
              type="button"
              :disabled="confirmando"
              @click="agregarMinuto"
            >
              Agregar 1 minuto más
            </button>

            <span class="deposito-ayuda">
              Confirma solamente cuando ya hayas depositado.
            </span>
          </div>
        </template>

        <template v-else-if="estado === 'confirmado'">
          <div class="deposito-icono-exito" aria-hidden="true">
            <svg
              width="66%"
              height="66%"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M5 12.5l4.5 4.5L19 7" />
            </svg>
          </div>

          <h2>¡Todo listo!</h2>

          <p class="deposito-descripcion">
            Tu depósito se registró correctamente.
          </p>

          <p class="deposito-retorno" role="status">
            Regresando al menú principal...
          </p>
        </template>

        <template v-else>
          <div class="deposito-icono-error" aria-hidden="true">
            !
          </div>

          <h2>No se puede continuar</h2>

          <p class="deposito-descripcion">
            No se pudo comprobar la autorización.
            Tu depósito no ha sido confirmado.
          </p>

          <button
            class="deposito-boton-secundario"
            type="button"
            @click="esReservaWeb ? router.replace('/guardar/reserva') : (estado = 'verificando', error = '', verificarOperacion())"
          >
            Reintentar verificación
          </button>
        </template>

        <p v-if="error" class="deposito-mensaje-error" role="alert">
          {{ error }}
        </p>

      </section>
    </div>
  </main>
</template>

<style scoped>
/* SYNCHRO-DIGITAL-CIRCULAR-V3: reloj digital proporcional */

/* SYNCHRO-DEPOSITO-ESTILOS-LIMPIOS-V7
   Diseno unificado con GuardarView.vue.
   Sin reglas heredadas de versiones anteriores.
*/

/* ==============================
   1. SUPERFICIE
   ============================== */

.deposito-pantalla,
.deposito-pantalla * {
  box-sizing: border-box;
}

.deposito-pantalla {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);

  gap: clamp(8px, 1.4vh, 16px);

  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;

  padding:
    clamp(10px, 1.7vh, 20px)
    clamp(12px, 2.4vw, 28px);

  overflow-x: hidden;
  overflow-y: auto;

  background: #f2f7fa;
  color: #01305c;
}

/* Reserva la misma primera fila que utiliza
   el boton Regresar de GuardarView. */
.deposito-pantalla::before {
  content: "";
  display: block;
  grid-row: 1;
  height: 22px;
  pointer-events: none;
}

/* ==============================
   2. CONTENEDOR PRINCIPAL
   ============================== */

.deposito-contenedor {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);

  grid-row: 2;
  place-self: start center;

  gap: clamp(9px, 1.4vh, 16px);

  width: min(100%, 760px);
  height: min(100%, 630px);

  min-width: 0;
  min-height: 0;

  margin: 0;
  padding: 0;
}

/* ==============================
   3. TITULO
   ============================== */

.deposito-encabezado {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-start;

  gap: 8px;

  width: 100%;
  margin: 0;
  padding: 0;

  align-self: start;
  justify-self: center;

  text-align: center;
}

.deposito-encabezado h1 {
  margin: 0;
  padding: 0;

  color: inherit;
  font-size: clamp(21px, 2.8vw, 32px);
  font-weight: 750;
  line-height: 1.18;
  letter-spacing: -0.025em;
}

.deposito-identificador {
  display: inline-flex;
  align-items: center;
  justify-content: center;

  gap: 9px;
  min-height: 32px;
  padding: 5px 14px;

  border: 1px solid #b7e6e9;
  border-radius: 24px;

  background: #e5f8fa;
  color: #01305c;

  font-size: 13px;
  font-weight: 700;
}

.deposito-punto {
  display: block;

  width: 8px;
  height: 8px;
  flex-shrink: 0;

  border-radius: 50%;
  background: #00b9c7;
}

/* ==============================
   4. TARJETA
   ============================== */

.deposito-tarjeta {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  gap: clamp(12px, 2vh, 20px);

  width: 100%;
  min-width: 0;
  min-height: 0;

  padding: clamp(16px, 2.5vw, 30px);

  border: 2px solid #d8e5eb;
  border-radius: 23px;

  background: #ffffff;
  color: #01305c;

  box-shadow: 0 8px 25px rgba(1, 48, 92, .06);

  text-align: center;
}

.deposito-tarjeta h2 {
  margin: 0;

  color: #01305c;
  font-size: clamp(25px, 3.4vw, 34px);
  font-weight: 800;
  line-height: 1.25;
  letter-spacing: -0.02em;
  text-align: center;
}

.deposito-descripcion {
  max-width: 520px;
  margin: 0;

  color: #526b7c;
  font-size: clamp(17px, 2.6vw, 22px);
  font-weight: 500;
  line-height: 1.5;
  text-align: center;
}

.deposito-instruccion {
  display: flex;
  flex-direction: column;
  align-items: center;

  gap: 8px;
  width: 100%;
}

/* ==============================
   5. DEPOSITO EN CURSO
   ============================== */

.deposito-tarjeta.en-deposito {
  height: 100%;
  justify-content: space-evenly;

  gap: clamp(10px, 1.6dvh, 20px);
}

.deposito-reloj {
  position: relative;

  display: grid;
  place-items: center;

  width: clamp(160px, min(32dvh, 52vw), 310px);

  max-width: 72%;
  aspect-ratio: 1;

  flex-shrink: 1;
  container-type: inline-size;
}

.deposito-circulo {
  width: 100%;
  height: 100%;
  overflow: visible;
}

.deposito-pista {
  stroke: #e1edef;
}

.deposito-avance {
  stroke: url(#synchro-timer-gradient-v3);

  transform: rotate(-90deg);
  transform-origin: 50% 50%;

  transition: stroke-dashoffset .9s cubic-bezier(.22, 1, .36, 1);
}

.deposito-avance.deposito-final {
  stroke: #8da5b3;
}

.deposito-tiempo {
  position: absolute;
  inset: 0;

  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  gap: 4px;
}

.deposito-digitos {
  color: #01305c;

  font-size: clamp(32px, 23cqw, 72px);
  font-weight: 800;
  font-variant-numeric: tabular-nums;

  line-height: 1;
  letter-spacing: -.045em;
}

.deposito-unidad {
  color: #657988;

  font-size: clamp(10px, 7.4cqw, 17px);
  font-weight: 600;
}

/* ==============================
   6. BOTONES
   ============================== */

.deposito-acciones {
  display: flex;
  flex-direction: column;
  align-items: center;

  gap: clamp(9px, 1.3dvh, 14px);

  width: min(100%, 470px);
}

.deposito-boton-principal,
.deposito-boton-secundario {
  width: 100%;
  min-height: clamp(52px, 6.5dvh, 66px);

  padding: 10px 18px;

  border-radius: 14px;

  font: inherit;
  font-size: clamp(16px, 1.7vw, 20px);
  font-weight: 750;

  cursor: pointer;
  touch-action: manipulation;
}

.deposito-boton-principal {
  border: 2px solid #01305c;

  background: #01305c;
  color: #ffffff;
}

.deposito-boton-principal:hover:not(:disabled) {
  background: #084b75;
}

.deposito-boton-secundario {
  border: 2px solid #00b9c7;

  background: #ffffff;
  color: #01305c;
}

.deposito-boton-secundario:hover:not(:disabled) {
  background: #e8f9fa;
}

.deposito-boton-principal:disabled,
.deposito-boton-secundario:disabled {
  opacity: .6;
  cursor: wait;
}

.deposito-boton-principal:focus-visible,
.deposito-boton-secundario:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 4px;
}

.deposito-ayuda {
  max-width: 390px;

  color: #526b7c;
  font-size: clamp(12px, 1.3vw, 15px);
  line-height: 1.4;
}

/* ==============================
   7. VERIFICACION
   ============================== */

.deposito-cargando {
  width: 54px;
  height: 54px;

  border: 5px solid #dcecf0;
  border-top-color: #00b9c7;
  border-radius: 50%;

  animation: deposito-giro 1s linear infinite;
}

/* ==============================
   8. CONFIRMACION Y ERRORES
   ============================== */

.deposito-tarjeta:not(.en-deposito) {
  height: 100%;
  min-height: 0;

  align-self: stretch;
  justify-content: center;

  gap: clamp(15px, 2.4dvh, 25px);
}

.deposito-icono-exito,
.deposito-icono-error {
  display: grid;
  place-items: center;

  width: clamp(78px, 10vw, 100px);
  height: clamp(78px, 10vw, 100px);

  flex-shrink: 0;

  border-radius: 50%;

  font-size: clamp(44px, 5vw, 58px);
  font-weight: 800;
}

.deposito-icono-exito {
  width: clamp(104px, 15vw, 136px);
  height: clamp(104px, 15vw, 136px);
  flex-shrink: 0;

  display: grid;
  place-items: center;

  border: 4px solid rgba(94, 198, 232, .35);
  border-radius: 50%;

  background: linear-gradient(145deg, #00b9c7, #008c9b);
  color: #ffffff;

  box-shadow:
    0 10px 26px rgba(0, 185, 199, .22),
    0 0 0 9px rgba(0, 185, 199, .09);
}

.deposito-icono-error {
  background: #fff1dc;
  color: #a86812;
}

.deposito-retorno {
  margin: 0;

  color: #526b7c;
  font-size: clamp(16px, 2.4vw, 20px);
  font-weight: 650;
  line-height: 1.4;
  text-align: center;
}

.deposito-mensaje-error {
  width: min(100%, 490px);

  margin: 0;
  padding: 12px 14px;

  border-radius: 10px;

  background: #fff0ed;
  color: #882f26;

  font-size: 13px;
  line-height: 1.5;
  text-align: center;
}

/* ==============================
   9. MODO OSCURO
   ============================== */

.deposito-pantalla.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .deposito-tarjeta {
  background: #103956;
  border-color: #355c73;
  color: #ffffff;

  box-shadow: 0 8px 25px rgba(0, 0, 0, .13);
}

.tema-oscuro .deposito-tarjeta h2,
.tema-oscuro .deposito-digitos {
  color: #ffffff;
}

.tema-oscuro .deposito-descripcion,
.tema-oscuro .deposito-unidad,
.tema-oscuro .deposito-ayuda,
.tema-oscuro .deposito-retorno {
  color: #d4e1e8;
}

.tema-oscuro .deposito-identificador {
  background: #194764;
  color: #ffffff;
  border-color: #5ec6e8;
}

.tema-oscuro .deposito-pista {
  stroke: #315772;
}

.tema-oscuro .deposito-boton-principal {
  background: #00b9c7;
  border-color: #00b9c7;
  color: #01305c;
}

.tema-oscuro .deposito-boton-principal:hover:not(:disabled) {
  background: #5ec6e8;
}

.tema-oscuro .deposito-boton-secundario {
  background: #123d5b;
  border-color: #00b9c7;
  color: #ffffff;
}

.tema-oscuro .deposito-boton-secundario:hover:not(:disabled) {
  background: #194b68;
}

.tema-oscuro .deposito-icono-exito {
  background: linear-gradient(145deg, #00b9c7, #008c9b);
  color: #ffffff;
  border-color: rgba(94, 198, 232, .4);
}

.tema-oscuro .deposito-icono-error {
  background: #523b24;
  color: #ffdca3;
}

.tema-oscuro .deposito-mensaje-error {
  background: #532f34;
  color: #ffe6de;
}

/* ==============================
   10. DISENO RESPONSIVO
   ============================== */

/* Tablet: mismas dimensiones de GuardarView. */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {

  .deposito-pantalla::before {
    height: 10px;
  }

  .deposito-contenedor {
    width: min(100%, 1060px);
    height: min(100%, 760px);

    gap: clamp(12px, 1.8vh, 22px);
  }

  .deposito-encabezado h1 {
    font-size: clamp(30px, 3vw, 40px);
  }
}

/* Tablet vertical. */
@media (min-width: 600px)
and (min-height: 700px)
and (orientation: portrait) {

  .deposito-contenedor {
    width: min(100%, 820px);
    height: min(100%, 1040px);
  }
}

/* Tablet y telefono vertical. */
@media (orientation: portrait) {

  .deposito-contenedor {
    max-width: 820px;
  }
}

/* Telefono vertical:
   coincide con la geometria de GuardarView. */
@media (orientation: portrait)
and (max-width: 599px) {

  .deposito-contenedor {
    width: min(100%, 620px);
    height: min(100%, 560px);
  }
}

/* Pantallas de poca altura:
   permitir desplazamiento sin cortar botones. */
@media (max-height: 650px) {

  .deposito-pantalla {
    display: block;
    overflow-y: auto;

    padding: 12px;
  }

  .deposito-pantalla::before {
    display: none;
  }

  .deposito-contenedor {
    display: flex;
    flex-direction: column;
    align-items: center;

    width: min(100%, 740px);
    height: auto;
    min-height: 0;

    margin: 0 auto;

    gap: 12px;
  }

  .deposito-encabezado {
    flex: 0 0 auto;
  }

  .deposito-tarjeta,
  .deposito-tarjeta.en-deposito {
    width: 100%;
    height: auto;
    min-height: 350px;

    gap: 10px;
  }

  .deposito-tarjeta:not(.en-deposito) {
    height: auto;
    min-height: 350px;
  }

  .deposito-reloj {
    width: clamp(145px, 30dvh, 220px);
  }

  .deposito-acciones {
    width: min(100%, 470px);
  }

  .deposito-boton-principal,
  .deposito-boton-secundario {
    min-height: 48px;
  }
}

/* Evitar que el teclado o el navegador reduzcan
   demasiado el contenido en pantallas estrechas. */
@media (max-width: 420px) {

  .deposito-tarjeta {
    padding: 15px;
  }

  .deposito-digitos {
    font-size: clamp(26px, 24cqw, 60px);
  }

  .deposito-identificador {
    font-size: 12px;
  }
}

/* ==============================
   11. ACCESIBILIDAD
   ============================== */

@keyframes deposito-giro {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {

  .deposito-cargando {
    animation: none;
  }

  .deposito-avance {
    transition: none;
  }
}

/* SYNCHRO: elementos exclusivos del contador responsivo */

.deposito-halo {
  stroke: #5ec6e8;
  opacity: .3;
}

.deposito-referencia {
  color: #008c99;
  font-size: clamp(9px, 7.3cqw, 14px);
  font-weight: 700;
  line-height: 1.2;
  white-space: nowrap;
}

.deposito-aviso-minuto {
  position: absolute;
  z-index: 3;
  top: -7%;
  right: -15%;

  padding: 7px 11px;
  border-radius: 999px;

  background: #00b9c7;
  color: #01305c;

  font-size: 14px;
  font-weight: 800;
  box-shadow: 0 5px 15px rgba(0, 185, 199, .24);
}

.tema-oscuro .deposito-referencia {
  color: #92e9ed;
}

.deposito-digitos {
  animation: deposito-numero-entrada .35s ease-out;
}

@keyframes deposito-numero-entrada {
  from {
    opacity: .6;
    transform: scale(.9);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

.deposito-minuto-enter-active,
.deposito-minuto-leave-active {
  transition: opacity .3s ease, transform .35s ease;
}

.deposito-minuto-enter-from,
.deposito-minuto-leave-to {
  opacity: 0;
  transform: translateY(10px) scale(.85);
}

@media (prefers-reduced-motion: reduce) {
  .deposito-digitos {
    animation: none;
  }

  .deposito-minuto-enter-active,
  .deposito-minuto-leave-active {
    transition: none;
  }
}


/* La reserva reutiliza el contador ya existente sin el desplazamiento adicional. */
.deposito-pantalla.reserva-web{overflow:hidden;grid-template-rows:minmax(0,1fr);}
.deposito-pantalla.reserva-web::before{display:none;}
.deposito-pantalla.reserva-web .deposito-contenedor{grid-row:1;place-self:center;width:min(100%,760px);height:min(100%,630px);}
@media(max-height:570px){
  .deposito-pantalla.reserva-web{padding:8px;}
  .deposito-pantalla.reserva-web .deposito-contenedor{gap:6px;}
  .deposito-pantalla.reserva-web .deposito-tarjeta{padding:10px;}
}
</style>