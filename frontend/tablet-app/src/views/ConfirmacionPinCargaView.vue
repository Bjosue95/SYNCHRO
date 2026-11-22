<script setup>
import { computed, onMounted, onBeforeUnmount } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { obtenerPreparacionCarga } from "../services/cargaSesionSeguraApi.js";
import { pasoCargaDemoValido } from "../services/pasoCargaDemostracion.js";

const route = useRoute();
const router = useRouter();
const { oscuro } = useTema();
const propsVistaPrevia = defineProps({
  modoVistaPrevia: { type: Boolean, default: false }
});

const datos = computed(() => ({
  espacio: String(route.query.espacio ?? ""),
  dispositivo: String(route.query.dispositivo ?? ""),
  modalidad: String(
    route.query.modalidad ??
    (route.query.dispositivo === "Laptop" ? "Laptop" : "")
  )
}));

let temporizador = null;

onMounted(() => {
  // Mantener visible la transicion para revisarla en el catalogo
  // sin consumir autorizaciones ni iniciar la colocacion.
  if (import.meta.env.DEV && propsVistaPrevia.modoVistaPrevia) return;

  if (
    !pasoCargaDemoValido(datos.value) ||
    obtenerPreparacionCarga()?.codigoEspacio !== datos.value.espacio
  ) {
    router.replace("/menu");
    return;
  }

  temporizador = window.setTimeout(() => {
    router.replace({
      path: "/cargar/colocar",
      query: datos.value
    });
  }, 2000);
});

onBeforeUnmount(() => {
  if (temporizador !== null) {
    window.clearTimeout(temporizador);
  }
});
</script>

<template>
  <main class="deposito-pantalla" :class="{ 'tema-oscuro': oscuro }">
    <div class="deposito-contenedor">
      <header class="deposito-encabezado">
        <h1>PIN confirmado</h1>
        <div class="deposito-identificador">
          <span class="deposito-punto"></span>
          Espacio {{ datos.espacio }}
        </div>
      </header>

      <section class="deposito-tarjeta">
        <div class="deposito-icono-exito" aria-hidden="true">
          &#10003;
        </div>

        <h2>Tu PIN de 6 d&iacute;gitos coincide</h2>

        <p class="deposito-descripcion">
          Tu PIN y tu registro facial están listos
          para proteger el dispositivo.
        </p>

        <p class="deposito-retorno" role="status">
          Preparando el espacio para colocar tu dispositivo...
        </p>
      </section>
    </div>
  </main>
</template>
<style scoped>
/* SYNCHRO-DIGITAL-CIRCULAR-V3: reloj digital proporcional */

/* SYNCHRO-DEPOSITO-ESTILOS-LIMPIOS-V7
   Diseno unificado con GuardarView.vue.
   Sin reglas heredadas de versiones anteriores.
*/

/* ==============================
   1. SUPERFICIE
   ============================== */

.deposito-pantalla,
.deposito-pantalla * {
  box-sizing: border-box;
}

.deposito-pantalla {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);

  gap: clamp(8px, 1.4vh, 16px);

  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;

  padding:
    clamp(10px, 1.7vh, 20px)
    clamp(12px, 2.4vw, 28px);

  overflow-x: hidden;
  overflow-y: auto;

  background: #f2f7fa;
  color: #01305c;
}

/* Reserva la misma primera fila que utiliza
   el boton Regresar de GuardarView. */
.deposito-pantalla::before {
  content: "";
  display: block;
  grid-row: 1;
  height: 22px;
  pointer-events: none;
}

/* ==============================
   2. CONTENEDOR PRINCIPAL
   ============================== */

.deposito-contenedor {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);

  grid-row: 2;
  place-self: start center;

  gap: clamp(9px, 1.4vh, 16px);

  width: min(100%, 760px);
  height: min(100%, 630px);

  min-width: 0;
  min-height: 0;

  margin: 0;
  padding: 0;
}

/* ==============================
   3. TITULO
   ============================== */

.deposito-encabezado {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-start;

  gap: 8px;

  width: 100%;
  margin: 0;
  padding: 0;

  align-self: start;
  justify-self: center;

  text-align: center;
}

.deposito-encabezado h1 {
  margin: 0;
  padding: 0;

  color: inherit;
  font-size: clamp(21px, 2.8vw, 32px);
  font-weight: 750;
  line-height: 1.18;
  letter-spacing: -0.025em;
}

.deposito-identificador {
  display: inline-flex;
  align-items: center;
  justify-content: center;

  gap: 9px;
  min-height: 32px;
  padding: 5px 14px;

  border: 1px solid #b7e6e9;
  border-radius: 24px;

  background: #e5f8fa;
  color: #01305c;

  font-size: 13px;
  font-weight: 700;
}

.deposito-punto {
  display: block;

  width: 8px;
  height: 8px;
  flex-shrink: 0;

  border-radius: 50%;
  background: #00b9c7;
}

/* ==============================
   4. TARJETA
   ============================== */

.deposito-tarjeta {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  gap: clamp(12px, 2vh, 20px);

  width: 100%;
  min-width: 0;
  min-height: 0;

  padding: clamp(16px, 2.5vw, 30px);

  border: 2px solid #d8e5eb;
  border-radius: 23px;

  background: #ffffff;
  color: #01305c;

  box-shadow: 0 8px 25px rgba(1, 48, 92, .06);

  text-align: center;
}

.deposito-tarjeta h2 {
  margin: 0;

  color: #01305c;
  font-size: clamp(25px, 3.4vw, 34px);
  font-weight: 800;
  line-height: 1.25;
  letter-spacing: -0.02em;
  text-align: center;
}

.deposito-descripcion {
  max-width: 520px;
  margin: 0;

  color: #526b7c;
  font-size: clamp(17px, 2.6vw, 22px);
  font-weight: 500;
  line-height: 1.5;
  text-align: center;
}

.deposito-instruccion {
  display: flex;
  flex-direction: column;
  align-items: center;

  gap: 8px;
  width: 100%;
}

/* ==============================
   5. DEPOSITO EN CURSO
   ============================== */

.deposito-tarjeta.en-deposito {
  height: 100%;
  justify-content: space-evenly;

  gap: clamp(10px, 1.6dvh, 20px);
}

.deposito-reloj {
  position: relative;

  display: grid;
  place-items: center;

  width: clamp(160px, min(32dvh, 52vw), 310px);

  max-width: 72%;
  aspect-ratio: 1;

  flex-shrink: 1;
  container-type: inline-size;
}

.deposito-circulo {
  width: 100%;
  height: 100%;
  overflow: visible;
}

.deposito-pista {
  stroke: #e1edef;
}

.deposito-avance {
  stroke: url(#synchro-timer-gradient-v3);

  transform: rotate(-90deg);
  transform-origin: 50% 50%;

  transition: stroke-dashoffset .9s cubic-bezier(.22, 1, .36, 1);
}

.deposito-avance.deposito-final {
  stroke: #8da5b3;
}

.deposito-tiempo {
  position: absolute;
  inset: 0;

  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  gap: 4px;
}

.deposito-digitos {
  color: #01305c;

  font-size: clamp(32px, 23cqw, 72px);
  font-weight: 800;
  font-variant-numeric: tabular-nums;

  line-height: 1;
  letter-spacing: -.045em;
}

.deposito-unidad {
  color: #657988;

  font-size: clamp(10px, 7.4cqw, 17px);
  font-weight: 600;
}

/* ==============================
   6. BOTONES
   ============================== */

.deposito-acciones {
  display: flex;
  flex-direction: column;
  align-items: center;

  gap: clamp(9px, 1.3dvh, 14px);

  width: min(100%, 470px);
}

.deposito-boton-principal,
.deposito-boton-secundario {
  width: 100%;
  min-height: clamp(52px, 6.5dvh, 66px);

  padding: 10px 18px;

  border-radius: 14px;

  font: inherit;
  font-size: clamp(16px, 1.7vw, 20px);
  font-weight: 750;

  cursor: pointer;
  touch-action: manipulation;
}

.deposito-boton-principal {
  border: 2px solid #01305c;

  background: #01305c;
  color: #ffffff;
}

.deposito-boton-principal:hover:not(:disabled) {
  background: #084b75;
}

.deposito-boton-secundario {
  border: 2px solid #00b9c7;

  background: #ffffff;
  color: #01305c;
}

.deposito-boton-secundario:hover:not(:disabled) {
  background: #e8f9fa;
}

.deposito-boton-principal:disabled,
.deposito-boton-secundario:disabled {
  opacity: .6;
  cursor: wait;
}

.deposito-boton-principal:focus-visible,
.deposito-boton-secundario:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 4px;
}

.deposito-ayuda {
  max-width: 390px;

  color: #526b7c;
  font-size: clamp(12px, 1.3vw, 15px);
  line-height: 1.4;
}

/* ==============================
   7. VERIFICACION
   ============================== */

.deposito-cargando {
  width: 54px;
  height: 54px;

  border: 5px solid #dcecf0;
  border-top-color: #00b9c7;
  border-radius: 50%;

  animation: deposito-giro 1s linear infinite;
}

/* ==============================
   8. CONFIRMACION Y ERRORES
   ============================== */

.deposito-tarjeta:not(.en-deposito) {
  height: 100%;
  min-height: 0;

  align-self: stretch;
  justify-content: center;

  gap: clamp(15px, 2.4dvh, 25px);
}

.deposito-icono-exito,
.deposito-icono-error {
  display: grid;
  place-items: center;

  width: clamp(78px, 10vw, 100px);
  height: clamp(78px, 10vw, 100px);

  flex-shrink: 0;

  border-radius: 50%;

  font-size: clamp(44px, 5vw, 58px);
  font-weight: 800;
}

.deposito-icono-exito {
  width: clamp(104px, 15vw, 136px);
  height: clamp(104px, 15vw, 136px);
  flex-shrink: 0;

  display: grid;
  place-items: center;

  border: 4px solid rgba(94, 198, 232, .35);
  border-radius: 50%;

  background: linear-gradient(145deg, #00b9c7, #008c9b);
  color: #ffffff;

  box-shadow:
    0 10px 26px rgba(0, 185, 199, .22),
    0 0 0 9px rgba(0, 185, 199, .09);
}

.deposito-icono-error {
  background: #fff1dc;
  color: #a86812;
}

.deposito-retorno {
  margin: 0;

  color: #526b7c;
  font-size: clamp(16px, 2.4vw, 20px);
  font-weight: 650;
  line-height: 1.4;
  text-align: center;
}

.deposito-mensaje-error {
  width: min(100%, 490px);

  margin: 0;
  padding: 12px 14px;

  border-radius: 10px;

  background: #fff0ed;
  color: #882f26;

  font-size: 13px;
  line-height: 1.5;
  text-align: center;
}

/* ==============================
   9. MODO OSCURO
   ============================== */

.deposito-pantalla.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .deposito-tarjeta {
  background: #103956;
  border-color: #355c73;
  color: #ffffff;

  box-shadow: 0 8px 25px rgba(0, 0, 0, .13);
}

.tema-oscuro .deposito-tarjeta h2,
.tema-oscuro .deposito-digitos {
  color: #ffffff;
}

.tema-oscuro .deposito-descripcion,
.tema-oscuro .deposito-unidad,
.tema-oscuro .deposito-ayuda,
.tema-oscuro .deposito-retorno {
  color: #d4e1e8;
}

.tema-oscuro .deposito-identificador {
  background: #194764;
  color: #ffffff;
  border-color: #5ec6e8;
}

.tema-oscuro .deposito-pista {
  stroke: #315772;
}

.tema-oscuro .deposito-boton-principal {
  background: #00b9c7;
  border-color: #00b9c7;
  color: #01305c;
}

.tema-oscuro .deposito-boton-principal:hover:not(:disabled) {
  background: #5ec6e8;
}

.tema-oscuro .deposito-boton-secundario {
  background: #123d5b;
  border-color: #00b9c7;
  color: #ffffff;
}

.tema-oscuro .deposito-boton-secundario:hover:not(:disabled) {
  background: #194b68;
}

.tema-oscuro .deposito-icono-exito {
  background: linear-gradient(145deg, #00b9c7, #008c9b);
  color: #ffffff;
  border-color: rgba(94, 198, 232, .4);
}

.tema-oscuro .deposito-icono-error {
  background: #523b24;
  color: #ffdca3;
}

.tema-oscuro .deposito-mensaje-error {
  background: #532f34;
  color: #ffe6de;
}

/* ==============================
   10. DISENO RESPONSIVO
   ============================== */

/* Tablet: mismas dimensiones de GuardarView. */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {

  .deposito-pantalla::before {
    height: 10px;
  }

  .deposito-contenedor {
    width: min(100%, 1060px);
    height: min(100%, 760px);

    gap: clamp(12px, 1.8vh, 22px);
  }

  .deposito-encabezado h1 {
    font-size: clamp(30px, 3vw, 40px);
  }
}

/* Tablet vertical. */
@media (min-width: 600px)
and (min-height: 700px)
and (orientation: portrait) {

  .deposito-contenedor {
    width: min(100%, 820px);
    height: min(100%, 1040px);
  }
}

/* Tablet y telefono vertical. */
@media (orientation: portrait) {

  .deposito-contenedor {
    max-width: 820px;
  }
}

/* Telefono vertical:
   coincide con la geometria de GuardarView. */
@media (orientation: portrait)
and (max-width: 599px) {

  .deposito-contenedor {
    width: min(100%, 620px);
    height: min(100%, 560px);
  }
}

/* Pantallas de poca altura:
   permitir desplazamiento sin cortar botones. */
@media (max-height: 650px) {

  .deposito-pantalla {
    display: block;
    overflow-y: auto;

    padding: 12px;
  }

  .deposito-pantalla::before {
    display: none;
  }

  .deposito-contenedor {
    display: flex;
    flex-direction: column;
    align-items: center;

    width: min(100%, 740px);
    height: auto;
    min-height: 0;

    margin: 0 auto;

    gap: 12px;
  }

  .deposito-encabezado {
    flex: 0 0 auto;
  }

  .deposito-tarjeta,
  .deposito-tarjeta.en-deposito {
    width: 100%;
    height: auto;
    min-height: 350px;

    gap: 10px;
  }

  .deposito-tarjeta:not(.en-deposito) {
    height: auto;
    min-height: 350px;
  }

  .deposito-reloj {
    width: clamp(145px, 30dvh, 220px);
  }

  .deposito-acciones {
    width: min(100%, 470px);
  }

  .deposito-boton-principal,
  .deposito-boton-secundario {
    min-height: 48px;
  }
}

/* Evitar que el teclado o el navegador reduzcan
   demasiado el contenido en pantallas estrechas. */
@media (max-width: 420px) {

  .deposito-tarjeta {
    padding: 15px;
  }

  .deposito-digitos {
    font-size: clamp(26px, 24cqw, 60px);
  }

  .deposito-identificador {
    font-size: 12px;
  }
}

/* ==============================
   11. ACCESIBILIDAD
   ============================== */

@keyframes deposito-giro {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {

  .deposito-cargando {
    animation: none;
  }

  .deposito-avance {
    transition: none;
  }
}

/* SYNCHRO: elementos exclusivos del contador responsivo */

.deposito-halo {
  stroke: #5ec6e8;
  opacity: .3;
}

.deposito-referencia {
  color: #008c99;
  font-size: clamp(9px, 7.3cqw, 14px);
  font-weight: 700;
  line-height: 1.2;
  white-space: nowrap;
}

.deposito-aviso-minuto {
  position: absolute;
  z-index: 3;
  top: -7%;
  right: -15%;

  padding: 7px 11px;
  border-radius: 999px;

  background: #00b9c7;
  color: #01305c;

  font-size: 14px;
  font-weight: 800;
  box-shadow: 0 5px 15px rgba(0, 185, 199, .24);
}

.tema-oscuro .deposito-referencia {
  color: #92e9ed;
}

.deposito-digitos {
  animation: deposito-numero-entrada .35s ease-out;
}

@keyframes deposito-numero-entrada {
  from {
    opacity: .6;
    transform: scale(.9);
  }
  to {
    opacity: 1;
    transform: scale(1);
  }
}

.deposito-minuto-enter-active,
.deposito-minuto-leave-active {
  transition: opacity .3s ease, transform .35s ease;
}

.deposito-minuto-enter-from,
.deposito-minuto-leave-to {
  opacity: 0;
  transform: translateY(10px) scale(.85);
}

@media (prefers-reduced-motion: reduce) {
  .deposito-digitos {
    animation: none;
  }

  .deposito-minuto-enter-active,
  .deposito-minuto-leave-active {
    transition: none;
  }
}

</style>