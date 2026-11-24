<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from "vue";
import {
  cargarMotorV3,
  descriptorValido,
  revisarCapturaV3,
  pngV3,
  MODELO_FACIAL_V3,
  type CapturaV3,
} from "../../services/facialEncomiendaV3";
const props = defineProps<{ consentido: boolean; disabled?: boolean }>();
const emit = defineEmits<{ capturada: [CapturaV3 | null] }>();
const video = ref<HTMLVideoElement | null>(null),
  encendida = ref(false),
  procesando = ref(false),
  error = ref(""),
  captura = ref<CapturaV3 | null>(null);
let stream: MediaStream | null = null,
  motor: any,
  montada = true,
  secuencia = 0;
function detener() {
  secuencia++;
  stream?.getTracks().forEach((t) => t.stop());
  stream = null;
  if (video.value) video.value.srcObject = null;
  encendida.value = false;
}
function limpiar() {
  detener();
  if (captura.value) {
    captura.value.descriptor.fill(0);
    captura.value.imagenPng = "";
  }
  captura.value = null;
  emit("capturada", null);
}
async function iniciar() {
  if (!props.consentido || procesando.value || props.disabled) return;
  limpiar();
  const intento = ++secuencia;
  procesando.value = true;
  error.value = "";
  try {
    if (!navigator.mediaDevices?.getUserMedia)
      throw new Error(
        "La cámara requiere una conexión segura y un navegador compatible.",
      );
    const nueva = await navigator.mediaDevices.getUserMedia({
      video: {
        facingMode: "user",
        width: { ideal: 640 },
        height: { ideal: 480 },
      },
      audio: false,
    });
    if (!montada || intento !== secuencia || !props.consentido) {
      nueva.getTracks().forEach((t) => t.stop());
      return;
    }
    stream = nueva;
    video.value!.srcObject = stream;
    await video.value!.play();
    motor = await cargarMotorV3();
    if (!montada || intento !== secuencia || !props.consentido) {
      detener();
      return;
    }
    encendida.value = true;
  } catch (e) {
    detener();
    error.value =
      e instanceof Error ? e.message : "No se pudo iniciar la cámara.";
  } finally {
    procesando.value = false;
  }
}
async function capturar() {
  if (!encendida.value || !props.consentido || procesando.value) return;
  const intento = secuencia;
  procesando.value = true;
  error.value = "";
  let mejor: CapturaV3 | null = null,
    puntaje = -Infinity,
    consecutivas = 0;
  try {
    for (let i = 0; i < 6; i++) {
      const datos = await motor.detect(video.value);
      if (!montada || intento !== secuencia || !props.consentido) return;
      const calidad = revisarCapturaV3(datos, video.value!);
      const embedding = Array.from(
        datos.face?.[0]?.embedding ?? [],
      ) as number[];
      if (calidad.error || !descriptorValido(embedding)) {
        consecutivas = 0;
        error.value =
          calidad.error || "El modelo V3 no obtuvo un descriptor completo.";
        if (mejor) mejor.descriptor.fill(0);
        mejor = null;
        puntaje = -Infinity;
        embedding.fill(0);
        continue;
      }
      consecutivas++;
      if (calidad.puntaje > puntaje) {
        if (mejor) mejor.descriptor.fill(0);
        mejor = {
          imagenPng: pngV3(datos, video.value!),
          descriptor: [...embedding],
          modelo: MODELO_FACIAL_V3,
        };
        puntaje = calidad.puntaje;
      }
      embedding.fill(0);
      if (consecutivas >= 3 && mejor) {
        captura.value = mejor;
        mejor = null;
        error.value = "";
        emit("capturada", captura.value);
        detener();
        return;
      }
    }
    if (!error.value)
      error.value = "Mantén el rostro centrado e inténtalo nuevamente.";
  } catch (e) {
    error.value =
      e instanceof Error ? e.message : "No se pudo capturar el rostro.";
  } finally {
    mejor?.descriptor.fill(0);
    procesando.value = false;
  }
}
watch(
  () => props.consentido,
  (v) => {
    if (!v) limpiar();
  },
);
onBeforeUnmount(() => {
  montada = false;
  limpiar();
});
</script>
<template>
  <div class="env-camera">
    <div class="env-camera-frame">
      <video
        ref="video"
        autoplay
        playsinline
        muted
        v-show="encendida || procesando"
        aria-label="Vista previa de tu cámara"
      ></video>
      <img
        v-if="captura"
        :src="'data:image/png;base64,' + captura.imagenPng"
        alt="Captura preparada para verificar tu identidad"
      />
      <p v-if="!encendida && !procesando && !captura">
        Tu cámara se activará únicamente cuando lo solicites.
      </p>
    </div>
    <p v-if="error" class="alerta" role="alert">{{ error }}</p>
    <p v-if="captura" role="status">
      Captura preparada. La verificación la realiza el servidor.
    </p>
    <p v-if="procesando" role="status">Procesando la captura…</p>
    <div class="env-actions">
      <button
        type="button"
        class="boton boton-secundario"
        :disabled="!consentido || disabled || procesando"
        @click="iniciar"
      >
        {{ captura ? "Tomar otra captura" : "Activar cámara" }}
      </button>
      <button
        v-if="encendida"
        type="button"
        class="boton boton-primario"
        :disabled="procesando || disabled"
        @click="capturar"
      >
        Capturar rostro
      </button>
      <button
        v-if="encendida"
        type="button"
        class="boton boton-secundario"
        @click="limpiar"
      >
        Apagar cámara
      </button>
    </div>
  </div>
</template>
