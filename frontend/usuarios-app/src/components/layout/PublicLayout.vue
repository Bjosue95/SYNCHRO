<script setup>
import { RouterLink } from "vue-router";
import { estadoSesion } from "../../services/sesion.js";
import BrandLogo from "../ui/BrandLogo.vue";
import ThemeToggle from "../ui/ThemeToggle.vue";
import AppIcon from "../ui/AppIcon.vue";
</script>

<template>
  <div class="sitio publico">
    <header class="cabecera-publica">
      <div class="contenedor header-publico">
        <RouterLink to="/" class="enlace-marca"><BrandLogo /></RouterLink>
        <nav class="nav-publica" aria-label="Navegación principal">
          <RouterLink to="/" class="nav-link" active-class="activo">Inicio</RouterLink>
          <RouterLink :to="{ path: '/', hash: '#servicios' }" class="nav-link">Servicios</RouterLink>
          <RouterLink :to="estadoSesion.cliente ? '/mi-cuenta' : '/acceso'"
            class="nav-link acceso-corto">
            {{ estadoSesion.cliente ? "Mi cuenta" : "Ingresar" }}
          </RouterLink>
        </nav>
        <div class="header-acciones">
          <ThemeToggle />
          <RouterLink :to="estadoSesion.cliente ? '/mi-cuenta' : '/acceso'"
            class="boton boton-primario header-ingresar">
            {{ estadoSesion.cliente ? "Mi panel" : "Empezar" }}
            <AppIcon nombre="flecha" :tamano="17"/>
          </RouterLink>
        </div>
      </div>
    </header>
    <main id="contenido" class="contenido-publico">
      <slot/>
    </main>
    <footer class="footer-publico">
      <div class="contenedor footer-interior">
        <img src="/branding/marca-completa.svg" alt="SYNCHRO, el punto donde todo se conecta" class="footer-logo"/>
        <span>© {{ new Date().getFullYear() }} SYNCHRO</span>
        <RouterLink to="/acceso">Acceso de clientes</RouterLink>
      </div>
    </footer>
  </div>
</template>
