<script setup>
import { computed, onMounted, onBeforeUnmount, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import {
  MINUTO_DEPOSITO_MS,
  segundosRestantesDeposito,
  extenderLimiteDeposito,
  formatearTiempoDeposito
} from "../utils/tiempoDeposito.js";

const route = useRoute();
const router = useRouter();
const { oscuro } = useTema();

const propsVistaPrevia = defineProps({
  modoVistaPrevia: {
    type: Boolean,
    default: false
  }
});

const claveIntento = "synchroWhatsappManualActivoV1";
const claveTiempo = "synchroDepositoInicioV1";

const casillero = computed(() => {
  const valor = route.query.casillero;
  return typeof valor === "string" &&
    /^[A-Za-z0-9-]{1,10}$/.test(valor)
    ? valor
    : "";
});

const estado = ref("verificando");
const segundos = ref(60);
const ventanaSegundos = ref(60);

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

function limpiarRelojes() {
  clearInterval(reloj);
  clearTimeout(redireccion);
  clearTimeout(reintento);
  reloj = null;
  redireccion = null;
  reintento = null;
}

function limpiarSesion() {
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

  sessionStorage.setItem(claveTiempo, JSON.stringify({
    intentoId: intento.intentoId,
    limiteMs
  }));

  comprobarTiempo();
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
    estado.value = "listo";
    segundos.value = 60;
    return;
  }

  cargarOperacion();
});

onBeforeUnmount(() => {
  vivo = false;
  limpiarRelojes();
});
</script>

<template>
  <main
    class="deposito-pantalla"
    :class="{ 'tema-oscuro': oscuro, 'deposito-en-curso': estado === 'listo' }"
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

            <div
              class="deposito-tiempo"
              role="timer"
              aria-live="off"
            >
              <span class="deposito-digitos">
                {{ textoTiempo }}
              </span>

              <span class="deposito-unidad">
                tiempo restante
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
            ✓
          </div>

          <h2>Depósito completado</h2>

          <p class="deposito-descripcion">
            El servidor confirmó correctamente tu depósito.
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
            @click="
              estado = 'verificando';
              error = '';
              verificarOperacion()
            "
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
.deposito-pantalla {
  display: flex;
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  overflow-y: auto;
  background: #f2f7fa;
  color: #01305c;
}

.deposito-contenedor {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: clamp(14px, 2.5vh, 24px);

  width: min(100%, 740px);
  min-height: 100%;
  margin: auto;
  padding: 18px 18px 24px;
}

.deposito-encabezado {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 9px;
  text-align: center;
}

.deposito-seccion {
  color: #527386;
  font-size: 13px;
  font-weight: 600;
}

.deposito-encabezado h1 {
  margin: 0;
  color: inherit;
  font-size: clamp(24px, 3vw, 35px);
  font-weight: 750;
  line-height: 1.18;
  letter-spacing: -0.025em;
}

.deposito-identificador {
  display: inline-flex;
  align-items: center;
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
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #00a6b4;
}

.deposito-tarjeta {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: clamp(12px, 2vh, 19px);

  width: min(100%, 520px);
  padding: clamp(22px, 3vw, 32px);

  border: 1px solid #d8e5eb;
  border-radius: 18px;
  background: #ffffff;
  box-shadow: 0 8px 25px rgba(1, 48, 92, 0.045);
  text-align: center;
}

.deposito-tarjeta h2 {
  margin: 0;
  color: #01305c;
  font-size: clamp(19px, 2.3vw, 23px);
  font-weight: 750;
  line-height: 1.3;
}

.deposito-instruccion {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.deposito-etapa {
  color: #00818f;
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 1.3px;
}

.deposito-instruccion p,
.deposito-descripcion {
  max-width: 390px;
  margin: 0;
  color: #5b6770;
  font-size: 14px;
  line-height: 1.5;
}

.deposito-reloj {
  position: relative;
  display: grid;
  place-items: center;

  width: clamp(142px, 22vh, 184px);
  aspect-ratio: 1;
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
  stroke: #00b9c7;
  transform: rotate(-90deg);
  transform-origin: 50% 50%;
  transition: stroke-dashoffset 0.45s linear;
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
  gap: 3px;
}

.deposito-digitos {
  color: #01305c;
  font-size: clamp(33px, 5vw, 43px);
  font-weight: 800;
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.045em;
  line-height: 1;
}

.deposito-unidad {
  color: #657988;
  font-size: 12px;
  font-weight: 600;
}

.deposito-tiempo-descripcion {
  max-width: 360px;
  margin: 0;
  color: #526b7c;
  font-size: 13px;
  line-height: 1.5;
}

.deposito-acciones {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  width: 100%;
}

.deposito-boton-principal,
.deposito-boton-secundario {
  width: min(100%, 340px);
  min-height: 54px;
  padding: 12px 18px;
  border-radius: 10px;
  font: inherit;
  font-size: 16px;
  font-weight: 750;
  cursor: pointer;
}

.deposito-boton-principal {
  border: 1px solid #01305c;
  background: #01305c;
  color: #ffffff;
}

.deposito-boton-principal:hover:not(:disabled) {
  background: #084b75;
}

.deposito-boton-principal:disabled {
  opacity: 0.6;
  cursor: wait;
}

.deposito-boton-secundario {
  border: 1px solid #9cc7d3;
  background: #e7f4f7;
  color: #01305c;
}

.deposito-boton-principal:focus-visible,
.deposito-boton-secundario:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 4px;
}

.deposito-ayuda {
  color: #667d8c;
  font-size: 12px;
  line-height: 1.4;
}

.deposito-cargando {
  width: 54px;
  height: 54px;
  border: 5px solid #dcecf0;
  border-top-color: #00b9c7;
  border-radius: 50%;
  animation: deposito-giro 1s linear infinite;
}

.deposito-icono-exito,
.deposito-icono-error {
  display: grid;
  place-items: center;
  width: 78px;
  height: 78px;
  border-radius: 50%;
  font-size: 46px;
  font-weight: 800;
}

.deposito-icono-exito {
  background: #e0f7ef;
  color: #078b70;
}

.deposito-icono-error {
  background: #fff1dc;
  color: #a86812;
}

.deposito-mensaje-error {
  width: 100%;
  margin: 0;
  padding: 11px 13px;
  border-radius: 8px;
  background: #fff0ed;
  color: #882f26;
  font-size: 13px;
  line-height: 1.5;
}

.deposito-retorno {
  margin: 0;
  color: #526b7c;
  font-size: 14px;
}

/* Modo oscuro */
.deposito-pantalla.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .deposito-tarjeta {
  background: #103956;
  border-color: #355c73;
}

.tema-oscuro .deposito-tarjeta h2,
.tema-oscuro .deposito-digitos {
  color: #ffffff;
}

.tema-oscuro .deposito-instruccion p,
.tema-oscuro .deposito-descripcion,
.tema-oscuro .deposito-tiempo-descripcion,
.tema-oscuro .deposito-ayuda,
.tema-oscuro .deposito-unidad,
.tema-oscuro .deposito-seccion,
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

.tema-oscuro .deposito-mensaje-error {
  background: #532f34;
  color: #ffe6de;
}

@keyframes deposito-giro {
  to { transform: rotate(360deg); }
}

/* Pantallas estrechas o de poca altura */
@media (max-width: 480px), (max-height: 590px) {
  .deposito-contenedor {
    gap: 10px;
    padding: 12px;
  }

  .deposito-tarjeta {
    padding: 17px;
    gap: 11px;
  }

  .deposito-reloj {
    width: 136px;
  }

  .deposito-digitos {
    font-size: 33px;
  }

  .deposito-encabezado h1 {
    font-size: 25px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .deposito-avance {
    transition: none;
  }

  .deposito-cargando {
    animation: none;
  }
}

/* SYNCHRO-DEPOSITO-TIEMPO-V2
   Tarjeta amplia, proporcionada como el menu. */

.deposito-contenedor {
  width: min(100%, 920px);
  height: 100%;
  min-height: 0;
  overflow-y: auto;
  gap: clamp(14px, 2.2dvh, 26px);
}

.deposito-encabezado {
  flex: 0 0 auto;
}

.deposito-tarjeta.en-deposito {
  width: min(100%, 740px);
  flex: 1 1 auto;
  min-height: 0;
  max-height: 740px;
  justify-content: space-evenly;
  gap: clamp(10px, 1.8dvh, 22px);
  padding: clamp(18px, 3vw, 34px);
  border-radius: 23px;
}

.deposito-tarjeta.en-deposito .deposito-reloj {
  width: clamp(156px, 25dvh, 270px);
  max-width: 58%;
  flex-shrink: 1;
}

.deposito-tarjeta.en-deposito .deposito-digitos {
  font-size: clamp(38px, 5.3vw, 60px);
}

.deposito-tarjeta.en-deposito .deposito-unidad {
  font-size: clamp(12px, 1.4vw, 16px);
}

.deposito-tarjeta.en-deposito .deposito-acciones {
  width: min(100%, 470px);
  gap: clamp(10px, 1.6dvh, 18px);
}

.deposito-tarjeta.en-deposito .deposito-boton-principal,
.deposito-tarjeta.en-deposito .deposito-boton-secundario {
  width: 100%;
  min-height: clamp(54px, 7dvh, 68px);
  border-radius: 14px;
  font-size: clamp(16px, 1.8vw, 21px);
}

.deposito-tarjeta.en-deposito .deposito-boton-secundario {
  border: 2px solid #00b9c7;
  background: transparent;
  color: #01305c;
}

.deposito-tarjeta.en-deposito .deposito-ayuda {
  max-width: 390px;
  color: #526b7c;
  font-size: clamp(13px, 1.3vw, 15px);
}

.tema-oscuro .deposito-tarjeta.en-deposito
.deposito-boton-secundario {
  background: #123d5b;
  border-color: #00b9c7;
  color: #ffffff;
}

.tema-oscuro .deposito-tarjeta.en-deposito
.deposito-ayuda {
  color: #d4e1e8;
}

/* Mantener todo accesible en pantallas bajas. */
@media (max-height: 650px) {
  .deposito-contenedor {
    height: auto;
    min-height: 100%;
    justify-content: flex-start;
    overflow: visible;
  }

  .deposito-tarjeta.en-deposito {
    flex: 0 0 auto;
    min-height: 390px;
    gap: 10px;
  }

  .deposito-tarjeta.en-deposito .deposito-reloj {
    width: clamp(120px, 22dvh, 170px);
  }
}

@media (max-width: 420px) {
  .deposito-tarjeta.en-deposito {
    padding: 16px;
  }

  .deposito-tarjeta.en-deposito .deposito-digitos {
    font-size: 35px;
  }
}

/* SYNCHRO-DEPOSITO-ALINEACION-MENU-V3
   Misma posicion vertical y estructura del menu. */

.deposito-contenedor {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);
  align-items: stretch;
  justify-content: stretch;

  width: min(100%, 800px);
  height: min(100%, 610px);
  min-height: 0;
  margin: auto;
  padding:
    clamp(12px, 2vh, 22px)
    clamp(12px, 2.4vw, 28px);

  gap: clamp(10px, 1.8vh, 20px);
  overflow: visible;
}

.deposito-encabezado {
  align-self: start;
  justify-self: center;
}

.deposito-encabezado h1 {
  font-size: clamp(22px, 2.8vw, 34px);
}

.deposito-tarjeta.en-deposito {
  width: min(100%, 740px);
  height: 100%;
  min-height: 0;
  max-height: none;
  flex: initial;
  align-self: stretch;
  justify-self: center;
}

@media (orientation: portrait) {
  .deposito-contenedor {
    width: min(100%, 630px);
    height: min(100%, 530px);
  }
}

@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .deposito-contenedor {
    width: min(100%, 1110px);
    height: min(100%, 760px);
    gap: clamp(12px, 2vh, 25px);
  }

  .deposito-encabezado h1 {
    font-size: clamp(29px, 3vw, 41px);
  }
}

@media (min-width: 600px)
and (min-height: 700px)
and (orientation: portrait) {
  .deposito-contenedor {
    width: min(100%, 820px);
    height: min(100%, 1000px);
  }
}

@media (max-height: 650px) {
  .deposito-contenedor {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: flex-start;
    width: min(100%, 740px);
    height: auto;
    min-height: 100%;
    padding: 12px;
    gap: 12px;
  }

  .deposito-tarjeta.en-deposito {
    height: auto;
    min-height: 390px;
    flex: 0 0 auto;
  }
}

/* SYNCHRO-DEPOSITO-GEOMETRIA-MENU-V4
   Alineacion compartida con MenuView.
   Solo afecta la pantalla cuando el deposito esta listo. */

.deposito-pantalla.deposito-en-curso {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: minmax(0, 1fr);
  place-items: center;
  box-sizing: border-box;

  padding:
    clamp(12px, 2vh, 22px)
    clamp(12px, 2.4vw, 28px);
}

.deposito-en-curso .deposito-contenedor {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);
  box-sizing: border-box;

  width: min(100%, 800px);
  height: min(100%, 610px);
  min-height: 0;

  margin: 0;
  padding: 0;
  gap: clamp(10px, 1.8vh, 20px);
  overflow: visible;
}

.deposito-en-curso .deposito-encabezado {
  align-self: start;
  justify-self: center;
}

.deposito-en-curso .deposito-tarjeta.en-deposito {
  box-sizing: border-box;
  width: 100%;
  height: 100%;
  min-height: 0;
  max-height: none;

  margin: 0;
  padding: clamp(16px, 2.5vw, 30px);

  justify-self: center;
  align-self: stretch;
  justify-content: space-evenly;
}

@media (orientation: portrait) {
  .deposito-en-curso .deposito-contenedor {
    width: min(100%, 630px);
    height: min(100%, 530px);
  }
}

/* Exactamente los mismos rangos de tablet que el menu. */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .deposito-en-curso .deposito-contenedor {
    width: min(100%, 1110px);
    height: min(100%, 760px);
    gap: clamp(12px, 2vh, 25px);
  }
}

@media (min-width: 600px)
and (min-height: 700px)
and (orientation: portrait) {
  .deposito-en-curso .deposito-contenedor {
    width: min(100%, 820px);
    height: min(100%, 1000px);
  }
}

/* En pantallas bajas, mantener desplazamiento seguro. */
@media (max-height: 650px) {
  .deposito-pantalla.deposito-en-curso {
    display: block;
    overflow-y: auto;
  }

  .deposito-en-curso .deposito-contenedor {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: flex-start;

    width: min(100%, 740px);
    height: auto;
    min-height: 100%;
    margin: 0 auto;
    gap: 12px;
  }

  .deposito-en-curso .deposito-tarjeta.en-deposito {
    height: auto;
    min-height: 390px;
    flex: 0 0 auto;
  }
}

/* SYNCHRO-AJUSTE-ALTURA-TITULO-V5
   Reduce la distancia visual entre la cabecera
   y el titulo sin cambiar el tamano de la tarjeta. */

@media (orientation: portrait) and (min-height: 651px) {
  .deposito-pantalla.deposito-en-curso
  .deposito-contenedor {
    transform: translateY(-38px);
  }
}

@media (max-height: 650px) {
  .deposito-pantalla.deposito-en-curso
  .deposito-contenedor {
    transform: none;
  }
}

/* SYNCHRO-DEPOSITO-TITULO-REFERENCIA-MENU-V6
   Ajuste fino del titulo respecto al menu.
   No modifica la posicion ni la altura de la tarjeta. */

@media (orientation: portrait) and (min-height: 651px) {
  .deposito-pantalla.deposito-en-curso
  .deposito-encabezado {
    position: relative;
    top: clamp(-18px, -1.2dvh, -8px);
  }
}
</style>