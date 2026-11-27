<script setup>
import { onMounted, onBeforeUnmount, ref, watch, computed } from "vue";
import { RouterLink, useRouter, useRoute } from "vue-router";
import { estadoSesion, cerrarSesion } from "../../services/sesion.js";
import AppIcon from "../ui/AppIcon.vue";
import BrandLogo from "../ui/BrandLogo.vue";
import ThemeToggle from "../ui/ThemeToggle.vue";

const router = useRouter();
const route = useRoute();
const abierto = ref(false);
const saliendo = ref(false);
const error = ref("");
const nombre = computed(() => estadoSesion.cliente?.nombreUsuario || "Cliente");
const inicial = computed(() => nombre.value.charAt(0).toLocaleUpperCase("es-HN"));
const secciones = [
  { ruta: "/bienvenida", nombre: "Inicio", icono: "inicio" },
  { ruta: "/reservar", nombre: "Reservar casillero", icono: "casillero" },
  { ruta: "/mis-reservas", nombre: "Mis reservas", icono: "reservas" },
  { ruta: "/mi-perfil", nombre: "Mi perfil", icono: "usuario" }
];

watch(() => route.fullPath, () => { abierto.value = false; });
function onTecla(evento) { if (evento.key === "Escape") abierto.value = false; }
onMounted(() => { window.addEventListener("keydown", onTecla); });
onBeforeUnmount(() => { window.removeEventListener("keydown", onTecla); });

async function salir() {
  if (saliendo.value) return;
  saliendo.value = true;
  error.value = "";
  try {
    await cerrarSesion();
    abierto.value = false;
    await router.replace("/");
  } catch (e) {
    error.value = e?.message || "No pudimos cerrar tu sesión.";
  } finally {
    saliendo.value = false;
  }
}
</script>

<template>
  <div class="app-shell">
    <div v-if="abierto" class="sidebar-overlay" @click="abierto = false" aria-hidden="true"></div>
    <aside class="sidebar" :class="{ abierta: abierto }" aria-label="Menú de usuario">
      <div class="sidebar-marca">
        <RouterLink to="/bienvenida" class="enlace-marca"><BrandLogo /></RouterLink>
        <button type="button" class="cerrar-sidebar" aria-label="Cerrar menú" @click="abierto = false">
          <AppIcon nombre="volver" :tamano="19"/>
        </button>
      </div>
      <div class="sidebar-cuerpo">
        <p class="sidebar-titulo">TU CUENTA</p>
        <nav class="sidebar-navegacion" aria-label="Navegación de cuenta">
          <RouterLink v-for="item in secciones" :key="item.ruta" :to="item.ruta"
            class="sidebar-link" active-class="activo">
            <span class="sidebar-icono"><AppIcon :nombre="item.icono" :tamano="21"/></span>
            <span>{{ item.nombre }}</span>
            <span v-if="route.path === item.ruta" class="sidebar-indicador" aria-hidden="true"></span>
          </RouterLink>
        </nav>

        <div class="sidebar-separador"></div>
        <p class="sidebar-titulo">SYNCHRO</p>
        <RouterLink to="/" class="sidebar-link">
          <span class="sidebar-icono"><AppIcon nombre="volver" :tamano="21"/></span>
          <span>Volver al sitio</span>
        </RouterLink>
      </div>

      <div class="sidebar-pie">
        <div class="sidebar-perfil">
          <span class="avatar">{{ inicial }}</span>
          <span class="perfil-datos"><strong>{{ nombre }}</strong><small>Cuenta personal</small></span>
        </div>
        <button type="button" class="sidebar-cerrar-sesion" :disabled="saliendo" @click="salir">
          <AppIcon nombre="salir" :tamano="20"/>
          {{ saliendo ? "Saliendo..." : "Cerrar sesión" }}
        </button>
        <p v-if="error" class="error-cerrar" role="alert">{{ error }}</p>
      </div>
    </aside>

    <div class="app-principal">
      <header class="topbar">
        <div class="topbar-identidad">
          <button type="button" class="boton-menu" :aria-expanded="abierto"
            aria-label="Abrir menú de usuario" @click="abierto = true">
            <AppIcon nombre="menu" :tamano="23"/>
          </button>
          <span class="topbar-titulo">SYNCHRO</span>
          <span class="topbar-separador" aria-hidden="true"></span>
          <span class="topbar-seccion">{{ route.meta?.titulo || "Mi espacio" }}</span>
        </div>
        <div class="topbar-acciones">
          <ThemeToggle/>
          <span class="avatar topbar-avatar" :title="nombre" :aria-label="`Cuenta de ${nombre}`">{{ inicial }}</span>
        </div>
      </header>

      <main class="panel-contenido" id="contenido">
        <div class="contenido-limite">
          <slot/>
        </div>
      </main>
    </div>
  </div>
</template>
