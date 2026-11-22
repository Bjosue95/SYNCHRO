<script setup>
import { iniciarRetiroCarga } from "../services/retiroCargaSesion.js";
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { useTema } from "../composables/useTema";

const router = useRouter();
const { oscuro } = useTema();

const paso = ref("dispositivo");
const dispositivo = ref("");
const espacioElegido = ref("");

const inventario = ref([]);
const cargando = ref(false);
const error = ref("");
const aviso = ref("");

const dispositivos = [
  {
    id: "Celular",
    titulo: "Celular",
    icono: "/iconos/04_carga_inalambrica"
  },
  {
    id: "Laptop",
    titulo: "Laptop",
    icono: "/iconos/06_carga_laptop"
  }
];

const catalogo = [
  { codigo: "W1", tipo: "Celular", descripcion: "Inalambrica" },
  { codigo: "W2", tipo: "Celular", descripcion: "Inalambrica" },

  { codigo: "C1", tipo: "Celular", descripcion: "USB-C" },
  { codigo: "C2", tipo: "Celular", descripcion: "Lightning" },
  { codigo: "C3", tipo: "Celular", descripcion: "USB-C" },
  { codigo: "C4", tipo: "Celular", descripcion: "Lightning" },

  { codigo: "L1", tipo: "Laptop", descripcion: "Laptop 1" },
  { codigo: "L2", tipo: "Laptop", descripcion: "Laptop 2" }
];

const espacios = computed(() => {
  return catalogo
    .filter(item => item.tipo === dispositivo.value)
    .map(item => {
      const registrado = inventario.value.find(x =>
        String(x.codigo ?? x.Codigo ?? "") === item.codigo
      );

      const estado = String(
        registrado?.estado ??
        registrado?.Estado ??
        "No registrado"
      );

      return {
        ...item,
        estado,
        puedeRetirar: estado === "Ocupado"
      };
    });
});

function seleccionarDispositivo(codigo) {
  dispositivo.value = codigo;
  espacioElegido.value = "";
  aviso.value = "";
}

function seleccionarEspacio(codigo) {
  const item = espacios.value.find(x => x.codigo === codigo);

  if (!item?.puedeRetirar) return;

  espacioElegido.value = codigo;
  aviso.value = "";
}

async function consultarEspacios() {
  cargando.value = true;
  error.value = "";
  aviso.value = "";

  try {
    const respuesta = await fetch("/api/espacios-carga", {
      credentials: "same-origin",
      cache: "no-store",
      headers: { Accept: "application/json" }
    });

    if (!respuesta.ok) {
      throw new Error(
        "No se pudo consultar el inventario de carga."
      );
    }

    const datos = await respuesta.json();

    if (!Array.isArray(datos)) {
      throw new Error(
        "El backend devolvio un formato inesperado."
      );
    }

    inventario.value = datos;

    if (
      espacioElegido.value &&
      !espacios.value.some(item =>
        item.codigo === espacioElegido.value &&
        item.puedeRetirar
      )
    ) {
      espacioElegido.value = "";
    }
  } catch (e) {
    inventario.value = [];
    espacioElegido.value = "";
    error.value =
      e?.message || "No se pudieron consultar los espacios.";
  } finally {
    cargando.value = false;
  }
}

function continuar() {
  if (paso.value === "espacio" && espacioElegido.value) {
    const item = espacios.value.find(
      x => x.codigo === espacioElegido.value && x.puedeRetirar
    );

    if (!item) {
      aviso.value = "El espacio ya no permite iniciar un retiro.";
      return;
    }

    iniciarRetiroCarga(espacioElegido.value);

    router.push("/retirar/dispositivo/rostro");
    return;
  }
  if (paso.value === "dispositivo") {
    if (!dispositivo.value) return;

    paso.value = "espacio";
    void consultarEspacios();
    return;
  }

  if (paso.value === "espacio" && espacioElegido.value) {
    aviso.value =
      "Espacio seleccionado: " + espacioElegido.value +
      ". El retiro requiere vincular la sesion de carga " +
      "con reconocimiento facial V3 y PIN de seis digitos. " +
      "Todavia no se ha autorizado abrir ni liberar el espacio.";
  }
}

function regresar() {
  if (paso.value === "espacio") {
    paso.value = "dispositivo";
    espacioElegido.value = "";
    error.value = "";
    aviso.value = "";
    return;
  }

  router.push("/retirar");
}
</script>

<template>
  <main class="pantalla-guardar" :class="{ 'tema-oscuro': oscuro }">

    <button class="regresar" type="button" @click="regresar">
      <span class="icono-regresar" aria-hidden="true">
        <img
          src="/iconos/complementarios/28_regresar.svg"
          alt=""
          class="regresar-claro"
        />
        <img
          src="/iconos/complementarios/28_regresar-oscuro.svg"
          alt=""
          class="regresar-oscuro"
        />
      </span>

      <span>
        {{
          paso === "dispositivo"
            ? "Volver a Retirar"
            : "Volver a dispositivo"
        }}
      </span>
    </button>

    <div class="contenido-guardar">

      <h1 v-if="paso === 'dispositivo'" class="titulo">
        &iquest;Qu&eacute; dispositivo deseas retirar?
      </h1>

      <h1 v-else class="titulo">
        &iquest;En qu&eacute; espacio dejaste tu dispositivo?
      </h1>

      <section
        v-if="paso === 'dispositivo'"
        class="cuadricula"
        aria-label="Tipo de dispositivo"
      >
        <button
          v-for="item in dispositivos"
          :key="item.id"
          type="button"
          class="tarjeta"
          :class="{ seleccionada: dispositivo === item.id }"
          :aria-pressed="dispositivo === item.id"
          @click="seleccionarDispositivo(item.id)"
        >
          <span class="contenedor-icono" aria-hidden="true">
            <img
              :src="item.icono + '.svg'"
              alt=""
              class="imagen-icono icono-claro"
            />
            <img
              :src="item.icono + '-oscuro.svg'"
              alt=""
              class="imagen-icono icono-oscuro"
            />
          </span>

          <span class="nombre-tarjeta">
            {{ item.titulo }}
          </span>
        </button>
      </section>

      <section
        v-else
        class="cuadricula cuadricula-espacios"
        aria-label="Espacios de carga"
      >
        <button
          v-for="item in espacios"
          :key="item.codigo"
          type="button"
          class="tarjeta"
          :disabled="!item.puedeRetirar || cargando"
          :class="{ seleccionada: espacioElegido === item.codigo }"
          :aria-pressed="espacioElegido === item.codigo"
          @click="seleccionarEspacio(item.codigo)"
        >
          <span class="nombre-tarjeta">
            {{ item.codigo }}
          </span>

          <span class="descripcion-carga">
            {{ item.descripcion }}
          </span>

          <span class="estado-carga">
            {{ item.estado }}
          </span>
        </button>

        <p v-if="cargando" class="aviso-carga">
          Consultando los espacios...
        </p>

        <p v-if="error" class="aviso-carga" role="alert">
          {{ error }}
        </p>

        <p v-if="aviso" class="aviso-carga" role="status">
          {{ aviso }}
        </p>

        <p
          v-if="
            !cargando &&
            !error &&
            !espacios.some(item => item.puedeRetirar)
          "
          class="aviso-carga"
        >
          No hay espacios registrados como ocupados
          para este dispositivo. Los depositos visuales
          todavia no crean sesiones en el backend.
        </p>
      </section>

      <div class="accion-guardar">
        <button
          class="boton-continuar"
          type="button"
          :disabled="
            paso === 'dispositivo'
              ? !dispositivo
              : !espacioElegido || cargando || !!error
          "
          @click="continuar"
        >
          <span>Continuar</span>

          <span class="icono-continuar" aria-hidden="true">
            <img
              src="/iconos/complementarios/29_continuar.svg"
              alt=""
              class="continuar-claro"
            />
            <img
              src="/iconos/complementarios/29_continuar-oscuro.svg"
              alt=""
              class="continuar-oscuro"
            />
          </span>
        </button>
      </div>

    </div>
  </main>
</template>
<style scoped>
/* SYNCHRO | GUARDAR. Cuatro filas reales: volver, título, tarjetas, continuar. */
.pantalla-guardar {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);
  gap: clamp(8px, 1.4vh, 16px);
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(10px, 1.7vh, 20px) clamp(12px, 2.4vw, 28px);
  background: #f2f7fa;
  color: #01305c;
  transition: background-color 150ms ease, color 150ms ease;
}
.regresar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  justify-self: start;
  width: max-content;
  max-width: 100%;
  min-height: 52px;
  padding: 8px 18px;
  border: 1.5px solid #c7e2e9;
  border-radius: 13px;
  background: #e4f2f6;
  color: inherit;
  font-size: 16px;
  font-weight: 700;
  text-decoration: none;
  transition: background-color 150ms ease, transform 150ms ease;
}
.regresar:active { transform: scale(.985); }
.icono-regresar { position: relative; display: block; width: 26px; height: 26px; flex: 0 0 26px; }
.icono-regresar img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; transition: opacity 150ms ease; }
.regresar-claro, .icono-claro { opacity: 1; }
.regresar-oscuro, .icono-oscuro { opacity: 0; }
.contenido-guardar {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr) auto;
  gap: clamp(9px, 1.4vh, 16px);
  width: min(100%, 760px);
  height: min(100%, 630px);
  min-width: 0;
  min-height: 0;
  place-self: center;
}
.titulo {
  margin: 0;
  font-size: clamp(21px, 2.8vw, 32px);
  line-height: 1.18;
  text-align: center;
}
.cuadricula {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  grid-template-rows: repeat(2, minmax(0, 1fr));
  gap: clamp(9px, 1.5vh, 18px);
  width: 100%;
  height: min(100%, 510px);
  min-height: 0;
  min-width: 0;
  place-self: center;
  margin: 0 auto;
}
.tarjeta {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: clamp(6px, 1.2vh, 13px);
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(8px, 1.8vh, 18px);
  border: 2px solid #dfeaf0;
  border-radius: 18px;
  background: #ffffff;
  color: #01305c;
  box-shadow: 0 3px 12px rgba(1,48,92,.05);
  text-align: center;
  cursor: pointer;
  transition: background-color 150ms ease, border-color 150ms ease, color 150ms ease, box-shadow 150ms ease, transform 150ms ease;
}
.tarjeta:active { transform: scale(.985); }
.tarjeta.seleccionada {
  border-color: #00b9c7;
  background: #e8f9fa;
  box-shadow: 0 0 0 2px rgba(0,185,199,.12);
}
/* La marca no afecta a las dimensiones de la tarjeta. */
.tarjeta.seleccionada::after {
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
  box-shadow: 0 2px 8px rgba(1, 48, 92, .12);
  pointer-events: none;
}
.contenedor-icono {
  position: relative;
  display: block;
  width: clamp(42px, 8vh, 80px);
  aspect-ratio: 1;
  flex: 0 1 auto;
}
.imagen-icono { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; transition: opacity 150ms ease; }
.nombre-tarjeta { font-size: clamp(14px, 1.7vw, 20px); font-weight: 600; line-height: 1.2; overflow-wrap: anywhere; }
.accion-guardar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  justify-self: center;
  width: 100%;
  min-height: 0;
  margin: 0;
}
.boton-continuar {
  min-width: 194px;
  min-height: 54px;
  padding: 9px 24px;
  border: 2px solid #00a8b8;
  border-radius: 14px;
  background: #00b9c7;
  color: #01305c;
  box-shadow: 0 4px 12px rgba(0, 185, 199, .18);
  font: inherit;
  font-size: 17px;
  font-weight: 800;
  cursor: pointer;
  touch-action: manipulation;
  transition: transform 150ms ease, opacity 150ms ease,
              box-shadow 150ms ease, background-color 150ms ease;
}
.boton-continuar:active:not(:disabled) { transform: scale(.985); }
.boton-continuar:disabled {
  opacity: 1;
  background: #d7e7ed;
  border-color: #bdcfd6;
  color: #526e7b;
  box-shadow: none;
  cursor: not-allowed;
}
.solo-lectura {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  padding: 0;
  margin: -1px;
  clip: rect(0,0,0,0);
  white-space: nowrap;
  border: 0;
}
.pantalla-guardar.tema-oscuro { background: #061f36; color: #ffffff; }
.tema-oscuro .regresar { background: #204766; border-color: #42647e; }
.tema-oscuro .regresar-claro, .tema-oscuro .icono-claro { opacity: 0; }
.tema-oscuro .regresar-oscuro, .tema-oscuro .icono-oscuro { opacity: 1; }
.tema-oscuro .tarjeta { background: #103956; border-color: #315573; color: #ffffff; }
.tema-oscuro .tarjeta.seleccionada { background: #174b65; border-color: #00b9c7; }
.tema-oscuro .boton-continuar:disabled { background: #294961; border-color: #42647e; color: #d5e6ef; }
.tarjeta:focus-visible, .regresar:focus-visible, .boton-continuar:focus-visible { outline: 3px solid #00b9c7; outline-offset: 3px; }
@media (orientation: portrait) {
  .contenido-guardar { width: min(100%, 620px); height: min(100%, 560px); }
  .cuadricula { height: min(100%, 470px); }
  .accion-guardar { justify-content: center; }
}
@media (max-height: 620px) {
  .pantalla-guardar { gap: 6px; padding: 7px 12px; }
  .contenido-guardar { gap: 6px; }
  .regresar { min-height: 42px; padding: 5px 12px; }
  .titulo { font-size: 21px; }
  .cuadricula { gap: 8px; }
  .tarjeta { padding: 6px; gap: 4px; }
  .contenedor-icono { width: clamp(32px, 8vh, 52px); }
  .nombre-tarjeta { font-size: 14px; }
  .boton-continuar { min-height: 42px; }
}
@media (max-height: 450px) {
  .pantalla-guardar { min-height: 350px; }
  .contenido-guardar { min-height: 255px; }
  .cuadricula { min-height: 200px; }
}
@media (prefers-reduced-motion: reduce) {
  .pantalla-guardar, .regresar, .tarjeta, .imagen-icono, .boton-continuar { transition: none; }
  .regresar:active, .tarjeta:active { transform: none; }
}

/* Tablet: cuadrícula grande y proporcionada; teléfono intacto */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .contenido-guardar {
    width: min(100%, 1060px);
    height: min(100%, 760px);
    gap: clamp(12px, 1.8vh, 22px);
  }
  .titulo { font-size: clamp(30px, 3vw, 40px); }
  .cuadricula { height: 100%; gap: clamp(14px, 2vh, 24px); }
  .tarjeta {
    padding: clamp(12px, 2.4vh, 28px);
    gap: clamp(9px, 1.6vh, 17px);
    border-radius: 24px;
  }
  .contenedor-icono { width: clamp(78px, 12vh, 124px); }
  .nombre-tarjeta { font-size: clamp(21px, 2.3vw, 27px); }
  .regresar {
    min-height: 56px;
    padding: 9px 20px;
    font-size: clamp(17px, 1.5vw, 19px);
  }
  .boton-continuar {
    min-width: 240px;
    min-height: 62px;
    font-size: 21px;
  }
}
@media (min-width: 600px) and (min-height: 700px) and (orientation: portrait) {
  .contenido-guardar {
    width: min(100%, 820px);
    height: min(100%, 1040px);
  }
}
@media (min-width: 768px) and (min-height: 550px) and (max-height: 650px) {
  .contenido-guardar { gap: 8px; }
  .titulo { font-size: 27px; }
  .cuadricula { gap: 10px; }
  .tarjeta { padding: 9px; gap: 5px; }
  .contenedor-icono { width: clamp(58px, 10vh, 77px); }
  .nombre-tarjeta { font-size: 19px; }
  .regresar { min-height: 48px; font-size: 16px; padding: 6px 16px; }
  .boton-continuar { min-height: 52px; font-size: 18px; }
}


/* SYNCHRO: Retiro de dispositivos */

.cuadricula-espacios {
  overflow-y: auto;
  grid-template-rows: repeat(2, minmax(110px, 1fr));
  grid-auto-rows: minmax(110px, 1fr);
  align-content: start;
  padding: 2px;
}

.descripcion-carga {
  font-size: clamp(13px, 1.5vw, 18px);
  color: #5b6770;
  font-weight: 600;
  text-align: center;
}

.estado-carga {
  font-size: clamp(12px, 1.3vw, 16px);
  font-weight: 700;
  color: #008996;
}

.tarjeta:disabled {
  opacity: .58;
  cursor: not-allowed;
}

.aviso-carga {
  grid-column: 1 / -1;
  margin: 6px;
  color: #526b7c;
  font-size: 14px;
  text-align: center;
}

.tema-oscuro .descripcion-carga,
.tema-oscuro .aviso-carga {
  color: #c7dbe5;
}

.tema-oscuro .estado-carga {
  color: #5ec6e8;
}

</style>
