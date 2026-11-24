<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from "vue";
import { RouterLink, useRoute } from "vue-router";
import { estadoSesion } from "../../services/sesion.js";
import BrandLogo from "../ui/BrandLogo.vue";
import ThemeToggle from "../ui/ThemeToggle.vue";
import AppIcon from "../ui/AppIcon.vue";
import SiteFooter from "../landing/SiteFooter.vue";

const route = useRoute();
const cabecera = ref<HTMLElement | null>(null);
const botonMenu = ref<HTMLButtonElement | null>(null);
const menuAbierto = ref(false);
const enlaces = [
  { hash: "#inicio", texto: "Inicio" },
  { hash: "#servicios", texto: "Servicios" },
  { hash: "#nosotros", texto: "Nosotros" },
  { hash: "#contacto", texto: "Contáctanos" }
];
const esAcceso = computed(() => route.path === "/acceso");
const destino = computed(() => esAcceso.value ? "/" :
  estadoSesion.cliente ? "/mi-cuenta" : "/acceso");
const etiqueta = computed(() => esAcceso.value ? "Volver al sitio" :
  estadoSesion.cliente ? "Mi espacio" : "Ingresar");

function cerrarMenu(evento: KeyboardEvent) {
  if (evento.key !== "Escape" || !menuAbierto.value) return;
  menuAbierto.value = false;
  botonMenu.value?.focus();
}
function cerrarFuera(evento: PointerEvent) {
  if (evento.target instanceof Node && !cabecera.value?.contains(evento.target)) menuAbierto.value = false;
}
function alSalir(evento: FocusEvent) {
  if (evento.relatedTarget instanceof Node && !cabecera.value?.contains(evento.relatedTarget)) menuAbierto.value = false;
}
watch(() => route.fullPath, () => { menuAbierto.value = false; });
onMounted(() => {
  document.addEventListener("keydown", cerrarMenu);
  document.addEventListener("pointerdown", cerrarFuera);
});
onBeforeUnmount(() => {
  document.removeEventListener("keydown", cerrarMenu);
  document.removeEventListener("pointerdown", cerrarFuera);
});
</script>

<template>
  <div class="sitio publico" :class="{ 'es-acceso': esAcceso }">
    <header ref="cabecera" class="cabecera-publica" @focusout="alSalir">
      <div class="contenedor header-publico">
        <RouterLink to="/" class="enlace-marca" aria-label="SYNCHRO, ir al inicio">
          <BrandLogo />
        </RouterLink>
        <button ref="botonMenu" type="button" class="menu-publico"
          aria-controls="navegacion-publica" :aria-expanded="menuAbierto"
          :aria-label="menuAbierto ? 'Cerrar menú de navegación' : 'Abrir menú de navegación'"
          @click="menuAbierto = !menuAbierto">
          <AppIcon :nombre="menuAbierto ? 'volver' : 'menu'" :tamano="22" />
        </button>
        <nav id="navegacion-publica" class="nav-publica" :class="{ abierta: menuAbierto }" aria-label="Navegación principal">
          <RouterLink v-for="enlace in enlaces" :key="enlace.hash" :to="{ path: '/', hash: enlace.hash }"
            class="nav-link" :class="{ activo: route.path === '/' && (route.hash || '#inicio') === enlace.hash }"
            :aria-current="route.path === '/' && (route.hash || '#inicio') === enlace.hash ? 'location' : false"
            @click="menuAbierto = false">{{ enlace.texto }}</RouterLink>
        </nav>
        <div class="header-acciones">
          <ThemeToggle />
          <RouterLink v-if="!esAcceso" :to="destino" class="acceso-enlace">
            {{ etiqueta }}
          </RouterLink>
        </div>
      </div>
    </header>

    <main id="contenido" class="contenido-publico">
      <slot />
    </main>

    <SiteFooter v-if="!esAcceso" />
  </div>
</template>

<style scoped>
.cabecera-publica { position:sticky; top:0; }
.header-acciones { margin-left:0; flex-shrink:0; }
.nav-publica { margin-inline:auto; }
.menu-publico { display:none; }
@media (max-width:900px) {
  .header-publico { gap:12px; }
  .menu-publico { display:inline-flex; align-items:center; justify-content:center; flex-shrink:0;
    width:44px; height:44px; margin-left:auto; padding:0; border:1px solid var(--border-strong);
    border-radius:8px; color:var(--text-strong); background:var(--surface); cursor:pointer; }
  .menu-publico:hover { background:var(--surface-hover); }
  .nav-publica { display:none; position:absolute; inset:100% 0 auto; margin:0; padding:12px 24px;
    background:var(--surface); border-bottom:1px solid var(--border); box-shadow:var(--shadow-md); }
  .nav-publica.abierta { display:flex; flex-direction:column; align-items:stretch; gap:4px; }
  .nav-link { padding:14px 8px; }
}
@media (max-width:620px) {
  .header-publico { gap:8px; }
  .header-acciones { gap:8px; }
}
@media (max-width:370px) {
  .header-publico, .header-acciones { gap:6px; }
  .enlace-marca :deep(.marca-letras) { width:96px; }
  .enlace-marca :deep(.marca-textos small) { display:none; }
  .enlace-marca :deep(.marca-real) { gap:6px; }
}
</style>
