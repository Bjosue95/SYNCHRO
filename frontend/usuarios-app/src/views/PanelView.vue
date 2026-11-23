<script setup>
import { computed } from "vue";
import { RouterLink } from "vue-router";
import { estadoSesion } from "../services/sesion.js";
import AppIcon from "../components/ui/AppIcon.vue";

const usuario = computed(() => estadoSesion.cliente?.nombreUsuario || "Cliente");
const inicial = computed(() => usuario.value.charAt(0).toLocaleUpperCase("es-HN"));
</script>

<template>
  <section class="pagina-interna">
    <div class="encabezado-pagina">
      <h1>Hola, {{ usuario }}</h1>
      <p>Desde aquí puedes gestionar tus solicitudes de SYNCHRO.</p>
    </div>

    <div class="panel-bienvenida">
      <span class="avatar-usuario">{{ inicial }}</span>
      <div class="panel-datos">
        <strong>{{ usuario }}</strong>
        <span>{{ estadoSesion.cliente?.telefono || "Cuenta SYNCHRO" }}</span>
      </div>
      <span class="estado-activo"><span class="punto-vivo"></span> Cuenta activa</span>
    </div>

    <h2 class="panel-subtitulo">¿Qué necesitas hacer?</h2>
    <div class="tarjetas-acciones">
      <RouterLink class="accion-principal" to="/reservar/encomienda">
        <span class="servicio-icono"><AppIcon nombre="reservas" :tamano="41"/></span>
        <h2>Crear pre-reserva</h2>
        <p>Registra una solicitud de encomienda seleccionando el tamaño y el destinatario.</p>
        <span class="accion-enlace">
          Nueva solicitud <AppIcon nombre="flecha" :tamano="20"/>
        </span>
      </RouterLink>

      <RouterLink class="accion-principal" to="/consultar">
        <span class="servicio-icono"><AppIcon nombre="buscar" :tamano="41"/></span>
        <h2>Consultar pre-reserva</h2>
        <p>Consulta el estado de una solicitud usando su referencia y clave privada.</p>
        <span class="accion-enlace">
          Consultar <AppIcon nombre="flecha" :tamano="20"/>
        </span>
      </RouterLink>
    </div>

    <div class="panel-informativo">
      <span class="aclaracion-icono"><AppIcon nombre="casillero" :tamano="27"/></span>
      <div>
        <h3>Reservas de pertenencias</h3>
        <p>
          La reserva web de casilleros personales, con selección de fecha y duración,
          aún no está habilitada. Por ahora puedes crear y consultar
          solicitudes de encomiendas desde tu cuenta.
        </p>
      </div>
    </div>
  </section>
</template>
