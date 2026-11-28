<script setup lang="ts">
import { computed, nextTick, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { crearCuenta, iniciarSesion } from "../services/sesion.js";
import { ErrorApi } from "../services/clientesApi.js";
import AppIcon from "../components/ui/AppIcon.vue";
import AccessHighlights from "../components/ui/AccessHighlights.vue";
import SynButton from "../components/ui/SynButton.vue";
import {
  usuarioValido, contrasenaValida, normalizarTelefono
} from "../services/validaciones.js";

const route = useRoute();
const router = useRouter();
const modo = ref<"registro" | "ingreso">(route.query.registro === "1" ? "registro" : "ingreso");
const formulario = reactive({ nombreUsuario: "", contrasena: "", telefono: "" });
type Campo = keyof typeof formulario;
const errores = reactive<Record<Campo, string>>({ nombreUsuario: "", contrasena: "", telefono: "" });
const tocados = reactive<Record<Campo, boolean>>({ nombreUsuario: false, contrasena: false, telefono: false });
const formularioRef = ref<HTMLFormElement | null>(null);
const alertaRef = ref<HTMLElement | null>(null);
const mostrarContrasena = ref(false);
const intentado = ref(false);
const enviando = ref(false);
const error = ref("");
const esRegistro = computed(() => modo.value === "registro");
const hayErrores = computed(() => Object.values(errores).some(Boolean));

function cambiarModo(nuevo: "registro" | "ingreso") {
  if (enviando.value) return;
  modo.value = nuevo;
  formulario.contrasena = "";
  mostrarContrasena.value = false;
  intentado.value = false;
  for (const campo of Object.keys(errores) as Campo[]) { errores[campo] = ""; tocados[campo] = false; }
  error.value = "";
}

function validarCampo(campo: Campo) {
  if (campo === "nombreUsuario") {
    errores[campo] = !formulario.nombreUsuario.trim() ? "Escribe tu nombre de usuario."
      : !usuarioValido(formulario.nombreUsuario) ? "Usa entre 3 y 80 caracteres: letras, números, punto, guion o guion bajo." : "";
  } else if (campo === "contrasena") {
    errores[campo] = !formulario.contrasena ? "Escribe tu contraseña."
      : esRegistro.value && !contrasenaValida(formulario.contrasena) ? "Tu contraseña debe contener entre 12 y 128 caracteres."
      : formulario.contrasena.length > 128 ? "La contraseña admite hasta 128 caracteres." : "";
  } else {
    errores[campo] = !esRegistro.value ? "" : !formulario.telefono.trim() ? "Escribe tu número de WhatsApp."
      : !normalizarTelefono(formulario.telefono) ? "Escribe el teléfono con código de país, por ejemplo +504XXXXXXXX." : "";
  }
}
function revisarCampo(campo: Campo, alSalir = false) {
  if (alSalir) tocados[campo] = true;
  if (intentado.value || tocados[campo]) validarCampo(campo);
  if (!alSalir && !enviando.value) error.value = "";
}

function mensajeError(causa: unknown) {
  const estado = causa instanceof ErrorApi ? causa.estado : null;
  if (causa instanceof ErrorApi && causa.codigo === "csrf") return "No pudimos verificar la seguridad de la solicitud. Actualiza la página e inténtalo nuevamente.";
  if (estado === 0) return "No pudimos conectar con el servidor. Revisa tu conexión e inténtalo nuevamente.";
  if (estado === 429) return "Demasiadas solicitudes. Espera antes de volver a intentarlo.";
  if (estado === 423) return "El servicio ha restringido el acceso. Contacta con SYNCHRO si el problema continúa.";
  if (estado === 403) return "El servidor no autorizó la solicitud. Actualiza la página e inténtalo nuevamente.";
  if (estado >= 500) return "El servicio no está disponible en este momento. Inténtalo nuevamente más tarde.";
  if (estado === 400 || estado === 422) return "No pudimos validar los datos. Revisa los campos e inténtalo nuevamente.";
  // No mostrar mensajes del servidor que puedan revelar si un usuario existe.
  return esRegistro.value
    ? "No pudimos crear la cuenta con esos datos. Revisa la información e inténtalo nuevamente."
    : "No pudimos iniciar sesión. Verifica tus datos e inténtalo nuevamente";
}

function destinoSeguro() {
  const destino = route.query.volver;
  return typeof destino === "string" &&
    /^\/(bienvenida|mis-reservas|mi-perfil|mi-cuenta|reservar(?:\/encomienda)?|consultar|(?:encomiendas|reservas)\/\d+)(\?|$)/.test(destino)
    ? destino : "/bienvenida";
}

async function enviar() {
  if (enviando.value) return;
  error.value = "";
  intentado.value = true;
  for (const campo of Object.keys(errores) as Campo[]) validarCampo(campo);
  if (hayErrores.value) {
    await nextTick();
    formularioRef.value?.querySelector<HTMLInputElement>('[aria-invalid="true"]')?.focus();
    return;
  }
  const nombreUsuario = formulario.nombreUsuario.trim();
  const telefono = esRegistro.value ? normalizarTelefono(formulario.telefono) : null;

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
    mostrarContrasena.value = false;
    await router.replace(destinoSeguro());
  } catch (e) {
    error.value = mensajeError(e);
    await nextTick();
    alertaRef.value?.focus({ preventScroll: true });
    alertaRef.value?.scrollIntoView({ block: "nearest" });
  } finally {
    enviando.value = false;
  }
}
</script>

<template>
  <section class="acceso-pagina syn-grid-surface" aria-labelledby="titulo-acceso">
    <div class="contenedor acceso-diseno">
      <AccessHighlights />

      <div class="formulario-tarjeta acceso-formulario">
        <header class="acceso-form-header">
          <h1 id="titulo-acceso">
            {{ esRegistro ? "Crea tu cuenta" : "Inicia sesión" }}
          </h1>
          <p>
            {{ esRegistro
              ? "Completa tus datos para comenzar."
              : "Reserva un casillero y consulta tus reservas." }}
          </p>
        </header>

        <p v-if="route.query.cuentaEliminada === '1'" class="alerta" role="status">Tu cuenta se eliminó correctamente.</p>
        <p v-if="route.query.error === 'servidor'" class="alerta" role="status">
          No pudimos verificar el acceso. Inténtalo de nuevo.
        </p>

        <form ref="formularioRef" class="formulario" novalidate @submit.prevent="enviar" :aria-busy="enviando">
          <p v-if="intentado && hayErrores" class="acceso-validacion" role="alert">Revisa los campos indicados para continuar.</p>
          <div class="campo">
            <label for="acceso-usuario">Nombre de usuario</label>
            <input id="acceso-usuario" v-model="formulario.nombreUsuario" type="text" required
              maxlength="80" autocomplete="username" autocapitalize="none" :spellcheck="false" :readonly="enviando"
              :aria-invalid="!!errores.nombreUsuario" :aria-describedby="errores.nombreUsuario ? 'usuario-error' : 'usuario-ayuda'"
              placeholder="Tu usuario" @input="revisarCampo('nombreUsuario')" @blur="revisarCampo('nombreUsuario', true)" />
            <small v-if="!errores.nombreUsuario" id="usuario-ayuda">Usa tu nombre de usuario, no tu correo electrónico.</small>
            <p v-else id="usuario-error" class="campo-error"><AppIcon nombre="alerta" :tamano="17" />{{ errores.nombreUsuario }}</p>
          </div>

          <div v-if="esRegistro" class="campo">
            <label for="acceso-telefono">Número de WhatsApp</label>
            <input id="acceso-telefono" v-model="formulario.telefono" type="tel" required maxlength="16"
              :readonly="enviando" :aria-invalid="!!errores.telefono" :aria-describedby="errores.telefono ? 'telefono-error' : undefined"
              autocomplete="tel" inputmode="tel" placeholder="+504XXXXXXXX"
              @input="revisarCampo('telefono')" @blur="revisarCampo('telefono', true)" />
            <p v-if="errores.telefono" id="telefono-error" class="campo-error"><AppIcon nombre="alerta" :tamano="17" />{{ errores.telefono }}</p>
          </div>

          <div class="campo">
            <label for="acceso-contrasena">Contraseña</label>
            <div class="acceso-clave">
            <input id="acceso-contrasena" v-model="formulario.contrasena" :type="mostrarContrasena ? 'text' : 'password'" required
              :minlength="esRegistro ? 12 : undefined" maxlength="128"
              :readonly="enviando" :aria-invalid="!!errores.contrasena"
              :aria-describedby="errores.contrasena ? 'contrasena-error' : esRegistro ? 'contrasena-ayuda' : undefined"
              :autocomplete="esRegistro ? 'new-password' : 'current-password'"
              :placeholder="esRegistro ? '12 caracteres como mínimo' : 'Tu contraseña'"
              @input="revisarCampo('contrasena')" @blur="revisarCampo('contrasena', true)" />
            <button type="button" class="acceso-ver-clave" aria-controls="acceso-contrasena"
              :aria-label="mostrarContrasena ? 'Ocultar contraseña' : 'Mostrar contraseña'" :aria-pressed="mostrarContrasena"
              @mousedown.prevent @click="mostrarContrasena = !mostrarContrasena">
              <AppIcon :nombre="mostrarContrasena ? 'ojo-oculto' : 'ojo'" :tamano="23" />
            </button>
            </div>
            <small v-if="esRegistro && !errores.contrasena" id="contrasena-ayuda">Entre 12 y 128 caracteres.</small>
            <p v-if="errores.contrasena" id="contrasena-error" class="campo-error"><AppIcon nombre="alerta" :tamano="17" />{{ errores.contrasena }}</p>
          </div>

          <p v-if="error" ref="alertaRef" class="alerta acceso-error" role="alert" tabindex="-1">{{ error }}</p>

          <SynButton tipo="submit" class="acceso-enviar" :cargando="enviando">
            {{ enviando ? (esRegistro ? "Creando cuenta..." : "Ingresando...") : esRegistro ? "Crear cuenta" : "Ingresar" }}
            <AppIcon nombre="flecha" :tamano="19" />
          </SynButton>
        </form>

        <div class="acceso-cambiar">
          <span>{{ esRegistro ? "¿Ya tienes una cuenta?" : "¿Aún no tienes cuenta?" }}</span>
          <button type="button" :disabled="enviando" @click="cambiarModo(esRegistro ? 'ingreso' : 'registro')">
            {{ esRegistro ? "Iniciar sesión" : "Crear cuenta" }}
          </button>
        </div>
      </div>
    </div>
  </section>
</template>
