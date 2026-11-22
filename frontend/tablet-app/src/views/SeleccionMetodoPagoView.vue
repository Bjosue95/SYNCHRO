<script setup>
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";

import ServiceCard from "../components/ServiceCard.vue";
import { useTema } from "../composables/useTema";
import {
  obtenerOrdenRetiroPagado,
  registrarMetodoPagoRetiro
} from "../services/retiroSesion";

const props = defineProps({
  ordenEjemplo: {
    type: Object,
    default: null
  }
});

const router = useRouter();
const { oscuro } = useTema();

const vistaPrevia =
  import.meta.env.DEV && props.ordenEjemplo !== null;

const orden = vistaPrevia
  ? props.ordenEjemplo
  : obtenerOrdenRetiroPagado();

const seleccion = ref("");

const metodos = [
  {
    codigo: "Efectivo",
    titulo: "Efectivo",
    icono: "/iconos/pagos/efectivo"
  },
  {
    codigo: "Tarjeta",
    titulo: "Tarjeta",
    icono: "/iconos/pagos/tarjeta"
  },
  {
    codigo: "NFC",
    titulo: "NFC",
    icono: "/iconos/pagos/nfc"
  }
];

onMounted(() => {
  if (!orden) {
    router.replace("/retirar/rostro");
  }
});

function seleccionar(codigo) {
  if (!orden) return;
  seleccion.value = codigo;
}

async function continuar() {
  if (!orden || !seleccion.value) return;

  if (vistaPrevia) {
    await router.push({
      path: "/vista-previa/pago-resumen",
      query: { metodo: seleccion.value }
    });
    return;
  }

  // Elegir el metodo no significa que el pago fue realizado.
  registrarMetodoPagoRetiro(seleccion.value);
  await router.push("/retirar/pago/resumen");
}
</script>

<template>
  <main
    class="pantalla-metodos"
    :class="{ 'tema-oscuro': oscuro }"
  >
    <div v-if="orden" class="contenido-metodos">
      <h1>¿Cómo deseas pagar?</h1>

      <section
        class="cuadricula-metodos"
        aria-label="Métodos de pago"
      >
        <button
          v-for="metodo in metodos"
          :key="metodo.codigo"
          type="button"
          class="celda-metodo"
          :class="{ seleccionada: seleccion === metodo.codigo }"
          :aria-pressed="seleccion === metodo.codigo"
          data-preview-permitido
          @click="seleccionar(metodo.codigo)"
        >
          <ServiceCard
            :titulo="metodo.titulo"
            descripcion=""
            :icono="metodo.icono"
            :oscuro="oscuro"
          />
        </button>

        <div class="espacio-vacio" aria-hidden="true"></div>
      </section>

      <div class="accion-metodos">
        <button
          type="button"
          class="boton-continuar"
          data-preview-permitido
          :disabled="!seleccion"
          @click="continuar"
        >
          <span>Continuar</span>

          <span class="icono-continuar" aria-hidden="true">
            <img
              src="/iconos/complementarios/29_continuar.svg"
              class="continuar-claro"
              alt=""
            />
            <img
              src="/iconos/complementarios/29_continuar-oscuro.svg"
              class="continuar-oscuro"
              alt=""
            />
          </span>
        </button>
      </div>
    </div>
  </main>
</template>

<style scoped>
/* SYNCHRO | SELECCION DE PAGO
   Geometria del titulo: MenuView.vue.
   Seleccion de tarjetas: GuardarView.vue.
   Continuar: boton unificado de SeleccionTamanoView.vue.
   La accion inferior no altera la posicion del titulo.
*/
.pantalla-metodos {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: minmax(0, 1fr);
  place-items: center;
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(12px, 2vh, 22px) clamp(12px, 2.4vw, 28px);
  background: #f2f7fa;
  color: #01305c;
  transition: background-color 150ms ease, color 150ms ease;
}

/* Misma posicion del titulo que en el menu. */
.contenido-metodos {
  position: relative;
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);
  gap: clamp(10px, 1.8vh, 20px);
  width: min(100%, 800px);
  height: min(100%, 610px);
  min-width: 0;
  min-height: 0;
  --espacio-boton: 92px;
}

.contenido-metodos > h1 {
  margin: 0;
  font-size: clamp(22px, 2.8vw, 34px);
  line-height: 1.18;
  font-weight: 700;
  text-align: center;
}

.cuadricula-metodos {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  grid-template-rows: repeat(2, minmax(0, 1fr));
  gap: clamp(10px, 1.8vh, 18px);
  grid-row: 2;
  align-self: start;
  width: 100%;
  height: calc(100% - var(--espacio-boton));
  min-width: 0;
  min-height: 0;
  margin: 0 auto;
}

.celda-metodo {
  position: relative;
  display: block;
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: 0;
  border: 0;
  border-radius: 24px;
  background: transparent;
  color: inherit;
  font: inherit;
  cursor: pointer;
  touch-action: manipulation;
}
.celda-metodo :deep(.tarjeta-servicio) {
  width: 100%;
  height: 100%;
  pointer-events: none;
}
.celda-metodo :deep(.tarjeta-servicio p) {
  display: none;
}
.celda-metodo.seleccionada :deep(.tarjeta-servicio) {
  background: #e8f9fa;
  border-color: #00b9c7;
  box-shadow: 0 0 0 2px rgba(0,185,199,.13);
}
.celda-metodo.seleccionada::after {
  content: "\2713";
  position: absolute;
  top: 10px;
  right: 10px;
  display: grid;
  place-items: center;
  width: clamp(26px, 3vw, 38px);
  aspect-ratio: 1;
  border-radius: 50%;
  background: #00b9c7;
  color: #01305c;
  font-size: clamp(17px, 1.8vw, 23px);
  font-weight: 800;
  line-height: 1;
  box-shadow: 0 2px 8px rgba(1,48,92,.12);
  pointer-events: none;
}
.espacio-vacio { pointer-events: none; }

/* El boton queda en la zona inferior sin tercera fila. */
.accion-metodos {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  justify-content: center;
  align-items: center;
  width: 100%;
  height: 72px;
  margin: 0;
}

.boton-continuar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  width: max-content;
  max-width: 100%;
  min-width: clamp(190px, 23vw, 250px);
  min-height: clamp(54px, 7vh, 64px);
  padding: 10px 24px;
  border: 2px solid #00b9c7;
  border-radius: 15px;
  background: #f4feff;
  color: #01305c;
  font: inherit;
  font-size: clamp(17px, 1.7vw, 21px);
  font-weight: 800;
  line-height: 1.2;
  box-shadow: 0 4px 0 #00a5b2, 0 7px 16px rgba(1,48,92,.10);
  cursor: pointer;
  touch-action: manipulation;
  transition: background-color 150ms ease, color 150ms ease,
              border-color 150ms ease, box-shadow 150ms ease, transform 150ms ease;
}
.boton-continuar:disabled {
  background: #edf3f5;
  color: #647784;
  border-color: #b5cad3;
  box-shadow: none;
  opacity: 1;
  cursor: not-allowed;
}
.boton-continuar:active:not(:disabled) {
  transform: translateY(3px) scale(.99);
  box-shadow: 0 1px 0 #00a5b2;
}
.icono-continuar {
  position: relative;
  display: block;
  width: 40px;
  height: 40px;
  flex: 0 0 40px;
}
.icono-continuar img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.continuar-claro { opacity: 1; }
.continuar-oscuro { opacity: 0; }

.pantalla-metodos.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}
.tema-oscuro .celda-metodo.seleccionada :deep(.tarjeta-servicio) {
  background: #174b65;
  border-color: #00b9c7;
}
.tema-oscuro .boton-continuar:not(:disabled) {
  background: #123d5b;
  color: #ffffff;
  border-color: #5ec6e8;
  box-shadow: 0 4px 0 #087d93, 0 7px 16px rgba(0,0,0,.22);
}
.tema-oscuro .boton-continuar:disabled {
  background: #254054;
  color: #c0d0da;
  border-color: #426984;
  box-shadow: none;
}
.tema-oscuro .continuar-claro { opacity: 0; }
.tema-oscuro .continuar-oscuro { opacity: 1; }

.celda-metodo:focus-visible,
.boton-continuar:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 4px;
}
@media (orientation: portrait) {
  .contenido-metodos { width: min(100%, 630px); height: min(100%, 530px); }
  .cuadricula-metodos {
    height: min(calc(100% - var(--espacio-boton)), 450px);
  }
}

/* En tablets verticales, el titulo comparte la cota del menu.
   La altura adicional participa en el grid, sin superponer tarjetas. */
@media (orientation: portrait) and (min-width: 400px) and (min-height: 680px) {
  .contenido-metodos > h1 {
    margin-top: calc(46px - clamp(12px, 2vh, 22px));
  }
}
@media (max-height: 620px) {
  .pantalla-metodos { padding: 8px 14px; }
  .contenido-metodos {
    gap: 8px;
    --espacio-boton: 65px;
  }
  .contenido-metodos > h1 { font-size: clamp(19px, 2.5vw, 27px); }
  .cuadricula-metodos { gap: 8px; }
  .accion-metodos { height: 54px; }
  .boton-continuar { min-height: 46px; padding: 5px 14px; }
  .icono-continuar { width: 30px; height: 30px; flex-basis: 30px; }
}
@media (max-height: 450px) {
  .pantalla-metodos { min-height: 300px; }
  .contenido-metodos { min-height: 250px; }
  .cuadricula-metodos { min-height: 150px; }
}
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .contenido-metodos {
    width: min(100%, 1110px);
    height: min(100%, 760px);
    gap: clamp(12px, 2vh, 25px);
  }
  .contenido-metodos > h1 { font-size: clamp(29px, 3vw, 41px); }
  .cuadricula-metodos {
    height: calc(100% - var(--espacio-boton));
    gap: clamp(12px, 1.7vw, 24px);
  }
}
@media (min-width: 600px) and (min-height: 700px) and (orientation: portrait) {
  .contenido-metodos {
    width: min(100%, 820px);
    height: min(100%, 1000px);
  }
}
@media (prefers-reduced-motion: reduce) {
  .pantalla-metodos, .boton-continuar { transition: none; }
  .boton-continuar:active:not(:disabled) { transform: none; }
}


</style>