<script setup>
import { ref } from "vue";
import { RouterLink } from "vue-router";
import AppIcon from "../components/ui/AppIcon.vue";
import { fechaHonduras, nombreEstadoReserva } from "../services/formatos.js";
import { apiClientes } from "../services/clientesApi.js";
import { tokenReservaValido, referenciaReservaValida } from "../services/validaciones.js";

const referencia = ref("");
const clave = ref("");
const consultando = ref(false);
const error = ref("");
const resultado = ref(null);


async function consultar() {
  if (consultando.value) return;
  error.value = "";
  resultado.value = null;
  if (!referenciaReservaValida(referencia.value) ||
      !tokenReservaValido(clave.value)) {
    error.value = "Escribe una referencia válida y su clave privada de 64 caracteres.";
    return;
  }
  consultando.value = true;
  try {
    resultado.value = await apiClientes.consultarPreReserva(
      referencia.value, clave.value
    );
  } catch (e) {
    error.value = e?.message || "No se pudo consultar la pre-reserva.";
  } finally {
    consultando.value = false;
  }
}
</script>

<template>
  <section class="contenedor pagina-interna">
    <div class="encabezado-pagina">
      <RouterLink to="/mi-cuenta" class="enlace-volver"><AppIcon nombre="volver" :tamano="20"/> Mi cuenta</RouterLink>
      <span class="eyebrow">Seguimiento privado</span>
      <h1>Consulta tu pre-reserva</h1>
      <p>Utiliza la referencia y clave de consulta que recibiste al crear la solicitud.</p>
    </div>

    <div class="reserva-grid">
      <div class="formulario-tarjeta">
        <h2>Buscar solicitud</h2>
        <p class="texto-suave">Por seguridad, solo puedes consultar solicitudes de tu propia cuenta.</p>
        <form @submit.prevent="consultar" class="formulario">
          <label class="campo">
            <span>Número de referencia</span>
            <input v-model="referencia" type="text" inputmode="numeric" required
              maxlength="10" autocomplete="off" placeholder="Ejemplo: 123" />
          </label>
          <label class="campo">
            <span>Clave privada de consulta</span>
            <textarea v-model="clave" rows="3" required maxlength="64"
              spellcheck="false" autocomplete="off"
              placeholder="Pega aquí la clave entregada al crear la pre-reserva"></textarea>
            <small>La clave no se guarda en el navegador y no aparece en la dirección web.</small>
          </label>
          <p v-if="error" class="alerta" role="alert">{{ error }}</p>
          <button type="submit" class="boton boton-primario boton-ancho"
            :disabled="consultando" :aria-busy="consultando">
            {{ consultando ? "Consultando..." : "Consultar estado" }}
            <AppIcon nombre="flecha" :tamano="20"/>
          </button>
        </form>

        <div v-if="resultado" class="resultado-consulta" role="status">
          <span class="eyebrow">Resultado de la consulta</span>
          <h3>Pre-reserva #{{ resultado.id }}</h3>
          <div class="resumen-reserva">
            <div><span>Estado actual</span><strong>{{ nombreEstadoReserva(resultado.estado) }}</strong></div>
            <div><span>Tamaño solicitado</span><strong>{{ resultado.tamanoSolicitado }}</strong></div>
            <div><span>Expiración</span><strong>{{ fechaHonduras(resultado.fechaExpiracion) }}</strong></div>
            <div><span>Depósito habilitado</span><strong>{{ resultado.depositoHabilitado ? "Sí" : "No" }}</strong></div>
          </div>
          <p class="texto-suave">{{ resultado.mensaje }}</p>
        </div>
      </div>

      <aside class="panel-pasos">
        <span class="eyebrow">Seguridad</span>
        <h2>Tu solicitud es privada</h2>
        <p>La referencia sola no permite consultar una pre-reserva. También se requiere su clave privada y una sesión iniciada con la cuenta que la creó.</p>
        <div class="nota-seguridad">
          <strong>¿No guardaste la clave?</strong>
          <p>SYNCHRO no puede recuperar la clave original cuando consultas una solicitud. Tendrás que crear una nueva pre-reserva si no la conservaste.</p>
        </div>
        <RouterLink to="/reservar/encomienda" class="boton boton-secundario boton-ancho">
          Nueva pre-reserva <span aria-hidden="true">→</span>
        </RouterLink>
      </aside>
    </div>
  </section>
</template>
