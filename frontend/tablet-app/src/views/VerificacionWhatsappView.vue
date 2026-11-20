<script setup>
import { computed, onMounted, onBeforeUnmount, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import QRCode from "qrcode";
import { consumirDescriptorV3, borrarDescriptorV3 } from "../services/descriptorFacialV3Temporal.js";
import { construirInicioFacialV3 } from "../services/solicitudInicioFacialV3.js";
import { useTema } from "../composables/useTema";

const route = useRoute();
const router = useRouter();
const { oscuro } = useTema();

const propsVistaPrevia = defineProps({
  modoVistaPrevia: {
    type: Boolean,
    default: false
  }
});

const fase = ref("preparando");
const qrImagen = ref("");
const error = ref("");
const mensaje = ref("Esperando confirmación de SYNCHRO...");

const casillero = computed(() => {
  const valor = route.query.casillero;
  return typeof valor === "string" &&
    /^[A-Za-z0-9-]{1,10}$/.test(valor)
    ? valor
    : "";
});

const clave = "synchroWhatsappManualActivoV1";
const mensajeCliente =
  "¡Hola, SYNCHRO! Acepto recibir por WhatsApp mi PIN de retiro y las notificaciones relacionadas con el servicio. ¡Gracias!";

let intentoId = "";
let tokenConsulta = "";
let intervalo = null;
let redireccion = null;
let consultando = false;
let pausadoHasta = 0;
let vivo = true;

function limpiarRelojes() {
  clearInterval(intervalo);
  clearTimeout(redireccion);
  intervalo = null;
  redireccion = null;
}

function guardarSesion(urlWhatsapp) {
  sessionStorage.setItem(clave, JSON.stringify({
    casillero: casillero.value,
    intentoId,
    tokenConsulta,
    urlWhatsapp,
    creadoMs: Date.now(),
  }));
}

function borrarSesion() {
  sessionStorage.removeItem(clave);
}

async function generarQrConLogo(contenido, opciones = {}) {
  const canvas = document.createElement("canvas");

  await QRCode.toCanvas(canvas, contenido, {
    ...opciones,
    width: 320,
    margin: 4,
    errorCorrectionLevel: "H",
    color: {
      dark: "#01305C",
      light: "#FFFFFF",
    },
  });

  const logo = new Image();
  logo.src = "/assets/isotipo-fondo-claro.png";

  try {
    await logo.decode();

    const ctx = canvas.getContext("2d");
    if (!ctx) return canvas.toDataURL("image/png");

    const centro = canvas.width / 2;
    const fondo = canvas.width * 0.19;
    const tamanoLogo = canvas.width * 0.145;

    ctx.fillStyle = "#FFFFFF";
    ctx.fillRect(
      centro - fondo / 2,
      centro - fondo / 2,
      fondo,
      fondo
    );

    ctx.drawImage(
      logo,
      centro - tamanoLogo / 2,
      centro - tamanoLogo / 2,
      tamanoLogo,
      tamanoLogo
    );
  } catch {
    // Mantener el QR escaneable sin logo si la imagen falla.
  }

  return canvas.toDataURL("image/png");
}
function mensajeAceptacionPorPertenencia() {
  const pertenencias = {
    mochila: "mi mochila o bolso",
    maleta: "mi maleta o equipaje",
    compras: "mis bolsas de compras",
    caja: "mis objetos personales y cajas",
  };

  const objeto = typeof route.query.objeto === "string"
    ? route.query.objeto
    : "";

  const descripcion =
    pertenencias[objeto] || "mis pertenencias personales";

  return "Hola, SYNCHRO. Acepto recibir por WhatsApp " +
    "mi PIN de retiro y las notificaciones relacionadas " +
    "con el resguardo temporal de " + descripcion + ".";
}
function enlaceCliente(valor) {
  const url = new URL(valor);

  const valido =
    url.protocol === "https:" &&
    (
      (url.hostname === "wa.me" &&
        /^\/[0-9]{8,15}$/.test(url.pathname)) ||
      (url.hostname === "api.whatsapp.com" &&
        url.pathname === "/send")
    );

  if (!valido) {
    throw new Error("El enlace de WhatsApp no es válido.");
  }

      // Conservar el codigo de autorizacion generado por el backend.
  url.searchParams.set("text", mensajeAceptacionPorPertenencia());
  return url.toString();
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
      `No se pudo completar la solicitud (${respuesta.status}).`
    );
    fallo.status = respuesta.status;
    throw fallo;
  }

  return datos;
}

async function csrf() {
  const datos = await solicitar(
    "/api/terminales/guardar/whatsapp/csrf-token"
  );

  if (!datos?.csrfToken) {
    throw new Error("No se obtuvo el token de seguridad.");
  }

  return datos.csrfToken;
}

function obtenerCaptura() {
  const imagen = sessionStorage.getItem("synchroRostroTemporal");

  if (!imagen?.startsWith("data:image/png;base64,")) {
    throw new Error(
      "No se encontró la captura facial. Regresa a la verificación."
    );
  }

  const binario = atob(imagen.split(",")[1]);

  if (!binario.length || binario.length > 1024 * 1024) {
    throw new Error("La captura facial no es válida.");
  }

  return Uint8Array.from(binario, x => x.charCodeAt(0));
}

function empezarConsulta() {
  clearInterval(intervalo);
  intervalo = setInterval(consultarEstado, 20000);
}

async function iniciar() {
  fase.value = "preparando";
  error.value = "";

  try {
    if (!casillero.value) {
      throw new Error("El casillero seleccionado no es válido.");
    }

    const captura = obtenerCaptura();
    const tokenCsrf = await csrf();

    let envioV3 = null;

    const registroV3 =
      import.meta.env.DEV &&
      import.meta.env.VITE_FACIAL_V3_REGISTRO === "true";

    if (registroV3) {
      const meta = JSON.parse(
        sessionStorage.getItem("synchroRostroMeta") || "{}"
      );

      if (meta.consentimientoV3 === true) {
        const descriptor = consumirDescriptorV3();

        if (!descriptor) {
          throw new Error(
            "El descriptor V3 venció. Regresa y repite la captura."
          );
        }

        try {
          envioV3 = construirInicioFacialV3(
            sessionStorage.getItem("synchroRostroTemporal"),
            descriptor,
            true
          );
        } finally {
          descriptor.fill(0);
        }
      }
    }

    if (!envioV3) borrarDescriptorV3();

    const datos = await solicitar(
      "/api/terminales/guardar/whatsapp/iniciar",
      {
        method: "POST",
        headers: {
          "Content-Type": envioV3
            ? "application/json"
            : "application/octet-stream",
          "X-CSRF-TOKEN": tokenCsrf,
          "X-SYNCHRO-CASILLERO": casillero.value,
          "X-SYNCHRO-CONSENTIMIENTO": "ACEPTO",
          ...(envioV3 ? {
            "X-SYNCHRO-CONSENTIMIENTO-BIOMETRICO": "ACEPTO"
          } : {})
        },
        body: envioV3 ? envioV3.body : captura,
      }
    );

    if (
      !datos?.intentoId ||
      !/^[0-9a-fA-F-]{36}$/.test(datos.intentoId) ||
      !/^[0-9a-fA-F]{64}$/.test(datos?.tokenConsulta || "")
    ) {
      throw new Error("El servidor devolvió una solicitud inválida.");
    }

    const url = enlaceCliente(datos.urlWhatsapp);

    intentoId = datos.intentoId;
    tokenConsulta = datos.tokenConsulta;

    // Guardar antes de dibujar el QR para poder
    // recuperar la operación después de F5.
    guardarSesion(datos.urlWhatsapp);

    qrImagen.value = await generarQrConLogo(url, {
      width: 320,
      margin: 2,
      errorCorrectionLevel: "M",
      color: {
        dark: "#01305C",
        light: "#FFFFFF",
      },
    });

    if (!vivo) return;

    sessionStorage.removeItem("synchroRostroTemporal");
    sessionStorage.removeItem("synchroRostroMeta");

    fase.value = "esperando";
    empezarConsulta();
    await consultarEstado();
  } catch (e) {
    if (!vivo) return;

    fase.value = "error";
    error.value = e?.message || "No se pudo generar el QR.";
  }
}

async function recuperarOIniciar() {
  try {
    const captura = sessionStorage.getItem("synchroRostroTemporal");

    if (captura?.startsWith("data:image/png;base64,")) {
      // Una nueva captura corresponde a una nueva solicitud.
      borrarSesion();
      await iniciar();
      return;
    }

    const contenido = sessionStorage.getItem(clave);

    if (!contenido) {
      throw new Error(
        "No hay una operación activa. Regresa al registro facial."
      );
    }

    const guardado = JSON.parse(contenido);

    if (
      guardado.casillero !== casillero.value ||
      !/^[0-9a-fA-F-]{36}$/.test(guardado.intentoId || "") ||
      !/^[0-9a-fA-F]{64}$/.test(guardado.tokenConsulta || "") ||
      !Number.isFinite(guardado.creadoMs) ||
      guardado.creadoMs > Date.now() ||
      Date.now() - guardado.creadoMs > 45 * 60 * 1000
    ) {
      throw new Error(
        "No se puede recuperar la solicitud. Consulta al operador."
      );
    }

    const url = enlaceCliente(guardado.urlWhatsapp);

    intentoId = guardado.intentoId;
    tokenConsulta = guardado.tokenConsulta;

    qrImagen.value = await generarQrConLogo(url, {
      width: 320,
      margin: 2,
      errorCorrectionLevel: "M",
      color: {
        dark: "#01305C",
        light: "#FFFFFF",
      },
    });

    if (!vivo) return;

    fase.value = "esperando";
    empezarConsulta();
    await consultarEstado();
  } catch (e) {
    if (!vivo) return;

    fase.value = "error";
    error.value = e?.message || "No se pudo recuperar la solicitud.";
  }
}

async function consultarEstado() {
  if (
    !vivo ||
    consultando ||
    fase.value !== "esperando" ||
    !intentoId ||
    !tokenConsulta ||
    Date.now() < pausadoHasta
  ) return;

  consultando = true;

  try {
    const datos = await solicitar(
      "/api/terminales/guardar/whatsapp/estado/" +
        encodeURIComponent(intentoId),
      {
        headers: {
          "X-SYNCHRO-INTENTO": tokenConsulta,
        },
      }
    );

    if (!vivo) return;

    if (datos?.estado === "Confirmado") {
      limpiarRelojes();
      borrarSesion();
      fase.value = "finalizado";

      redireccion = setTimeout(() => {
        if (vivo) router.replace("/menu");
      }, 1800);

      return;
    }

    if (
      datos?.reservaConfirmada === true &&
      datos?.pinEntregado === true
    ) {
      limpiarRelojes();
      fase.value = "aprobado";

      redireccion = setTimeout(() => {
        if (!vivo) return;

        router.replace({
          path: "/guardar/deposito",
          query: { casillero: casillero.value },
        });
      }, 1400);

      return;
    }

    if (
      ["Fallido", "Vencido", "Cancelado"].includes(datos?.estado)
    ) {
      limpiarRelojes();
      fase.value = "error";
      error.value =
        "Esta solicitud ya no está disponible. Consulta al operador.";
      return;
    }

    mensaje.value = "Esperando confirmación de SYNCHRO...";
  } catch (e) {
    if (vivo && fase.value === "esperando") {
      if (e?.status === 429) {
        pausadoHasta = Date.now() + 65000;
        mensaje.value =
          "Hay muchas consultas. SYNCHRO continuará la verificación automáticamente.";
      } else {
        mensaje.value = "Sin conexión. Reintentando...";
      }
    }
  } finally {
    consultando = false;
  }
}

onMounted(async () => {
  if (import.meta.env.DEV && propsVistaPrevia.modoVistaPrevia) {
    fase.value = "esperando";
    mensaje.value = "Esperando confirmación...";

    qrImagen.value = await generarQrConLogo(
      "SYNCHRO - VISTA PREVIA SIN OPERACION",
      {
        width: 320,
        margin: 2,
        errorCorrectionLevel: "M",
        color: {
          dark: "#01305C",
          light: "#FFFFFF"
        }
      }
    );
    return;
  }

  await recuperarOIniciar();
});

onBeforeUnmount(() => {
  vivo = false;
  limpiarRelojes();
});
</script>

<template>
  <main class="pantalla-wa" :class="{ 'tema-oscuro': oscuro }">
    <div class="contenido">
      <header class="titulo">
        <h1>
          {{
            fase === "aprobado" || fase === "finalizado"
              ? "Confirmación"
              : "Escanea el QR"
          }}
        </h1>
      </header>

      <section class="centro">
        <div class="qr-marco">
          <div v-if="fase === 'preparando'" class="sin-qr">
            <span class="cargando"></span>
            <p>Preparando código QR...</p>
          </div>

          <img
            v-else-if="fase === 'esperando' && qrImagen"
            class="qr"
            :src="qrImagen"
            alt="Código QR de WhatsApp de SYNCHRO"
          />

          <div
            v-else-if="fase === 'aprobado' || fase === 'finalizado'"
            class="resultado-icono"
            role="status"
          >
            <span aria-hidden="true">✓</span>
            <strong>¡Listo!</strong>
          </div>

          <div v-else class="sin-qr">
            <span class="advertencia">!</span>
            <p>No se pudo continuar.</p>
          </div>
        </div>

        <div class="informacion">
          <span class="codigo-casillero">
            Casillero {{ casillero || "—" }}
          </span>

          <p v-if="fase === 'esperando'">
            Escanea con tu celular.
          </p>

          <div
            v-if="fase === 'esperando'"
            class="espera-synchro"
            role="status"
            aria-live="polite"
          >
            <span class="espera-giro" aria-hidden="true"></span>
            <span>{{ mensaje }}</span>
          </div>

          <p v-if="fase === 'aprobado'">
            Preparando el depósito...
          </p>

          <p v-if="fase === 'finalizado'">
            Depósito confirmado. Regresando al menú...
          </p>
        </div>
      </section>

      <div v-if="error" class="estado" role="alert">
        {{ error }}
      </div>

      <footer class="pie">
        <button
          v-if="fase === 'error'"
          type="button"
          class="reintentar"
          @click="router.replace({
            path: '/guardar/verificar-rostro',
            query: { casillero }
          })"
        >
          Volver a verificación facial
        </button>
      </footer>
    </div>
  </main>
</template>
<style scoped>
.pantalla-wa {
  position: relative;
  height: 100%;
  width: 100%;
  overflow-y: auto;
  background: #f2f7fa;
  color: #01305c;
  font-family: Arial, Helvetica, sans-serif;
}
.contenido {
  width: min(100%, 800px);
  min-height: 100%;
  margin: auto;
  padding: 32px 26px 26px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 20px;
}
.titulo {
  text-align: center;
}
.titulo h1 {
  margin: 12px 0 7px;
  font-size: clamp(30px, 4.5vw, 42px);
  letter-spacing: -.8px;
}
.centro {
  width: min(100%, 620px);
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 18px;
}
.qr-marco {
  width: min(78vw, 355px);
  aspect-ratio: 1;
  padding: 18px;
  border: 2px solid #d3e8ed;
  background: #fff;
  border-radius: 28px;
  box-shadow: 0 14px 38px rgba(1,48,92,.09);
  display: grid;
  place-items: center;
}
.qr {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.sin-qr {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 15px;
  text-align: center;
  color: #5b6770;
}
.cargando {
  width: 45px;
  height: 45px;
  border: 4px solid #d5e9f0;
  border-top-color: #00b9c7;
  border-radius: 50%;
  animation: girar 1s linear infinite;
}
@keyframes girar { to { transform: rotate(360deg); } }
.advertencia {
  display: grid;
  place-items: center;
  width: 58px;
  height: 58px;
  border-radius: 50%;
  background: #fff0e9;
  color: #a6442a;
  font-size: 32px;
  font-weight: 900;
}
.informacion {
  text-align: center;
}
.codigo-casillero {
  display: inline-block;
  background: #e3f4f7;
  border: 1px solid #bde4ea;
  border-radius: 100px;
  padding: 8px 18px;
  font-size: 14px;
  font-weight: 800;
}
.informacion p {
  margin: 0 auto;
  max-width: 490px;
  color: #5b6770;
  font-size: 17px;
  line-height: 1.5;
}
.estado {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  width: min(100%, 620px);
  min-height: 62px;
  padding: 14px 20px;
  border: 1px solid #c9e5ec;
  background: #e8f6f9;
  border-radius: 16px;
  font-size: 15px;
  text-align: center;
  font-weight: 700;
}
.resultado-icono {
  display: grid;
  place-items: center;
  gap: 12px;
  color: #01305c;
}
.resultado-icono span {
  display: grid;
  place-items: center;
  background: #00b9c7;
  width: 115px;
  height: 115px;
  border-radius: 50%;
  font-size: 58px;
  font-weight: 900;
}
.resultado-icono strong { font-size: 30px; }
.pie {
  width: min(100%, 620px);
  text-align: center;
}
.reintentar {
  margin-top: 16px;
  border: 2px solid #00b9c7;
  background: #fff;
  color: #01305c;
  border-radius: 14px;
  padding: 13px 28px;
  min-height: 56px;
  font: inherit;
  font-weight: 800;
  cursor: pointer;
}
@media (max-height: 850px) {
  .contenido { padding: 14px 18px; gap: 12px; }
  .qr-marco { width: min(56vw, 250px); }
  .titulo h1 { font-size: 29px; }
}
@media (prefers-reduced-motion: reduce) {
  .cargando { animation: none; }
}

/* SYNCHRO-WA-TEMA-UNIFICADO-V3 */

#app .synchro-app .pantalla-wa {
  background: #f2f7fa;
  color: #01305c;
  transition:
    background-color 150ms ease,
    color 150ms ease;
}

#app .synchro-app .pantalla-wa.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

#app .synchro-app .pantalla-wa.tema-oscuro
.titulo,
#app .synchro-app .pantalla-wa.tema-oscuro
.centro,
#app .synchro-app .pantalla-wa.tema-oscuro
.informacion,
#app .synchro-app .pantalla-wa.tema-oscuro
.pie {
  background: transparent;
  color: #ffffff;
}

#app .synchro-app .pantalla-wa.tema-oscuro
.informacion p {
  color: #c6dfea;
}

#app .synchro-app .pantalla-wa.tema-oscuro
.codigo-casillero {
  background: #17425b;
  color: #ffffff;
  border-color: #426984;
}

#app .synchro-app .pantalla-wa.tema-oscuro
.qr-marco {
  background: #ffffff;
  border-color: #5ec6e8;
}

#app .synchro-app .pantalla-wa.tema-oscuro
.resultado-icono {
  color: #01305c;
}

#app .synchro-app .pantalla-wa.tema-oscuro
.estado {
  background: #103956;
  color: #ffffff;
  border-color: #315573;
}

#app .synchro-app .pantalla-wa.tema-oscuro

#app .synchro-app .pantalla-wa.tema-oscuro





/* SYNCHRO - Espera de confirmacion */
.espera-synchro {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  margin-top: 10px;
  color: #008c99;
  font-weight: 700;
  text-align: center;
}

.espera-giro {
  display: inline-block;
  width: 23px;
  height: 23px;
  flex-shrink: 0;
  border: 3px solid rgba(0, 185, 199, 0.2);
  border-top-color: #00b9c7;
  border-radius: 50%;
  animation: synchro-girar 1s linear infinite;
}

.tema-oscuro .espera-synchro {
  color: #5ec6e8;
}

@keyframes synchro-girar {
  to { transform: rotate(360deg); }
}

@media (prefers-reduced-motion: reduce) {
  .espera-giro {
    animation: none;
  }
}
</style>
