<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink, useRouter } from "vue-router";

import AppHeader from "../components/AppHeader.vue";
import HelpDialog from "../components/HelpDialog.vue";
import TamanoCard from "../components/TamanoCard.vue";

import { useTema } from "../composables/useTema";
import { obtenerCasilleros } from "../services/casillerosApi";

// Tema y ayuda
const { oscuro, cambiarTema } = useTema();
const router = useRouter();
const centroAyuda = ref(null);

function abrirAyuda() {
  centroAyuda.value?.abrir();
}

// Opciones de tamaño
const tamanos = [
  {
    codigo: "S",
    titulo: "Pequeño",
    descripcion: "Para objetos personales pequeños",
  },
  {
    codigo: "M",
    titulo: "Mediano",
    descripcion: "Para bolsos, compras y paquetes medianos",
  },
  {
    codigo: "L",
    titulo: "Grande",
    descripcion: "Para mochilas y paquetes de mayor tamaño",
  },
];

// Estado de la interfaz
const tamanoSeleccionado = ref("");
const paso = ref("seleccion");

// Estado de la consulta al backend
const casilleros = ref([]);
const cargando = ref(true);
const errorCarga = ref("");

// Contar solamente casilleros normales disponibles
const disponiblesPorTamano = computed(() => {
  const cantidades = {
    S: 0,
    M: 0,
    L: 0,
  };

  const equivalencias = {
    Pequeño: "S",
    Mediano: "M",
    Grande: "L",
  };

  for (const casillero of casilleros.value) {
    if (casillero.tipo !== "Normal" || casillero.estado !== "Disponible") {
      continue;
    }

    const codigo = equivalencias[casillero.tamano];

    if (codigo) {
      cantidades[codigo]++;
    }
  }

  return cantidades;
});

// Consultar la API
async function cargarDisponibilidad() {
  cargando.value = true;
  errorCarga.value = "";

  try {
    casilleros.value = await obtenerCasilleros();

    // Invalidar una selección que dejó de estar disponible
    if (
      tamanoSeleccionado.value &&
      disponiblesPorTamano.value[tamanoSeleccionado.value] === 0
    ) {
      tamanoSeleccionado.value = "";
    }
  } catch (error) {
    console.error("Error al consultar casilleros:", error);

    casilleros.value = [];
    tamanoSeleccionado.value = "";
    errorCarga.value =
      "No fue posible consultar los casilleros. Intenta nuevamente.";
  } finally {
    cargando.value = false;
  }
}

// Consultar al abrir esta pantalla
onMounted(cargarDisponibilidad);

// Tamaño elegido
const seleccion = computed(() =>
  tamanos.find((item) => item.codigo === tamanoSeleccionado.value),
);

// Permitir continuar solo con disponibilidad confirmada
const puedeContinuar = computed(() => {
  return (
    !cargando.value &&
    !errorCarga.value &&
    !!tamanoSeleccionado.value &&
    disponiblesPorTamano.value[tamanoSeleccionado.value] > 0
  );
});

function seleccionarTamano(codigo) {
  if (cargando.value || errorCarga.value) return;

  if (disponiblesPorTamano.value[codigo] > 0) {
    tamanoSeleccionado.value = codigo;
  }
}

function continuar() {
  if (!puedeContinuar.value) return;

  paso.value = "resumen";
}

function cambiarSeleccion() {
  paso.value = "seleccion";
}

function irACasilleros() {
  if (!puedeContinuar.value) return;

  router.push({
    path: "/guardar/pertenencias/casillero",
    query: {
      tamano: tamanoSeleccionado.value,
    },
  });
}
</script>

<template>
  <main class="pantalla-tamanos" :class="{ 'tema-oscuro': oscuro }">
    <!-- Encabezado de SYNCHRO -->
    <AppHeader
      :oscuro="oscuro"
      @cambiar-tema="cambiarTema"
      @abrir-ayuda="abrirAyuda"
    />

    <!-- Navegación -->
    <RouterLink to="/guardar/pertenencias" class="regresar">
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

      <span>Volver a Mis pertenencias</span>
    </RouterLink>

    <section class="contenido">
      <!-- PASO 1: SELECCIONAR TAMAÑO -->
      <template v-if="paso === 'seleccion'">
        <h1>Selecciona el tamaño del casillero</h1>

        <p class="subtitulo">
          Elige el espacio que mejor se adapte a tus pertenencias.
        </p>

        <p class="mensaje-seleccion" role="status">
          {{
            cargando
              ? "Consultando disponibilidad de casilleros..."
              : errorCarga
                ? errorCarga
                : "Disponibilidad consultada correctamente."
          }}
        </p>

        <button
          v-if="errorCarga"
          type="button"
          class="boton-secundario"
          @click="cargarDisponibilidad"
        >
          Reintentar consulta
        </button>

        <div class="lista-tamanos">
          <TamanoCard
            v-for="item in tamanos"
            :key="item.codigo"
            :tamano="item.codigo"
            :titulo="item.titulo"
            :descripcion="item.descripcion"
            :oscuro="oscuro"
            :seleccionado="tamanoSeleccionado === item.codigo"
            @seleccionar="seleccionarTamano"
            :disponibles="
              cargando || errorCarga ? null : disponiblesPorTamano[item.codigo]
            "
          />
        </div>

        <div class="acciones">
          <p class="mensaje-seleccion" aria-live="polite">
            {{
              seleccion
                ? `Seleccionaste un casillero ${seleccion.titulo.toLowerCase()}.`
                : "Selecciona una opción para continuar."
            }}
          </p>

          <button
            type="button"
            class="boton-continuar"
            :disabled="!puedeContinuar"
            @click="continuar"
          >
            Continuar
          </button>
        </div>
      </template>

      <!-- PASO 2: RESUMEN -->
      <template v-else>
        <h1>Tamaño seleccionado</h1>

        <p class="subtitulo">Revisa tu elección antes de continuar.</p>

        <div class="resumen">
          <span class="codigo-tamano">
            {{ seleccion?.codigo }}
          </span>

          <h2>Casillero {{ seleccion?.titulo }}</h2>

          <p>{{ seleccion?.descripcion }}</p>

          <p class="aviso">
            Esta es una selección de demostración. La disponibilidad de
            casilleros todavía no está conectada al sistema.
          </p>

          <RouterLink
            :to="{
              path: '/guardar/pertenencias/casillero',
              query: { tamano: tamanoSeleccionado },
            }"
            custom
            v-slot="{ navigate }"
          >
            <button
              type="button"
              class="boton-continuar"
              :disabled="!puedeContinuar"
              @click="navigate"
            >
              Elegir casillero
            </button>
          </RouterLink>

          <button
            type="button"
            class="boton-secundario"
            @click="cambiarSeleccion"
          >
            Cambiar tamaño
          </button>
        </div>
      </template>
    </section>

    <!-- Centro de ayuda -->
    <HelpDialog ref="centroAyuda" :oscuro="oscuro" />
  </main>
</template>

<style scoped>
/* =========================================
   SYNCHRO | SELECCIÓN DE TAMAÑO
   ========================================= */

.pantalla-tamanos {
  display: flex;
  flex-direction: column;
  gap: 18px;

  width: 100%;
  min-height: 100dvh;
  padding: 20px 26px;

  background: #f2f7fa;
  color: #01305c;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

/* Navegación táctil */

.regresar {
  display: inline-flex;
  align-items: center;
  align-self: flex-start;
  gap: 12px;

  min-height: 56px;
  padding: 10px 20px;

  border-radius: 14px;
  background: #e4f2f6;
  color: inherit;

  text-decoration: none;
  font-size: 16px;
  font-weight: 600;

  transition:
    background-color 150ms ease,
    color 150ms ease;
}

.icono-regresar {
  position: relative;
  display: block;
  width: 30px;
  height: 30px;
  flex-shrink: 0;
}

.icono-regresar img {
  position: absolute;
  inset: 0;

  width: 100%;
  height: 100%;
  object-fit: contain;

  transition: opacity 150ms ease;
}

.regresar-claro {
  opacity: 1;
}

.regresar-oscuro {
  opacity: 0;
}

/* Contenido */

.contenido {
  width: min(100%, 1000px);
  margin: 0 auto;
  text-align: center;
}

h1 {
  margin: 0;
  font-size: clamp(24px, 3vw, 34px);
}

.subtitulo {
  margin: 12px 0 24px;
  color: #5b7282;
  font-size: 16px;

  transition: color 150ms ease;
}

/* Tarjetas */

.lista-tamanos {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 18px;
}

/* Acciones */

.acciones {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;

  margin-top: 26px;
}

.mensaje-seleccion {
  min-height: 24px;
  margin: 0;
  color: #5b7282;
}

.boton-continuar {
  min-width: 220px;
  min-height: 56px;
  padding: 12px 28px;

  border: none;
  border-radius: 14px;

  background: #00b9c7;
  color: #01305c;

  font: inherit;
  font-size: 17px;
  font-weight: 700;
  cursor: pointer;
}

.boton-continuar:disabled {
  background: #dce5ea;
  color: #81939e;
  cursor: not-allowed;
}

/* Resumen */

.resumen {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;

  width: min(100%, 520px);
  margin: 0 auto;
  padding: 30px;

  border: 1px solid #dfeaf0;
  border-radius: 20px;

  background: #ffffff;

  transition:
    background-color 150ms ease,
    border-color 150ms ease;
}

.codigo-tamano {
  display: grid;
  place-items: center;

  width: 72px;
  height: 72px;

  border-radius: 14px;
  background: #e4f2f6;
  color: #01305c;

  font-size: 30px;
  font-weight: 700;
}

.resumen h2,
.resumen p {
  margin: 0;
}

.aviso {
  color: #5b7282;
  font-size: 14px;
  line-height: 1.5;
}

.boton-secundario {
  min-height: 56px;
  padding: 12px 24px;

  border: 2px solid #00b9c7;
  border-radius: 14px;

  background: transparent;
  color: inherit;

  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

button:focus-visible,
.regresar:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

/* =========================================
   MODO OSCURO
   ========================================= */

.tema-oscuro {
  background: #061f36;
  color: #ffffff;
}

.tema-oscuro .regresar {
  background: #204766;
}

.tema-oscuro .regresar-claro {
  opacity: 0;
}

.tema-oscuro .regresar-oscuro {
  opacity: 1;
}

.tema-oscuro .subtitulo,
.tema-oscuro .mensaje-seleccion,
.tema-oscuro .aviso {
  color: #c6dfea;
}

.tema-oscuro .resumen {
  background: #103956;
  border-color: #315573;
}

.tema-oscuro .boton-continuar:disabled {
  background: #315573;
  color: #a7becd;
}

/* Pantallas estrechas */

@media (max-width: 650px) {
  .lista-tamanos {
    grid-template-columns: 1fr;
  }
}

/* Accesibilidad */

@media (prefers-reduced-motion: reduce) {
  .pantalla-tamanos,
  .regresar,
  .icono-regresar img,
  .subtitulo,
  .resumen {
    transition: none;
  }
}
</style>
