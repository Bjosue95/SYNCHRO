<script setup>
import { computed, ref, onUnmounted } from "vue";
import {
  RouterView,
  useRoute,
  useRouter,
} from "vue-router";

import AppHeader from "./components/AppHeader.vue";
import HelpDialog from "./components/HelpDialog.vue";
import { useTema } from "./composables/useTema";

const route = useRoute();
const router = useRouter();

const { oscuro, cambiarTema } = useTema();

const centroAyuda = ref(null);
const transicion = ref("synchro-inicio");

const mostrarEncabezado = computed(
  () => route.path !== "/"
);

function abrirAyuda() {
  centroAyuda.value?.abrir();
}

function nivel(path) {
  if (path === "/") return 0;
  if (path === "/menu") return 1;
  return path.split("/").filter(Boolean).length + 1;
}

const quitarGuardia = router.beforeEach((to, from) => {
  if (from.path === "/" || to.path === "/") {
    transicion.value = "synchro-inicio";
    return;
  }

  transicion.value =
    nivel(to.path) < nivel(from.path)
      ? "synchro-retroceder"
      : "synchro-avanzar";
});

onUnmounted(quitarGuardia);
</script>

<template>
  <div
    class="synchro-app"
    :class="{ 'tema-oscuro': oscuro }"
  >
    <!-- ENCABEZADO UNICO Y PERMANENTE -->
    <Transition name="cabecera-inicio">
      <div
        v-if="mostrarEncabezado"
        class="barra-cabecera"
      >
        <AppHeader
          :oscuro="oscuro"
          @cambiar-tema="cambiarTema"
          @abrir-ayuda="abrirAyuda"
        />
      </div>
    </Transition>

    <!-- SOLO EL CONTENIDO CAMBIA DE PANTALLA -->
    <div class="escenario-synchro">
      <RouterView v-slot="{ Component, route: pagina }">
        <Transition
          :name="transicion"
          :mode="transicion === 'synchro-inicio' ? 'out-in' : undefined"
        >
          <component
            :is="Component"
            :key="pagina.path"
            class="vista-synchro"
          />
        </Transition>
      </RouterView>
    </div>

    <!-- AYUDA COMPARTIDA -->
    <HelpDialog
      v-if="mostrarEncabezado"
      ref="centroAyuda"
      :oscuro="oscuro"
    />
  </div>
</template>

<style>
html,
body,
#app {
  width: 100%;
  height: 100%;
  margin: 0;
  padding: 0;
  overflow: hidden;
}

body,
#app {
  background: #061f36;
  font-family: Arial, Helvetica, sans-serif;
}

.synchro-app {
  --alto-cabecera: 104px;

  position: relative;
  isolation: isolate;

  width: 100%;
  height: 100dvh;
  overflow: hidden;
}

/* Encabezado fijo, fuera de la navegacion */

.barra-cabecera {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  z-index: 20;

  height: var(--alto-cabecera);
  padding: 20px 26px 0;

  background: #f2f7fa;

  transition:
    background-color 150ms ease;
}

.synchro-app.tema-oscuro .barra-cabecera {
  background: #061f36;
}

/* Las dimensiones del encabezado no dependen
   del contenido de las pantallas. */

.barra-cabecera .encabezado {
  width: 100%;
  height: 84px !important;
  min-height: 84px !important;

  flex-shrink: 0;
  flex-wrap: nowrap !important;
}

.barra-cabecera .acciones-header {
  flex: 0 0 auto;
  flex-wrap: nowrap;
}

.barra-cabecera .identidad {
  flex-shrink: 1;
}

/* El escenario conserva el tamaño del viewport. */

.escenario-synchro {
  position: absolute;
  inset: 0;
  overflow: hidden;
  background: #f2f7fa;
}

.synchro-app.tema-oscuro .escenario-synchro {
  background: #061f36;
}

/* Cada pantalla se anima independientemente. */

.escenario-synchro > .vista-synchro {
  position: absolute;
  inset: 0;

  width: 100%;
  height: 100%;
  min-height: 0;
  overflow: hidden;
}

/* Bienvenida utiliza toda la pantalla. */

.escenario-synchro > .vista-synchro.bienvenida {
  top: 0;
  height: 100%;
}

/* Menu y pantallas interiores comienzan debajo
   del encabezado permanente. */

.escenario-synchro
  > .vista-synchro:not(.bienvenida) {
  top: var(--alto-cabecera);
  height: calc(100% - var(--alto-cabecera));
}

/* Encabezado coordinado con el fundido del menu */

.cabecera-inicio-enter-active {
  transition: opacity 420ms ease-in-out 420ms;
}

.cabecera-inicio-enter-from {
  opacity: 0;
}

.cabecera-inicio-enter-to {
  opacity: 1;
}

.cabecera-inicio-leave-active {
  transition: opacity 420ms ease-in-out;
}

.cabecera-inicio-leave-to {
  opacity: 0;
}

@media (prefers-reduced-motion: reduce) {
  .cabecera-inicio-enter-active,
  .cabecera-inicio-leave-active {
    transition: none !important;
  }

  .cabecera-inicio-enter-from {
    opacity: 1;
  }
}
/* Tablets con poca altura */

@media (max-height: 550px) {
  .synchro-app {
    --alto-cabecera: 76px;
  }

  .barra-cabecera {
    height: 76px;
    padding: 10px 18px 0;
  }

  .barra-cabecera .encabezado {
    height: 66px !important;
    min-height: 66px !important;
  }
}

/* Tamaño de cabecera ajustado a la superficie táctil. */
@media (min-width: 431px) and (max-width: 599px) and (min-height: 620px) {
  .synchro-app { --alto-cabecera: 112px; }
  .barra-cabecera { height: 112px; padding: 12px 12px 0; }
  .barra-cabecera .encabezado {
    height: 100px !important;
    min-height: 100px !important;
  }
}

@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .synchro-app { --alto-cabecera: clamp(116px, 11vh, 132px); }
  .barra-cabecera {
    height: var(--alto-cabecera);
    padding: 12px clamp(16px, 2.2vw, 30px) 0;
  }
  .barra-cabecera .encabezado {
    height: calc(var(--alto-cabecera) - 12px) !important;
    min-height: calc(var(--alto-cabecera) - 12px) !important;
  }
}

@media (prefers-reduced-motion: reduce) {
  .barra-cabecera,
  .cabecera-inicio-enter-active {
    transition: none;
  }
}
</style>