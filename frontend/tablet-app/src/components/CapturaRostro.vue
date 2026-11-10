<script setup>
import { ref, onBeforeUnmount } from "vue";
import { esDemo } from "../services/tabletApi";
defineProps({ ocupado: Boolean });
const emit = defineEmits(["captura"]);
const video = ref(null);
const activa = ref(false);
const error = ref("");
const permiso = ref(false);
const iniciando = ref(false);
let stream;
let vigente = true;
function detener() {
  stream?.getTracks().forEach((t) => t.stop());
  stream = null;
  if (video.value) video.value.srcObject = null;
  activa.value = false;
}
async function iniciar() {
  if (iniciando.value || activa.value) return;
  error.value = "";
  if (!permiso.value) {
    error.value =
      "Confirma que aceptas utilizar una captura para esta demostración.";
    return;
  }
  iniciando.value = true;
  try {
    if (!navigator.mediaDevices?.getUserMedia)
      throw new Error("CamaraNoDisponible");
    const nuevo = await navigator.mediaDevices.getUserMedia({
      video: {
        facingMode: "user",
        width: { ideal: 640 },
        height: { ideal: 480 },
      },
      audio: false,
    });
    if (!vigente) {
      nuevo.getTracks().forEach((t) => t.stop());
      return;
    }
    stream = nuevo;
    video.value.srcObject = stream;
    await video.value.play();
    activa.value = true;
  } catch {
    detener();
    error.value =
      "No pudimos acceder a la cámara. Revisa el permiso del navegador y vuelve a intentarlo.";
  } finally {
    iniciando.value = false;
  }
}
function capturar() {
  if (!activa.value || !video.value.videoWidth) return;
  const canvas = document.createElement("canvas");
  canvas.width = 512;
  canvas.height = 384;
  const ctx = canvas.getContext("2d");
  ctx.drawImage(video.value, 0, 0, 512, 384);
  canvas.toBlob((blob) => {
    canvas.width = canvas.height = 0;
    detener();
    if (blob && vigente) emit("captura", blob);
  }, "image/png");
}
function ejemplo() {
  if (!esDemo || !permiso.value) return;
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 128;
  const ctx = canvas.getContext("2d");
  ctx.fillStyle = "#dfeaf0";
  ctx.fillRect(0, 0, 128, 128);
  ctx.fillStyle = "#01305c";
  ctx.beginPath();
  ctx.arc(64, 46, 22, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillRect(30, 80, 68, 48);
  canvas.toBlob((blob) => {
    canvas.width = canvas.height = 0;
    if (blob && vigente) emit("captura", blob);
  }, "image/png");
}
onBeforeUnmount(() => {
  vigente = false;
  detener();
});
</script>
<template>
  <div class="rostro-area">
    <div class="camera-window">
      <video
        ref="video"
        muted
        playsinline
        :class="{ visible: activa }"
        aria-label="Vista de la cámara"
      />
      <div v-if="!activa" class="camera-placeholder">
        <img
          src="/iconos/complementarios/38_escanear_rostro-oscuro.svg"
          alt=""
        /><span>Coloca tu rostro dentro del marco</span>
      </div>
      <div class="camera-frame"></div>
      <span class="camera-label">Verificación simulada</span>
    </div>
    <p class="tablet-muted camera-instruction">
      Mira de frente, busca buena iluminación y evita cubrir tu rostro.
    </p>
    <label class="camera-consent"
      ><input
        v-model="permiso"
        type="checkbox"
        :disabled="ocupado || activa"
      />Acepto usar una captura temporal para esta demostración. No es una
      verificación biométrica profesional.</label
    >
    <p v-if="error" role="alert" class="tablet-error">{{ error }}</p>
    <div class="tablet-actions">
      <button
        type="button"
        class="tablet-button"
        :disabled="ocupado || iniciando || !permiso"
        @click="activa ? capturar() : iniciar()"
      >
        {{
          ocupado
            ? "Verificando…"
            : iniciando
              ? "Activando cámara…"
              : activa
                ? "Capturar y verificar"
                : "Activar cámara"
        }}</button
      ><button
        v-if="activa"
        type="button"
        class="tablet-button secondary"
        @click="detener"
      >
        Apagar cámara</button
      ><button
        v-if="esDemo && !activa"
        type="button"
        class="tablet-button secondary"
        :disabled="ocupado || iniciando || !permiso"
        @click="ejemplo"
      >
        Usar captura de ejemplo
      </button>
    </div>
  </div>
</template>
<style scoped>
.camera-window {
  height: 270px;
  position: relative;
  background: #061f36;
  border-radius: 16px;
  overflow: hidden;
  display: grid;
  place-items: center;
}
.camera-window video {
  width: 100%;
  height: 100%;
  object-fit: cover;
  visibility: hidden;
  position: absolute;
  transform: scaleX(-1);
}
.camera-window video.visible {
  visibility: visible;
}
.camera-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 15px;
  color: #c6dfea;
  font-size: 13px;
}
.camera-placeholder img {
  width: 80px;
  height: 80px;
  opacity: 0.8;
}
.camera-frame {
  position: absolute;
  inset: 22px 30%;
  border: 2px solid #00b9c7;
  border-radius: 45%;
  pointer-events: none;
}
.camera-label {
  position: absolute;
  top: 12px;
  left: 12px;
  background: #103956;
  color: #d3f8fc;
  padding: 5px 10px;
  border-radius: 6px;
  font-size: 11px;
}
.camera-instruction {
  font-size: 13px;
  text-align: center;
  margin: 13px 0;
}
.camera-consent {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  font-size: 13px !important;
  font-weight: 400 !important;
  line-height: 1.6;
}
.camera-consent input {
  width: 22px;
  height: 22px;
  min-width: 22px;
  accent-color: #007c89;
  margin-top: 2px;
}
</style>
