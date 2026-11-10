<script setup>
import { ref } from "vue";

defineProps({
  oscuro: {
    type: Boolean,
    default: false,
  },
});

const dialogo = ref(null);

function abrir() {
  dialogo.value?.showModal();
}

function cerrar() {
  dialogo.value?.close();
}

defineExpose({ abrir, cerrar });
</script>

<template>
  <dialog
    ref="dialogo"
    class="centro-ayuda"
    :class="{ oscuro }"
    aria-labelledby="titulo-ayuda"
  >
    <h2 id="titulo-ayuda">Centro de ayuda</h2>
    <p>Selecciona una pregunta para conocer los pasos.</p>

    <div class="preguntas">
      <details>
        <summary>¿Cómo guardo mis pertenencias?</summary>
        <p>
          Selecciona Guardar pertenencias, elige el compartimento y la duración, confirma el pago simulado y guarda tus objetos. Conserva el PIN y los datos de recuperación.
        </p>
      </details>

      <details>
        <summary>¿Cómo envío una encomienda?</summary>
        <p>
          Crea la reserva desde tu cuenta de cliente en la web. En la terminal, selecciona Tengo una reserva. El destinatario debe aceptar por WhatsApp antes del depósito.
        </p>
      </details>

      <details>
        <summary>¿Cómo retiro una encomienda?</summary>
        <p>
          Selecciona Retirar. El proceso requiere una captura de rostro simulada del destinatario y un PIN temporal enviado por WhatsApp.
        </p>
      </details>

      <details>
        <summary>¿Cómo utilizo una reserva?</summary>
        <p>
          Selecciona Tengo una reserva y sigue las instrucciones de
          identificación.
        </p>
      </details>

      <details>
        <summary>¿Cómo cargo mi dispositivo?</summary>
        <p>
          Selecciona Cargar dispositivo y sigue las indicaciones para celulares
          o laptops.
        </p>
      </details>
    </div>

    <button type="button" class="cerrar" @click="cerrar">Cerrar ayuda</button>
  </dialog>
</template>

<style scoped>
.centro-ayuda {
  width: min(90vw, 480px);
  max-height: 85dvh;
  margin: auto;
  padding: 24px;
  overflow-y: auto;
  border: none;
  border-radius: 20px;
  background: #ffffff;
  color: #01305c;
}

.centro-ayuda::backdrop {
  background: rgba(0, 15, 30, 0.7);
}

h2 {
  font-size: 23px;
}

.centro-ayuda > p {
  margin: 12px 0 18px;
}

.preguntas {
  display: grid;
  gap: 10px;
}

details {
  padding: 14px;
  border: 1px solid #d9e6ed;
  border-radius: 12px;
  background: #f2f7fa;
}

summary {
  cursor: pointer;
  font-weight: 600;
}

details p {
  margin-top: 12px;
  font-size: 14px;
  line-height: 1.6;
}

.cerrar {
  width: 100%;
  min-height: 48px;
  margin-top: 20px;
  border: 0;
  border-radius: 12px;
  background: #01305c;
  color: #ffffff;
  font-weight: 600;
  cursor: pointer;
}

.oscuro {
  background: #103956;
  color: #ffffff;
}

.oscuro details {
  background: #183f5b;
  border-color: #42647e;
}

button:focus-visible,
summary:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}
</style>
