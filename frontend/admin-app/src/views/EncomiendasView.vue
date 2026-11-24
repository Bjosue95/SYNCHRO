<script setup lang="ts">
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
} from "vue";
import {
  apiAdmin,
  consultarSesion,
  operarEncomienda,
} from "../services/adminApi";

const lista = ref<any[]>([]),
  detalle = ref<any>(null),
  error = ref(""),
  aviso = ref("");
const cargando = ref(false),
  procesando = ref(false),
  administrador = ref(false),
  filtro = ref("");
const pinTemporal = ref<any>(null),
  fotoUrl = ref(""),
  motivoFoto = ref("");
let borrarPin: ReturnType<typeof setTimeout>,
  borrarFoto: ReturnType<typeof setTimeout>;
const consentimientos = reactive<Record<string, any>>({});
const pago = reactive({
  metodo: "",
  importeHnl: "",
  referencia: "",
  justificacionImporte: "",
  pagoComprobado: false,
});
const entrega = reactive({
  telefono: "",
  mensajeReferencia: "",
  envioComprobado: false,
});
const extension = reactive({ incidencia: "", motivo: "", hastaUtc: "" });
const reemplazar = ref(false);
const ficha = ref<HTMLElement | null>(null);
const visibles = computed(() =>
  lista.value.filter((e) => !filtro.value || e.estado === filtro.value),
);
const enPreparacion = computed(() =>
  ["Preparacion", "ListaParaDeposito"].includes(detalle.value?.estado),
);
const autorizado = computed(
  () => detalle.value?.condiciones.consentimientosConfirmados === true,
);
const pagado = computed(
  () => detalle.value?.pago.estado === "ConfirmadoManual",
);
const ruta = () => `/${detalle.value.id}`;
function fecha(valor: string) {
  return valor
    ? new Date(valor).toLocaleString("es-HN", {
        timeZone: "America/Tegucigalpa",
      })
    : "Pendiente";
}
function estado(valor: string) {
  return (
    (
      {
        Preparacion: "En preparación",
        ListaParaDeposito: "Listo para depósito",
        Depositada: "Depositada",
        Retirada: "Entregada",
        ConfirmadoManual: "Confirmado manualmente",
        Pequeno: "Pequeño",
      } as Record<string, string>
    )[valor] || valor
  );
}
function ocultarPin() {
  ocultarValorPin();
  pinTemporal.value = null;
  entrega.telefono = "";
  entrega.mensajeReferencia = "";
  entrega.envioComprobado = false;
}
function ocultarValorPin() {
  clearTimeout(borrarPin);
  if (pinTemporal.value) pinTemporal.value.pin = "";
}
function prepararEntrega(p: any) {
  ocultarPin();
  const participante = detalle.value[p.finalidad === "Deposito" ? "remitente" : "destinatario"];
  pinTemporal.value = { ...p, pin: "", telefono: participante.telefono };
  entrega.telefono = participante.telefono;
}
function ocultarFoto() {
  clearTimeout(borrarFoto);
  if (fotoUrl.value) URL.revokeObjectURL(fotoUrl.value);
  fotoUrl.value = "";
}
async function cargar() {
  cargando.value = true;
  try {
    const d = await apiAdmin("/api/admin/encomiendas/operativas");
    if (!Array.isArray(d?.encomiendas))
      throw new Error("Respuesta de encomiendas inválida.");
    lista.value = d.encomiendas;
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    cargando.value = false;
  }
}
async function seleccionar(id: number, reiniciar = true) {
  if (reiniciar) {
    ocultarPin();
    ocultarFoto();
    aviso.value = "";
    error.value = "";
  }
  const d = await apiAdmin(`/api/admin/encomiendas/operativas/${id}`);
  detalle.value = d;
  if (reiniciar) {
    for (const rol of ["remitente", "destinatario"])
      consentimientos[rol] = {
        telefono: "",
        codigo: "",
        mensajeReferencia: "",
        mensajeComprobado: false,
        aceptaEncomienda: false,
      };
    Object.assign(pago, {
      metodo: "",
      importeHnl:
        d.pago.tarifaConfigurada == null
          ? ""
          : String(d.pago.tarifaConfigurada),
      referencia: "",
      justificacionImporte: "",
      pagoComprobado: false,
    });
    Object.assign(extension, { incidencia: "", motivo: "", hastaUtc: "" });
    motivoFoto.value = "";
    reemplazar.value = false;
  }
}
async function abrir(id: number) {
  if (procesando.value) return;
  procesando.value = true;
  try {
    await seleccionar(id);
    await nextTick();
    ficha.value?.focus({ preventScroll: true });
    ficha.value?.scrollIntoView({
      block: "start",
      behavior: matchMedia("(prefers-reduced-motion: reduce)").matches
        ? "auto"
        : "smooth",
    });
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    procesando.value = false;
  }
}
async function ejecutar(accion: () => Promise<unknown>, mensaje: string) {
  if (procesando.value) return;
  procesando.value = true;
  error.value = "";
  aviso.value = "";
  try {
    await accion();
    aviso.value = mensaje;
    await seleccionar(detalle.value.id, false);
    await cargar();
  } catch (e) {
    error.value = (e as Error).message;
  } finally {
    procesando.value = false;
  }
}
function consentimiento(rol: string, rechazar = false) {
  return ejecutar(
    () =>
      operarEncomienda(
        `${ruta()}/consentimientos/${rol}/${rechazar ? "rechazar" : "confirmar"}`,
        { ...consentimientos[rol] },
      ),
    rechazar ? "Rechazo registrado." : "Consentimiento comprobado y auditado.",
  );
}
function confirmarPago() {
  return ejecutar(
    () =>
      operarEncomienda(`${ruta()}/pago/confirmar`, {
        ...pago,
        importeHnl: Number(pago.importeHnl),
      }),
    "Pago manual confirmado por el servidor.",
  );
}
async function emitir(finalidad: string) {
  ocultarPin();
  await ejecutar(async () => {
    const d = await operarEncomienda(`${ruta()}/pines/${finalidad}/emitir`, {
      reemplazar: reemplazar.value,
    });
    pinTemporal.value = d;
    entrega.telefono = d.telefono;
    borrarPin = setTimeout(ocultarValorPin, 60000);
    reemplazar.value = false;
  }, "PIN emitido. Su valor se ocultará en 60 segundos; todavía debes entregarlo y registrar el envío.");
}
function confirmarEntrega() {
  const pinId = pinTemporal.value?.id;
  return ejecutar(async () => {
    await operarEncomienda(`${ruta()}/pines/entrega`, { pinId, ...entrega });
    ocultarPin();
  }, "Entrega manual del PIN registrada y auditada.");
}
const enlacePin = computed(() => {
  const p = pinTemporal.value;
  if (!p?.pin || !/^\+[1-9][0-9]{7,14}$/.test(p.telefono)) return "";
  const texto = `SYNCHRO · Encomienda #${detalle.value.id}. PIN exclusivo de ${p.finalidad.toLowerCase()}: ${p.pin}. Vence: ${fecha(p.venceUtc)}. No lo compartas.`;
  return `https://wa.me/${p.telefono.slice(1)}?text=${encodeURIComponent(texto)}`;
});
async function consultarFoto(id: string) {
  await ejecutar(async () => {
    ocultarFoto();
    const r = await fetch(
      `/api/admin/encomiendas/operativas${ruta()}/evidencias/${id}/foto`,
      {
        credentials: "same-origin",
        cache: "no-store",
        headers: { "X-SYNCHRO-MOTIVO": motivoFoto.value },
      },
    );
    if (!r.ok) {
      const d = await r.json().catch(() => null);
      throw new Error(d?.mensaje || "No se autorizó consultar la evidencia.");
    }
    fotoUrl.value = URL.createObjectURL(await r.blob());
    borrarFoto = setTimeout(ocultarFoto, 60000);
  }, "Consulta de evidencia auditada. Se ocultará automáticamente en 60 segundos.");
}
function extender(id: string) {
  return ejecutar(
    () =>
      operarEncomienda(`${ruta()}/evidencias/${id}/conservacion`, {
        ...extension,
        hastaUtc: new Date(extension.hastaUtc).toISOString(),
      }),
    "Prórroga de conservación registrada y auditada.",
  );
}
function alOcultar() {
  if (document.hidden) {
    ocultarValorPin();
    ocultarFoto();
  }
}
onMounted(async () => {
  document.addEventListener("visibilitychange", alOcultar);
  try {
    const s = await consultarSesion();
    administrador.value = s.roles.includes("Administrador");
    await cargar();
  } catch (e) {
    error.value = (e as Error).message;
  }
});
onBeforeUnmount(() => {
  document.removeEventListener("visibilitychange", alOcultar);
  ocultarPin();
  ocultarFoto();
});
</script>

<template>
  <section class="enc-page">
    <div class="page-heading">
      <div>
        <h1>Encomiendas</h1>
        <p>
          Consentimientos independientes, pago del remitente y entrega manual de
          PIN.
        </p>
      </div>
      <button
        type="button"
        class="enc-button"
        :disabled="cargando || procesando"
        @click="cargar"
      >
        Actualizar listado
      </button>
    </div>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <p v-if="aviso" class="enc-notice" role="status">{{ aviso }}</p>
    <section class="enc-card" aria-label="Listado de encomiendas">
      <label class="enc-field enc-filter"
        >Estado<select v-model="filtro">
          <option value="">Todos los estados</option>
          <option
            v-for="e in [
              'Preparacion',
              'Depositada',
              'Retirada',
              'Vencida',
              'Rechazada',
            ]"
            :key="e"
            :value="e"
          >
            {{ estado(e) }}
          </option>
        </select></label
      >
      <p v-if="cargando" role="status">Consultando encomiendas…</p>
      <div class="enc-table-wrap">
        <table class="enc-table">
          <caption>
            Últimas 200 encomiendas operativas
          </caption>
          <thead>
            <tr>
              <th>Encomienda</th>
              <th>Estado</th>
              <th>Pago</th>
              <th>Casillero</th>
              <th>Ficha</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="e in visibles"
              :key="e.id"
              :class="{ seleccionada: detalle?.id === e.id }"
            >
              <td>
                <strong>#{{ e.id }}</strong
                ><small>{{ estado(e.tamano) }}</small>
              </td>
              <td>{{ estado(e.estado) }}</td>
              <td>
                {{ estado(e.pagoEstado)
                }}<small v-if="e.importeHnl != null"
                  >L {{ Number(e.importeHnl).toFixed(2) }}</small
                >
              </td>
              <td>{{ e.casillero || "Sin asignar" }}</td>
              <td>
                <button
                  type="button"
                  class="enc-button"
                  :disabled="procesando"
                  :aria-label="'Abrir encomienda ' + e.id"
                  @click="abrir(e.id)"
                >
                  Abrir ficha
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-if="!cargando && !error && !visibles.length">
        No hay encomiendas en este estado.
      </p>
    </section>
    <article
      v-if="detalle"
      ref="ficha"
      tabindex="-1"
      class="enc-detail"
      :aria-busy="procesando"
      aria-label="Ficha operativa de la encomienda"
    >
      <header class="enc-detail-heading">
        <div>
          <h2>Encomienda #{{ detalle.id }}</h2>
          <p>{{ estado(detalle.tamano) }} · {{ estado(detalle.estado) }}</p>
        </div>
        <span>Vigencia de preparación: {{ fecha(detalle.venceUtc) }}</span>
      </header>
      <div class="enc-participants">
        <section
          v-for="rol in ['remitente', 'destinatario']"
          :key="rol"
          class="enc-card"
        >
          <h3>{{ rol === "remitente" ? "Remitente" : "Destinatario" }}</h3>
          <dl class="enc-facts">
            <div>
              <dt>
                {{
                  rol === "remitente"
                    ? "Cuenta vinculada"
                    : "Contacto independiente"
                }}
              </dt>
              <dd>#{{ detalle[rol].personaId }}</dd>
            </div>
            <div>
              <dt>WhatsApp</dt>
              <dd>{{ detalle[rol].telefono }}</dd>
            </div>
            <div>
              <dt>Comunicaciones</dt>
              <dd>{{ estado(detalle[rol].consentimiento) }}</dd>
            </div>
            <div v-if="rol === 'destinatario'">
              <dt>Aceptación de encomienda</dt>
              <dd>
                {{ detalle[rol].aceptaEncomienda ? "Comprobada" : "Pendiente" }}
              </dd>
            </div>
            <div v-else>
              <dt>Referencia facial V3</dt>
              <dd>
                {{ detalle.facial.vigente ? "Vigente" : "Pendiente o vencida" }}
              </dd>
            </div>
            <div v-if="detalle[rol].verificadoUtc">
              <dt>Comprobación manual</dt>
              <dd>
                {{ fecha(detalle[rol].verificadoUtc) }} ·
                {{ detalle[rol].administrador }}
              </dd>
            </div>
          </dl>
          <form
            v-if="enPreparacion && detalle[rol].consentimiento === 'Pendiente'"
            class="enc-form"
            @submit.prevent="consentimiento(rol)"
          >
            <p class="enc-help">
              Compara el mensaje recibido en el WhatsApp oficial con este
              código:
              <strong class="enc-code">{{ detalle[rol].codigo }}</strong>. El
              enlace no confirma la autorización.
            </p>
            <label class="enc-field"
              >Número desde el que se recibió<input
                v-model="consentimientos[rol].telefono"
                type="tel"
                autocomplete="off"
                required
                :disabled="procesando"
                placeholder="+504…"
            /></label>
            <label class="enc-field"
              >Código del mensaje recibido<input
                v-model="consentimientos[rol].codigo"
                autocomplete="off"
                required
                maxlength="32"
                :disabled="procesando"
            /></label>
            <label class="enc-field"
              >Referencia comprobable del mensaje<input
                v-model="consentimientos[rol].mensajeReferencia"
                required
                maxlength="160"
                :disabled="procesando"
            /></label>
            <label class="enc-check"
              ><input
                v-model="consentimientos[rol].mensajeComprobado"
                type="checkbox"
                :disabled="procesando"
              />
              He comprobado el mensaje y la autorización desde este
              número.</label
            >
            <label v-if="rol === 'destinatario'" class="enc-check"
              ><input
                v-model="consentimientos[rol].aceptaEncomienda"
                type="checkbox"
                :disabled="procesando"
              />
              El destinatario aceptó recibir esta encomienda.</label
            >
            <div class="enc-actions">
              <button
                class="enc-button enc-primary"
                :disabled="
                  procesando ||
                  !consentimientos[rol].mensajeComprobado ||
                  (rol === 'destinatario' &&
                    !consentimientos[rol].aceptaEncomienda)
                "
              >
                Confirmar consentimiento</button
              ><button
                type="button"
                class="enc-button"
                :disabled="
                  procesando ||
                  !consentimientos[rol].mensajeComprobado ||
                  !consentimientos[rol].telefono ||
                  !consentimientos[rol].codigo ||
                  !consentimientos[rol].mensajeReferencia
                "
                @click="consentimiento(rol, true)"
              >
                Registrar rechazo
              </button>
            </div>
          </form>
        </section>
      </div>
      <section class="enc-card">
        <h3>Pago del remitente</h3>
        <dl class="enc-facts">
          <div>
            <dt>Estado</dt>
            <dd>{{ estado(detalle.pago.estado) }}</dd>
          </div>
          <div>
            <dt>Importe HNL</dt>
            <dd>
              {{
                detalle.pago.importeHnl == null
                  ? "Sin importe definido"
                  : Number(detalle.pago.importeHnl).toFixed(2)
              }}
            </dd>
          </div>
          <div v-if="pagado">
            <dt>Confirmado por</dt>
            <dd>
              {{ detalle.pago.administrador }} ·
              {{ fecha(detalle.pago.fechaUtc) }} · {{ detalle.pago.metodo }}
            </dd>
          </div>
        </dl>
        <p class="enc-help">
          Sin pasarela de pago. Confirma únicamente dinero efectivamente
          recibido. Un cobro simulado de otros servicios no acredita este pago.
          El destinatario no paga de nuevo.
        </p>
        <form
          v-if="enPreparacion && !pagado"
          class="enc-form enc-payment"
          @submit.prevent="confirmarPago"
        >
          <label class="enc-field"
            >Método<select
              v-model="pago.metodo"
              required
              :disabled="procesando || !autorizado"
            >
              <option value="" disabled>Selecciona un método</option>
              <option>Efectivo</option>
              <option>Transferencia</option>
              <option>Tarjeta</option>
            </select></label
          >
          <label class="enc-field"
            >Importe comprobado (HNL)<input
              v-model="pago.importeHnl"
              type="number"
              min="0.01"
              max="100000"
              step="0.01"
              required
              :readonly="detalle.pago.tarifaConfigurada != null"
              :disabled="procesando || !autorizado"
          /></label>
          <label class="enc-field"
            >Referencia del comprobante<input
              v-model="pago.referencia"
              required
              maxlength="160"
              :disabled="procesando || !autorizado"
          /></label>
          <label v-if="detalle.pago.tarifaConfigurada == null" class="enc-field"
            >Justificación del importe sin tarifa configurada<textarea
              v-model="pago.justificacionImporte"
              required
              maxlength="500"
              :disabled="procesando || !autorizado"
            ></textarea>
          </label>
          <label class="enc-check"
            ><input
              v-model="pago.pagoComprobado"
              type="checkbox"
              :disabled="procesando || !autorizado"
            />
            He verificado que este importe fue recibido por SYNCHRO.</label
          ><button
            class="enc-button enc-primary"
            :disabled="
              procesando ||
              !autorizado ||
              !pago.pagoComprobado ||
              Number(pago.importeHnl) <= 0
            "
          >
            Confirmar pago recibido
          </button>
          <p v-if="!autorizado" class="enc-help">
            Primero comprueba los consentimientos de ambos participantes.
          </p>
        </form>
      </section>
      <section class="enc-card">
        <h3>PIN independientes</h3>
        <p class="enc-help">
          Un PIN de depósito para el remitente y otro de retiro para el
          destinatario. Cada uno es temporal y de un solo uso. Emitirlo no
          confirma su envío por WhatsApp.
        </p>
        <ul>
          <li v-for="p in detalle.pines" :key="p.id">
            {{ p.finalidad }}: {{ p.estado }} · Vence {{ fecha(p.venceUtc) }}
            <button v-if="p.estado === 'Emitido'" type="button" class="enc-button" :disabled="procesando || !pagado" @click="prepararEntrega(p)">Registrar entrega manual</button>
          </li>
        </ul>
        <div class="enc-actions">
          <button
            type="button"
            class="enc-button enc-primary"
            :disabled="
              procesando ||
              !enPreparacion ||
              !pagado ||
              !autorizado ||
              !detalle.facial.vigente
            "
            @click="emitir('deposito')"
          >
            Emitir PIN de depósito</button
          ><button
            type="button"
            class="enc-button enc-primary"
            :disabled="
              procesando ||
              detalle.estado !== 'Depositada' ||
              !pagado ||
              detalle.destinatario.consentimiento !== 'Confirmado'
            "
            @click="emitir('retiro')"
          >
            Emitir PIN de retiro
          </button>
        </div>
        <label class="enc-check"
          ><input v-model="reemplazar" type="checkbox" :disabled="procesando" />
          Reemitir e invalidar el PIN anterior de la misma finalidad.</label
        >
        <form
          v-if="pinTemporal"
          class="enc-pin enc-form"
          @submit.prevent="confirmarEntrega"
        >
          <h4>Entrega manual · {{ pinTemporal.finalidad }}</h4>
          <output v-if="pinTemporal.pin" class="enc-pin-value" aria-label="PIN temporal">{{
            pinTemporal.pin
          }}</output>
          <p v-else class="enc-help">El valor del PIN está oculto. Si ya lo entregaste, registra aquí la referencia del mensaje. El servidor no permite recuperarlo.</p>
          <p>
            Número autorizado: {{ pinTemporal.telefono }}. Vence
            {{ fecha(pinTemporal.venceUtc) }}.
          </p>
          <a
            v-if="enlacePin"
            :href="enlacePin"
            class="enc-button"
            target="_blank"
            rel="noopener noreferrer"
            >Abrir WhatsApp para entrega manual</a
          >
          <p class="enc-help">
            Comprueba el destinatario y envía el mensaje manualmente. El valor
            se oculta a los 60 segundos o al salir de esta ficha.
          </p>
          <label class="enc-field"
            >Número al que se entregó<input
              v-model="entrega.telefono"
              type="tel"
              required /></label
          ><label class="enc-field"
            >Referencia del mensaje enviado<input
              v-model="entrega.mensajeReferencia"
              required
              maxlength="160" /></label
          ><label class="enc-check"
            ><input v-model="entrega.envioComprobado" type="checkbox" />
            Comprobé la entrega manual al número autorizado.</label
          >
          <div class="enc-actions">
            <button
              class="enc-button enc-primary"
              :disabled="procesando || !entrega.envioComprobado"
            >
              Registrar entrega del PIN</button
            ><button type="button" class="enc-button" @click="ocultarValorPin">
              Ocultar PIN
            </button>
          </div>
        </form>
      </section>
      <section class="enc-card">
        <h3>Depósito, retiro y evidencia</h3>
        <p>
          Estado: {{ estado(detalle.estado) }}. Uso vinculado:
          {{ detalle.usoId || "Pendiente" }}. Terminal:
          {{ detalle.terminalId || "Pendiente" }}.
        </p>
        <p class="enc-help">
          La tablet está pendiente de integración. La apertura física continúa
          simulada. La fotografía del destinatario es evidencia; no acredita una
          coincidencia biométrica.
        </p>
        <div v-for="e in detalle.evidencias" :key="e.id" class="enc-evidence">
          <strong>{{
            e.tipo === "CapturaDestinatario"
              ? "Captura del destinatario"
              : "Verificación del remitente"
          }}</strong>
          <p>
            {{ fecha(e.capturadaUtc) }} ·
            {{
              e.coincidenciaBiometrica === true
                ? "Referencia facial del remitente comprobada"
                : "Sin afirmación de coincidencia biométrica del destinatario"
            }}
          </p>
          <template
            v-if="administrador && e.fotografiaDisponible && e.entregaUtc"
            ><label class="enc-field"
              >Motivo de acceso a la evidencia<input
                v-model="motivoFoto"
                maxlength="500" /></label
            ><button
              type="button"
              class="enc-button"
              :disabled="procesando || !motivoFoto.trim()"
              @click="consultarFoto(e.id)"
            >
              Consultar fotografía con auditoría
            </button>
            <details>
              <summary>Prorrogar conservación por incidencia formal</summary>
              <form class="enc-form" @submit.prevent="extender(e.id)">
                <label class="enc-field"
                  >Referencia de incidencia<input
                    v-model="extension.incidencia"
                    required
                    maxlength="160" /></label
                ><label class="enc-field"
                  >Justificación<textarea
                    v-model="extension.motivo"
                    required
                    maxlength="500"
                  ></textarea></label
                ><label class="enc-field"
                  >Fecha límite de prórroga<input
                    v-model="extension.hastaUtc"
                    type="datetime-local"
                    required
                /></label>
                <p class="enc-help">
                  Cada extensión requiere una incidencia y queda limitada a los
                  próximos 30 días.
                </p>
                <button class="enc-button" :disabled="procesando">
                  Registrar prórroga
                </button>
              </form>
            </details></template
          >
        </div>
        <div v-if="fotoUrl">
          <img
            :src="fotoUrl"
            class="enc-photo"
            alt="Evidencia restringida de la persona que retiró la encomienda"
          /><button type="button" class="enc-button" @click="ocultarFoto">
            Ocultar fotografía
          </button>
        </div>
      </section>
      <section class="enc-card">
        <h3>Historial auditado</h3>
        <ol class="enc-history">
          <li v-for="h in detalle.historial" :key="h.id">
            <time>{{ fecha(h.fechaUtc) }}</time>
            <div>
              <strong>{{ h.accion }}</strong>
              <p>{{ h.detalle }}</p>
              <small
                >{{ h.estado }} · {{ h.actor
                }}<template v-if="h.administrador">
                  · {{ h.administrador }}</template
                ></small
              >
            </div>
          </li>
        </ol>
        <p class="enc-help">
          Últimas 300 acciones. Los valores de los PIN y las imágenes no forman
          parte del historial.
        </p>
      </section>
    </article>
  </section>
</template>

<style scoped>
.enc-page {
  display: grid;
  gap: 22px;
  color: var(--ink);
}
.enc-page h2,
.enc-page h3 {
  margin: 0 0 14px;
}
.enc-card {
  background: var(--white);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 24px;
  min-width: 0;
}
.enc-detail {
  display: grid;
  gap: 22px;
  scroll-margin-top: 90px;
}
.enc-detail-heading,
.enc-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.enc-participants {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 22px;
}
.enc-field {
  display: grid;
  gap: 7px;
  font-size: 14px;
  font-weight: 600;
  min-width: 0;
}
.enc-field input,
.enc-field select,
.enc-field textarea {
  width: 100%;
  min-height: 44px;
  padding: 10px 12px;
  border: 1px solid var(--border);
  background: var(--white);
  color: var(--ink);
  border-radius: 7px;
}
.enc-field textarea {
  min-height: 84px;
  resize: vertical;
}
.enc-form {
  display: grid;
  gap: 16px;
  margin-top: 20px;
}
.enc-check {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  line-height: 1.5;
  padding: 8px 0;
}
.enc-check input {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  margin-top: 2px;
  accent-color: var(--teal);
}
.enc-button {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  min-height: 44px;
  padding: 10px 16px;
  border: 1px solid var(--border);
  border-radius: 7px;
  background: var(--white);
  color: var(--navy);
  font-weight: 650;
  text-align: center;
}
.enc-primary {
  background: var(--navy);
  color: var(--white);
  border-color: var(--navy);
}
.enc-help,
.enc-facts dt,
.enc-detail-heading > span {
  color: var(--muted);
  font-size: 13px;
  line-height: 1.65;
}
.enc-facts {
  display: grid;
  gap: 12px;
  margin: 0;
}
.enc-facts div {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}
.enc-facts dd {
  margin: 0;
  overflow-wrap: anywhere;
}
.enc-actions {
  justify-content: flex-start;
}
.enc-filter {
  max-width: 270px;
  margin-bottom: 18px;
}
.enc-table-wrap {
  overflow: auto;
  max-height: 470px;
}
.enc-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 14px;
}
.enc-table caption {
  text-align: left;
  padding-bottom: 12px;
  color: var(--muted);
  font-size: 13px;
}
.enc-table th,
.enc-table td {
  padding: 14px 12px;
  border-bottom: 1px solid var(--border);
  white-space: nowrap;
}
.enc-table small {
  display: block;
  color: var(--muted);
  margin-top: 5px;
}
.enc-table .seleccionada {
  background: var(--canvas);
}
.enc-notice,
.enc-pin {
  background: var(--canvas);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 18px;
}
.enc-pin-value {
  font-size: 32px;
  letter-spacing: 0.15em;
  font-variant-numeric: tabular-nums;
  color: var(--navy);
  font-weight: 750;
}
.enc-code {
  overflow-wrap: anywhere;
}
.enc-evidence {
  border-top: 1px solid var(--border);
  padding: 18px 0;
}
.enc-evidence details {
  margin-top: 18px;
}
.enc-evidence summary {
  cursor: pointer;
  padding: 12px 0;
}
.enc-photo {
  display: block;
  max-width: min(100%, 280px);
  margin: 18px 0;
  border-radius: 8px;
}
.enc-history {
  list-style: none;
  padding: 0;
  margin: 0;
}
.enc-history li {
  display: grid;
  grid-template-columns: 170px minmax(0, 1fr);
  gap: 18px;
  padding: 18px 0;
  border-bottom: 1px solid var(--border);
}
.enc-history time,
.enc-history small {
  font-size: 12px;
  color: var(--muted);
}
.enc-history p {
  margin: 6px 0;
  overflow-wrap: anywhere;
}
.enc-detail-heading span {
  max-width: 310px;
}
@media (max-width: 1000px) {
  .enc-participants {
    grid-template-columns: 1fr;
  }
  .enc-card {
    padding: 20px;
  }
}
@media (max-width: 540px) {
  .enc-history li {
    grid-template-columns: 1fr;
    gap: 6px;
  }
  .enc-card {
    padding: 16px;
  }
  .enc-facts div {
    grid-template-columns: 1fr;
    gap: 2px;
  }
  .enc-actions > * {
    width: 100%;
  }
}
</style>
