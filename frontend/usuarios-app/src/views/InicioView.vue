<script setup>
import { RouterLink } from "vue-router";
import { estadoSesion } from "../services/sesion.js";
</script>

<template>
  <div>
    <section class="portada">
      <div class="contenedor portada-grid">
        <div class="portada-texto">
          <span class="eyebrow portada-etiqueta"><span class="punto-vivo"></span> Portal de usuarios SYNCHRO</span>
          <h1>Tu espacio,<br /><em>cuando lo necesitas.</em></h1>
          <p>
            Gestiona tu cuenta y solicitudes web para servicios inteligentes de
            almacenamiento y encomiendas. Un solo lugar para conectar tus necesidades.
          </p>
          <div class="botones-portada">
            <RouterLink :to="estadoSesion.cliente ? '/mi-cuenta' : '/acceso'" class="boton boton-luz">
              {{ estadoSesion.cliente ? "Ir a mi cuenta" : "Comenzar ahora" }}
              <span aria-hidden="true">↗</span>
            </RouterLink>
            <RouterLink to="/reservar/encomienda" class="boton boton-fantasma">
              Pre-reservar encomienda <span aria-hidden="true">→</span>
            </RouterLink>
          </div>
        </div>
        <div class="hero-visual" aria-hidden="true">
          <div class="circulo-uno"></div>
          <div class="circulo-dos"></div>
          <div class="terminal-ilustrada">
            <div class="terminal-cabecera">
              <span class="terminal-marca">SYNCHRO</span>
              <span class="luces"><i></i><i></i><i></i></span>
            </div>
            <div class="terminal-cuadricula">
              <div v-for="n in 6" :key="n" class="terminal-puerta"><span>{{ 'M0' + n }}</span><b></b></div>
            </div>
            <div class="terminal-pie">Conecta · Guarda · Retira</div>
          </div>
          <div class="etiqueta-flotante"><span>✓</span> Solicitudes desde tu cuenta</div>
        </div>
      </div>
    </section>

    <section class="contenedor bloque-servicios">
      <div class="titulo-seccion">
        <span class="eyebrow">Servicios SYNCHRO</span>
        <h2>Todo conectado en un mismo lugar</h2>
        <p>El prototipo integra distintos servicios según las necesidades de cada ubicación.</p>
      </div>

      <div class="rejilla-servicios">
        <article class="tarjeta-servicio">
          <span class="servicio-icono">▣</span>
          <h3>Almacenamiento personal</h3>
          <p>Espacios para resguardar temporalmente tus pertenencias.</p>
          <span class="servicio-estado">Reserva web pendiente de integración</span>
        </article>
        <article class="tarjeta-servicio destacada">
          <span class="servicio-icono">▤</span>
          <h3>Encomiendas</h3>
          <p>Solicita una pre-reserva para coordinar el servicio con tu destinatario.</p>
          <RouterLink to="/reservar/encomienda" class="enlace-texto">
            Iniciar pre-reserva <span aria-hidden="true">→</span>
          </RouterLink>
        </article>
        <article class="tarjeta-servicio">
          <span class="servicio-icono">❄</span>
          <h3>Temperatura controlada</h3>
          <p>Opciones de frío y calor para artículos compatibles.</p>
          <span class="servicio-estado">Reserva web pendiente de integración</span>
        </article>
        <article class="tarjeta-servicio">
          <span class="servicio-icono">ϟ</span>
          <h3>Carga de dispositivos</h3>
          <p>Carga presencial de celulares y laptops, con pago al finalizar.</p>
          <span class="servicio-estado">Disponible únicamente en terminal</span>
        </article>
      </div>
    </section>

    <section class="contenedor panel-aclaracion">
      <span class="aclaracion-icono" aria-hidden="true">i</span>
      <div>
        <strong>Información sobre las pre-reservas</strong>
        <p>
          La pre-reserva de encomiendas no significa que un casillero esté asignado.
          Para el depósito se requieren autorizaciones y verificaciones adicionales.
        </p>
      </div>
    </section>
  </div>
</template>
