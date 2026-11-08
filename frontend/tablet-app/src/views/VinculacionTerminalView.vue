<script setup>
import { onMounted, ref } from "vue";
import { useTema } from "../composables/useTema";

const { oscuro, cambiarTema } = useTema();

const codigoTerminal = "SYN-TER-001";

const solicitando = ref(false);
const error = ref("");
const codigoVisual = ref("");
const fechaExpiracion = ref("");

// El token secreto permanece solo en memoria.
const tokenVinculacion = ref("");

const confirmando = ref(false);

async function confirmarVinculacion() {
  if (confirmando.value || !tokenVinculacion.value) return;

  confirmando.value = true;
  error.value = "";

  try {
    const respuesta = await fetch("/api/terminales/vinculacion/confirmar", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      credentials: "same-origin",
      body: JSON.stringify({
        token: tokenVinculacion.value,
      }),
    });

    if (respuesta.status === 401) {
      error.value =
        "La solicitud aún no está aprobada, ha vencido o no es válida.";
      return;
    }

    if (!respuesta.ok) {
      throw new Error(`HTTP ${respuesta.status}`);
    }

    estadoSesion.value = "autorizada";

    // El secreto ya fue utilizado: borrarlo de memoria.
    tokenVinculacion.value = "";
    codigoVisual.value = "";
    fechaExpiracion.value = "";
  } catch (e) {
    console.error("Error al confirmar la vinculación");

    error.value = "No se pudo confirmar la vinculación. Revisa la conexión.";
  } finally {
    confirmando.value = false;
  }
}

const estadoSesion = ref("comprobando");

async function comprobarSesionTerminal() {
  try {
    const respuesta = await fetch("/api/terminales/mi-sesion", {
      credentials: "same-origin",
      cache: "no-store",
    });

    if (respuesta.ok) {
      estadoSesion.value = "autorizada";
    } else if (respuesta.status === 401) {
      estadoSesion.value = "sin-sesion";
    } else {
      estadoSesion.value = "error";
    }
  } catch (e) {
    console.error("Error al comprobar la sesión:", e);
    estadoSesion.value = "error";
  }
}

onMounted(comprobarSesionTerminal);

async function solicitarVinculacion() {
  if (solicitando.value) return;

  solicitando.value = true;
  error.value = "";

  try {
    const respuesta = await fetch("/api/terminales/vinculacion/solicitar", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      credentials: "same-origin",
      body: JSON.stringify({
        codigoTerminal,
      }),
    });

    if (!respuesta.ok) {
      throw new Error(`Error HTTP ${respuesta.status}`);
    }

    const datos = await respuesta.json();

    codigoVisual.value = datos.codigoVisual;
    fechaExpiracion.value = datos.fechaExpiracion;
    tokenVinculacion.value = datos.token;
  } catch (e) {
    console.error("Error de vinculación:", e);

    error.value = "No se pudo solicitar la vinculación. Comprueba la conexión.";
  } finally {
    solicitando.value = false;
  }
}
</script>

<template>
  <main class="pantalla" :class="{ 'tema-oscuro': oscuro }">
    <header class="encabezado">
      <strong>SYNCHRO</strong>

      <button type="button" @click="cambiarTema">Cambiar tema</button>
    </header>

    <section class="tarjeta">
      <span class="etiqueta">CONFIGURACIÓN DE TERMINAL</span>

      <h1>Vincular dispositivo</h1>

      <p class="descripcion">
        Esta terminal necesita autorización de un administrador antes de
        utilizar los servicios de SYNCHRO.
      </p>

      <button
        type="button"
        class="boton-principal"
        :disabled="confirmando"
        @click="confirmarVinculacion"
      >
        {{
          confirmando ? "Comprobando autorización..." : "Confirmar vinculación"
        }}
      </button>

      <div class="identificador">
        <span>Terminal registrada</span>
        <strong>{{ codigoTerminal }}</strong>
      </div>

      <p v-if="estadoSesion === 'comprobando'" class="descripcion">
        Comprobando autorización de la terminal...
      </p>

      <p v-else-if="estadoSesion === 'autorizada'" class="descripcion">
        Esta tablet tiene una sesión autorizada.
      </p>

      <p v-else-if="estadoSesion === 'sin-sesion'" class="descripcion">
        Este navegador todavía no tiene una sesión de terminal.
      </p>

      <p v-else class="error">
        No se pudo comprobar la sesión. Revisa la conexión.
      </p>

      <template v-if="codigoVisual">
        <p class="indicacion">Muestra este código al administrador:</p>

        <div class="codigo">{{ codigoVisual }}</div>

        <p class="descripcion">
          El código vence en 5 minutos. Espera la aprobación del administrador.
        </p>
      </template>

      <button
        v-else-if="estadoSesion !== 'autorizada'"
        class="boton-principal"
        type="button"
        :disabled="solicitando"
        @click="solicitarVinculacion"
      >
        {{ solicitando ? "Generando código..." : "Solicitar vinculación" }}
      </button>

      <p v-if="error" class="error" role="alert">
        {{ error }}
      </p>
    </section>
  </main>
</template>

<style scoped>
.pantalla {
  min-height: 100dvh;
  padding: 24px;
  background: #f2f7fa;
  color: #01305c;
  transition:
    background-color 150ms ease,
    color 150ms ease;
}

.pantalla.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.encabezado {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 16px;
}

.encabezado button {
  min-height: 48px;
  padding: 10px 16px;
  border: 1px solid #00b9c7;
  border-radius: 12px;
  background: transparent;
  color: inherit;
  cursor: pointer;
}

.tarjeta {
  width: min(100%, 520px);
  margin: 60px auto;
  padding: 32px;
  border-radius: 24px;
  background: #ffffff;
  color: #01305c;
  text-align: center;
  box-shadow: 0 8px 30px #061f3614;
}

.tema-oscuro .tarjeta {
  background: #10334f;
  color: #ffffff;
}

.etiqueta {
  color: #009ca9;
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 1px;
}

h1 {
  margin: 16px 0;
  font-size: clamp(26px, 4vw, 36px);
}

.descripcion,
.indicacion {
  line-height: 1.6;
}

.identificador {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 26px 0;
  padding: 18px;
  border-radius: 14px;
  background: #e4f2f6;
}

.tema-oscuro .identificador {
  background: #061f36;
}

.codigo {
  margin: 20px 0;
  font-size: clamp(32px, 6vw, 48px);
  font-weight: 800;
  letter-spacing: 5px;
  color: #00a9b7;
  font-variant-numeric: tabular-nums;
}

.boton-principal {
  width: 100%;
  min-height: 56px;
  padding: 14px;
  border: 0;
  border-radius: 14px;
  background: #00b9c7;
  color: #061f36;
  font-size: 17px;
  font-weight: 700;
  cursor: pointer;
}

.boton-principal:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error {
  margin-top: 16px;
  color: #d9534f;
}
</style>
