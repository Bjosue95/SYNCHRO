<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import {
  obtenerOrdenRetiroPagado,
  limpiarRetiro
} from "../services/retiroSesion";
import {
  finalizarRetiroPersonalPagado
} from "../services/retiroFacialManualApi";

const router = useRouter();
const { oscuro } = useTema();

const orden = ref(obtenerOrdenRetiroPagado());
const verificando = ref(false);
const completado = ref(false);
const mensaje = ref("");
const casilleroFinal = ref("");

const importe = computed(() => {
  const valor = orden.value?.importeHnl;
  return Number.isFinite(valor)
    ? new Intl.NumberFormat("es-HN", {
        style: "currency",
        currency: "HNL"
      }).format(valor)
    : "No disponible";
});

onMounted(() => {
  if (!orden.value) {
    router.replace("/retirar/rostro");
  }
});

onBeforeUnmount(() => {
  // Ninguna autorizacion queda guardada al abandonar la vista.
  if (!completado.value) limpiarRetiro();
});

async function comprobarPago() {
  if (!orden.value || verificando.value || completado.value) return;

  verificando.value = true;
  mensaje.value = "";

  try {
    // El servidor exige un pago previamente confirmado
    // por un operador autorizado.
    const resultado = await finalizarRetiroPersonalPagado(
      orden.value.cobroId,
      orden.value.autorizacionPago
    );

    casilleroFinal.value = resultado.codigoCasillero;
    completado.value = true;

    limpiarRetiro();

    mensaje.value =
      "Retiro confirmado. En este prototipo, abre manualmente " +
      "el casillero asignado.";
  } catch (error) {
    const detalle = error instanceof Error
      ? error.message
      : "No se pudo comprobar el pago.";

    mensaje.value =
      `${detalle} Si el pago aun no ha sido confirmado, ` +
      "solicita ayuda al operador. La autorizacion temporal " +
      "puede vencer y requerir una nueva verificacion.";
  } finally {
    verificando.value = false;
  }
}

function finalizar() {
  limpiarRetiro();
  router.replace("/menu");
}
</script>

<template>
  <main class="pantalla-pago" :class="{ 'tema-oscuro': oscuro }">
    <section class="contenido-pago" v-if="orden">
      <header>
        <span class="etiqueta">SYNCHRO · RETIRO PERSONAL</span>
        <h1>{{ completado ? "Retiro completado" : "Resumen de tu resguardo" }}</h1>
        <p v-if="!completado">
          Tu PIN ha sido verificado. El casillero permanece cerrado
          hasta confirmar el pago.
        </p>
      </header>

      <div class="tarjeta-resumen">
        <div class="dato">
          <span>Casillero</span>
          <strong>{{ orden.codigoCasillero }}</strong>
        </div>
        <div class="dato">
          <span>Tiempo registrado</span>
          <strong>{{ orden.minutosUtilizados }} minuto(s)</strong>
        </div>
        <div class="dato total">
          <span>Importe</span>
          <strong>{{ importe }}</strong>
        </div>
        <p v-if="orden.descripcionTarifa" class="descripcion">
          {{ orden.descripcionTarifa }}
        </p>
      </div>

      <p v-if="!completado" class="estado">
        <span class="punto"></span>
        Pago pendiente de confirmación administrativa
      </p>

      <p v-if="completado" class="estado completado">
        <span class="punto"></span>
        Casillero {{ casilleroFinal }} disponible
      </p>

      <p v-if="mensaje" class="mensaje" role="status">
        {{ mensaje }}
      </p>

      <button
        class="boton-principal"
        type="button"
        :disabled="verificando"
        @click="completado ? finalizar() : comprobarPago()"
      >
        {{
          completado
            ? "Finalizar"
            : verificando
              ? "Comprobando..."
              : "Verificar pago y retirar"
        }}
      </button>

      <p v-if="!completado" class="nota">
        Este prototipo utiliza pagos simulados.
        La tablet no puede confirmar pagos por sí misma.
      </p>
    </section>
  </main>
</template>

<style scoped>
.pantalla-pago {
  min-height: 100%;
  background: #f2f7fa;
  color: #01305c;
  padding: clamp(20px, 4vw, 36px);
  overflow-y: auto;
  box-sizing: border-box;
}

.contenido-pago {
  width: min(100%, 520px);
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

header {
  text-align: center;
}

.etiqueta {
  font-size: 12px;
  font-weight: 800;
  letter-spacing: .09em;
  color: #0990a3;
}

h1 {
  font-size: clamp(26px, 3vw, 34px);
  margin: 12px 0;
}

header p, .nota {
  color: #5b7282;
  line-height: 1.5;
}

.tarjeta-resumen {
  padding: 24px;
  border: 1px solid #b8dce7;
  border-radius: 20px;
  background: #ffffff;
  box-shadow: 0 8px 24px rgba(1, 48, 92, .08);
}

.dato {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 0;
  border-bottom: 1px solid #dce9ef;
}

.dato span {
  color: #5b7282;
}

.dato strong {
  font-size: clamp(17px, 2vw, 23px);
  text-align: right;
}

.total {
  border-bottom: 0;
}

.total strong {
  color: #008c9a;
  font-size: clamp(25px, 3vw, 32px);
}

.descripcion {
  font-size: 13px;
  margin: 10px 0 0;
  color: #5b7282;
}

.estado {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 9px;
  text-align: center;
  font-weight: 650;
}

.punto {
  width: 10px;
  height: 10px;
  flex-shrink: 0;
  border-radius: 50%;
  background: #d59c27;
}

.completado .punto {
  background: #16a47c;
}

.mensaje {
  margin: 0;
  padding: 14px;
  border-radius: 10px;
  background: #e5f3f7;
  line-height: 1.5;
  text-align: center;
}

.boton-principal {
  align-self: center;
  min-height: 58px;
  max-width: 100%;
  padding: 12px 24px;
  border: 2px solid #00b9c7;
  border-radius: 15px;
  background: #f4feff;
  color: #01305c;
  font: inherit;
  font-size: 18px;
  font-weight: 800;
  box-shadow: 0 4px 0 #00a5b2;
  cursor: pointer;
}

.boton-principal:disabled {
  opacity: .6;
  cursor: not-allowed;
}

.boton-principal:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 4px;
}

.nota {
  text-align: center;
  font-size: 13px;
}

.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .tarjeta-resumen {
  background: #103956;
  border-color: #42647e;
  box-shadow: 0 8px 24px rgba(0, 0, 0, .18);
}

.tema-oscuro .dato {
  border-bottom-color: #42647e;
}

.tema-oscuro .total {
  border-bottom: 0;
}

.tema-oscuro .dato span,
.tema-oscuro .descripcion,
.tema-oscuro header p,
.tema-oscuro .nota {
  color: #d4e2ea;
}

.tema-oscuro .total strong {
  color: #5ec6e8;
}

.tema-oscuro .boton-principal {
  background: #123d5b;
  color: #ffffff;
  border-color: #5ec6e8;
  box-shadow: 0 4px 0 #087d93;
}

.tema-oscuro .mensaje {
  background: #204766;
}

@media (max-width: 480px) {
  .dato {
    flex-direction: column;
    align-items: flex-start;
  }

  .dato strong {
    text-align: left;
  }
}
</style>