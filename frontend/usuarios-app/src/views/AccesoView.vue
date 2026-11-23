<script setup>
import { computed, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { crearCuenta, iniciarSesion } from "../services/sesion.js";
import AppIcon from "../components/ui/AppIcon.vue";
import {
  usuarioValido, contrasenaValida, normalizarTelefono
} from "../services/validaciones.js";

const route = useRoute();
const router = useRouter();
const modo = ref(route.query.registro === "1" ? "registro" : "ingreso");
const formulario = reactive({ nombreUsuario: "", contrasena: "", telefono: "" });
const enviando = ref(false);
const error = ref("");
const esRegistro = computed(() => modo.value === "registro");

function cambiarModo(nuevo) {
  modo.value = nuevo;
  formulario.contrasena = "";
  error.value = "";
}

function destinoSeguro() {
  const destino = route.query.volver;
  return typeof destino === "string" &&
    /^\/(mi-cuenta|reservar\/encomienda|consultar)(\?|$)/.test(destino)
    ? destino : "/mi-cuenta";
}

async function enviar() {
  if (enviando.value) return;
  error.value = "";
  const nombreUsuario = formulario.nombreUsuario.trim();
  if (!usuarioValido(nombreUsuario)) {
    error.value = "Usa entre 3 y 80 caracteres: letras, números, punto, guion o guion bajo.";
    return;
  }
  if (esRegistro.value && !contrasenaValida(formulario.contrasena)) {
    error.value = "Tu contraseña debe contener entre 12 y 128 caracteres.";
    return;
  }
  const telefono = esRegistro.value ? normalizarTelefono(formulario.telefono) : null;
  if (esRegistro.value && !telefono) {
    error.value = "Escribe el teléfono con código de país, por ejemplo +504XXXXXXXX.";
    return;
  }

  enviando.value = true;
  try {
    const credenciales = {
      nombreUsuario,
      contrasena: formulario.contrasena
    };
    if (esRegistro.value) {
      await crearCuenta({ ...credenciales, telefono });
    } else {
      await iniciarSesion(credenciales);
    }
    formulario.contrasena = "";
    await router.replace(destinoSeguro());
  } catch (e) {
    error.value = e?.estado === 401
      ? "Usuario o contraseña incorrectos."
      : (e?.message || "No fue posible acceder a SYNCHRO.");
  } finally {
    enviando.value = false;
  }
}
</script>

<template>
  <section class="acceso-pagina">
    <div class="contenedor acceso-diseno">
      <div class="acceso-presentacion">
        <span class="eyebrow">TU CUENTA SYNCHRO</span>
        <h1>Un acceso.<br>Todo conectado.</h1>
        <p>
          Gestiona tus solicitudes de forma sencilla y mantén
          tu información protegida en un solo lugar.
        </p>
        <div class="beneficios">
          <span>
            <span class="beneficio-icono"><AppIcon nombre="seguridad" :tamano="25"/></span>
            Acceso protegido
          </span>
          <span>
            <span class="beneficio-icono"><AppIcon nombre="reservas" :tamano="25"/></span>
            Solicitudes desde tu cuenta
          </span>
          <span>
            <span class="beneficio-icono"><AppIcon nombre="whatsapp" :tamano="25"/></span>
            Comunicación con tu destinatario
          </span>
        </div>
      </div>

      <div class="formulario-tarjeta acceso-formulario">
        <div class="pestanas" role="group" aria-label="Tipo de acceso">
          <button type="button" :class="{ activo: !esRegistro }"
            :aria-pressed="!esRegistro" @click="cambiarModo('ingreso')">
            Ingresar
          </button>
          <button type="button" :class="{ activo: esRegistro }"
            :aria-pressed="esRegistro" @click="cambiarModo('registro')">
            Crear cuenta
          </button>
        </div>

        <h2>{{ esRegistro ? "Crea tu cuenta" : "Bienvenido a SYNCHRO" }}</h2>
        <p class="texto-suave">
          {{ esRegistro ? "Registra tus datos para gestionar tus solicitudes."
            : "Accede con las credenciales de tu cuenta." }}
        </p>

        <p v-if="route.query.error === 'servidor'" class="alerta" role="status">
          No se pudo comprobar tu sesión. Inténtalo nuevamente.
        </p>

        <form class="formulario" @submit.prevent="enviar">
          <label class="campo">
            <span>Nombre de usuario</span>
            <input v-model="formulario.nombreUsuario" type="text" required
              maxlength="80" autocomplete="username"
              placeholder="Tu nombre de usuario"/>
          </label>

          <label v-if="esRegistro" class="campo">
            <span>Número de WhatsApp</span>
            <input v-model="formulario.telefono" type="tel"
              required maxlength="16" autocomplete="tel"
              inputmode="tel" placeholder="+504XXXXXXXX"/>
            <small>Incluye el código de país.</small>
          </label>

          <label class="campo">
            <span>Contraseña</span>
            <input v-model="formulario.contrasena" type="password" required
              :minlength="esRegistro ? 12 : undefined"
              maxlength="128"
              :autocomplete="esRegistro ? 'new-password' : 'current-password'"
              :placeholder="esRegistro ? 'Mínimo 12 caracteres' : 'Tu contraseña'"/>
          </label>

          <p v-if="error" class="alerta" role="alert">{{ error }}</p>

          <button type="submit" class="boton boton-primario boton-ancho"
            :disabled="enviando" :aria-busy="enviando">
            {{ enviando ? "Comprobando..." : esRegistro ? "Crear mi cuenta" : "Ingresar a mi cuenta" }}
            <AppIcon nombre="flecha" :tamano="20"/>
          </button>
        </form>

        <p class="nota-pie">Tu acceso es personal y protegido.</p>
      </div>
    </div>
  </section>
</template>
