<script setup>
import { computed } from "vue";
import { RouterLink, useRoute } from "vue-router";
import { estadoSesion } from "../../services/sesion.js";
import BrandLogo from "../ui/BrandLogo.vue";
import ThemeToggle from "../ui/ThemeToggle.vue";

const route = useRoute();
const esAcceso = computed(() => route.path === "/acceso");
const destino = computed(() => esAcceso.value ? "/" :
  estadoSesion.cliente ? "/mi-cuenta" : "/acceso");
const etiqueta = computed(() => esAcceso.value ? "Volver al sitio" :
  estadoSesion.cliente ? "Mi espacio" : "Ingresar");
</script>

<template>
  <div class="sitio publico" :class="{ 'es-acceso': esAcceso }">
    <header class="cabecera-publica">
      <div class="contenedor header-publico">
        <RouterLink to="/" class="enlace-marca" aria-label="SYNCHRO, ir al inicio">
          <BrandLogo />
        </RouterLink>
        <div class="header-acciones">
          <ThemeToggle />
          <RouterLink :to="destino" class="acceso-enlace">
            {{ etiqueta }}
          </RouterLink>
        </div>
      </div>
    </header>

    <main id="contenido" class="contenido-publico">
      <slot />
    </main>

    <footer v-if="!esAcceso" class="footer-publico">
      <div class="contenedor footer-interior">
        <RouterLink to="/" class="enlace-marca"><BrandLogo compacto /></RouterLink>
        <span>© {{ new Date().getFullYear() }} SYNCHRO</span>
        <RouterLink to="/acceso">Acceso de clientes</RouterLink>
      </div>
    </footer>
  </div>
</template>
