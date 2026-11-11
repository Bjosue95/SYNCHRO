<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from "vue";
import { useRoute, RouterLink } from "vue-router";
import IconoSynchro from "../components/IconoSynchro.vue";
import TabletLayout from "../components/TabletLayout.vue";
import { solicitar } from "../services/tabletApi";
const route = useRoute();
const tipo = computed(() => route.params.tipo);
const nombres = {
  carga: "Carga tu dispositivo",
  personal: "Un espacio para tus pertenencias",
};
const paso = ref(0),
  error = ref(""),
  ocupado = ref(false),
  lista = ref([]),
  elegido = ref(""),
  minutos = ref(30),
  reserva = ref(null),
  pago = ref(null),
  cerrado = ref(false),
  confirmado = ref(false),
  pagoId = ref(null),
  metodoPago = ref("");
const controlador = new AbortController();
const disponibles = computed(() =>
  lista.value.filter(
    (x) =>
      x.estado === "Disponible" &&
      x.configuracion.habilitado &&
      (tipo.value === "carga"
        ? ["CargaCelular", "CargaLaptop"].includes(x.configuracion.clase)
        : x.configuracion.clase === { personal: "Personal" }[tipo.value]),
  ),
);
const compartimento = computed(
  () =>
    disponibles.value.find((x) => x.configuracion.codigo === elegido.value)
      ?.configuracion,
);
const duraciones = computed(() =>
  Object.keys(compartimento.value?.tarifas || {})
    .map(Number)
    .sort((a, b) => a - b),
);
const monto = computed(() =>
  Number(compartimento.value?.tarifas[minutos.value] || 0),
);
const dinero = (n) =>
  new Intl.NumberFormat("es-HN", { style: "currency", currency: "HNL" }).format(
    n,
  );
async function ejecutar(fn) {
  if (ocupado.value) return;
  ocupado.value = true;
  error.value = "";
  try {
    await fn();
  } catch (e) {
    if (e.name !== "AbortError") error.value = e.message;
  } finally {
    ocupado.value = false;
  }
}
function seleccionar(codigo) {
  elegido.value = codigo;
  minutos.value = duraciones.value[0] || 30;
}
function headers() {
  return { "X-SYNCHRO-SERVICIO": reserva.value?.credencial || "" };
}
function ruta(accion = "") {
  return `/api/terminales/servicios/${encodeURIComponent(reserva.value.operacion.codigo)}/reservas/${reserva.value.operacion.id}${accion}`;
}
function api(r, datos) {
  return solicitar(r, {
    metodo: "POST",
    datos,
    encabezados: headers(),
    csrf: "/api/terminales/servicios/csrf-token",
    signal: controlador.signal,
  });
}
async function cargar() {
  await ejecutar(async () => {
    lista.value = await solicitar("/api/servicios/compartimentos", {
      signal: controlador.signal,
    });
    if (disponibles.value.length)
      seleccionar(disponibles.value[0].configuracion.codigo);
  });
}
async function reservar() {
  await ejecutar(async () => {
    if (!compartimento.value)
      throw new Error("Selecciona un compartimento disponible.");
    reserva.value = await api(
      `/api/terminales/servicios/${encodeURIComponent(elegido.value)}/reservas`,
      { minutos: minutos.value },
    );
    pagoId.value = crypto.randomUUID();
    metodoPago.value = "";
    paso.value = 1;
  });
}
async function pagar() {
  await ejecutar(async () => {
    if (!metodoPago.value)
      throw new Error("Selecciona un método de pago para continuar.");
    pago.value = await api(ruta("/pagar"), {
      idOperacion: pagoId.value,
      metodoPago: metodoPago.value,
    });
    paso.value = 2;
  });
}
async function iniciar() {
  await ejecutar(async () => {
    if (!confirmado.value)
      throw new Error("Confirma que colocaste tus objetos antes de continuar.");
    if (reserva.value.operacion.clase === "CargaLaptop" && !cerrado.value)
      throw new Error("Confirma que la laptop está cerrada.");
    await api(ruta("/iniciar"), { equipoCerrado: cerrado.value });
    paso.value = 3;
  });
}
async function cancelar() {
  await ejecutar(async () => {
    await api(ruta("/cancelar"));
    reserva.value = null;
    pago.value = null;
    metodoPago.value = "";
    pagoId.value = null;
    paso.value = 0;
  });
  if (!reserva.value) await cargar();
}
onMounted(cargar);
onBeforeUnmount(() => {
  controlador.abort();
  if (reserva.value) reserva.value.credencial = "";
  if (pago.value) pago.value.pinRetiro = null;
});
</script>
<template>
  <TabletLayout
    :titulo="nombres[tipo] || 'Servicios temporales'"
    descripcion="Elige tu espacio y el tiempo que necesitas. Conoce el precio antes de comenzar."
    :pasos="paso < 3 ? ['Espacio y tiempo', 'Pago', 'Guardar'] : []"
    :paso="paso"
  >
    <p v-if="error" role="alert" class="tablet-error">{{ error }}</p>
    <div v-if="paso === 0" class="tablet-two">
      <section class="tablet-panel">
        <h2>1. Elige tu compartimento</h2>
        <p v-if="ocupado" role="status" class="tablet-muted">
          Consultando disponibilidad…
        </p>
        <template v-else-if="disponibles.length"
          ><div class="compartimentos">
            <button
              v-for="s in disponibles"
              :key="s.configuracion.codigo"
              class="compartimento"
              :class="{ selected: elegido === s.configuracion.codigo }"
              @click="seleccionar(s.configuracion.codigo)"
            >
              <span class="compartimento-code">{{
                s.configuracion.codigo
              }}</span
              ><span>{{
                s.configuracion.clase === "CargaLaptop"
                  ? "Laptop"
                  : s.configuracion.clase === "CargaCelular"
                    ? "Celular"
                    : s.configuracion.tamano
              }}</span
              ><small>{{ s.configuracion.conector || "Disponible" }}</small>
            </button>
          </div>
          <h2 class="section-spacing">
            <IconoSynchro nombre="21_tiempo_tarifa" /> 2. ¿Cuánto tiempo
            necesitas?
          </h2>
          <div class="tablet-choices">
            <button
              v-for="m in duraciones"
              :key="m"
              class="tablet-choice"
              :class="{ selected: minutos === m }"
              @click="minutos = m"
            >
              {{ m }} min
            </button>
          </div></template
        >
        <p v-else class="tablet-note">
          No hay compartimentos disponibles para este servicio. Puedes volver a
          consultar.
        </p>
        <button
          v-if="!disponibles.length && !ocupado"
          class="tablet-button secondary"
          @click="cargar"
        >
          Actualizar disponibilidad
        </button>
      </section>
      <aside class="tablet-panel">
        <span class="tablet-badge">Tu selección</span>
        <h2 class="section-spacing">
          {{ compartimento?.codigo || "Selecciona un espacio" }}
        </h2>
        <p class="tablet-muted">
          {{
            tipo === "carga"
              ? "Carga prepagada. Selecciona un conector compatible con tu equipo."
              : tipo === "personal"
                ? "Resguardo temporal de tus objetos personales."
                : "Temperatura y control térmico simulados en el prototipo."
          }}
        </p>
        <p
          v-if="compartimento?.temperaturaSimulada != null"
          class="tablet-note section-spacing"
        >
          Temperatura simulada: {{ compartimento.temperaturaSimulada }} °C
        </p>
        <div class="tablet-total">
          <span>Total · {{ minutos }} min</span
          ><strong>{{ dinero(monto) }}</strong>
        </div>
        <button
          class="tablet-button full"
          :disabled="ocupado || !compartimento"
          @click="reservar"
        >
          Continuar al pago <IconoSynchro nombre="29_continuar" tema="claro" />
        </button>
        <p class="tablet-muted fine-print">
          El tiempo comienza después del pago y al confirmar que guardaste tus
          objetos.
        </p>
      </aside>
    </div>
    <div v-else-if="paso === 1" class="tablet-two">
      <section class="tablet-panel">
        <h2>Revisa y confirma</h2>
        <ul class="tablet-checks">
          <li>Compartimento {{ reserva.operacion.codigo }}</li>
          <li>{{ reserva.operacion.minutos }} minutos de uso</li>
          <li>Tu PIN se mostrará una sola vez al confirmar el pago.</li>
        </ul>
        <p class="tablet-note section-spacing">
          Pago exclusivamente simulado para el prototipo. No se recibe efectivo
          real ni se procesa una tarjeta.
        </p>
      </section>
      <aside class="tablet-panel">
        <h2><IconoSynchro nombre="22_pagos" /> Total a pagar</h2>
        <div class="tablet-total">
          <strong>{{ dinero(reserva.operacion.monto) }}</strong>
        </div>
        <h3 class="section-spacing">Método de pago simulado</h3>
        <div class="tablet-choices" role="group" aria-label="Método de pago simulado">
          <button
            type="button"
            class="tablet-choice"
            :class="{ selected: metodoPago === 'Tarjeta' }"
            :aria-pressed="metodoPago === 'Tarjeta'"
            :disabled="ocupado"
            @click="metodoPago = 'Tarjeta'"
          >
            Tarjeta
          </button>
          <button
            type="button"
            class="tablet-choice"
            :class="{ selected: metodoPago === 'NFC' }"
            :aria-pressed="metodoPago === 'NFC'"
            :disabled="ocupado"
            @click="metodoPago = 'NFC'"
          >
            NFC
          </button>
          <button
            type="button"
            class="tablet-choice"
            :class="{ selected: metodoPago === 'Efectivo' }"
            :aria-pressed="metodoPago === 'Efectivo'"
            :disabled="ocupado"
            @click="metodoPago = 'Efectivo'"
          >
            Efectivo
          </button>
        </div>
        <p class="tablet-muted fine-print">
          {{ metodoPago === 'Efectivo'
            ? 'Simularemos la recepción del efectivo. No introduzcas billetes ni monedas.'
            : metodoPago === 'Tarjeta'
              ? 'Simularemos el pago con tarjeta bancaria. No ingreses números de tarjeta ni claves reales.'
              : metodoPago === 'NFC'
                ? 'Simularemos un pago sin contacto. No acerques tarjetas ni dispositivos para cobrar.'
                : 'Selecciona uno de los tres métodos de pago para continuar.' }}
        </p>
        <button class="tablet-button full" :disabled="ocupado || !metodoPago" @click="pagar">
          {{ ocupado ? "Confirmando…" : "Confirmar pago simulado" }}</button
        ><button
          class="tablet-button secondary full section-spacing"
          :disabled="ocupado"
          @click="cancelar"
        >
          Cancelar reserva
        </button>
      </aside>
    </div>
    <div v-else-if="paso === 2" class="tablet-two">
      <section class="tablet-panel">
        <span class="tablet-badge">Pago {{ metodoPago === 'Efectivo' ? 'en efectivo' : metodoPago === 'Tarjeta' ? 'con tarjeta' : 'NFC' }} simulado confirmado</span>
        <h2 class="section-spacing">
          Guarda tus objetos en {{ reserva.operacion.codigo }}
        </h2>
        <p class="tablet-muted">
          {{
            reserva.operacion.clase === "CargaLaptop"
              ? "Conecta la laptop con USB-C PD o tu cargador propio y guárdala cerrada."
              : tipo === "carga"
                ? "Conecta el dispositivo al puerto elegido y colócalo dentro del compartimento."
                : "Coloca tus objetos y comprueba que la puerta quede cerrada."
          }}
        </p>
        <label class="confirm-check"
          ><input v-model="confirmado" type="checkbox" />He colocado mis objetos
          y cerrado la puerta.</label
        ><label
          v-if="reserva.operacion.clase === 'CargaLaptop'"
          class="confirm-check"
          ><input v-model="cerrado" type="checkbox" />La laptop está
          cerrada.</label
        ><button
          class="tablet-button full"
          :disabled="ocupado || !confirmado"
          @click="iniciar"
        >
          {{ ocupado ? "Iniciando…" : "Comenzar tiempo de uso" }}
        </button>
      </section>
      <aside class="tablet-panel">
        <h2>Guarda tu PIN de retiro</h2>
        <p v-if="pago.pinRetiro" class="tablet-pin">{{ pago.pinRetiro }}</p>
        <p v-else class="tablet-note">
          El pago ya se había confirmado. El PIN anterior no se muestra de
          nuevo.
        </p>
        <p class="tablet-muted">
          No compartas este PIN. Lo necesitarás junto con la referencia y la
          credencial de la reserva.
        </p>
        <p class="tablet-note section-spacing">
          El casillero y la carga física están simulados.
        </p>
      </aside>
    </div>
    <section v-else class="tablet-panel tablet-receipt" role="status">
      <div class="tablet-receipt-icon">
        <IconoSynchro nombre="30_confirmar" :tamano="36" />
      </div>
      <h2>Todo listo en {{ reserva.operacion.codigo }}</h2>
      <p class="tablet-muted">
        Tu tiempo de {{ reserva.operacion.minutos }} minutos ha comenzado.
        Puedes recuperar tus objetos aunque se agote el tiempo.
      </p>
      <p v-if="pago.pinRetiro" class="tablet-pin">{{ pago.pinRetiro }}</p>
      <details class="recovery-details">
        <summary>Ver datos necesarios para recuperar mis objetos</summary>
        <p>Referencia: {{ reserva.operacion.id }}</p>
        <p>
          Credencial: <code>{{ reserva.credencial }}</code>
        </p>
        <small
          >Datos privados. Consérvalos antes de finalizar; no se guardan en este
          navegador.</small
        >
      </details>
      <div class="tablet-actions">
        <RouterLink class="tablet-button" to="/menu"
          >Ya guardé mis datos · Finalizar</RouterLink
        >
      </div>
    </section>
  </TabletLayout>
</template>
<style scoped>
.compartimentos {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}
.compartimento {
  min-height: 130px;
  border: 1px solid var(--line);
  border-radius: 14px;
  background: var(--surface);
  color: var(--navy);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 12px;
}
.compartimento.selected {
  border: 2px solid var(--teal);
  background: var(--soft);
}
.compartimento-code {
  font-size: 25px;
  font-weight: 700;
}
.compartimento small {
  color: var(--muted);
  font-size: 11px;
}
.section-spacing {
  margin-top: 24px;
}
.full {
  width: 100%;
}
.fine-print {
  font-size: 12px;
  margin-top: 18px;
}
.confirm-check {
  display: flex;
  gap: 12px;
  align-items: center;
  margin: 22px 0;
  font-size: 14px;
}
.confirm-check input {
  width: 24px;
  height: 24px;
  accent-color: #007c89;
}
.recovery-details {
  text-align: left;
  margin-top: 20px;
  font-size: 13px;
  background: var(--soft);
  padding: 15px;
  border-radius: 12px;
  overflow-wrap: anywhere;
}
.recovery-details summary {
  min-height: 40px;
  cursor: pointer;
  font-weight: 700;
}
.recovery-details p {
  padding: 8px 0;
}
.recovery-details code {
  font-size: 12px;
}
@media (max-width: 500px) {
  .compartimentos {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
