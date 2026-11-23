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
      <h1>Mi espacio</h1>
      <p>Crea y consulta tus solicitudes de encomienda.</p>
    </div>

    <div class="panel-bienvenida">
      <span class="avatar-usuario">{{ inicial }}</span>
      <div class="panel-datos">
        <strong>{{ usuario }}</strong>
        <span>{{ estadoSesion.cliente?.telefono || "Cuenta SYNCHRO" }}</span>
      </div>
      <span class="estado-activo"><span class="punto-vivo"></span> Cuenta activa</span>
    </div>

    <h2 class="panel-subtitulo">Pre-reservas de encomienda</h2>
    <div class="tarjetas-acciones">
      <RouterLink class="accion-principal" to="/reservar/encomienda">
        <AppIcon nombre="reservas"/>
        <div>
          <strong>Crear pre-reserva</strong>
          <p>Elige el tamaño del paquete e indica el teléfono del destinatario.</p>
        </div>
        <AppIcon nombre="flecha"/>
      </RouterLink>

      <RouterLink class="accion-principal" to="/consultar">
        <AppIcon nombre="buscar"/>
        <div>
          <strong>Consultar pre-reserva</strong>
          <p>Revisa el estado con la referencia y la clave privada.</p>
        </div>
        <AppIcon nombre="flecha"/>
      </RouterLink>
    </div>

    <div class="panel-informativo">
      <AppIcon nombre="casillero"/>
      <div>
        <h3>Reservas de pertenencias</h3>
        <p>
          La reserva web de casilleros personales, con selección de fecha y duración,
          aún no está habilitada. Desde tu cuenta puedes crear y consultar
          pre-reservas de encomiendas.
        </p>
      </div>
    </div>
  </section>
</template>
