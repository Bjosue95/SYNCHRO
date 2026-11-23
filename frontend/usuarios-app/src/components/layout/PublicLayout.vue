<script setup>
import { computed } from "vue";
import { RouterLink, useRoute } from "vue-router";
import { estadoSesion } from "../../services/sesion.js";
import BrandLogo from "../ui/BrandLogo.vue";
import ThemeToggle from "../ui/ThemeToggle.vue";

const route = useRoute();
const esAcceso = computed(() => route.path === "/acceso");
const esInicio = computed(() => route.name === "inicio");
const destino = computed(() => estadoSesion.cliente ? "/mi-cuenta" : "/acceso");
const etiqueta = computed(() => estadoSesion.cliente ? "Mi espacio" : "Ingresar");
</script>

<template>
  <div class="sitio publico" :class="{ 'es-acceso': esAcceso }">
    <header class="cabecera-publica" :class="{ 'cabecera-landing': esInicio }">
      <div class="contenedor header-publico">
        <RouterLink to="/" class="enlace-marca" aria-label="SYNCHRO, ir al inicio">
          <BrandLogo />
        </RouterLink>
        <nav v-if="esInicio" class="sitio-secciones" aria-label="Navegación de SYNCHRO">
          <a href="#inicio">Inicio</a>
          <a href="#historia">Nuestra historia</a>
          <a href="#servicios">Nuestros servicios</a>
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

    <footer v-if="!esAcceso" class="footer-publico">
      <div class="contenedor footer-interior">
        <div class="footer-marca">
          <RouterLink to="/" class="enlace-marca"><BrandLogo compacto /></RouterLink>
          <p>Casilleros inteligentes · Comayagua, Honduras</p>
        </div>
        <nav class="footer-enlaces" aria-label="Explorar SYNCHRO">
          <h2>Explora SYNCHRO</h2>
          <RouterLink to="/#servicios">Nuestros servicios</RouterLink>
          <RouterLink to="/#modelo-synchro">El casillero SYNCHRO</RouterLink>
          <RouterLink to="/#historia">Nuestra historia</RouterLink>
        </nav>
        <nav class="footer-enlaces" aria-label="Cuenta y ayuda">
          <h2>Cuenta y ayuda</h2>
          <RouterLink :to="destino">{{ estadoSesion.cliente ? "Mi espacio" : "Acceso de clientes" }}</RouterLink>
          <RouterLink v-if="!estadoSesion.cliente" :to="{ name: 'acceso', query: { registro: '1' } }">Crear cuenta</RouterLink>
          <RouterLink to="/consultar">Consultar solicitud</RouterLink>
          <RouterLink to="/#preguntas">Preguntas frecuentes</RouterLink>
        </nav>
      </div>
      <div class="contenedor footer-cierre">
        <span>© {{ new Date().getFullYear() }} SYNCHRO</span>
        <RouterLink to="/#inicio">Volver arriba ↑</RouterLink>
      </div>
    </footer>
  </div>
</template>
