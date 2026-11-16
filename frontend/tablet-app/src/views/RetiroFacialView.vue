<script setup>
import { ref, nextTick, onBeforeUnmount } from "vue";
import { RouterLink, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { registrarCaptura } from "../services/retiroSesion";

const router = useRouter();
const { oscuro } = useTema();

const video = ref(null);
const foto = ref("");
const fase = ref("inicial");
const error = ref("");

let medios = null;
let montado = true;

function detenerCamara() {
  if (medios) {
    medios.getTracks().forEach(t => t.stop());
    medios = null;
  }

  if (video.value) {
    video.value.srcObject = null;
  }
}

async function activarCamara() {
  detenerCamara();
  foto.value = "";
  error.value = "";
  fase.value = "cargando";

  try {
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error("Camara no disponible.");
    }

    const flujo = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: "user" },
      audio: false
    });

    if (!montado) {
      flujo.getTracks().forEach(t => t.stop());
      return;
    }

    medios = flujo;
    fase.value = "camara";

    await nextTick();

    if (!video.value) {
      throw new Error("Vista de camara no disponible.");
    }

    video.value.srcObject = medios;
    await video.value.play();
  } catch (e) {
    detenerCamara();
    if (!montado) return;

    fase.value = "error";
    error.value =
      "No se pudo activar la camara. Revisa los permisos.";
  }
}

function tomarFoto() {
  if (
    fase.value !== "camara" ||
    !video.value ||
    video.value.readyState < 2
  ) return;

  const canvas = document.createElement("canvas");
  canvas.width = 320;
  canvas.height = 240;

  const ctx = canvas.getContext("2d");
  if (!ctx) {
    error.value = "No se pudo preparar la captura.";
    return;
  }

  ctx.drawImage(video.value, 0, 0, 320, 240);
  foto.value = canvas.toDataURL("image/png");

  canvas.width = 0;
  canvas.height = 0;

  detenerCamara();
  fase.value = "capturada";
}

function continuar() {
  if (fase.value !== "capturada" || !foto.value) return;

  registrarCaptura();
  foto.value = "";
  router.push("/retirar/pin");
}

onBeforeUnmount(() => {
  montado = false;
  detenerCamara();
  foto.value = "";
});
</script>

<template>
  <main
    class="pantalla-facial"
    :class="{ 'tema-oscuro': oscuro }"
  >
    <RouterLink to="/retirar" class="volver">
      <img
        src="/iconos/complementarios/28_regresar.svg"
        alt=""
        aria-hidden="true"
      />
      <span>Volver</span>
    </RouterLink>

    <section class="contenido-facial">
      <h1>Verificaci&oacute;n facial</h1>

      <p class="instruccion">
        Coloca tu rostro frente a la c&aacute;mara.
      </p>

      <div class="visor">
        <video
          v-show="fase === 'camara'"
          ref="video"
          autoplay
          muted
          playsinline
        ></video>

        <img
          v-if="fase === 'capturada'"
          :src="foto"
          class="fotografia"
          alt="Vista previa de la captura"
        />

        <div
          v-if="fase === 'camara'"
          class="guia-rostro"
          aria-hidden="true"
        ></div>

        <div
          v-if="
            fase === 'inicial' ||
            fase === 'cargando' ||
            fase === 'error'
          "
          class="visor-mensaje"
        >
          <svg
            width="62"
            height="62"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.4"
            aria-hidden="true"
          >
            <path d="M3 8V5a2 2 0 0 1 2-2h3" />
            <path d="M16 3h3a2 2 0 0 1 2 2v3" />
            <path d="M21 16v3a2 2 0 0 1-2 2h-3" />
            <path d="M8 21H5a2 2 0 0 1-2-2v-3" />
            <circle cx="12" cy="10" r="3" />
            <path d="M6.5 18a5.5 5.5 0 0 1 11 0" />
          </svg>

          <span>
            {{
              fase === "cargando"
                ? "Activando camara..."
                : "Camara desactivada"
            }}
          </span>
        </div>
      </div>

      <p v-if="error" class="error" role="alert">
        {{ error }}
      </p>

      <div class="acciones">
        <button
          v-if="fase === 'inicial' || fase === 'error'"
          type="button"
          class="principal"
          @click="activarCamara"
        >
          Activar c&aacute;mara
        </button>

        <button
          v-else-if="fase === 'camara'"
          type="button"
          class="principal"
          @click="tomarFoto"
        >
          Tomar captura
        </button>

        <button
          v-else-if="fase === 'capturada'"
          type="button"
          class="principal"
          @click="continuar"
        >
          Continuar
        </button>
      </div>

      <p class="nota">
        Captura de demostraci&oacute;n, sin verificaci&oacute;n
        real de identidad.
      </p>
    </section>
  </main>
</template>

<style scoped>
.pantalla-facial,
.pantalla-facial * {
  box-sizing: border-box;
}

.pantalla-facial {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow-y: auto;
  padding: 12px 18px 20px;
  background: #f2f7fa;
  color: #01305c;
}

.volver {
  display: flex;
  align-items: center;
  gap: 10px;
  align-self: flex-start;
  min-height: 48px;
  padding: 8px 16px;
  border: 1.5px solid #c7e2e9;
  border-radius: 13px;
  background: #e4f2f6;
  color: inherit;
  text-decoration: none;
  font-weight: 700;
}

.volver img {
  width: 25px;
  height: 25px;
}

.contenido-facial {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: clamp(10px, 1.8vh, 18px);
  width: min(100%, 580px);
  margin: auto;
  padding: 12px 0;
  text-align: center;
}

.contenido-facial h1 {
  margin: 0;
  font-size: clamp(25px, 3vw, 36px);
  line-height: 1.2;
}

.instruccion {
  margin: 0;
  color: #597486;
  font-size: 16px;
}

.visor {
  position: relative;
  display: grid;
  place-items: center;
  width: min(100%, 380px);
  aspect-ratio: 4 / 3;
  overflow: hidden;
  border: 3px solid #b2d5e0;
  border-radius: 18px;
  background: #082b46;
  color: #c8e8ef;
}

.visor video,
.visor .fotografia {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transform: scaleX(-1);
}

.guia-rostro {
  position: absolute;
  width: 49%;
  height: 78%;
  border: 3px dashed #5ec6e8;
  border-radius: 48%;
  pointer-events: none;
}

.visor-mensaje {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 13px;
  font-size: 15px;
}

.acciones {
  width: min(100%, 300px);
}

.principal {
  min-height: 55px;
  width: 100%;
  padding: 10px 20px;
  border: 2px solid #00a4b2;
  border-radius: 13px;
  background: #00b9c7;
  color: #01305c;
  font: inherit;
  font-size: 18px;
  font-weight: 800;
  cursor: pointer;
}

.principal:focus-visible,
.volver:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

.error {
  margin: 0;
  color: #9b322a;
  font-size: 14px;
}

.nota {
  margin: 0;
  color: #637b89;
  font-size: 12px;
}

.tema-oscuro.pantalla-facial {
  background: #061f36;
  color: #fff;
}

.tema-oscuro .volver {
  background: #204766;
  border-color: #42647e;
}

.tema-oscuro .instruccion,
.tema-oscuro .nota {
  color: #d1e2ea;
}

@media (max-height: 570px) {
  .visor {
    width: min(100%, 255px);
  }

  .contenido-facial {
    gap: 8px;
  }
}
</style>