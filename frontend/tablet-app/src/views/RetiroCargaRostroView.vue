<script setup>
import { obtenerEspacioRetiroCarga } from "../services/retiroCargaSesion.js";
import { verificarRostroV3RetiroCarga } from "../services/retiroCargaApi.js";
import {
  guardarRostroCargaV3
} from "../services/rostroCargaTemporal.js";
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { registrarVerificacionCarga } from "../services/verificacionCargaTemporal.js";
import { infoCarga } from "../services/catalogoEspaciosCarga.js";
import { registrarCaptura } from "../services/retiroSesion";
import { validarRostroSimuladoRetiro, validarDescriptorFacialV3Temporal, validarRostroV3Retiro } from "../services/retiroFacialManualApi";
import { registrarDescriptorV3, borrarDescriptorV3, copiarDescriptorV3, consumirDescriptorV3 } from "../services/descriptorFacialV3Temporal.js";

// SYNCHRO-FACIAL-FLUJO-LIMPIO-V5
// Guardar: solo detectar y capturar.
// Retiro: comparar con la referencia registrada.

const route = useRoute();
const esVistaPrevia = import.meta.env.DEV &&
  route.path.startsWith("/vista-previa/");
const consentimientoDemo = ref(false);
let temporizadorDemo = null;

const modoCarga = computed(() => route.path === "/cargar/rostro");

const seleccionCargaValida = computed(() => {
  const codigo = String(route.query.espacio ?? "");
  const info = infoCarga(codigo);
  const dispositivo = route.query.dispositivo;
  const modalidad = dispositivo === "Laptop"
    ? "Laptop"
    : route.query.modalidad;

  return !!info &&
    info.dispositivo === dispositivo &&
    info.modalidad === modalidad;
});

const espacioCarga = computed(() =>
  seleccionCargaValida.value
    ? String(route.query.espacio)
    : "Sin seleccionar"
);
const router = useRouter();
const { oscuro } = useTema();
const pruebaDescriptorV3 = import.meta.env.DEV && import.meta.env.VITE_FACIAL_V3_PRUEBA === "true";
const propsVistaPrevia = defineProps({
  modoRetiroEjemplo: {
    type: Boolean,
    default: false
  }
});

const registroV3Habilitado = import.meta.env.DEV && import.meta.env.VITE_FACIAL_V3_REGISTRO === "true";
const retiroV3Habilitado = pruebaDescriptorV3 && import.meta.env.VITE_FACIAL_V3_RETIRO === "true";

const retirando = computed(() =>
  (import.meta.env.DEV && propsVistaPrevia.modoRetiroEjemplo) ||
  route.path.startsWith("/retirar/")
);

const video = ref(null);
const consentimiento = ref(false);
const aceptaDescriptorV3 = ref(false);
const aceptaRetiroV3 = ref(false);
const usarRetiroV3 = computed(() =>
  retirando.value && retiroV3Habilitado && aceptaRetiroV3.value
);
const encendida = ref(false);
const iniciando = ref(false);
const detectado = ref(false);
const tomada = ref(false);
const validandoEnServidor = ref(false);

const fotoUrl = ref("");
const fotoBase64 = ref("");
const estado = ref("Pulsa Comenzar escaneo para iniciar.");
const error = ref("");
const validacionCargaV3 = ref(false);

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
let mejorCaptura = null;
let muestrasCalidad = 0;
let muestrasDistanciaV3 = [];
let ultimoResumenDistanciaV3 = "";

// Diagnostico local: no envia ni conserva vectores, solo distancias agregadas.
function distanciaInternaV3(a, b) {
  if (!Array.isArray(a) || !Array.isArray(b) ||
      a.length !== 1024 || b.length !== 1024) return null;
  let producto = 0;
  let normaA = 0;
  let normaB = 0;
  for (let i = 0; i < 1024; i++) {
    producto += a[i] * b[i];
    normaA += a[i] * a[i];
    normaB += b[i] * b[i];
  }
  if (normaA <= 1e-8 || normaB <= 1e-8) return null;
  return 1 - Math.max(-1, Math.min(1, producto / Math.sqrt(normaA * normaB)));
}
function limpiarMuestrasDistanciaV3() {
  for (const muestra of muestrasDistanciaV3) muestra.fill(0);
  muestrasDistanciaV3 = [];
}

function limpiarMejorCaptura() {
  if (mejorCaptura) mejorCaptura.descriptor.fill(0);
  mejorCaptura = null;
  muestrasCalidad = 0;
  limpiarMuestrasDistanciaV3();
}
let montada = true;

function limpiarCaptura() {
  validacionCargaV3.value = false;
  if (fotoUrl.value) {
    URL.revokeObjectURL(fotoUrl.value);
  }

  fotoUrl.value = "";
  fotoBase64.value = "";
  tomada.value = false;
}

function detenerCamara() {
  limpiarMejorCaptura();
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
      mesh: { enabled: pruebaDescriptorV3 && (aceptaDescriptorV3.value || usarRetiroV3.value) },
      description: { enabled: pruebaDescriptorV3 && (aceptaDescriptorV3.value || usarRetiroV3.value) },
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

function revisarCalidadV3(datos) {
  const v = video.value;
  const box = datos.face?.[0]?.box;

  if (!v || !box || !v.videoWidth || !v.videoHeight) {
    return {
      valido: false,
      mensaje: "Ajusta el rostro frente a la cámara."
    };
  }

  const fx = v.videoWidth / (datos.width || v.videoWidth);
  const fy = v.videoHeight / (datos.height || v.videoHeight);

  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = 64;

  const ctx = canvas.getContext("2d", {
    willReadFrequently: true
  });

  if (!ctx) {
    return {
      valido: false,
      mensaje: "No se pudo revisar la iluminación."
    };
  }

  ctx.drawImage(
    v, box[0] * fx, box[1] * fy,
    box[2] * fx, box[3] * fy,
    0, 0, 64, 64
  );

  const pixeles = ctx.getImageData(0, 0, 64, 64).data;
  const grises = new Float32Array(4096);

  let suma = 0;
  let cuadrados = 0;
  let detalle = 0;

  for (let i = 0; i < grises.length; i++) {
    const j = i * 4;

    const luz =
      0.2126 * pixeles[j] +
      0.7152 * pixeles[j + 1] +
      0.0722 * pixeles[j + 2];

    grises[i] = luz;
    suma += luz;
    cuadrados += luz * luz;

    if (i % 64 > 0) {
      detalle += Math.abs(luz - grises[i - 1]);
    }
  }

  const promedio = suma / grises.length;

  const contraste = Math.sqrt(Math.max(
    0,
    cuadrados / grises.length - promedio * promedio
  ));

  if (promedio < 45) {
    return {
      valido: false,
      mensaje: "Hay poca luz. Busca una zona más iluminada."
    };
  }

  if (promedio > 215) {
    return {
      valido: false,
      mensaje: "Hay luz excesiva. Evita la luz directa en el rostro."
    };
  }

  if (contraste < 16) {
    return {
      valido: false,
      mensaje: "La imagen tiene poco contraste. Ajusta la iluminación."
    };
  }

  return {
    valido: true,
    puntaje:
      contraste +
      detalle / grises.length -
      Math.abs(promedio - 130) * 0.08
  };
}

async function analizar() {
  timer = null;

  if (!montada || !encendida.value || tomada.value) return;

  const version = secuencia;

  try {
    const datos = await motor.detect(video.value);

    if (!montada || version !== secuencia || !encendida.value) {
      return;
    }

    if (datos.error) {
      throw new Error(String(datos.error));
    }

    const indicacion = estadoRostro(datos);

    const modoV3 = pruebaDescriptorV3 &&
      (aceptaDescriptorV3.value || usarRetiroV3.value);

    const calidad = modoV3 && indicacion === "listo"
      ? revisarCalidadV3(datos)
      : null;

    detectado.value =
      indicacion === "listo" &&
      (!modoV3 || calidad?.valido === true);

    consecutivos = detectado.value
      ? consecutivos + 1
      : 0;

    if (!detectado.value) {
      limpiarMejorCaptura();
    }

    error.value = "";

    estado.value = detectado.value
      ? "Mantente inmóvil mientras seleccionamos la mejor captura..."
      : indicacion !== "listo"
        ? indicacion
        : calidad?.mensaje;

    if (consecutivos >= 3) {
      let png;
      let descriptor;

      if (modoV3) {
        const embedding = datos.face?.[0]?.embedding;

        if (!embedding || embedding.length !== 1024) {
          throw new Error(
            "V3 no generó un descriptor completo."
          );
        }

        const candidata = await crearPng(datos);

        if (!montada ||
            version !== secuencia ||
            !consentimiento.value) {
          return;
        }

        muestrasCalidad++;
        if (import.meta.env.DEV) {
          muestrasDistanciaV3.push(Array.from(embedding));
        }

        if (!mejorCaptura ||
            calidad.puntaje > mejorCaptura.puntaje) {

          if (mejorCaptura) {
            mejorCaptura.descriptor.fill(0);
          }

          mejorCaptura = {
            png: candidata,
            descriptor: Array.from(embedding),
            puntaje: calidad.puntaje
          };
        }

        if (muestrasCalidad < 3) {
          estado.value =
            `Comparando capturas ${muestrasCalidad}/3...`;
          return;
        }

        if (import.meta.env.DEV && muestrasDistanciaV3.length === 3) {
          const m = muestrasDistanciaV3;
          const distancias = [
            distanciaInternaV3(m[0], m[1]),
            distanciaInternaV3(m[0], m[2]),
            distanciaInternaV3(m[1], m[2])
          ];
          ultimoResumenDistanciaV3 = distancias.every(Number.isFinite)
            ? distancias.map(d => d.toFixed(4)).join(" / ")
            : "no calculable";
        }
        limpiarMuestrasDistanciaV3();

        png = mejorCaptura.png;
        descriptor = mejorCaptura.descriptor;

        mejorCaptura = null;
        muestrasCalidad = 0;
      } else {
        png = await crearPng(datos);
        descriptor = datos.face?.[0]?.embedding;
      }

      try {
        const base64 = await blobATexto(png);

        if (!montada ||
            version !== secuencia ||
            !consentimiento.value) {
          return;
        }

        limpiarCaptura();

        if (modoV3 &&
            !registrarDescriptorV3(descriptor)) {
          throw new Error(
            "No se pudo preparar la referencia facial V3."
          );
        }

        fotoUrl.value = URL.createObjectURL(png);
        fotoBase64.value = base64;
        tomada.value = true;

        detenerCamara();

        estado.value = modoV3
          ? "V3: mejor captura seleccionada entre tres tomas."
          : "Captura lista.";
        if (import.meta.env.DEV && modoV3 && ultimoResumenDistanciaV3) {
          estado.value += " Distancias internas: " +
            ultimoResumenDistanciaV3 + ".";
        }

        // Mantener la validacion V3 del deposito.
        if (modoV3 &&
            !retirando.value &&
            aceptaDescriptorV3.value) {

          const temporal = copiarDescriptorV3();

          if (!temporal) {
            error.value =
              "Repite la captura para probar V3.";
          } else {
            estado.value =
              "Comprobando conexión V3 con SYNCHRO...";

            try {
              await validarDescriptorFacialV3Temporal(
                temporal
              );

              if (montada) {
                estado.value =
                  (modoCarga.value
                    ? "Descriptor V3 validado por el backend (solo prueba)."
                    : "V3 validado por backend (solo prueba).");
                if (modoCarga.value) validacionCargaV3.value = true;
              }
            } catch (e) {
              if (montada) {
                estado.value =
                  "V3 no se validó en el backend.";

                error.value =
                  e?.message || "Error de conexión V3.";
              }
            } finally {
              temporal.fill(0);
            }
          }
        }
      } finally {
        if (modoV3 && descriptor) {
          descriptor.fill(0);
        }
      }
    }
  } catch (e) {
    limpiarMejorCaptura();
    consecutivos = 0;
    detectado.value = false;
    estado.value = "No se pudo analizar la imagen.";
    error.value =
      e?.message || "Error al detectar el rostro.";
  } finally {
    if (montada &&
        encendida.value &&
        !tomada.value) {
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
  ultimoResumenDistanciaV3 = "";
  borrarDescriptorV3();
  detenerCamara();
  limpiarCaptura();
  error.value = "";
  if (consentimiento.value) {
    iniciarCamara();
  }
}

function cancelar() {
  borrarDescriptorV3();
  consentimiento.value = false;
  detenerCamara();
  limpiarCaptura();
}

async function continuar() {
  if (esVistaPrevia) {
    if (!tomada.value) return;
    await router.push({
      path: "/vista-previa/retiro-carga-pin",
      query: route.query
    });
    return;
  }
  if (!tomada.value || !fotoBase64.value ||
      validandoEnServidor.value) return;

  if (!usarRetiroV3.value) {
    error.value = "Debes autorizar la verificacion facial V3.";
    return;
  }
  const descriptor = copiarDescriptorV3();

  if (!descriptor) {
    error.value = "La captura V3 vencio. Repite el escaneo.";
    return;
  }

  validandoEnServidor.value = true;
  error.value = "";
  estado.value = "Verificando identidad con SYNCHRO...";

  try {
    await verificarRostroV3RetiroCarga(descriptor);

    if (!montada) return;

    limpiarCaptura();

    await router.replace("/retirar/dispositivo/pin");
  } catch (e) {
    if (montada) {
      error.value = e?.message || "No se pudo verificar el rostro.";
      estado.value = import.meta.env.DEV && ultimoResumenDistanciaV3
        ? "Distancias entre las tres tomas: " +
          ultimoResumenDistanciaV3 + "."
        : "Repite el escaneo si es necesario.";
    }
  } finally {
    descriptor.fill(0);
    validandoEnServidor.value = false;
  }
}

function comenzarEscaneo() {
  if (esVistaPrevia) {
    if (!consentimientoDemo.value || encendida.value || tomada.value) return;
    error.value = "";
    detectado.value = true;
    encendida.value = true;
    estado.value = "Escaneando rostro...";
    window.clearTimeout(temporizadorDemo);
    temporizadorDemo = window.setTimeout(() => {
      if (!montada) return;
      encendida.value = false;
      tomada.value = true;
      estado.value = "Escaneo visual completado.";
    }, 1800);
    return;
  }
  if (modoCarga.value && (
    !pruebaDescriptorV3 ||
    !registroV3Habilitado ||
    !aceptaDescriptorV3.value
  )) {
    error.value = "Debes habilitar V3 y aceptar el aviso facial.";
    return;
  }
  if (consentimiento.value) {
    iniciarCamara();
  } else {
    consentimiento.value = true;
  }
}
watch(aceptaDescriptorV3, () => {
  borrarDescriptorV3();

  if (!encendida.value && !iniciando.value) {
    motor = null;
  }
});
watch(aceptaRetiroV3, () => {
  borrarDescriptorV3();

  if (!encendida.value && !iniciando.value)
    motor = null;
});

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
  window.clearTimeout(temporizadorDemo);
  borrarDescriptorV3();
  montada = false;
  detenerCamara();
  limpiarCaptura();
  document.removeEventListener("visibilitychange", ocultar);
});
</script>

<template>
  <main class="pantalla-rostro" :class="{ 'tema-oscuro': oscuro, 'facial-con-imagen': encendida || tomada }">
    <header class="titulo-paso">
      <h1>Verificación facial</h1>
      <p>Verifica tu identidad para retirar el dispositivo.</p>
    </header>

    <section class="zona-facial" aria-label="Registro facial">
      <div class="panel-facial">
        <div class="visor">
          <video
            ref="video"
            muted
            playsinline
            :class="{ visible: encendida }"
          ></video>

          <img
            v-if="tomada && !esVistaPrevia"
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

          <div v-if="(!encendida && !tomada) || (esVistaPrevia && encendida)" class="sin-camara">
            <img
              :src="oscuro ? '/iconos/complementarios/38_escanear_rostro-oscuro.svg' : '/iconos/complementarios/38_escanear_rostro.svg'"
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
                ? (retirando ? "Continúa para introducir el PIN de retiro." : "Continúa para crear tu PIN de retiro.")
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
      <label
        v-if="esVistaPrevia && !encendida && !tomada"
        class="consentimiento-v3"
        data-preview-permitido
      >
        <input
          v-model="consentimientoDemo"
          data-preview-permitido
          type="checkbox"
        />
        <span>
          Autorizo verificar mi identidad para retirar el dispositivo.
        </span>
      </label>
      <label
        v-if="!esVistaPrevia && retiroV3Habilitado && retirando && !encendida && !tomada"
        class="consentimiento-v3"
      >
        <input v-model="aceptaRetiroV3" type="checkbox" />
        <span>
          Autorizo comparar mi rostro con el registrado para verificar
          mi identidad al retirar el dispositivo. También utilizaré mi PIN.
        </span>
      </label>      <label
        v-if="!esVistaPrevia && pruebaDescriptorV3 && registroV3Habilitado && !retirando && !encendida && !tomada"
        class="consentimiento-v3"
      >
        <input
          v-model="aceptaDescriptorV3"
          type="checkbox"
        />
        <span>
          Autorizo registrar mi rostro para proteger el dispositivo
          y verificar mi identidad al retirarlo.
        </span>
      </label>
      <button
        v-if="!tomada && !encendida && !iniciando"
        type="button"
        class="continuar"
        :disabled="esVistaPrevia && !consentimientoDemo"
        data-preview-permitido
        @click="comenzarEscaneo"
      >
        <span>
          {{ consentimiento ? "Reintentar escaneo" : "Comenzar escaneo" }}
        </span>
      </button>

      <button
        v-if="tomada && error && !validandoEnServidor"
        class="continuar"
        type="button"
        @click="repetir"
      >
        <span>Repetir captura</span>
      </button>

      <button
        class="continuar"
        type="button"
        v-if="tomada && !error"
        :disabled="validandoEnServidor"
        data-preview-permitido
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

/* Tablet horizontal: titulo en la misma cota que MenuView.
   Solo cambia el encabezado; el visor, ovalo y botones siguen fijos. */
@media (orientation: landscape) and (min-width: 768px) and (min-height: 700px) {
  .titulo-paso {
    top: calc(clamp(12px, 2vh, 22px) - 10px);
  }
}

/* Tablet vertical: mismo inicio del titulo que el menu (46px bajo el encabezado).
   El padding del titulo (10px) se descuenta para obtener la cota visual. */
@media (orientation: portrait) and (min-width: 600px) and (min-height: 680px) {
  .titulo-paso {
    top: 36px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .linea-escaneo {
    animation: none;
    top: 50%;
  }
}

.consentimiento-v3 {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
  max-width: 44rem;
  margin: 0 auto 0.6rem;
  text-align: left;
  font-size: 0.85rem;
  line-height: 1.35;
}

.consentimiento-v3 input {
  width: 1.2rem;
  height: 1.2rem;
  flex-shrink: 0;
}

/* SYNCHRO-FACIAL-TEMA-V6
   Tema claro y oscuro para Guardar y Retirar.
   La camara y la validacion permanecen intactas. */

/* MODO CLARO */
.pantalla-rostro:not(.tema-oscuro) {
  background: #f2f7fa;
  color: #01305c;
}

.pantalla-rostro:not(.tema-oscuro) .visor {
  background: #f2f7fa;
}

.pantalla-rostro:not(.tema-oscuro) .titulo-paso h1 {
  color: #01305c;
  text-shadow: none;
}

.pantalla-rostro:not(.tema-oscuro) .titulo-paso p {
  color: #5b6770;
  text-shadow: none;
}

.pantalla-rostro:not(.tema-oscuro) .panel-facial::after {
  background: linear-gradient(
    to bottom,
    transparent,
    rgba(242,247,250,.70) 40%,
    #f2f7fa 100%
  );
}

.pantalla-rostro:not(.tema-oscuro) .guia-ovalo {
  border-color: #01305c;
  box-shadow: 0 0 0 999px rgba(1,48,92,.035);
}

.pantalla-rostro:not(.tema-oscuro) .guia-ovalo.activo {
  border-color: #00a99b;
  box-shadow: 0 0 18px rgba(0,185,199,.22);
}

.pantalla-rostro:not(.tema-oscuro) .guia-ovalo.terminado {
  border-color: #00b9c7;
  box-shadow: 0 0 18px rgba(0,185,199,.25);
}

.pantalla-rostro:not(.tema-oscuro) .texto-panel h2 {
  color: #01305c;
}

.pantalla-rostro:not(.tema-oscuro) .texto-panel p,
.pantalla-rostro:not(.tema-oscuro) .retroalimentacion {
  color: #465f70;
}

.pantalla-rostro:not(.tema-oscuro) .retroalimentacion .error {
  color: #a32f30;
}

.pantalla-rostro:not(.tema-oscuro) .pie-facial {
  background: linear-gradient(180deg, #0b4477 0%, #01305c 100%);
  border-top: 1px solid rgba(94,198,232,.28);
  color: #ffffff;
}

.pantalla-rostro:not(.tema-oscuro) .continuar {
  background: #ffffff;
  border-color: #5ec6e8;
  color: #01305c;
  box-shadow: 0 8px 20px rgba(0,0,0,.16);
}

/* CAMARA ACTIVA O FOTO CAPTURADA:
   Conservar texto legible sobre la imagen. */

.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.panel-facial::after {
  background: linear-gradient(
    to bottom,
    transparent,
    rgba(1,22,42,.65) 40%,
    rgba(1,22,42,.96) 100%
  );
}

.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.titulo-paso h1 {
  color: #ffffff;
  text-shadow: 0 2px 12px rgba(0,0,0,.55);
}

.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.titulo-paso p {
  color: #e2f3f8;
  text-shadow: 0 2px 10px rgba(0,0,0,.55);
}

.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.texto-panel h2 {
  color: #ffffff;
}

.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.texto-panel p,
.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.retroalimentacion {
  color: #e2f3f8;
}

.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.retroalimentacion .error {
  color: #ffcebd;
}

.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.guia-ovalo:not(.activo):not(.terminado) {
  border-color: rgba(245,253,255,.94);
}

/* MODO OSCURO: identidad original */
.pantalla-rostro.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.pantalla-rostro.tema-oscuro .visor {
  background: #061f36;
}

.pantalla-rostro.tema-oscuro .pie-facial {
  background: rgba(1,22,42,.96);
  color: #ffffff;
}


.pantalla-rostro:not(.tema-oscuro).facial-con-imagen .pie-facial {
  background: linear-gradient(180deg, rgba(11,68,119,.98) 0%, rgba(1,48,92,.99) 100%);
  border-top-color: rgba(94,198,232,.28);
}

/* SYNCHRO-FACIAL-PIE-FLOTANTE-V7
   Elimina la barra rectangular inferior.
   Conserva el boton y las funciones existentes. */

.pantalla-rostro .pie-facial,
.pantalla-rostro.tema-oscuro .pie-facial,
.pantalla-rostro:not(.tema-oscuro) .pie-facial,
.pantalla-rostro:not(.tema-oscuro).facial-con-imagen .pie-facial {
  background: transparent !important;
  border: none !important;
  box-shadow: none !important;
}

/* El boton permanece flotante y visible. */
.pantalla-rostro .pie-facial .continuar {
  position: relative;
  z-index: 2;
}

/* Mantener legibles los avisos del pie. */
.pantalla-rostro:not(.tema-oscuro).facial-con-imagen
.consentimiento-v3 {
  color: #ffffff;
  text-shadow: 0 1px 4px rgba(0,0,0,.65);
}

</style>