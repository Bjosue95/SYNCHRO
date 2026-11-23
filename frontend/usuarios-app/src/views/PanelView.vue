<script setup>
import { RouterLink } from "vue-router";
import { estadoSesion } from "../services/sesion.js";
</script>

<template>
  <section class="contenedor pagina-interna">
    <div class="encabezado-pagina">
      <span class="eyebrow">Mi cuenta SYNCHRO</span>
      <h1>Bienvenido<span v-if="estadoSesion.cliente">, {{ estadoSesion.cliente.nombreUsuario }}</span></h1>
      <p>Desde aquí puedes solicitar y consultar los servicios web habilitados.</p>
    </div>

    <div class="panel-bienvenida">
      <span class="avatar-usuario" aria-hidden="true">S</span>
      <div class="panel-datos">
        <strong>{{ estadoSesion.cliente?.nombreUsuario || "Cliente SYNCHRO" }}</strong>
        <span>{{ estadoSesion.cliente?.telefono || "Cuenta registrada" }}</span>
      </div>
      <span class="estado-activo"><span class="punto-vivo"></span> Cuenta activa</span>
    </div>

    <div class="tarjetas-acciones">
      <RouterLink class="accion-principal" to="/reservar/encomienda">
        <span class="servicio-icono">▤</span>
        <h2>Nueva pre-reserva</h2>
        <p>Solicita una pre-reserva de encomienda con el teléfono del destinatario y el tamaño del paquete.</p>
        <span class="accion-enlace">Comenzar <span aria-hidden="true">→</span></span>
      </RouterLink>
      <RouterLink class="accion-principal" to="/consultar">
        <span class="servicio-icono">⌕</span>
        <h2>Consultar pre-reserva</h2>
        <p>Revisa una solicitud utilizando su referencia y clave de consulta privada.</p>
        <span class="accion-enlace">Consultar <span aria-hidden="true">→</span></span>
      </RouterLink>
    </div>

    <div class="panel-informativo">
      <h3>¿Y las reservas de pertenencias?</h3>
      <p>
        Las reservas de pertenencias por tamaño, fecha y duración todavía
        no están disponibles en el portal web. Por ahora puedes gestionar
        pre-reservas de encomiendas y consultar sus estados.
      </p>
    </div>
  </section>
</template>
