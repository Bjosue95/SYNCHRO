<script setup>
import { computed, onBeforeUnmount, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { obtenerEleccionReservaWeb, registrarFotoReservaWeb } from "../services/reservaWebSesion.js";
import { registrarCaptura } from "../services/retiroSesion.js";
import { registrarFotoRetiroEncomienda } from "../services/retiroEncomiendaSesion.js";
import { validarDescriptorFacialV3Temporal, validarRostroV3Retiro } from "../services/retiroFacialManualApi";
import { registrarDescriptorV3, borrarDescriptorV3, copiarDescriptorV3, consumirDescriptorV3 } from "../services/descriptorFacialV3Temporal.js";

// SYNCHRO-FACIAL-FLUJO-LIMPIO-V5
// Guardar: solo detectar y capturar.
// Retiro: comparar con la referencia registrada.

const route = useRoute();
const router = useRouter();
const { oscuro } = useTema();
const esReservaWeb = computed(() => route.path === "/guardar/reserva/captura");
const esRetiroEncomienda = computed(() => route.path === "/retirar/encomienda");
const reservaPersonal = computed(() => esReservaWeb.value && obtenerEleccionReservaWeb()?.servicio === "Personal");
const depositoConfirmado = ref(false);
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
const usarDescriptor = computed(() => !esRetiroEncomienda.value && (!esReservaWeb.value || reservaPersonal.value));
const encendida = ref(false);
const iniciando = ref(false);
const detectado = ref(false);
const tomada = ref(false);
const validandoEnServidor = ref(false);

const fotoUrl = ref("");
const fotoBase64 = ref("");
const estado = ref("Inicia el escaneo para continuar.");
const error = ref("");

const casillero = computed(() => {
  if (esReservaWeb.value) {
    return "Reserva web";
  }
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

function limpiarMejorCaptura() {
  if (mejorCaptura) mejorCaptura.descriptor.fill(0);
  mejorCaptura = null;
  muestrasCalidad = 0;
}
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
      mesh: { enabled: usarDescriptor.value },
      description: { enabled: usarDescriptor.value },
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

    const modoV3 = usarDescriptor.value;

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
          ? "Captura lista."
          : "Captura lista.";

        // Mantener la validacion V3 del deposito.
        if (modoV3 &&
            !retirando.value && !esReservaWeb.value &&
            consentimiento.value) {

          const temporal = copiarDescriptorV3();

          if (!temporal) {
            error.value =
              "Repite el registro facial.";
          } else {
            estado.value =
              "Validando registro facial...";

            try {
              await validarDescriptorFacialV3Temporal(
                temporal
              );

              if (montada) {
                estado.value =
                  "Registro facial listo.";
              }
            } catch (e) {
              if (montada) {
                estado.value =
                  "No se pudo validar el registro facial.";

                error.value =
                  e?.message || "No se pudo validar el registro facial.";
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
  if (!consentimiento.value || !tomada.value || !fotoBase64.value || validandoEnServidor.value) return;
  if (esReservaWeb.value) {
    if (!obtenerEleccionReservaWeb()) {
      error.value = "Vuelve a seleccionar el servicio de tu reserva.";
      return;
    }
    if (!consentimiento.value) {
      error.value = "Debes autorizar la captura fotográfica.";
      return;
    }
    try {
      const coincidencia=/^data:image\/png;base64,([A-Za-z0-9+/=]+)$/.exec(fotoBase64.value);
      if (!coincidencia) throw new Error("La fotografía no tiene un formato PNG válido.");
      const descriptor = reservaPersonal.value ? consumirDescriptorV3() : null;
      try {
        if(reservaPersonal.value && !descriptor)throw new Error("Repite el registro facial V3.");
        registrarFotoReservaWeb(coincidencia[1],descriptor);
      } finally { if(descriptor)descriptor.fill(0); }
      limpiarCaptura();
      detenerCamara();
      await router.push("/guardar/reserva/pin");
    } catch (e) {
      error.value=e?.message||"No se pudo preparar la fotografía.";
    }
    return;
  }

  if (esRetiroEncomienda.value) {
    if (!consentimiento.value) { error.value="Debes autorizar la captura fotográfica."; return; }
    try {
      const foto=/^data:image\/png;base64,([A-Za-z0-9+/=]+)$/.exec(fotoBase64.value);
      if(!foto)throw new Error("Fotografía PNG inválida.");
      registrarFotoRetiroEncomienda(foto[1]);
      limpiarCaptura();detenerCamara();
      await router.push("/retirar/encomienda/pin");
    }catch(e){error.value=e?.message||"No se pudo preparar la fotografía.";}
    return;
  }
  if (retirando.value) {
    validandoEnServidor.value = true;
    error.value = "";
    estado.value = "Verificando captura con SYNCHRO...";
    try {
      const descriptorV3 = consumirDescriptorV3();

      let tokenFacial;

      try {
        if (!descriptorV3) {
          throw new Error(
            "El descriptor V3 venció. Repite la captura."
          );
        }

        tokenFacial = await validarRostroV3Retiro(descriptorV3);
      } finally {
        if (descriptorV3) descriptorV3.fill(0);
      }
      if (!montada) return;
      registrarCaptura(tokenFacial);
      limpiarCaptura();
      router.push("/retirar/pin");
    } catch (e) {
      if (montada) {
        error.value = e?.message || "El servidor no pudo verificar la referencia facial.";
        estado.value = "Repite la captura si es necesario.";
      }
    } finally {
      validandoEnServidor.value = false;
    }
    return;
  }

  const referencia = copiarDescriptorV3();
  if (!referencia) { error.value="La referencia facial venció. Repite el escaneo."; return; }
  referencia.fill(0);
  sessionStorage.setItem("synchroRostroTemporal", fotoBase64.value);
  sessionStorage.setItem(
    "synchroRostroMeta",
    JSON.stringify({
      casillero: casillero.value,
      objeto: route.query.objeto || "",
      tamano: route.query.tamano || "",
      consentimientoV3: usarDescriptor.value && consentimiento.value && !retirando.value,
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
  if (esReservaWeb.value && !obtenerEleccionReservaWeb()) {
    error.value = "Selecciona antes el tipo de reserva.";
    return;
  }
  // La acción explícita de iniciar el escaneo acepta el aviso mostrado.
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
  <main class="pantalla-rostro" :class="{ 'tema-oscuro': oscuro, 'facial-con-imagen': encendida || tomada }">
    <header class="titulo-paso">
      <h1>Registro facial</h1>
      <p v-if="!retirando && !esReservaWeb && casillero !== 'Sin seleccionar'">Casillero {{ casillero }}</p>
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
              :src="oscuro ? '/iconos/complementarios/38_escanear_rostro-oscuro.svg' : '/iconos/complementarios/38_escanear_rostro.svg'"
              alt=""
            />
          </div>
        </div>

        <div class="texto-panel">
          <h2>
            {{ depositoConfirmado ? "Depósito confirmado" : tomada ? "Fotografía capturada" : "Coloca tu rostro en el óvalo" }}
          </h2>

          <p v-if="tomada">
            {{ (esReservaWeb ? "Continúa para introducir el PIN de depósito recibido por WhatsApp." : retirando ? "Continúa para introducir el PIN de retiro." : "Continúa para enviar el PIN por WhatsApp.") }}
          </p>
        </div>

        <div class="retroalimentacion" role="status" aria-live="polite">
          <p v-if="error" class="error">{{ error }}</p>
          <p v-else>{{ estado }}</p>
          <details v-if="!encendida && !tomada && !depositoConfirmado" class="privacidad-facial">
            <summary data-preview-permitido>Privacidad</summary>
            <p id="aviso-facial" class="privacidad-facial__texto">
            {{ usarDescriptor
              ? (retirando ? "Al iniciar el escaneo, autorizas comparar tu rostro con la referencia del depósito. También necesitas tu PIN."
                : "Al iniciar el escaneo, autorizas registrar tu referencia facial cifrada para verificar el retiro. Se elimina al cerrar el servicio o vencer la reserva." + (reservaPersonal ? " La fotografía de evidencia se conserva hasta 30 días." : ""))
              : "Al iniciar el escaneo, autorizas tomar una fotografía como evidencia de la encomienda, sin comparación biométrica. Se conserva hasta 30 días tras completar la operación." }}
          </p>
          </details>
        </div>
      </div>
    </section>

    <footer class="pie-facial">
      <button
        v-if="!tomada && !encendida && !iniciando && !depositoConfirmado"
        type="button"
        class="continuar"
        aria-describedby="aviso-facial"
        @click="comenzarEscaneo"
      >
        <span>
          {{ consentimiento ? "Reintentar escaneo" : "Iniciar escaneo" }}
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
        v-if="tomada && !error && !depositoConfirmado"
        :disabled="validandoEnServidor"
        @click="continuar"
      >
        <span>{{ esReservaWeb || esRetiroEncomienda ? "Continuar al PIN" : "Continuar" }}</span>

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
      <button
        v-if="esReservaWeb && depositoConfirmado"
        type="button"
        class="continuar"
        @click="router.replace('/menu')"
      >
        Finalizar
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

/* Aviso accesible bajo demanda, sin párrafos permanentes en el recorrido. */
.privacidad-facial {
  position: relative; z-index: 3; flex: 0 0 auto;
  font-size: 0.8rem; text-align: center; color: #01305c;
}
.privacidad-facial summary {
  cursor: pointer; padding: 5px 10px; min-height: 28px;
  text-decoration: underline; text-underline-offset: 3px;
}
.privacidad-facial summary:focus-visible {
  outline: 2px solid #00b9c7; outline-offset: 2px; border-radius: 4px;
}
.privacidad-facial__texto {
  position: absolute; bottom: calc(100% + 8px); left: 50%;
  transform: translateX(-50%); width: min(390px, 85vw);
  max-height: min(36dvh, 220px); overflow-y: auto; box-sizing: border-box;
  padding: 16px; margin: 0; background: #fff; color: #01305c;
  border: 1px solid #5ec6e8; border-radius: 12px;
  box-shadow: 0 8px 24px rgba(0,0,0,.18); text-align: left; line-height: 1.5;
}
.tema-oscuro .privacidad-facial { color: #d8f5fa; }
.tema-oscuro .privacidad-facial__texto {
  background: #103956; color: #fff; border-color: #315573;
}

/* Privacidad pertenece al grupo de instrucciones; el botón queda próximo. */
.retroalimentacion:has(.privacidad-facial) {
  display: flex;
  justify-content: center;
  align-items: center;
  flex-wrap: wrap;
  column-gap: 8px;
  row-gap: 2px;
}
.privacidad-facial summary {
  padding: 3px 6px;
  min-height: 28px;
}
.pantalla-rostro:has(.privacidad-facial) .panel-facial {
  padding-bottom: 104px;
}
.pantalla-rostro:has(.privacidad-facial) .pie-facial {
  box-sizing: border-box;
  height: 96px;
  justify-content: flex-start;
  padding: 6px 16px 8px;
}
.pantalla-rostro:has(.privacidad-facial) .pie-facial .continuar {
  flex-shrink: 0;
}
</style>
