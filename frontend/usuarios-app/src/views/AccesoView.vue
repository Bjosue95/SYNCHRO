<script setup>
import { computed, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { crearCuenta, iniciarSesion } from "../services/sesion.js";
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
        <span class="eyebrow portada-etiqueta">Portal privado SYNCHRO</span>
        <h1>Todo comienza<br />con tu cuenta.</h1>
        <p>Solicita y consulta pre-reservas desde un espacio seguro para nuestros clientes.</p>
        <div class="beneficios">
          <span><b>✓</b> Acceso protegido por contraseña</span>
          <span><b>✓</b> Consultas privadas de solicitudes</span>
          <span><b>✓</b> Consentimiento WhatsApp del destinatario</span>
        </div>
      </div>

      <div class="formulario-tarjeta acceso-formulario">
        <div class="pestanas" role="group" aria-label="Tipo de acceso">
          <button type="button" :class="{ activo: !esRegistro }" :aria-pressed="!esRegistro"
            @click="cambiarModo('ingreso')">Ingresar</button>
          <button type="button" :class="{ activo: esRegistro }" :aria-pressed="esRegistro"
            @click="cambiarModo('registro')">Crear cuenta</button>
        </div>

        <h2>{{ esRegistro ? "Crea tu cuenta" : "Bienvenido de nuevo" }}</h2>
        <p class="texto-suave">
          {{ esRegistro ? "Regístrate para acceder a las solicitudes de SYNCHRO."
            : "Ingresa con el usuario y contraseña que registraste." }}
        </p>
        <p v-if="route.query.error === 'servidor'" class="alerta" role="status">
          No se pudo comprobar la sesión. Verifica que el backend esté iniciado.
        </p>

        <form @submit.prevent="enviar" class="formulario">
          <label class="campo">
            <span>Nombre de usuario</span>
            <input v-model="formulario.nombreUsuario" type="text" required maxlength="80"
              autocomplete="username" placeholder="Tu nombre de usuario" />
          </label>
          <label v-if="esRegistro" class="campo">
            <span>WhatsApp / teléfono</span>
            <input v-model="formulario.telefono" type="tel" required maxlength="16"
              autocomplete="tel" inputmode="tel" placeholder="+504XXXXXXXX" />
            <small>Incluye el código de país. Se utilizará para identificar tu cuenta.</small>
          </label>
          <label class="campo">
            <span>Contraseña</span>
            <input v-model="formulario.contrasena" type="password" required
              :minlength="esRegistro ? 12 : undefined" maxlength="128"
              :autocomplete="esRegistro ? 'new-password' : 'current-password'"
              :placeholder="esRegistro ? 'Mínimo 12 caracteres' : 'Tu contraseña'" />
          </label>

          <p v-if="error" class="alerta" role="alert">{{ error }}</p>
          <button type="submit" class="boton boton-primario boton-ancho"
            :disabled="enviando" :aria-busy="enviando">
            {{ enviando ? "Comprobando..." : esRegistro ? "Crear cuenta" : "Iniciar sesión" }}
            <span aria-hidden="true">→</span>
          </button>
        </form>
        <p class="nota-pie">Tu sesión se protege mediante una cookie segura del servidor.</p>
      </div>
    </div>
  </section>
</template>
