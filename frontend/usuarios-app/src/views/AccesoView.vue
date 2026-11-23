<script setup>
import { computed, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { crearCuenta, iniciarSesion } from "../services/sesion.js";
import AppIcon from "../components/ui/AppIcon.vue";
import AccessHighlights from "../components/ui/AccessHighlights.vue";
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
  <section class="acceso-pagina" aria-labelledby="titulo-acceso">
    <div class="contenedor acceso-diseno">
      <AccessHighlights />

      <div class="formulario-tarjeta acceso-formulario">
        <header class="acceso-form-header">
          <h1 id="titulo-acceso">
            {{ esRegistro ? "Crea tu cuenta" : "Inicia sesión" }}
          </h1>
          <p>
            {{ esRegistro
              ? "Registra tu usuario y número de WhatsApp"
              : "Accede a tus pre-reservas de encomienda" }}
          </p>
        </header>

        <p v-if="route.query.error === 'servidor'" class="alerta" role="status">
          No pudimos verificar el acceso. Inténtalo de nuevo.
        </p>

        <form class="formulario" @submit.prevent="enviar">
          <label class="campo">
            <span>Nombre de usuario</span>
            <input v-model="formulario.nombreUsuario" type="text" required
              maxlength="80" autocomplete="username" />
          </label>

          <label v-if="esRegistro" class="campo">
            <span>Número de WhatsApp</span>
            <input v-model="formulario.telefono" type="tel" required maxlength="16"
              autocomplete="tel" inputmode="tel" placeholder="+504XXXXXXXX" />
          </label>

          <div class="campo">
            <label for="contrasena-acceso">Contraseña</label>
            <input id="contrasena-acceso" v-model="formulario.contrasena" type="password" required
              :minlength="esRegistro ? 12 : undefined" maxlength="128"
              :autocomplete="esRegistro ? 'new-password' : 'current-password'"
              :aria-describedby="esRegistro ? 'requisito-contrasena' : undefined" />
            <small v-if="esRegistro" id="requisito-contrasena">12 caracteres como mínimo</small>
          </div>

          <p v-if="error" class="alerta" role="alert">{{ error }}</p>

          <button type="submit" class="boton boton-primario boton-ancho"
            :disabled="enviando" :aria-busy="enviando">
            {{ enviando ? "Comprobando..." : esRegistro ? "Crear cuenta" : "Ingresar" }}
            <AppIcon nombre="flecha" />
          </button>
        </form>

        <div class="acceso-cambiar">
          <span>{{ esRegistro ? "¿Ya tienes una cuenta?" : "¿Aún no tienes cuenta?" }}</span>
          <button type="button" @click="cambiarModo(esRegistro ? 'ingreso' : 'registro')">
            {{ esRegistro ? "Iniciar sesión" : "Crear cuenta" }}
          </button>
        </div>
      </div>
    </div>
  </section>
</template>
