<script setup>
import { computed, ref, onBeforeUnmount } from "vue";
import { useRoute, RouterLink } from "vue-router";
import IconoSynchro from "../components/IconoSynchro.vue";
import TabletLayout from "../components/TabletLayout.vue";
import CapturaRostro from "../components/CapturaRostro.vue";
import { solicitar, esDemo } from "../services/tabletApi";
const route = useRoute();
const retiro = computed(() => route.meta.retiro === true);
const paso = ref(0),
  ocupado = ref(false),
  error = ref(""),
  referencia = ref(""),
  credencial = ref(""),
  pin = ref(""),
  casillero = ref(""),
  reserva = ref(null),
  evidencia = ref(null),
  sesion = ref(false),
  verificacion = ref(""),
  codigo = ref("");
const controlador = new AbortController();
const pasos = computed(() =>
  retiro.value
    ? ["Tu encomienda", "Tu rostro", "PIN y retiro"]
    : ["Tu reserva", "Tu rostro", "Depósito"],
);
async function ejecutar(accion) {
  if (ocupado.value) return;
  ocupado.value = true;
  error.value = "";
  try {
    await accion();
  } catch (e) {
    if (e.name !== "AbortError") error.value = e.message;
  } finally {
    ocupado.value = false;
  }
}
function headers() {
  return { "X-SYNCHRO-RESERVA": credencial.value.trim() };
}
async function consultar() {
  await ejecutar(async () => {
    if (
      !Number.isSafeInteger(Number(referencia.value)) ||
      Number(referencia.value) <= 0
    )
      throw new Error("Introduce un número de referencia válido.");
    if (retiro.value) {
      await solicitar("/api/destinatarios/mi-sesion", {
        signal: controlador.signal,
      });
      sesion.value = true;
      paso.value = 1;
      return;
    }
    if (!/^[A-Fa-f0-9]{64}$/.test(credencial.value.trim()))
      throw new Error(
        "Revisa la credencial de 64 caracteres que recibiste con tu reserva.",
      );
    reserva.value = await solicitar(
      "/api/terminales/encomiendas/reservas/consultar",
      {
        metodo: "POST",
        datos: {
          referencia: Number(referencia.value),
          tokenGestion: credencial.value.trim(),
        },
        signal: controlador.signal,
      },
    );
    if (!reserva.value.puedeContinuarVerificacion)
      throw new Error(
        reserva.value.mensaje ||
          "El destinatario debe confirmar el consentimiento por WhatsApp antes del depósito.",
      );
    paso.value = 1;
  });
}
async function login() {
  await ejecutar(async () => {
    if (
      !Number.isSafeInteger(Number(verificacion.value)) ||
      Number(verificacion.value) <= 0 ||
      !/^\d{6}$/.test(codigo.value)
    )
      throw new Error(
        "Revisa la referencia de verificación y el código de seis dígitos.",
      );
    await solicitar("/api/destinatarios/login", {
      metodo: "POST",
      csrf: "/api/destinatarios/csrf-token",
      datos: {
        verificacionId: Number(verificacion.value),
        codigo: codigo.value,
      },
      signal: controlador.signal,
    });
    sesion.value = true;
    codigo.value = "";
  });
}
async function verificar(blob) {
  await ejecutar(async () => {
    const ruta = retiro.value
      ? `/api/terminales/encomiendas/usos/${referencia.value}/evidencias-destinatario`
      : `/api/terminales/encomiendas/reservas/${referencia.value}/evidencias-remitente`;
    evidencia.value = await solicitar(ruta, {
      metodo: "POST",
      datos: blob,
      encabezados: headers(),
      signal: controlador.signal,
    });
    paso.value = 2;
  });
}
async function confirmar() {
  await ejecutar(async () => {
    if (new Date(evidencia.value.venceUtc) <= new Date()) {
      paso.value = 1;
      evidencia.value = null;
      throw new Error("La captura venció. Vuelve a verificar tu rostro.");
    }
    if (retiro.value && !/^\d{6}$/.test(pin.value))
      throw new Error(
        "Introduce el PIN de seis dígitos recibido por WhatsApp.",
      );
    if (!retiro.value && !/^[A-Za-z0-9-]{1,10}$/.test(casillero.value))
      throw new Error("Introduce el código de un casillero disponible.");
    const ruta = retiro.value
      ? `/api/terminales/encomiendas/usos/${referencia.value}/retirar`
      : `/api/terminales/encomiendas/reservas/${referencia.value}/depositar`;
    await solicitar(ruta, {
      metodo: "POST",
      datos: retiro.value
        ? { evidenciaId: evidencia.value.evidenciaId, pin: pin.value }
        : {
            evidenciaId: evidencia.value.evidenciaId,
            codigoCasillero: casillero.value,
          },
      encabezados: headers(),
      signal: controlador.signal,
    });
    credencial.value = "";
    pin.value = "";
    evidencia.value = null;
    paso.value = 3;
  });
}
function rellenarDemo() {
  if (esDemo) {
    referencia.value = retiro.value ? "17" : "1";
    credencial.value = "A".repeat(64);
    verificacion.value = "12";
    codigo.value = "123456";
    casillero.value = "E01";
  }
}
onBeforeUnmount(() => {
  controlador.abort();
  credencial.value = "";
  pin.value = "";
  codigo.value = "";
  evidencia.value = null;
});
</script>
<template>
  <TabletLayout
    :titulo="
      retiro ? 'Retira tu encomienda' : 'Tu reserva, lista para depositar'
    "
    :descripcion="
      retiro
        ? 'Tu captura y el PIN de WhatsApp nos ayudan a comprobar el retiro.'
        : 'Usa la reserva que creaste desde tu cuenta en la web. No necesitas repetir el teléfono del destinatario.'
    "
    :pasos="paso < 3 ? pasos : []"
    :paso="paso"
  >
    <p v-if="error" role="alert" class="tablet-error">{{ error }}</p>
    <div v-if="paso === 0" class="tablet-two">
      <section class="tablet-panel">
        <h2>
          {{ retiro ? "Encuentra tu encomienda" : "Consulta tu reserva" }}
        </h2>
        <form class="tablet-form" @submit.prevent="consultar">
          <label
            >{{ retiro ? "Número de encomienda" : "Número de reserva"
            }}<input
              v-model="referencia"
              class="tablet-input"
              inputmode="numeric"
              autocomplete="off"
              required
              placeholder="Ej. 1234" /></label
          ><label v-if="!retiro"
            >Credencial de tu reserva<input
              v-model="credencial"
              class="tablet-input"
              type="password"
              autocomplete="off"
              maxlength="64"
              required
              placeholder="La credencial privada de tu reserva web"
            /><small
              >La recibiste al crear la reserva. No la compartas.</small
            ></label
          ><button class="tablet-button" :disabled="ocupado">
            {{ ocupado ? "Consultando…" : "Continuar"
            }}<IconoSynchro
              v-if="!ocupado"
              nombre="29_continuar"
              tema="claro"
            />
          </button>
        </form>
        <button
          v-if="esDemo"
          class="demo-fill"
          type="button"
          @click="rellenarDemo"
        >
          Rellenar datos de ejemplo
        </button>
        <form
          v-if="retiro && !sesion"
          class="tablet-form recipient-login"
          @submit.prevent="login"
        >
          <p class="tablet-muted">
            Si aún no has verificado tu acceso, utiliza los datos de tu
            verificación telefónica.
          </p>
          <label
            >Referencia de verificación<input
              v-model="verificacion"
              class="tablet-input"
              inputmode="numeric"
              required
              autocomplete="off" /></label
          ><label
            >Código de acceso<input
              v-model="codigo"
              class="tablet-input"
              inputmode="numeric"
              type="password"
              maxlength="6"
              required
              autocomplete="off" /></label
          ><button class="tablet-button secondary" :disabled="ocupado">
            Verificar mi acceso
          </button>
          <p v-if="sesion" role="status">Acceso verificado.</p>
        </form>
        <p v-if="retiro && sesion" role="status" class="tablet-note">
          Acceso verificado. Puedes continuar con tu encomienda.
        </p>
      </section>
      <aside class="tablet-panel">
        <span class="tablet-badge">{{
          retiro
            ? "Solo tú puedes retirarla"
            : "Encomiendas de clientes registrados"
        }}</span>
        <h2 class="aside-title">Antes de continuar</h2>
        <ul class="tablet-checks">
          <li>
            {{
              retiro
                ? "Ten a mano el PIN temporal de WhatsApp."
                : "Tu reserva debe estar vigente y creada en la web."
            }}
          </li>
          <li>El destinatario debe haber aceptado los avisos por WhatsApp.</li>
          <li>
            Cada persona utiliza su propia referencia de rostro registrada.
          </li>
          <li>La comprobación de rostro es una simulación del prototipo.</li>
        </ul>
      </aside>
    </div>
    <div v-else-if="paso === 1" class="tablet-two">
      <section class="tablet-panel">
        <h2>
          {{
            retiro
              ? "Verifiquemos al destinatario"
              : "Verifiquemos al remitente"
          }}
        </h2>
        <CapturaRostro :ocupado="ocupado" @captura="verificar" />
      </section>
      <aside class="tablet-panel">
        <h2>Una captura, solo para este paso</h2>
        <p class="tablet-muted">
          Se compara con la referencia de
          {{ retiro ? "quien retira" : "quien deposita" }}. La captura del
          remitente no sirve para verificar al destinatario.
        </p>
        <p class="tablet-note" style="margin-top: 20px">
          Esta demostración no realiza reconocimiento facial profesional ni
          prueba de vida física.
        </p>
      </aside>
    </div>
    <div v-else-if="paso === 2" class="tablet-two">
      <section class="tablet-panel">
        <span class="tablet-badge">Captura simulada comprobada</span>
        <h2 class="aside-title">
          {{ retiro ? "Ingresa tu PIN temporal" : "Confirma el casillero" }}
        </h2>
        <form class="tablet-form" @submit.prevent="confirmar">
          <label v-if="retiro"
            >PIN recibido por WhatsApp<input
              v-model="pin"
              class="tablet-input"
              type="password"
              inputmode="numeric"
              maxlength="6"
              autocomplete="off"
              required
              placeholder="••••••" /></label
          ><label v-else
            >Código del casillero disponible<input
              v-model="casillero"
              class="tablet-input"
              autocomplete="off"
              maxlength="10"
              required
              placeholder="Ej. E01"
          /></label>
          <p v-if="retiro && esDemo" class="tablet-note">
            PIN ficticio para esta demostración: 654321.
          </p>
          <button class="tablet-button" :disabled="ocupado">
            {{
              ocupado
                ? "Confirmando…"
                : retiro
                  ? "Confirmar retiro"
                  : "Confirmar depósito"
            }}
          </button>
        </form>
        <p class="tablet-muted" style="font-size: 12px; margin-top: 16px">
          La captura es temporal. Si vence, te pediremos repetirla antes de
          continuar.
        </p>
      </section>
      <aside class="tablet-panel">
        <h2>
          {{ retiro ? "Revisa tu PIN" : "Tu destinatario recibirá el PIN" }}
        </h2>
        <p class="tablet-muted">
          {{
            retiro
              ? "El PIN es personal y tiene una vigencia limitada. Una captura aprobada por sí sola no autoriza el retiro."
              : "El PIN temporal se envía por WhatsApp al número que ya está guardado en tu reserva. No se muestra en esta terminal."
          }}
        </p>
        <p class="tablet-note" style="margin-top: 20px">
          La apertura del casillero es simulada en este prototipo.
        </p>
      </aside>
    </div>
    <section v-else class="tablet-panel tablet-receipt" role="status">
      <div class="tablet-receipt-icon">
        <IconoSynchro nombre="30_confirmar" :tamano="36" />
      </div>
      <h2>{{ retiro ? "Retiro registrado" : "Depósito registrado" }}</h2>
      <p class="tablet-muted">
        {{
          retiro
            ? "El PIN y la captura se han utilizado para este retiro."
            : "La notificación con el PIN de retiro quedó encolada para WhatsApp."
        }}
      </p>
      <p class="tablet-note" style="margin-top: 20px">
        {{
          esDemo
            ? "Recorrido con datos ficticios. No se enviaron mensajes ni se abrió un casillero real."
            : "Prototipo: la apertura física es simulada. Una notificación encolada aún no confirma su entrega."
        }}
      </p>
      <div class="tablet-actions">
        <RouterLink class="tablet-button" to="/menu"
          >Finalizar y volver al inicio</RouterLink
        >
      </div>
    </section>
  </TabletLayout>
</template>
<style scoped>
.demo-fill {
  border: 0;
  background: none;
  color: var(--teal);
  text-decoration: underline;
  min-height: 48px;
  padding: 12px 0;
  font-size: 13px;
}
.aside-title {
  margin-top: 22px;
}
.recipient-login {
  margin-top: 20px;
  padding-top: 20px;
  border-top: 1px solid var(--line);
}
</style>
