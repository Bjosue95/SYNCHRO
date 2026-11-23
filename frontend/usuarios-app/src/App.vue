<script setup>
import { onMounted, ref } from "vue";
import { RouterLink, RouterView, useRouter } from "vue-router";
import { estadoSesion, comprobarSesion, cerrarSesion } from "./services/sesion.js";

const router = useRouter();
const saliendo = ref(false);
const errorSalir = ref("");

onMounted(() => {
  void comprobarSesion().catch(() => {
    // El estado del backend también se indica en las acciones protegidas.
  });
});

async function salir() {
  if (saliendo.value) return;
  saliendo.value = true;
  errorSalir.value = "";
  try {
    await cerrarSesion();
    await router.replace("/");
  } catch (error) {
    errorSalir.value = error?.message || "No se pudo cerrar la sesión.";
  } finally {
    saliendo.value = false;
  }
}
</script>

<template>
  <div class="sitio">
    <header class="cabecera">
      <div class="contenedor barra">
        <RouterLink to="/" class="marca" aria-label="SYNCHRO - Inicio">
          <span class="marca-simbolo" aria-hidden="true">S</span>
          <span class="marca-nombre">SYNCHRO<small>El punto donde todo se conecta</small></span>
        </RouterLink>

        <nav class="navegacion" aria-label="Navegación principal">
          <RouterLink to="/" class="enlace-menu">Inicio</RouterLink>
          <template v-if="estadoSesion.cliente">
            <RouterLink to="/mi-cuenta" class="enlace-menu">Mi cuenta</RouterLink>
            <RouterLink to="/reservar/encomienda" class="enlace-menu">Reservas</RouterLink>
            <button class="enlace-menu salir" type="button" :disabled="saliendo" @click="salir">
              {{ saliendo ? "Cerrando..." : "Salir" }}
            </button>
          </template>
          <RouterLink v-else to="/acceso" class="enlace-entrar">Ingresar</RouterLink>
        </nav>
      </div>
      <p v-if="errorSalir" class="aviso-global" role="alert">{{ errorSalir }}</p>
    </header>

    <main class="principal" id="contenido">
      <RouterView v-slot="{ Component, route }">
        <Transition name="pagina" mode="out-in">
          <component :is="Component" :key="route.path" />
        </Transition>
      </RouterView>
    </main>

    <footer class="pie-sitio">
      <div class="contenedor pie-contenido">
        <span><strong>SYNCHRO</strong> · Soluciones inteligentes de autoservicio</span>
        <span>Portal de usuarios · Prototipo en desarrollo</span>
      </div>
    </footer>
  </div>
</template>
