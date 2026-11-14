<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";

import TamanoCard from "../components/TamanoCard.vue";
import { useTema } from "../composables/useTema";
import { obtenerCasilleros } from "../services/casillerosApi";

const route = useRoute();
const router = useRouter();
const { oscuro } = useTema();

const objetos = {
  mochila: "Mochila o bolso",
  maleta: "Maleta o equipaje",
  compras: "Bolsas de compras",
  caja: "Objetos personales y cajas",
};

// Tamaños aprobados para cada categoría.
const permitidos = {
  mochila: ["M", "L"],
  maleta: ["M", "L"],
  compras: ["S", "M", "L"],
  caja: ["S", "M", "L"],
};

const objeto = computed(() =>
  typeof route.query.objeto === "string"
    ? route.query.objeto
    : ""
);

const nombreObjeto = computed(() =>
  objetos[objeto.value] || ""
);

const tamanos = [
  {
    codigo: "S",
    titulo: "Pequeño",
    descripcion: "Para objetos pequeños",
  },
  {
    codigo: "M",
    titulo: "Mediano",
    descripcion: "Para pertenencias medianas",
  },
  {
    codigo: "L",
    titulo: "Grande",
    descripcion: "Para objetos que necesitan más espacio",
  },
];

const tamanosVisibles = computed(() => {
  const codigos = permitidos[objeto.value] || [];

  return tamanos.filter((item) =>
    codigos.includes(item.codigo)
  );
});

// Estado de consulta real.
const casilleros = ref([]);
const cargando = ref(true);
const error = ref("");
const seleccionado = ref("");

const equivalencias = {
  "Pequeño": "S",
  "Mediano": "M",
  "Grande": "L",
};

// Solo cuentan casilleros normales disponibles.
const disponibles = computed(() => {
  const resultado = { S: 0, M: 0, L: 0 };

  for (const casillero of casilleros.value) {
    if (
      casillero.tipo !== "Normal" ||
      casillero.estado !== "Disponible"
    ) {
      continue;
    }

    const codigo = equivalencias[casillero.tamano];

    if (codigo) {
      resultado[codigo]++;
    }
  }

  return resultado;
});

const tamanoElegido = computed(() =>
  tamanos.find((item) => item.codigo === seleccionado.value)
);

async function cargarDisponibilidad() {
  cargando.value = true;
  error.value = "";

  try {
    const resultado = await obtenerCasilleros();

    casilleros.value = resultado;

    if (
      seleccionado.value &&
      disponibles.value[seleccionado.value] === 0
    ) {
      seleccionado.value = "";
    }
  } catch (e) {
    console.error("Error al consultar casilleros:", e);

    casilleros.value = [];
    seleccionado.value = "";

    error.value =
      "No se pudo consultar la disponibilidad. Intenta nuevamente.";
  } finally {
    cargando.value = false;
  }
}

function seleccionarTamano(codigo) {
  if (cargando.value || error.value) return;

  const permitido = tamanosVisibles.value.some(
    (item) => item.codigo === codigo
  );

  if (permitido && disponibles.value[codigo] > 0) {
    seleccionado.value = codigo;
  }
}


const puedeContinuar = computed(() =>
  !cargando.value &&
  !error.value &&
  !!seleccionado.value &&
  disponibles.value[seleccionado.value] > 0
);

function continuarACasillero() {
  if (!puedeContinuar.value) return;

  router.push({
    path: "/guardar/casillero",
    query: {
      objeto: objeto.value,
      tamano: seleccionado.value,
    },
  });
}
onMounted(() => {
  if (!nombreObjeto.value) {
    router.replace("/guardar");
    return;
  }

  cargarDisponibilidad();
});
</script>

<template>
  <main
    class="pantalla-tamanos"
    :class="{ 'tema-oscuro': oscuro }"
  >
    <!-- El encabezado permanente ya existe en App.vue -->

    <RouterLink to="/guardar" class="regresar">
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

      <span>Volver</span>
    </RouterLink>

    <div class="contenido-tamanos">
      <div class="introduccion">
        <h1>Selecciona el tamaño del casillero</h1>
        <p>Para: <strong>{{ nombreObjeto }}</strong></p>
      </div>

      <div class="mensaje" role="status" aria-live="polite">
        <span v-if="cargando">
          Consultando disponibilidad...
        </span>
        <span v-else-if="error" class="error">
          {{ error }}
        </span>
        <span v-else>
          Elige el espacio adecuado para tus pertenencias.
        </span>
      </div>

      <section
        class="lista-tamanos"
        :class="{ 'dos-opciones': tamanosVisibles.length === 2 }"
        aria-label="Tamaños de casillero"
      >
        <TamanoCard
          v-for="item in tamanosVisibles"
          :key="item.codigo"
          :tamano="item.codigo"
          :titulo="item.titulo"
          :descripcion="item.descripcion"
          :oscuro="oscuro"
          :seleccionado="seleccionado === item.codigo"
          :disponibles="
            cargando || error
              ? null
              : disponibles[item.codigo]
          "
          @seleccionar="seleccionarTamano"
        />
      </section>

      <div class="acciones">
        <button
          v-if="error"
          type="button"
          class="reintentar"
          @click="cargarDisponibilidad"
        >
          Reintentar
        </button>

        <p v-else-if="seleccionado" aria-live="polite">
          Seleccionaste el tamaño
          {{ tamanoElegido?.titulo.toLowerCase() }}.
        </p>

        <p v-else-if="!cargando">
          Selecciona un tamaño disponible.
        </p>
      
      <button
        type="button"
        class="boton-ir-casilleros"
        :disabled="!puedeContinuar"
        @click="continuarACasillero"
      >
        <span>Continuar</span>

        <span class="icono-ir" aria-hidden="true">
          <img
            src="/iconos/complementarios/29_continuar.svg"
            class="ir-claro"
            alt=""
          />

          <img
            src="/iconos/complementarios/29_continuar-oscuro.svg"
            class="ir-oscuro"
            alt=""
          />
        </span>
      </button>
</div>
    </div>
  </main>
</template>

<style scoped>
/* SYNCHRO | TAMAÑOS. Todas las regiones tienen una fila asignada. */
.pantalla-tamanos {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);
  gap: clamp(7px, 1.25vh, 14px);
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(9px, 1.6vh, 18px) clamp(12px, 2.4vw, 28px);
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
  min-height: 46px;
  padding: 7px 16px;
  border-radius: 13px;
  background: #e4f2f6;
  color: inherit;
  font-weight: 600;
  text-decoration: none;
  transition: background-color 150ms ease, transform 150ms ease;
}
.regresar:active { transform: scale(.985); }
.icono-regresar { position: relative; display: block; width: 26px; height: 26px; flex: 0 0 26px; }
.icono-regresar img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; transition: opacity 150ms ease; }
.regresar-claro { opacity: 1; }
.regresar-oscuro { opacity: 0; }
.contenido-tamanos {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto auto minmax(0, 1fr) auto;
  gap: clamp(8px, 1.25vh, 15px);
  width: min(100%, 1000px);
  height: min(100%, 475px);
  min-width: 0;
  min-height: 0;
  place-self: center;
}
.introduccion { min-width: 0; text-align: center; }
.introduccion h1 { margin: 0; font-size: clamp(20px, 2.7vw, 30px); line-height: 1.2; }
.introduccion p { margin: 5px 0 0; color: #5b7282; font-size: clamp(13px, 1.5vw, 16px); line-height: 1.25; }
.mensaje { min-height: 18px; min-width: 0; text-align: center; color: #5b7282; font-size: 14px; line-height: 1.3; }
.error { color: #bd3939; }
.lista-tamanos {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  grid-template-rows: minmax(0, 1fr);
  gap: clamp(10px, 1.5vw, 18px);
  width: min(100%, 750px);
  height: min(100%, 275px);
  min-width: 0;
  min-height: 0;
  place-self: center;
  margin: 0 auto;
}
.lista-tamanos.dos-opciones {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  width: min(100%, 500px);
}
.acciones { display: flex; align-items: center; justify-content: center; min-height: 38px; text-align: center; }
.acciones p { margin: 0; color: #5b7282; font-size: 14px; line-height: 1.3; }
.reintentar { min-height: 44px; padding: 8px 20px; border: 0; border-radius: 12px; background: #00b9c7; color: #01305c; font: inherit; font-weight: 700; }
.pantalla-tamanos.tema-oscuro { background: #061f36; color: #ffffff; }
.tema-oscuro .regresar { background: #204766; }
.tema-oscuro .regresar-claro { opacity: 0; }
.tema-oscuro .regresar-oscuro { opacity: 1; }
.tema-oscuro .introduccion p, .tema-oscuro .mensaje, .tema-oscuro .acciones p { color: #c6dfea; }
.regresar:focus-visible, .reintentar:focus-visible { outline: 3px solid #00b9c7; outline-offset: 3px; }
@media (orientation: portrait) {
  .contenido-tamanos { height: min(100%, 730px); }
  .lista-tamanos, .lista-tamanos.dos-opciones {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: repeat(3, minmax(0, 1fr));
    width: min(100%, 470px);
    height: min(100%, 570px);
    gap: clamp(8px, 1.5vh, 14px);
  }
  .lista-tamanos.dos-opciones {
    grid-template-rows: repeat(2, minmax(0, 1fr));
    height: min(100%, 390px);
  }
}
@media (max-height: 620px) {
  .pantalla-tamanos { padding: 7px 12px; gap: 5px; }
  .contenido-tamanos { gap: 5px; height: min(100%, 360px); }
  .regresar { min-height: 40px; padding: 5px 12px; }
  .introduccion h1 { font-size: clamp(18px, 2.4vw, 23px); }
  .introduccion p { margin-top: 2px; }
  .mensaje { font-size: 12px; }
  .lista-tamanos { height: min(100%, 205px); gap: 8px; }
  .acciones { min-height: 30px; }
}
@media (max-height: 450px) {
  .pantalla-tamanos { min-height: 360px; }
  .contenido-tamanos { min-height: 265px; }
  .lista-tamanos { min-height: 178px; }
}
@media (prefers-reduced-motion: reduce) {
  .pantalla-tamanos, .regresar, .icono-regresar img { transition: none; }
  .regresar:active { transform: none; }
}

/* Tablet: opciones más grandes y menos margen vacío */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .contenido-tamanos {
    width: min(100%, 1120px);
    height: min(100%, 700px);
    gap: clamp(12px, 2vh, 22px);
  }
  .introduccion h1 { font-size: clamp(30px, 3.1vw, 40px); }
  .introduccion p { font-size: clamp(17px, 1.7vw, 20px); }
  .mensaje { font-size: clamp(16px, 1.5vw, 18px); }
  .lista-tamanos {
    width: min(100%, 1060px);
    height: 100%;
    gap: clamp(14px, 1.8vw, 24px);
  }
  .lista-tamanos.dos-opciones { width: min(100%, 780px); }
  .acciones { min-height: 42px; }
  .acciones p { font-size: 17px; }
}
@media (min-width: 600px) and (min-height: 700px) and (orientation: portrait) {
  .contenido-tamanos {
    width: min(100%, 800px);
    height: min(100%, 1030px);
  }
  .lista-tamanos,
  .lista-tamanos.dos-opciones {
    width: min(100%, 720px);
    height: 100%;
  }
}
@media (min-width: 768px) and (min-height: 550px) and (max-height: 650px) {
  .contenido-tamanos { height: min(100%, 510px); gap: 6px; }
  .introduccion h1 { font-size: 27px; }
  .introduccion p { font-size: 15px; }
  .mensaje { font-size: 14px; }
  .lista-tamanos { gap: 10px; }
  .acciones { min-height: 30px; }
}


/* Paso 21: boton para elegir casillero */

.acciones {
  gap: 12px;
  flex-wrap: wrap;
}

.boton-ir-casilleros {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;

  min-width: 170px;
  min-height: 52px;
  padding: 7px 18px;

  border: 2px solid #00b9c7;
  border-radius: 14px;

  background: #f4feff;
  color: #01305c;

  font: inherit;
  font-size: clamp(16px, 1.7vw, 20px);
  font-weight: 800;
  cursor: pointer;

  transition:
    background-color 150ms ease,
    transform 150ms ease;
}

.boton-ir-casilleros:active:not(:disabled) {
  transform: translateY(2px) scale(0.99);
}

.boton-ir-casilleros:disabled {
  background: #edf3f5;
  color: #647784;
  border-color: #b5cad3;
  cursor: not-allowed;
}

.icono-ir {
  position: relative;
  display: block;
  flex: 0 0 34px;

  width: 34px;
  height: 34px;
}

.icono-ir img {
  position: absolute;
  inset: 0;

  width: 100%;
  height: 100%;
  object-fit: contain;

  transition: opacity 150ms ease;
}

.ir-claro { opacity: 1; }
.ir-oscuro { opacity: 0; }

.tema-oscuro .boton-ir-casilleros:not(:disabled) {
  background: #123d5b;
  color: #ffffff;
  border-color: #5ec6e8;
}

.tema-oscuro .boton-ir-casilleros:disabled {
  background: #254054;
  color: #c0d0da;
  border-color: #426984;
}

.tema-oscuro .ir-claro { opacity: 0; }
.tema-oscuro .ir-oscuro { opacity: 1; }

.boton-ir-casilleros:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

@media (max-height: 620px) {
  .boton-ir-casilleros {
    min-height: 43px;
    padding: 3px 12px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .boton-ir-casilleros,
  .icono-ir img {
    transition: none;
  }
}

/* SYNCHRO-CHECK-Y-CONTINUAR-UNIFICADOS-V1 */

/* El mensaje no debe desplazar el boton Continuar.
   Se reserva la columna central para el boton. */

#app .synchro-app .pantalla-tamanos .acciones {
  display: grid;

  grid-template-columns:
    minmax(0, 1fr)
    auto
    minmax(0, 1fr);

  align-items: center;
  justify-items: center;

  gap: clamp(8px, 1.3vw, 16px);

  width: 100%;
  min-width: 0;
  min-height: 70px;
  margin: 0;
}

#app .synchro-app .pantalla-tamanos .acciones > p {
  grid-column: 1;
  justify-self: center;

  margin: 0;
  max-width: 100%;

  text-align: center;
  font-size: clamp(13px, 1.4vw, 16px);
  line-height: 1.3;
}

#app .synchro-app .pantalla-tamanos
.acciones > .reintentar {
  grid-column: 1;
}

/* Estilo identico al boton Continuar de Guardar */

#app .synchro-app .pantalla-tamanos
.boton-ir-casilleros {
  grid-column: 2;
  justify-self: center;

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

  box-shadow:
    0 4px 0 #00a5b2,
    0 7px 16px rgba(1, 48, 92, 0.10);

  cursor: pointer;
  touch-action: manipulation;

  opacity: 1;

  transition:
    background-color 150ms ease,
    color 150ms ease,
    border-color 150ms ease,
    box-shadow 150ms ease,
    transform 150ms ease;
}

/* Icono original 29_continuar */

#app .synchro-app .pantalla-tamanos
.boton-ir-casilleros .icono-ir {
  position: relative;
  display: block;

  width: 40px;
  height: 40px;
  flex: 0 0 40px;

  background: transparent;
  border: 0;
  border-radius: 0;
}

#app .synchro-app .pantalla-tamanos
.boton-ir-casilleros .icono-ir img {
  position: absolute;
  inset: 0;

  width: 100%;
  height: 100%;
  object-fit: contain;
}

/* Deshabilitado */

#app .synchro-app .pantalla-tamanos
.boton-ir-casilleros:disabled {
  background: #edf3f5;
  color: #647784;
  border-color: #b5cad3;
  box-shadow: none;

  opacity: 1;
  cursor: not-allowed;
}

/* Efecto al tocar */

#app .synchro-app .pantalla-tamanos
.boton-ir-casilleros:active:not(:disabled) {
  transform: translateY(3px) scale(0.99);
  box-shadow: 0 1px 0 #00a5b2;
}

/* Modo oscuro */

#app .synchro-app
.pantalla-tamanos.tema-oscuro
.boton-ir-casilleros:not(:disabled) {
  background: #123d5b;
  color: #ffffff;
  border-color: #5ec6e8;

  box-shadow:
    0 4px 0 #087d93,
    0 7px 16px rgba(0, 0, 0, 0.22);
}

#app .synchro-app
.pantalla-tamanos.tema-oscuro
.boton-ir-casilleros:disabled {
  background: #254054;
  color: #c0d0da;
  border-color: #426984;
  box-shadow: none;
}

/* El boton permanece centrado en tablets verticales.
   El mensaje pasa arriba para evitar superposiciones. */

@media (max-width: 850px) {
  #app .synchro-app .pantalla-tamanos .acciones {
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: auto auto;
    gap: 8px;
  }

  #app .synchro-app .pantalla-tamanos
  .acciones > p,
  #app .synchro-app .pantalla-tamanos
  .acciones > .reintentar {
    grid-column: 1;
    justify-self: center;
  }

  #app .synchro-app .pantalla-tamanos
  .boton-ir-casilleros {
    grid-column: 1;
    justify-self: center;
  }
}

@media (max-height: 620px) {
  #app .synchro-app .pantalla-tamanos
  .boton-ir-casilleros {
    min-height: 46px;
    padding: 5px 14px;
  }

  #app .synchro-app .pantalla-tamanos
  .boton-ir-casilleros .icono-ir {
    width: 30px;
    height: 30px;
    flex-basis: 30px;
  }
}

@media (prefers-reduced-motion: reduce) {
  #app .synchro-app .pantalla-tamanos
  .boton-ir-casilleros {
    transition: none;
  }
}
</style>
