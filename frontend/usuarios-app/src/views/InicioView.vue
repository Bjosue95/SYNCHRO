<script setup>
import { RouterLink } from "vue-router";
import { estadoSesion } from "../services/sesion.js";
import AppIcon from "../components/ui/AppIcon.vue";
</script>

<template>
  <div>
    <section class="portada">
      <div class="contenedor portada-grid">
        <div class="portada-texto">
          <span class="eyebrow portada-etiqueta">SYNCHRO · SERVICIOS INTELIGENTES</span>
          <h1>Tu tiempo vale.<br><em>Tu espacio también.</em></h1>
          <p>
            Almacenamiento y encomiendas conectados a una experiencia sencilla.
            Crea tu cuenta para gestionar tus solicitudes desde donde estés.
          </p>
          <div class="botones-portada">
            <RouterLink :to="estadoSesion.cliente ? '/mi-cuenta' : '/acceso'"
              class="boton boton-luz">
              {{ estadoSesion.cliente ? "Ir a mi espacio" : "Crear mi cuenta" }}
              <span aria-hidden="true">↗</span>
            </RouterLink>
            <RouterLink to="/reservar/encomienda" class="boton boton-fantasma">
              Solicitar una encomienda
              <span aria-hidden="true">→</span>
            </RouterLink>
          </div>
        </div>

        <div class="hero-visual" aria-hidden="true">
          <div class="circulo-uno"></div>
          <div class="circulo-dos"></div>
          <div class="terminal-ilustrada">
            <div class="terminal-cabecera">
              <img class="terminal-marca" src="/branding/nombre-azul.svg" alt="">
              <span class="luces"><i></i><i></i><i></i></span>
            </div>
            <div class="terminal-cuadricula">
              <div v-for="n in 6" :key="n" class="terminal-puerta">
                <span>{{ "M0" + n }}</span><b></b>
              </div>
            </div>
            <div class="terminal-pie">GUARDA · CONECTA · RETIRA</div>
          </div>
          <div class="etiqueta-flotante">
            <img src="/iconos/claro/check.svg" alt="">
            Gestión desde tu cuenta
          </div>
        </div>
      </div>
    </section>

    <section id="servicios" class="contenedor bloque-servicios">
      <div class="titulo-seccion">
        <span class="eyebrow">SOLUCIONES SYNCHRO</span>
        <h2>Un servicio para cada momento</h2>
        <p>Desde guardar tus pertenencias hasta gestionar la entrega de un paquete.</p>
      </div>

      <div class="rejilla-servicios">
        <article class="tarjeta-servicio">
          <span class="servicio-icono"><AppIcon nombre="casillero" :tamano="43"/></span>
          <h3>Almacenamiento personal</h3>
          <p>Espacios seguros para guardar tus pertenencias durante el tiempo que necesites.</p>
          <span class="servicio-estado">Reserva web próximamente</span>
        </article>
        <article class="tarjeta-servicio destacada">
          <span class="servicio-icono"><AppIcon nombre="encomienda" :tamano="43"/></span>
          <h3>Encomiendas</h3>
          <p>Inicia una solicitud desde tu cuenta y coordina el siguiente paso con el destinatario.</p>
          <RouterLink to="/reservar/encomienda" class="enlace-texto">
            Nueva pre-reserva <AppIcon nombre="flecha" :tamano="19"/>
          </RouterLink>
        </article>
        <article class="tarjeta-servicio">
          <span class="servicio-icono"><AppIcon nombre="temperatura" :tamano="43"/></span>
          <h3>Temperatura controlada</h3>
          <p>Espacios especializados para productos compatibles que necesitan frío o calor.</p>
          <span class="servicio-estado">Reserva web próximamente</span>
        </article>
        <article class="tarjeta-servicio">
          <span class="servicio-icono"><AppIcon nombre="carga" :tamano="43"/></span>
          <h3>Carga de dispositivos</h3>
          <p>Recarga celulares o laptops en estaciones de uso presencial y paga al finalizar.</p>
          <span class="servicio-estado">Servicio presencial</span>
        </article>
      </div>
    </section>

    <section class="contenedor panel-aclaracion">
      <span class="aclaracion-icono"><AppIcon nombre="seguridad" :tamano="29"/></span>
      <div>
        <strong>Solicitudes gestionadas de forma segura</strong>
        <p>
          Una pre-reserva de encomienda no asigna todavía un casillero.
          La utilización del servicio requiere las verificaciones correspondientes.
        </p>
      </div>
    </section>
  </div>
</template>
