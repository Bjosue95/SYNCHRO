<script setup>
import { computed, onMounted, onUnmounted, ref } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";

import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import CasilleroCard from "../components/CasilleroCard.vue";

import { useTema } from "../composables/useTema";
import { obtenerCasilleros } from "../services/casillerosApi";
import { reservarCasilleroTerminal } from "../services/reservasTerminalApi";
import { registrarDepositoPersonal } from "../services/depositosTerminalApi";
import { retirarPertenenciasPersonales } from "../services/retirosPersonalesTerminalApi";

// Componentes compartidos
const { oscuro, cambiarTema } = useTema();
const centroAyuda = ref(null);
const route = useRoute();
const router = useRouter();

function abrirAyuda() {
  centroAyuda.value?.abrir();
}

// Tamaños reconocidos por SYNCHRO
const nombresTamanos = {
  S: "Pequeño",
  M: "Mediano",
  L: "Grande",
};

// Leer el tamaño seleccionado en la pantalla anterior
const tamano = computed(() => route.query.tamano);

const tamanoValido = computed(() => {
  return (
    typeof tamano.value === "string" &&
    Object.hasOwn(nombresTamanos, tamano.value)
  );
});

const nombreTamano = computed(() => {
  return tamanoValido.value ? nombresTamanos[tamano.value] : "";
});

// Información del backend
const casilleros = ref([]);
const cargando = ref(true);
const error = ref("");

// Selección de la interfaz
const codigoSeleccionado = ref("");
const paso = ref("seleccion");

// Consultar casilleros reales
async function cargarCasilleros() {
  if (!tamanoValido.value) {
    cargando.value = false;
    return;
  }

  cargando.value = true;
  error.value = "";
  codigoSeleccionado.value = "";
  paso.value = "seleccion";

  try {
    casilleros.value = await obtenerCasilleros();
  } catch (e) {
    console.error("Error al consultar casilleros:", e);

    casilleros.value = [];
    error.value = "No se pudo consultar la disponibilidad. Intenta nuevamente.";
  } finally {
    cargando.value = false;
  }
}

onMounted(cargarCasilleros);

// Filtrar por tamaño y tipo normal
const casillerosFiltrados = computed(() => {
  if (!tamanoValido.value) return [];

  return casilleros.value.filter((casillero) => {
    return (
      casillero.tamano === nombreTamano.value && casillero.tipo === "Normal"
    );
  });
});

// Contar solamente los disponibles
const cantidadDisponible = computed(() => {
  return casillerosFiltrados.value.filter(
    (casillero) => casillero.estado === "Disponible",
  ).length;
});

// Casillero elegido
const casilleroSeleccionado = computed(() => {
  return casillerosFiltrados.value.find(
    (casillero) => casillero.codigo === codigoSeleccionado.value,
  );
});

const puedeContinuar = computed(() => {
  return (
    !cargando.value &&
    !error.value &&
    casilleroSeleccionado.value?.estado === "Disponible"
  );
});

function seleccionarCasillero(codigo) {
  if (cargando.value || error.value) return;

  const casillero = casillerosFiltrados.value.find(
    (item) => item.codigo === codigo,
  );

  if (casillero?.estado !== "Disponible") return;

  codigoSeleccionado.value = codigo;
}

async function continuar() {
  if (!puedeContinuar.value) return;

  cargando.value = true;
  error.value = "";

  try {
    // Consultar nuevamente la disponibilidad real
    const actuales = await obtenerCasilleros();

    casilleros.value = actuales;

    // Verificar el casillero elegido
    const casillero = actuales.find(
      (item) => item.codigo === codigoSeleccionado.value,
    );

    if (
      !casillero ||
      casillero.tamano !== nombreTamano.value ||
      casillero.tipo !== "Normal" ||
      casillero.estado !== "Disponible"
    ) {
      codigoSeleccionado.value = "";

      error.value =
        "Este casillero ya no está disponible. Actualiza la lista y selecciona otro.";

      return;
    }

    // Solo avanzar cuando la disponibilidad esté confirmada
    paso.value = "resumen";
  } catch (e) {
    console.error("Error al verificar disponibilidad:", e);

    error.value = "No se pudo verificar el casillero. Intenta nuevamente.";
  } finally {
    cargando.value = false;
  }
}

function cambiarSeleccion() {
  paso.value = "seleccion";
}

// Estado de la reserva real
const reservando = ref(false);
const reservaConfirmada = ref(null);
const errorReserva = ref("");
const depositando = ref(false);
const depositoConfirmado = ref(null);
const errorDeposito = ref("");
// El PIN solo vive en la memoria de esta pantalla; no se guarda en storage.
const pinTemporal = ref("");
let temporizadorPin = null;
const retiroId = ref("");
const retiroPin = ref("");
const retirando = ref(false);
const errorRetiro = ref("");
const retiroConfirmado = ref(null);

function abrirRetiro() {
  if (reservando.value || depositando.value) return;
  errorRetiro.value = "";
  retiroConfirmado.value = null;
  retiroId.value = "";
  retiroPin.value = "";
  paso.value = "retiro";
}

function volverASeleccion() {
  retiroPin.value = "";
  paso.value = "seleccion";
}

function finalizarDeposito() {
  pinTemporal.value = "";
  if (temporizadorPin !== null) clearTimeout(temporizadorPin);
  temporizadorPin = null;
  router.replace("/menu");
}

async function confirmarRetiro() {
  if (retirando.value || retiroConfirmado.value) return;
  const id = Number(retiroId.value);
  if (!Number.isSafeInteger(id) || id <= 0 || !/^\d{6}$/.test(retiroPin.value)) {
    errorRetiro.value = "Escribe el número de reserva y los seis dígitos del PIN.";
    return;
  }
  retirando.value = true;
  errorRetiro.value = "";
  try {
    const resultado = await retirarPertenenciasPersonales(id, retiroPin.value);
    retiroPin.value = "";
    retiroConfirmado.value = resultado;
    casilleros.value = casilleros.value.map((casillero) =>
      casillero.codigo === resultado.codigoCasillero
        ? { ...casillero, estado: "Disponible" }
        : casillero,
    );
  } catch (e) {
    // Nunca registrar PINs en console.log o mensajes de error.
    retiroPin.value = "";
    errorRetiro.value = e.message || "No se pudo confirmar el retiro.";
  } finally {
    retirando.value = false;
  }
}

// Contador basado en el vencimiento de SQL Server
const ahora = ref(Date.now());
let intervaloReserva = null;

const segundosRestantes = computed(() => {
  const fecha = reservaConfirmada.value?.fechaVencimiento;

  if (!fecha) return null;

  const vencimiento = Date.parse(fecha);

  if (Number.isNaN(vencimiento)) return null;

  return Math.max(0, Math.ceil((vencimiento - ahora.value) / 1000));
});

const tiempoRestante = computed(() => {
  const segundos = segundosRestantes.value;

  if (segundos === null) return "--:--";

  const minutos = Math.floor(segundos / 60);
  const resto = segundos % 60;

  return `${String(minutos).padStart(2, "0")}:${String(resto).padStart(2, "0")}`;
});

onUnmounted(() => {
  if (intervaloReserva !== null) clearInterval(intervaloReserva);
  if (temporizadorPin !== null) clearTimeout(temporizadorPin);
  pinTemporal.value = "";
  retiroPin.value = "";
});

async function confirmarReserva() {
  if (reservando.value || reservaConfirmada.value) return;
  if (!codigoSeleccionado.value) return;

  reservando.value = true;
  errorReserva.value = "";

  try {
    const resultado = await reservarCasilleroTerminal(codigoSeleccionado.value);

    // Guardar el resultado confirmado por SQL Server
    reservaConfirmada.value = resultado;

    ahora.value = Date.now();

    if (intervaloReserva !== null) {
      clearInterval(intervaloReserva);
    }

    intervaloReserva = setInterval(() => {
      ahora.value = Date.now();

      if (segundosRestantes.value === 0) {
        clearInterval(intervaloReserva);
        intervaloReserva = null;
      }
    }, 1000);

    // Actualizar el estado local del casillero
    casilleros.value = casilleros.value.map((casillero) =>
      casillero.codigo === codigoSeleccionado.value
        ? { ...casillero, estado: "Reservado" }
        : casillero,
    );
  } catch (e) {
    console.error("Error al reservar casillero:", e);

    errorReserva.value = e.message || "No se pudo confirmar la reserva.";
  } finally {
    reservando.value = false;
  }
}

async function confirmarDeposito() {
  if (
    depositando.value ||
    depositoConfirmado.value ||
    !reservaConfirmada.value ||
    segundosRestantes.value === null ||
    segundosRestantes.value <= 0
  ) {
    return;
  }

  depositando.value = true;
  errorDeposito.value = "";

  try {
    const { idUso, tokenDeposito } = reservaConfirmada.value;

    const resultado = await registrarDepositoPersonal(idUso, tokenDeposito);

    // Confirmación recibida desde SQL Server.
    const { pinRetiro, ...depositoSinPin } = resultado;
    if (typeof pinRetiro !== "string" || !/^\d{6}$/.test(pinRetiro)) {
      throw new Error("El depósito fue recibido, pero no se pudo mostrar el PIN. Solicita ayuda al administrador.");
    }
    depositoConfirmado.value = depositoSinPin;
    pinTemporal.value = pinRetiro;

    // En un kiosco compartido, ocultar el secreto y volver al menú tras 2 min.
    if (temporizadorPin !== null) clearTimeout(temporizadorPin);
    temporizadorPin = setTimeout(finalizarDeposito, 120000);

    // Detener el contador después del depósito.
    if (intervaloReserva !== null) {
      clearInterval(intervaloReserva);
      intervaloReserva = null;
    }

    // El casillero ahora está ocupado.
    casilleros.value = casilleros.value.map((casillero) =>
      casillero.codigo === resultado.codigoCasillero
        ? { ...casillero, estado: "Ocupado" }
        : casillero,
    );

    // Ya no necesitamos conservar el token de depósito.
    const { tokenDeposito: tokenDescartado, ...reservaSinToken } = reservaConfirmada.value;
    reservaConfirmada.value = reservaSinToken;
  } catch (error) {
    errorDeposito.value = error.message || "No se pudo confirmar el depósito.";
  } finally {
    depositando.value = false;
  }
}
</script>

<template>
  <main class="pantalla-casilleros" :class="{ 'tema-oscuro': oscuro }">
    <!-- Encabezado -->
    <AppHeader
      :oscuro="oscuro"
      @cambiar-tema="cambiarTema"
      @abrir-ayuda="abrirAyuda"
    />

    <!-- Regresar -->
    <RouterLink to="/guardar/pertenencias/tamano" class="regresar">
      <span class="icono-regresar" aria-hidden="true">
        <img
          src="/iconos/complementarios/28_regresar.svg"
          alt=""
          class="regresar-claro"
        />
        <img
          src="/iconos/complementarios/28_regresar-oscuro.svg"
          alt=""
          class="regresar-oscuro"
        />
      </span>

      <span>Volver a tamaños</span>
    </RouterLink>

    <section class="contenido">
      <!-- Retiro personal en la terminal de pruebas; ruta propia en una fase posterior -->
      <template v-if="paso === 'retiro'">
        <h1>Retirar pertenencias personales</h1>
        <div class="resumen grupo-retiro">
          <template v-if="retiroConfirmado">
            <p class="aviso" role="status">¡Retiro registrado correctamente!</p>
            <p>Casillero: <strong>{{ retiroConfirmado.codigoCasillero }}</strong></p>
            <p>Estado: <strong>{{ retiroConfirmado.estadoCasillero }}</strong></p>
            <p class="mensaje">En el prototipo, abre manualmente el casillero y recoge tus pertenencias.</p>
            <button type="button" class="boton-continuar" @click="router.replace('/menu')">
              Finalizar
            </button>
          </template>
          <template v-else>
            <p class="mensaje">Introduce el número de reserva y el PIN que recibiste al depositar.</p>
            <label for="retiro-uso">Número de reserva</label>
            <input
              id="retiro-uso"
              v-model.trim="retiroId"
              class="campo-retiro"
              inputmode="numeric"
              pattern="[0-9]*"
              autocomplete="off"
              placeholder="Ejemplo: 17"
            />
            <label for="retiro-pin">PIN de retiro</label>
            <input
              id="retiro-pin"
              v-model.trim="retiroPin"
              class="campo-retiro"
              type="password"
              inputmode="numeric"
              pattern="[0-9]{6}"
              maxlength="6"
              autocomplete="off"
              placeholder="6 dígitos"
            />
            <p v-if="errorRetiro" class="aviso" role="alert">{{ errorRetiro }}</p>
            <button
              type="button"
              class="boton-continuar"
              :disabled="retirando || !retiroId || retiroPin.length !== 6"
              @click="confirmarRetiro"
            >
              {{ retirando ? "Validando retiro..." : "Confirmar retiro" }}
            </button>
            <button type="button" class="boton-secundario" @click="volverASeleccion">
              Regresar a casilleros
            </button>
          </template>
        </div>
      </template>

      <!-- Tamaño inválido -->
      <div v-else-if="!tamanoValido" class="mensaje">
        <h1>Selecciona primero un tamaño</h1>
        <p>
          Regresa a la pantalla anterior para elegir un casillero pequeño,
          mediano o grande.
        </p>
      </div>

      <!-- Selección de casillero -->
      <template v-else-if="paso === 'seleccion'">
        <h1>Selecciona tu casillero</h1>

        <button type="button" class="boton-secundario" @click="abrirRetiro">
          ¿Ya tienes una reserva? Retirar pertenencias
        </button>

        <p class="subtitulo">Tamaño elegido: {{ nombreTamano }}</p>

        <p class="estado-consulta" role="status">
          {{
            cargando
              ? "Consultando casilleros..."
              : error
                ? error
                : `${cantidadDisponible} casilleros disponibles`
          }}
        </p>

        <button
          v-if="error"
          type="button"
          class="boton-secundario"
          @click="cargarCasilleros"
        >
          Reintentar consulta
        </button>

        <div v-if="!cargando && !error" class="lista-casilleros">
          <CasilleroCard
            v-for="casillero in casillerosFiltrados"
            :key="casillero.codigo"
            :codigo="casillero.codigo"
            :tamano="casillero.tamano"
            :tipo="casillero.tipo"
            :estado="casillero.estado"
            :oscuro="oscuro"
            :seleccionado="codigoSeleccionado === casillero.codigo"
            @seleccionar="seleccionarCasillero"
          />
        </div>

        <p
          v-if="!cargando && !error && casillerosFiltrados.length === 0"
          class="mensaje"
        >
          No hay casilleros de este tamaño registrados.
        </p>

        <div class="acciones">
          <p class="estado-consulta" aria-live="polite">
            {{
              codigoSeleccionado
                ? `Casillero seleccionado: ${codigoSeleccionado}`
                : "Toca un casillero disponible para continuar."
            }}
          </p>

          <button
            type="button"
            class="boton-continuar"
            :disabled="!puedeContinuar"
            @click="continuar"
          >
            Continuar
          </button>
        </div>
      </template>

      <!-- Resumen de demostración -->
      <template v-else>
        <h1>Revisa tu selección</h1>

        <div class="resumen">
          <span class="codigo-destacado">
            {{ codigoSeleccionado }}
          </span>

          <h2>Casillero {{ nombreTamano }}</h2>

          <p>Tipo: Normal</p>

          <!-- Reserva registrada correctamente -->
          <template v-if="reservaConfirmada">
            <p v-if="!depositoConfirmado" class="aviso" role="status">
              ¡Reserva confirmada correctamente!
            </p>

            <p>
              Número de reserva:
              <strong>{{ reservaConfirmada.idUso }}</strong>
            </p>

            <p>
              Casillero:
              <strong>{{ reservaConfirmada.codigoCasillero }}</strong>
            </p>

            <!-- Depósito registrado correctamente -->
            <template v-if="depositoConfirmado">
              <p class="aviso" role="status">
                ¡Depósito registrado correctamente!
              </p>

              <p>
                Estado del casillero:
                <strong>{{ depositoConfirmado.estadoCasillero }}</strong>
              </p>

              <p class="mensaje">
                Tus pertenencias han quedado registradas en SYNCHRO.
              </p>
              <p class="aviso">Anota el número de reserva y este PIN. El PIN se muestra una sola vez.</p>
              <p class="pin-retiro" role="status">{{ pinTemporal }}</p>
              <p class="mensaje">Por seguridad, esta pantalla se cerrará automáticamente después de dos minutos.</p>
              <button type="button" class="boton-continuar" @click="finalizarDeposito">
                Ya guardé el PIN · Finalizar
              </button>
            </template>

            <!-- Reserva pendiente de depósito -->
            <template v-else>
              <p v-if="segundosRestantes !== null" class="aviso" role="timer">
                Tiempo restante para depositar:
                <strong>{{ tiempoRestante }}</strong>
              </p>

              <p v-if="segundosRestantes === 0" role="status">
                El plazo de la reserva ha terminado. Consulta nuevamente la
                disponibilidad.
              </p>

              <p v-if="segundosRestantes === null" class="aviso">
                No se pudo mostrar el tiempo restante.
              </p>

              <template v-if="segundosRestantes > 0">
                <p class="mensaje">
                  Abre manualmente el casillero asignado, coloca tus
                  pertenencias y cierra la puerta.
                </p>

                <p class="mensaje">
                  Cuando hayas terminado, confirma el depósito.
                </p>
              </template>

              <button
                type="button"
                class="boton-continuar"
                :disabled="
                  depositando ||
                  segundosRestantes === null ||
                  segundosRestantes <= 0
                "
                @click="confirmarDeposito"
              >
                {{
                  depositando ? "Registrando depósito..." : "Confirmar depósito"
                }}
              </button>

              <p v-if="errorDeposito" class="aviso" role="alert">
                {{ errorDeposito }}
              </p>
            </template>
          </template>

          <!-- Antes de confirmar la reserva -->
          <template v-else>
            <p class="aviso">
              Has elegido un casillero. Todavía no se ha reservado en el
              sistema.
            </p>

            <p v-if="errorReserva" class="aviso" role="alert">
              {{ errorReserva }}
            </p>

            <button
              type="button"
              class="boton-continuar"
              :disabled="reservando"
              @click="confirmarReserva"
            >
              {{ reservando ? "Confirmando reserva..." : "Confirmar reserva" }}
            </button>
          </template>
        </div>
      </template>
    </section>

    <HelpDialog ref="centroAyuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
/* SYNCHRO | SELECCIÓN DE CASILLERO */

.pantalla-casilleros {
  display: flex;
  flex-direction: column;
  gap: 18px;

  min-height: 100dvh;
  padding: 20px 26px;

  background: #f2f7fa;
  color: #01305c;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

/* Regresar: área táctil de 56 px */

.regresar {
  display: inline-flex;
  align-items: center;
  align-self: flex-start;
  gap: 12px;

  min-height: 56px;
  padding: 10px 20px;

  border-radius: 14px;
  background: #e4f2f6;
  color: inherit;

  text-decoration: none;
  font-size: 16px;
  font-weight: 600;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

.icono-regresar {
  position: relative;
  display: block;
  width: 30px;
  height: 30px;
  flex-shrink: 0;
}

.icono-regresar img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
  transition: opacity 150ms ease;
}

.regresar-claro {
  opacity: 1;
}

.regresar-oscuro {
  opacity: 0;
}

/* Contenido */

.contenido {
  width: min(100%, 1000px);
  margin: 0 auto;
  text-align: center;
}

h1 {
  margin: 0;
  font-size: clamp(24px, 3vw, 34px);
}

.subtitulo,
.estado-consulta {
  color: #5b7282;
  transition: color 150ms ease;
}

.subtitulo {
  margin: 12px 0;
}

.estado-consulta {
  min-height: 24px;
  margin: 12px 0;
}

/* Casilleros */

.lista-casilleros {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 18px;

  width: min(100%, 650px);
  margin: 22px auto 0;
}

/* Acciones */

.acciones {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;

  margin-top: 24px;
}

.boton-continuar,
.boton-secundario {
  min-height: 56px;
  padding: 12px 26px;
  border-radius: 14px;

  font: inherit;
  font-size: 17px;
  font-weight: 700;
  cursor: pointer;
}

.boton-continuar {
  min-width: 220px;
  border: none;
  background: #00b9c7;
  color: #01305c;
}

.boton-continuar:disabled {
  background: #dce5ea;
  color: #81939e;
  cursor: not-allowed;
}

.boton-secundario {
  border: 2px solid #00b9c7;
  background: transparent;
  color: inherit;
}

/* Resumen */

.resumen {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;

  width: min(100%, 520px);
  margin: 24px auto;
  padding: 30px;

  border: 1px solid #dfeaf0;
  border-radius: 20px;
  background: #ffffff;

  transition:
    background-color 150ms ease,
    border-color 150ms ease;
}

.codigo-destacado {
  display: grid;
  place-items: center;

  width: 110px;
  height: 75px;

  border-radius: 14px;
  background: #e4f2f6;
  color: #01305c;

  font-size: 30px;
  font-weight: 700;
}

.aviso,
.mensaje {
  color: #5b7282;
  line-height: 1.5;
}

.regresar:focus-visible,
button:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

/* Tema oscuro */

.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .regresar {
  background: #204766;
}

.tema-oscuro .regresar-claro {
  opacity: 0;
}

.tema-oscuro .regresar-oscuro {
  opacity: 1;
}

.tema-oscuro .subtitulo,
.tema-oscuro .estado-consulta,
.tema-oscuro .aviso,
.tema-oscuro .mensaje {
  color: #c6dfea;
}

.tema-oscuro .resumen {
  background: #103956;
  border-color: #315573;
}

.tema-oscuro .boton-continuar:disabled {
  background: #315573;
  color: #a7becd;
}

/* Controles del retiro táctiles y compatibles con ambos temas. */
.grupo-retiro { display: flex; flex-direction: column; gap: 12px; }
.campo-retiro {
  width: 100%;
  min-height: 52px;
  padding: 10px 14px;
  border: 1px solid #7898ad;
  border-radius: 12px;
  background: #fff;
  color: #01305c;
  font-size: 18px;
}
.pin-retiro {
  font-size: clamp(28px, 7vw, 42px);
  font-weight: 800;
  letter-spacing: .18em;
  text-align: center;
  margin: 0;
}
.tema-oscuro .campo-retiro {
  background: #103956;
  border-color: #5f8ca8;
  color: #fff;
}

/* Adaptación */

@media (max-width: 650px) {
  .lista-casilleros {
    grid-template-columns: 1fr;
  }
}

@media (prefers-reduced-motion: reduce) {
  .pantalla-casilleros,
  .regresar,
  .icono-regresar img,
  .subtitulo,
  .estado-consulta,
  .resumen {
    transition: none;
  }
}
</style>
