<script setup>
import { RouterLink } from "vue-router";
import { estadoSesion } from "../services/sesion.js";
import AppIcon from "../components/ui/AppIcon.vue";
import modeloMultiple from "../assets/multiple.webp";
import modeloPertenencias from "../assets/pertenencias.webp";
import modeloTemperatura from "../assets/temperatura.webp";
</script>

<template>
  <div>
    <section class="portada" aria-labelledby="titulo-portada">
      <div class="contenedor portada-grid">
        <div class="portada-texto">
          <h1 id="titulo-portada">Casilleros y encomiendas</h1>
          <p>
            Solicita una pre-reserva de encomienda y consulta su estado desde tu cuenta.
            Conoce también los casilleros para pertenencias, temperatura y carga.
          </p>
          <div class="botones-portada">
            <RouterLink to="/reservar/encomienda" class="boton boton-primario">
              Solicitar pre-reserva <AppIcon nombre="flecha"/>
            </RouterLink>
            <RouterLink :to="estadoSesion.cliente ? '/mi-cuenta' : { name: 'acceso', query: { registro: '1' } }"
              class="boton boton-secundario">
              {{ estadoSesion.cliente ? "Mi espacio" : "Crear cuenta" }}
            </RouterLink>
          </div>
        </div>

        <figure class="portada-modelo">
          <img :src="modeloMultiple" width="1400" height="788" fetchpriority="high"
            alt="Casillero modular SYNCHRO con compartimentos de varios tamaños, pantalla central y estaciones de carga">
          <figcaption>Modelo múltiple de exhibición</figcaption>
        </figure>
      </div>
    </section>

    <section id="servicios" class="contenedor bloque-servicios" aria-labelledby="titulo-servicios">
      <div class="titulo-seccion">
        <h2 id="titulo-servicios">Servicios de casillero</h2>
      </div>

      <div class="rejilla-servicios">
        <article class="tarjeta-servicio">
          <div class="servicio-texto">
            <h3><AppIcon nombre="casillero"/> Almacenamiento personal</h3>
            <p>Guarda mochilas, bolsos y objetos de uso diario.</p>
            <span class="servicio-estado">Reserva web próximamente</span>
          </div>
          <img class="servicio-modelo" :src="modeloPertenencias" width="950" height="535"
            loading="lazy" decoding="async" alt="Modelo de exhibición con una mochila en un compartimento abierto">
        </article>

        <article class="tarjeta-servicio">
          <div class="servicio-texto">
            <h3><AppIcon nombre="encomienda"/> Encomiendas</h3>
            <p>Inicia una solicitud y coordina el siguiente paso con el destinatario.</p>
            <RouterLink to="/reservar/encomienda" class="enlace-texto">
              Nueva pre-reserva <AppIcon nombre="flecha"/>
            </RouterLink>
          </div>
        </article>

        <article class="tarjeta-servicio">
          <div class="servicio-texto">
            <h3><AppIcon nombre="temperatura"/> Temperatura controlada</h3>
            <p>Espacios para productos compatibles que requieren frío o calor.</p>
            <span class="servicio-estado">Reserva web próximamente</span>
          </div>
          <img class="servicio-modelo" :src="modeloTemperatura" width="950" height="535"
            loading="lazy" decoding="async" alt="Modelo térmico de exhibición con zonas fría y caliente">
        </article>

        <article class="tarjeta-servicio">
          <div class="servicio-texto">
            <h3><AppIcon nombre="carga"/> Carga de dispositivos</h3>
            <p>Recarga celulares o laptops y paga al finalizar.</p>
            <span class="servicio-estado">Servicio presencial</span>
          </div>
        </article>
      </div>
    </section>

    <section class="contenedor panel-aclaracion">
      <AppIcon nombre="seguridad"/>
      <div>
        <h2>Cuándo se asigna un casillero</h2>
        <p>La pre-reserva registra tu solicitud. La asignación requiere verificaciones posteriores.</p>
      </div>
    </section>
  </div>
</template>
