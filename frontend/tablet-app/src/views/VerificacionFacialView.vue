<script setup>
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { registrarCaptura } from "../services/retiroSesion";
import { validarRostroSimuladoRetiro } from "../services/retiroFacialManualApi";

// SYNCHRO-FACIAL-FLUJO-LIMPIO-V5
// Guardar: solo detectar y capturar.
// Retiro: comparar con la referencia registrada.

const route = useRoute();
const router = useRouter();
const { oscuro } = useTema();
const propsVistaPrevia = defineProps({
  modoRetiroEjemplo: {
    type: Boolean,
    default: false
  }
});

const retirando = computed(() =>
  (import.meta.env.DEV && propsVistaPrevia.modoRetiroEjemplo) ||
  route.path.startsWith("/retirar/")
);

const video = ref(null);
const consentimiento = ref(false);
const encendida = ref(false);
const iniciando = ref(false);
const detectado = ref(false);
const tomada = ref(false);
const validandoEnServidor = ref(false);

const fotoUrl = ref("");
const fotoBase64 = ref("");
const estado = ref("Pulsa Comenzar escaneo para iniciar.");
const error = ref("");

const casillero = computed(() => {
  const codigo = route.query.casillero;
  return (
    typeof codigo === "string" &&
    /^[A-Za-z0-9-]{1,10}$/.test(codigo)
  ) ? codigo : "Sin seleccionar";
});

let motor = null;
let stream = null;
let timer = null;
let secuencia = 0;
let consecutivos = 0;
let montada = true;

function limpiarCaptura() {
  if (fotoUrl.value) {
    URL.revokeObjectURL(fotoUrl.value);
  }

  fotoUrl.value = "";
  fotoBase64.value = "";
  tomada.value = false;
}

function detenerCamara() {
  secuencia++;

  if (timer !== null) {
    clearTimeout(timer);
    timer = null;
  }

  if (stream) {
    stream.getTracks().forEach(track => track.stop());
    stream = null;
  }

  if (video.value) {
    video.value.pause();
    video.value.srcObject = null;
  }

  encendida.value = false;
  detectado.value = false;
  consecutivos = 0;
}

async function prepararMotor() {
  if (motor) return motor;

  if (!window.Human) {
    await new Promise((resolve, reject) => {
      const script = document.createElement("script");
      script.src =
        "https://cdn.jsdelivr.net/npm/@vladmandic/human@3.3.6/dist/human.js";
      script.async = true;
      script.onload = resolve;
      script.onerror = () =>
        reject(new Error("No se pudo cargar Human.js. Revisa internet."));
      document.head.appendChild(script);
    });
  }

  const Human = window.Human?.Human ?? window.Human;

  if (typeof Human !== "function") {
    throw new Error("Human.js no esta disponible.");
  }

  const nuevo = new Human({
    backend: "webgl",
    modelBasePath:
      "https://cdn.jsdelivr.net/npm/@vladmandic/human@3.3.6/models/",
    cacheSensitivity: 0,
    face: {
      enabled: true,
      detector: {
        enabled: true,
        maxDetected: 2,
      },
      mesh: { enabled: false },
      description: { enabled: false },
      iris: { enabled: false },
      emotion: { enabled: false },
    },
    body: { enabled: false },
    hand: { enabled: false },
    object: { enabled: false },
    gesture: { enabled: false },
  });

  await nuevo.load();
  await nuevo.warmup();
  motor = nuevo;
  return nuevo;
}

function estadoRostro(datos) {
  const caras = datos?.face;

  if (!caras?.length) {
    return "Coloca tu rostro dentro del óvalo.";
  }

  if (caras.length !== 1) {
    return "Debe aparecer una sola persona.";
  }

  const box = caras[0]?.box;
  const w = datos.width || video.value?.videoWidth;
  const h = datos.height || video.value?.videoHeight;

  if (
    !Array.isArray(box) ||
    !box.slice(0, 4).every(Number.isFinite) ||
    !w || !h
  ) {
    return "Ajusta tu posición frente a la cámara.";
  }

  const cx = (box[0] + box[2] / 2) / w;
  const cy = (box[1] + box[3] / 2) / h;

  if (cx < 0.31 || cx > 0.69 || cy < 0.24 || cy > 0.76) {
    return "Centra tu rostro dentro del óvalo.";
  }

  if (box[2] < w * 0.20 || box[3] < h * 0.24) {
    return "Acércate un poco.";
  }

  if (box[2] > w * 0.75 || box[3] > h * 0.85) {
    return "Aléjate un poco.";
  }

  return "listo";
}

function blobATexto(blob) {
  return new Promise((resolve, reject) => {
    const lector = new FileReader();
    lector.onload = () => resolve(String(lector.result || ""));
    lector.onerror = () => reject(new Error("No se pudo leer la captura."));
    lector.readAsDataURL(blob);
  });
}

async function crearPng(datos) {
  const v = video.value;
  const box = datos.face[0].box;

  const vw = v.videoWidth;
  const vh = v.videoHeight;

  const fx = vw / (datos.width || vw);
  const fy = vh / (datos.height || vh);

  const x = box[0] * fx;
  const y = box[1] * fy;
  const w = box[2] * fx;
  const h = box[3] * fy;

  const lado = Math.min(
    Math.max(w, h) * 1.45,
    Math.min(vw, vh)
  );

  const sx = Math.max(0, Math.min(vw - lado, x + w / 2 - lado / 2));
  const sy = Math.max(0, Math.min(vh - lado, y + h / 2 - lado / 2));

  const canvas = document.createElement("canvas");
  canvas.width = 256;
  canvas.height = 256;

  const ctx = canvas.getContext("2d");
  if (!ctx) {
    throw new Error("No se pudo preparar la imagen.");
  }

  ctx.drawImage(v, sx, sy, lado, lado, 0, 0, 256, 256);

  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (blob && blob.size <= 1024 * 1024) {
        resolve(blob);
      } else {
        reject(new Error("Captura PNG inválida."));
      }
    }, "image/png");
  });
}

async function analizar() {
  timer = null;

  if (!montada || !encendida.value || tomada.value) return;

  const version = secuencia;

  try {
    const datos = await motor.detect(video.value);

    if (!montada || version !== secuencia || !encendida.value) return;
    if (datos.error) {
      throw new Error(String(datos.error));
    }

    const indicacion = estadoRostro(datos);

    detectado.value = indicacion === "listo";
    consecutivos = detectado.value ? consecutivos + 1 : 0;

    error.value = "";
    estado.value = detectado.value
      ? "Rostro detectado. Mantente inmóvil..."
      : indicacion;

    if (consecutivos >= 3) {
      estado.value = "Preparando captura...";

      const png = await crearPng(datos);
      const base64 = await blobATexto(png);

      if (!montada || version !== secuencia || !consentimiento.value) return;

      limpiarCaptura();

      fotoUrl.value = URL.createObjectURL(png);
      fotoBase64.value = base64;
      tomada.value = true;

      detenerCamara();
      estado.value = "Captura lista.";
    }
  } catch (e) {
    consecutivos = 0;
    detectado.value = false;
    estado.value = "No se pudo analizar la imagen.";
    error.value = e?.message || "Error al detectar el rostro.";
  } finally {
    if (montada && encendida.value && !tomada.value) {
      timer = setTimeout(analizar, 650);
    }
  }
}

async function iniciarCamara() {
  if (!consentimiento.value || encendida.value || iniciando.value || tomada.value) return;

  const version = ++secuencia;
  iniciando.value = true;
  error.value = "";

  try {
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error("Se necesita HTTPS o localhost para usar la cámara.");
    }

    estado.value = "Solicitando acceso a la cámara...";

    const nueva = await navigator.mediaDevices.getUserMedia({
      video: {
        facingMode: "user",
        width: { ideal: 640 },
        height: { ideal: 480 },
      },
      audio: false,
    });

    if (!montada || version !== secuencia || !consentimiento.value) {
      nueva.getTracks().forEach(t => t.stop());
      return;
    }

    stream = nueva;
    video.value.srcObject = nueva;
    await video.value.play();

    if (!montada || version !== secuencia) return;

    encendida.value = true;
    estado.value = "Cargando detector facial...";
    await prepararMotor();

    if (!montada || version !== secuencia) return;

    estado.value = "Alinea tu rostro dentro del óvalo.";
    timer = setTimeout(analizar, 250);
  } catch (e) {
    detenerCamara();
    error.value = e?.message || "No se pudo abrir la cámara.";
    estado.value = "Cámara no disponible.";
  } finally {
    iniciando.value = false;
  }
}

function repetir() {
  detenerCamara();
  limpiarCaptura();
  error.value = "";
  if (consentimiento.value) {
    iniciarCamara();
  }
}

function cancelar() {
  consentimiento.value = false;
  detenerCamara();
  limpiarCaptura();
}

async function continuar() {
  if (!tomada.value || !fotoBase64.value || validandoEnServidor.value) return;

  if (retirando.value) {
    validandoEnServidor.value = true;
    error.value = "";
    estado.value = "Verificando captura con SYNCHRO...";
    try {
      const tokenFacial = await validarRostroSimuladoRetiro(fotoBase64.value);
      if (!montada) return;
      registrarCaptura(tokenFacial);
      limpiarCaptura();
      router.push("/retirar/pin");
    } catch (e) {
      if (montada) {
        error.value = e?.message || "El servidor no pudo comparar el rostro simulado.";
        estado.value = "Repite la captura si es necesario.";
      }
    } finally {
      validandoEnServidor.value = false;
    }
    return;
  }

  sessionStorage.setItem("synchroRostroTemporal", fotoBase64.value);
  sessionStorage.setItem(
    "synchroRostroMeta",
    JSON.stringify({
      casillero: casillero.value,
      objeto: route.query.objeto || "",
      tamano: route.query.tamano || "",
      fecha: new Date().toISOString(),
    }),
  );

  router.push({
    path: "/guardar/verificar-whatsapp",
    query: {
      casillero: casillero.value,
      objeto: route.query.objeto,
      tamano: route.query.tamano,
    },
  });
}

function comenzarEscaneo() {
  if (consentimiento.value) {
    iniciarCamara();
  } else {
    consentimiento.value = true;
  }
}
watch(consentimiento, (acepta) => {
  if (!acepta) {
    cancelar();
  } else if (!tomada.value) {
    iniciarCamara();
  }
});

function ocultar() {
  if (document.hidden) {
    cancelar();
  }
}

document.addEventListener("visibilitychange", ocultar);

onBeforeUnmount(() => {
  montada = false;
  detenerCamara();
  limpiarCaptura();
  document.removeEventListener("visibilitychange", ocultar);
});
</script>

<template>
  <main class="pantalla-rostro" :class="{ 'tema-oscuro': oscuro }">
    <header class="titulo-paso">
      <h1>Registro facial</h1>
      <p>Casillero {{ casillero }}</p>
    </header>

    <section class="zona-facial" aria-label="Registro facial de demostración">
      <div class="panel-facial">
        <div class="visor">
          <video
            ref="video"
            muted
            playsinline
            :class="{ visible: encendida }"
          ></video>

          <img
            v-if="tomada"
            class="foto-tomada"
            :src="fotoUrl"
            alt="Captura facial temporal preparada"
          />

          <div
            class="guia-ovalo"
            :class="{ activo: detectado, terminado: tomada }"
            aria-hidden="true"
          >
            <span
              v-if="encendida && !tomada"
              class="linea-escaneo"
            ></span>

            <span v-if="tomada" class="marca-ok">✓</span>
          </div>

          <div v-if="!encendida && !tomada" class="sin-camara">
            <img
              src="/iconos/complementarios/38_escanear_rostro-oscuro.svg"
              alt=""
            />
          </div>
        </div>

        <div class="texto-panel">
          <h2>
            {{ tomada ? "Rostro capturado" : "Coloca tu rostro en el óvalo" }}
          </h2>

          <p>
            {{
              tomada
                ? (retirando ? "Continúa para introducir el PIN de retiro." : "Continúa para enviar el PIN por WhatsApp.")
                : "La captura se realizará automáticamente."
            }}
          </p>
        </div>

        <div class="retroalimentacion" role="status" aria-live="polite">
          <p v-if="error" class="error">{{ error }}</p>
          <p v-else>{{ estado }}</p>
        </div>
      </div>
    </section>

    <footer class="pie-facial">
      <button
        v-if="!tomada && !encendida && !iniciando"
        type="button"
        class="continuar"
        @click="comenzarEscaneo"
      >
        <span>
          {{ consentimiento ? "Reintentar escaneo" : "Comenzar escaneo" }}
        </span>
      </button>

      <button
        class="continuar"
        type="button"
        v-if="tomada"
        :disabled="validandoEnServidor"
        @click="continuar"
      >
        <span>Continuar</span>

        <span class="icono-continuar" aria-hidden="true">
          <img
            class="claro"
            src="/iconos/complementarios/29_continuar.svg"
            alt=""
          />
          <img
            class="oscuro"
            src="/iconos/complementarios/29_continuar-oscuro.svg"
            alt=""
          />
        </span>
      </button>
    </footer>
  </main>
</template>

<style scoped>
.pantalla-rostro {
  position: absolute;
  inset: 0;
  padding: 0;
  margin: 0;
  overflow: hidden;
  background: #061f36;
  color: #ffffff;
}

.titulo-paso {
  position: absolute;
  top: 68px;
  left: 14px;
  right: 14px;
  z-index: 4;
  text-align: center;
  padding: 10px;
}

.titulo-paso h1 {
  margin: 0;
  color: #ffffff;
  font-size: clamp(27px, 4.5vw, 40px);
  text-shadow: 0 2px 12px rgba(0, 0, 0, .48);
}

.titulo-paso p {
  margin: 6px 0 0;
  color: #d8f5fa;
  font-size: 15px;
  text-shadow: 0 2px 10px rgba(0, 0, 0, .5);
}

.zona-facial,
.panel-facial {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
}

.panel-facial {
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  align-items: stretch;
  gap: 10px;
  padding: 0 24px 130px;
  background: transparent;
}

.panel-facial::after {
  content: "";
  position: absolute;
  inset: 42% 0 0;
  background: linear-gradient(
    to bottom,
    transparent,
    rgba(1, 22, 42, .65) 40%,
    rgba(1, 22, 42, .96) 100%
  );
  pointer-events: none;
  z-index: 1;
}

.visor {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: hidden;
  background: #061f36;
  z-index: 0;
}

.visor video,
.foto-tomada {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
  object-position: center center;
  transform: scaleX(-1);
}

.visor video:not(.visible) {
  opacity: 0;
}

.visor video.visible {
  opacity: 1;
}

.guia-ovalo {
  position: absolute;
  left: 50%;
  top: 43%;
  transform: translate(-50%, -50%);
  width: min(58vw, 365px);
  height: min(54vh, 570px);
  border: 4px dashed rgba(245,253,255,.94);
  border-radius: 50%;
  z-index: 2;
  box-shadow: 0 0 0 999px rgba(1,18,35,.12);
  overflow: hidden;
  pointer-events: none;
  transition: border-color .25s, box-shadow .25s;
}

.guia-ovalo.activo {
  border: 4px solid #00d6b9;
  box-shadow: 0 0 18px rgba(0,214,185,.45);
}

.guia-ovalo.terminado {
  border: 4px solid #00b9c7;
  box-shadow: 0 0 20px rgba(0,185,199,.45);
}

.linea-escaneo {
  position: absolute;
  left: 9%;
  top: 15%;
  width: 82%;
  height: 2px;
  background: linear-gradient(90deg, transparent, #5ec6e8, transparent);
  animation: desplazamiento-facial 2.2s ease-in-out infinite alternate;
}

@keyframes desplazamiento-facial {
  from { top: 16%; }
  to { top: 83%; }
}

.marca-ok {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  display: grid;
  place-items: center;
  width: 62px;
  height: 62px;
  border-radius: 50%;
  background: #00b9c7;
  color: #012b4c;
  font-size: 36px;
  font-weight: 900;
}

.sin-camara {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1;
}

.sin-camara img {
  width: 78px;
  height: 78px;
  opacity: .9;
}

.texto-panel,
.retroalimentacion {
  position: relative;
  z-index: 3;
  width: 100%;
  max-width: 680px;
  margin-left: auto;
  margin-right: auto;
  flex: 0 0 auto;
}

.texto-panel {
  text-align: center;
}

.texto-panel h2 {
  margin: 0;
  color: #ffffff;
  font-size: clamp(22px, 3vw, 29px);
}

.texto-panel p {
  margin: 8px 0 0;
  color: #e2f3f8;
}

.retroalimentacion {
  color: #e2f3f8;
  text-align: center;
}

.retroalimentacion p {
  margin: 0;
}

.retroalimentacion .error {
  color: #ffcebd;
}

.pie-facial {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 122px;
  z-index: 5;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 7px;
  margin: 0;
  padding: 8px 16px;
  background: rgba(1, 22, 42, .96);
}

.continuar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  min-width: 210px;
  min-height: 60px;
  padding: 10px 24px;
  border: 2px solid #00b9c7;
  border-radius: 15px;
  background: #f4feff;
  color: #01305c;
  font: inherit;
  font-size: 19px;
  font-weight: 800;
  cursor: pointer;
  box-shadow: 0 4px 0 #00a5b2;
}

.continuar:disabled {
  background: #edf3f5;
  color: #647784;
  border-color: #b5cad3;
  box-shadow: none;
  cursor: not-allowed;
}

.icono-continuar {
  position: relative;
  display: block;
  width: 36px;
  height: 36px;
}

.icono-continuar img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.claro { opacity: 1; }
.oscuro { opacity: 0; }

.tema-oscuro .claro { opacity: 0; }
.tema-oscuro .oscuro { opacity: 1; }

@media (max-height: 850px) {
  .guia-ovalo {
    height: 42vh;
  }

  .panel-facial {
    padding: 0 14px 124px;
    gap: 5px;
  }

  .titulo-paso {
    top: 56px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .linea-escaneo {
    animation: none;
    top: 50%;
  }
}
</style>