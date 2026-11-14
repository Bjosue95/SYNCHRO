<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";

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

const permitidos = {
  mochila: ["M", "L"],
  maleta: ["M", "L"],
  compras: ["S", "M", "L"],
  caja: ["S", "M", "L"],
};

const nombres = {
  S: "Pequeño",
  M: "Mediano",
  L: "Grande",
};

const objeto = computed(() =>
  typeof route.query.objeto === "string"
    ? route.query.objeto
    : ""
);

const tamano = computed(() =>
  typeof route.query.tamano === "string"
    ? route.query.tamano
    : ""
);

const valido = computed(() =>
  !!objetos[objeto.value] &&
  (permitidos[objeto.value] || []).includes(tamano.value)
);

const regresar = computed(() => ({
  path: "/guardar/tamano",
  query: { objeto: objeto.value },
}));

const casilleros = ref([]);
const cargando = ref(true);
const error = ref("");
const seleccionado = ref("");
const pagina = ref(0);

const porPagina = 8;

// Mostrar exclusivamente casilleros reales disponibles.
const disponibles = computed(() =>
  casilleros.value
    .filter((item) =>
      item.tipo === "Normal" &&
      item.estado === "Disponible" &&
      item.tamano === nombres[tamano.value] &&
      typeof item.codigo === "string" &&
      item.codigo.trim().length > 0
    )
    .sort((a, b) =>
      a.codigo.localeCompare(b.codigo, "es", {
        numeric: true,
      })
    )
);

const totalPaginas = computed(() =>
  Math.max(1, Math.ceil(disponibles.value.length / porPagina))
);

const visibles = computed(() =>
  disponibles.value.slice(
    pagina.value * porPagina,
    (pagina.value + 1) * porPagina
  )
);

async function consultar() {
  cargando.value = true;
  error.value = "";

  try {
    casilleros.value = await obtenerCasilleros();

    if (
      !disponibles.value.some(
        (item) => item.codigo === seleccionado.value
      )
    ) {
      seleccionado.value = "";
    }

    if (pagina.value >= totalPaginas.value) {
      pagina.value = 0;
    }
  } catch (e) {
    console.error("Error consultando casilleros:", e);

    casilleros.value = [];
    seleccionado.value = "";
    error.value =
      "No se pudo consultar la disponibilidad.";
  } finally {
    cargando.value = false;
  }
}

function elegir(codigo) {
  if (cargando.value || error.value) return;

  const existe = disponibles.value.some(
    (item) => item.codigo === codigo
  );

  if (existe) {
    seleccionado.value = codigo;
  }
}

function cambiarPagina(valor) {
  if (valor < 0 || valor >= totalPaginas.value) return;

  pagina.value = valor;
  seleccionado.value = "";
}

onMounted(() => {
  if (!valido.value) {
    router.replace("/guardar");
    return;
  }

  consultar();
});
</script>

<template>
  <main
    class="pantalla-casilleros"
    :class="{ 'tema-oscuro': oscuro }"
  >
    <!-- Encabezado compartido desde App.vue -->

    <RouterLink :to="regresar" class="volver">
      <span class="icono-volver" aria-hidden="true">
        <img
          src="/iconos/complementarios/28_regresar.svg"
          class="claro"
          alt=""
        />
        <img
          src="/iconos/complementarios/28_regresar-oscuro.svg"
          class="oscuro"
          alt=""
        />
      </span>

      <span>Volver a tamaños</span>
    </RouterLink>

    <div class="contenido-casilleros">
    <header class="titulo-paso">
      <h1>Elige tu casillero</h1>

      <p>
        {{ objetos[objeto] }} · {{ nombres[tamano] }}
      </p>

      <p
        v-if="!cargando && !error"
        class="cantidad"
        role="status"
      >
        {{ disponibles.length }}
        casillero{{ disponibles.length === 1 ? "" : "s" }}
        disponible{{ disponibles.length === 1 ? "" : "s" }}
      </p>
    </header>

    <section
      class="zona-casilleros"
      aria-label="Casilleros disponibles"
    >
      <div v-if="cargando" class="mensaje" role="status">
        Consultando casilleros...
      </div>

      <div v-else-if="error" class="mensaje" role="alert">
        <p>{{ error }}</p>

        <button
          type="button"
          class="boton-secundario"
          @click="consultar"
        >
          Reintentar
        </button>
      </div>

      <div
        v-else-if="disponibles.length === 0"
        class="mensaje"
        role="status"
      >
        No hay casilleros disponibles de este tamaño.
      </div>

      <div v-else class="cuadricula-casilleros"
        :class="['columnas-' + Math.min(4, visibles.length), { 'una-fila': visibles.length <= 4 }]"
      >
        <button
          v-for="item in visibles"
          :key="item.codigo"
          type="button"
          class="casillero"
          :class="{
            seleccionado: seleccionado === item.codigo
          }"
          :aria-pressed="seleccionado === item.codigo"
          @click="elegir(item.codigo)"
        >
          <span class="dibujo" aria-hidden="true">
            <img
              src="/iconos/complementarios/15_ubicacion_casillero.svg"
              class="claro"
              alt=""
            />

            <img
              src="/iconos/complementarios/15_ubicacion_casillero-oscuro.svg"
              class="oscuro"
              alt=""
            />
          </span>

          <strong class="codigo">
            {{ item.codigo }}
          </strong>

          <span class="estado">
            {{
              seleccionado === item.codigo
                ? "Seleccionado"
                : "Disponible"
            }}
          </span>

          <span
            v-if="seleccionado === item.codigo"
            class="marca"
            aria-hidden="true"
          >
            ✓
          </span>
        </button>
      </div>
    </section>

    <footer class="pie">
      <p aria-live="polite">
        {{
          seleccionado
            ? `Casillero ${seleccionado} elegido. Aún no está reservado.`
            : "Toca un casillero disponible para seleccionarlo."
        }}
      </p>

      <nav
        v-if="totalPaginas > 1 && !error && !cargando"
        class="paginas"
        aria-label="Páginas de casilleros"
      >
        <button
          type="button"
          :disabled="pagina === 0"
          @click="cambiarPagina(pagina - 1)"
        >
          Anterior
        </button>

        <span>{{ pagina + 1 }} / {{ totalPaginas }}</span>

        <button
          type="button"
          :disabled="pagina + 1 >= totalPaginas"
          @click="cambiarPagina(pagina + 1)"
        >
          Siguiente
        </button>
      </nav>
    </footer>
    </div>
  </main>
</template>

<style scoped>
/* SYNCHRO-CASILLEROS-DISENO-UNIFICADO-V1 */

/* Misma estructura que la pantalla de tamaños */

.pantalla-casilleros {
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

  background: #f2f7fa;
  color: #01305c;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

/* Volver: misma apariencia que antes */

.volver {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  justify-self: start;
  gap: 10px;

  width: max-content;
  max-width: 100%;
  min-height: 52px;
  padding: 8px 18px;

  border: 2px solid #86bdcb;
  border-radius: 14px;

  background: #e4f2f6;
  color: inherit;

  font: inherit;
  font-weight: 700;
  text-decoration: none;

  box-shadow: 0 4px 0 rgba(1, 48, 92, .14);

  transition:
    background-color 150ms ease,
    transform 150ms ease,
    box-shadow 150ms ease;
}

.volver:active {
  transform: translateY(3px);
  box-shadow: 0 1px 0 rgba(1, 48, 92, .14);
}

/* Iconos originales */

.icono-volver,
.dibujo {
  display: block;
  position: relative;
  flex-shrink: 0;
}

.icono-volver {
  width: 28px;
  height: 28px;
}

.icono-volver img,
.dibujo img {
  position: absolute;
  inset: 0;

  width: 100%;
  height: 100%;
  object-fit: contain;

  transition: opacity 150ms ease;
}

.claro { opacity: 1; }
.oscuro { opacity: 0; }

/* Contenedor principal centrado */

.contenido-casilleros {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr) auto;
  gap: clamp(12px, 1.8vh, 22px);

  width: min(100%, 1120px);
  height: min(100%, 760px);

  min-width: 0;
  min-height: 0;

  place-self: center;
}

/* Título */

.titulo-paso {
  min-width: 0;
  text-align: center;
}

.titulo-paso h1 {
  margin: 0;

  font-size: clamp(26px, 3vw, 40px);
  font-weight: 700;
  line-height: 1.2;
}

.titulo-paso p {
  margin: 6px 0 0;
  font-size: clamp(15px, 1.6vw, 20px);
  line-height: 1.25;
}

.titulo-paso .cantidad {
  color: #008c99;
  font-weight: 700;
}

/* Zona de casilleros */

.zona-casilleros {
  display: grid;
  place-items: center;

  width: 100%;
  min-width: 0;
  min-height: 0;
}

/* Grid de cuatro columnas para tablet horizontal */

.cuadricula-casilleros {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  grid-auto-rows: minmax(0, 1fr);

  gap: clamp(10px, 1.6vw, 20px);

  width: min(100%, 1060px);
  height: min(100%, 520px);

  min-width: 0;
  min-height: 0;

  place-self: center;
}

/* Evitar tarjetas exageradamente grandes
   cuando hay pocos casilleros */

.cuadricula-casilleros.una-fila {
  height: min(100%, 260px);
}

.cuadricula-casilleros.columnas-1 {
  grid-template-columns: minmax(0, 1fr);
  width: min(100%, 260px);
}

.cuadricula-casilleros.columnas-2 {
  grid-template-columns: repeat(2, minmax(0, 1fr));
  width: min(100%, 540px);
}

.cuadricula-casilleros.columnas-3 {
  grid-template-columns: repeat(3, minmax(0, 1fr));
  width: min(100%, 800px);
}

/* Tarjetas con la misma identidad visual
   que las tarjetas de tamaño */

.casillero {
  position: relative;

  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  gap: clamp(5px, 1vh, 11px);

  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;

  padding: clamp(8px, 1.7vh, 18px);

  border: 2px solid #dfeaf0;
  border-radius: 22px;

  background: #ffffff;
  color: #01305c;

  font: inherit;
  text-align: center;
  cursor: pointer;
  touch-action: manipulation;

  box-shadow: 0 3px 12px rgba(1, 48, 92, .05);

  transition:
    background-color 150ms ease,
    border-color 150ms ease,
    box-shadow 150ms ease,
    transform 150ms ease;
}

/* Selección */

.casillero:active {
  transform: scale(.985);
}

.casillero.seleccionado {
  background: #e8f9fa;
  border-color: #00b9c7;
  box-shadow: 0 0 0 3px rgba(0, 185, 199, .14);
}

/* Ilustración y textos */

.dibujo {
  width: clamp(48px, 9vh, 94px);
  height: clamp(48px, 9vh, 94px);
}

.codigo {
  font-size: clamp(19px, 2.3vw, 28px);
  font-weight: 800;
}

.estado {
  color: #008c99;
  font-size: clamp(13px, 1.5vw, 17px);
  font-weight: 700;
}

.marca {
  position: absolute;
  top: 9px;
  right: 10px;

  display: grid;
  place-items: center;

  width: 28px;
  height: 28px;

  border-radius: 50%;
  background: #00b9c7;
  color: #01305c;
  font-weight: 800;
}

/* Estado inferior */

.pie {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 12px;

  min-height: 48px;
  text-align: center;
}

.pie p {
  margin: 0;
  font-size: clamp(14px, 1.5vw, 17px);
  line-height: 1.3;
}

.paginas {
  display: inline-flex;
  align-items: center;
  gap: 10px;
}

.paginas button,
.boton-secundario {
  min-height: 46px;
  padding: 8px 18px;

  border: 2px solid #00b9c7;
  border-radius: 13px;

  background: #f4feff;
  color: #01305c;

  font: inherit;
  font-weight: 700;
  cursor: pointer;
}

.paginas button:disabled {
  opacity: .45;
  cursor: not-allowed;
}

.mensaje {
  text-align: center;
  font-size: clamp(16px, 1.7vw, 20px);
}

/* Modo oscuro */

.pantalla-casilleros.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .volver {
  background: #204766;
  border-color: #5ec6e8;
}

.tema-oscuro .claro {
  opacity: 0;
}

.tema-oscuro .oscuro {
  opacity: 1;
}

.tema-oscuro .casillero {
  background: #103956;
  border-color: #315573;
  color: #ffffff;
}

.tema-oscuro .casillero.seleccionado {
  background: #174b65;
  border-color: #00b9c7;
}

.tema-oscuro .estado,
.tema-oscuro .cantidad {
  color: #5ec6e8;
}

.tema-oscuro .paginas button,
.tema-oscuro .boton-secundario {
  background: #123d5b;
  color: #ffffff;
  border-color: #5ec6e8;
}

/* Tablet vertical */

@media (orientation: portrait) {
  .contenido-casilleros {
    width: min(100%, 820px);
    height: min(100%, 1040px);
  }

  .cuadricula-casilleros {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    width: min(100%, 720px);
    height: min(100%, 780px);
  }

  .cuadricula-casilleros.una-fila {
    height: min(100%, 460px);
  }

  .cuadricula-casilleros.columnas-1 {
    grid-template-columns: minmax(0, 1fr);
    width: min(100%, 330px);
    height: min(100%, 260px);
  }

  .cuadricula-casilleros.columnas-2 {
    width: min(100%, 720px);
    height: min(100%, 260px);
  }
}

/* Pantallas compactas */

@media (max-height: 650px) {
  .pantalla-casilleros {
    gap: 6px;
    padding: 7px 12px;
  }

  .contenido-casilleros {
    gap: 6px;
  }

  .volver {
    min-height: 42px;
    padding: 5px 12px;
  }

  .titulo-paso h1 {
    font-size: 23px;
  }

  .titulo-paso p {
    margin-top: 2px;
    font-size: 13px;
  }

  .cuadricula-casilleros {
    gap: 8px;
  }

  .casillero {
    gap: 3px;
    padding: 6px;
    border-radius: 15px;
  }

  .dibujo {
    width: 45px;
    height: 45px;
  }

  .codigo {
    font-size: 18px;
  }

  .estado {
    font-size: 12px;
  }

  .pie {
    min-height: 34px;
  }
}

/* Celulares y tablets pequeñas */

@media (max-width: 480px) {
  .pantalla-casilleros {
    padding-inline: 10px;
  }

  .titulo-paso h1 {
    font-size: 23px;
  }

  .casillero {
    padding: 6px;
  }

  .dibujo {
    width: 40px;
    height: 40px;
  }

  .codigo {
    font-size: 18px;
  }

  .estado {
    font-size: 12px;
  }

  .marca {
    width: 23px;
    height: 23px;
  }
}

/* Accesibilidad */

.volver:focus-visible,
.casillero:focus-visible,
.paginas button:focus-visible,
.boton-secundario:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

@media (prefers-reduced-motion: reduce) {
  .pantalla-casilleros,
  .volver,
  .casillero,
  .icono-volver img,
  .dibujo img {
    transition: none;
  }

  .volver:active,
  .casillero:active {
    transform: none;
  }
}

/* SYNCHRO-CHECK-Y-CONTINUAR-UNIFICADOS-V1 */

/* Mismo check de Guardar y Seleccion de tamanos */

.casillero {
  position: relative;
}

/* Ocultar la marca de texto anterior,
   para no mostrar dos checks. */

.casillero .marca {
  display: none;
}

.casillero.seleccionado::after {
  content: "\2713";

  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 2;

  display: grid;
  place-items: center;

  width: clamp(27px, 3vw, 38px);
  height: clamp(27px, 3vw, 38px);

  border-radius: 50%;

  background: #00b9c7;
  color: #01305c;

  font-size: clamp(18px, 2vw, 23px);
  font-weight: 800;
  line-height: 1;

  box-shadow: 0 2px 8px rgba(1, 48, 92, 0.12);

  pointer-events: none;
}

@media (max-height: 620px) {
  .casillero.seleccionado::after {
    top: 5px;
    right: 5px;
    width: 25px;
    height: 25px;
    font-size: 17px;
  }
}
</style>