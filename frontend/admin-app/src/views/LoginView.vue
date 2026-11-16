<script setup>
import { onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";

import AuthLayout from "../layouts/AuthLayout.vue";
import { apiAdmin, consultarSesion } from "../services/adminApi";

const ruta = useRoute();
const router = useRouter();

const usuario = ref("");
const clave = ref("");
const visible = ref(false);
const cargando = ref(false);
const error = ref("");

function destinoSeguro() {
  const valor = ruta.query.volver;

  if (
    typeof valor === "string" &&
    (valor === "/admin" || valor.startsWith("/admin/"))
  ) {
    return valor;
  }

  return "/admin";
}

onMounted(async () => {
  try {
    await consultarSesion();
    await router.replace(destinoSeguro());
  } catch {
    // Sin sesion valida: mostrar el formulario.
  }
});

async function entrar() {
  if (cargando.value) return;

  error.value = "";
  cargando.value = true;

  try {
    await apiAdmin("/api/admin/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        nombreUsuario: usuario.value,
        contrasena: clave.value,
      }),
    });

    await consultarSesion();

    clave.value = "";
    await router.replace(destinoSeguro());
  } catch (e) {
    error.value =
      e?.message ||
      "No fue posible iniciar sesión. Inténtalo nuevamente.";
  } finally {
    cargando.value = false;
  }
}
</script>

<template>
  <AuthLayout>
    <section class="access-card" aria-labelledby="access-title">
      <div class="access-heading">
        <h1 id="access-title">Iniciar sesión</h1>
        <p>Ingresa a tu cuenta para administrar SYNCHRO.</p>
      </div>

      <form class="access-form" @submit.prevent="entrar">
        <div class="access-field">
          <label for="admin-usuario">Usuario</label>

          <input
            id="admin-usuario"
            v-model.trim="usuario"
            name="username"
            type="text"
            autocomplete="username"
            autocapitalize="none"
            spellcheck="false"
            placeholder="Nombre de usuario"
            :disabled="cargando"
            required
          />
        </div>

        <div class="access-field">
          <label for="admin-clave">Contraseña</label>

          <div class="access-password">
            <input
              id="admin-clave"
              v-model="clave"
              name="password"
              :type="visible ? 'text' : 'password'"
              autocomplete="current-password"
              placeholder="Ingresa tu contraseña"
              :disabled="cargando"
              required
            />

            <button
              type="button"
              class="password-toggle"
              :aria-label="visible ? 'Ocultar contraseña' : 'Mostrar contraseña'"
              :aria-pressed="visible"
              :disabled="cargando"
              @click="visible = !visible"
            >
              {{ visible ? "Ocultar" : "Mostrar" }}
            </button>
          </div>
        </div>

        <p v-if="error" class="access-error" role="alert">
          {{ error }}
        </p>

        <button
          class="access-submit"
          type="submit"
          :disabled="cargando"
          :aria-busy="cargando"
        >
          {{
            cargando
              ? "Verificando acceso..."
              : "Iniciar sesión"
          }}
        </button>
      </form>
    </section>
  </AuthLayout>
</template>

<style scoped>
.access-card {
  width: min(100%, 410px);
  margin: auto;
  padding: clamp(22px, 3vh, 32px);

  background: #ffffff;
  border: 1px solid #dce6ec;
  border-radius: 14px;

  box-shadow: 0 9px 30px rgba(1, 48, 92, 0.055);
}

.access-heading {
  margin-bottom: 23px;
}

.access-heading h1 {
  margin: 0 0 7px;

  font-size: clamp(26px, 3vw, 31px);
  font-weight: 750;
  line-height: 1.2;
  letter-spacing: -0.035em;

  color: #01305c;
}

.access-heading p {
  margin: 0;

  color: #526b7a;
  font-size: 14px;
  line-height: 1.5;
}

.access-form {
  display: grid;
  gap: 16px;
}

.access-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}

.access-field label {
  color: #163c55;
  font-size: 13px;
  font-weight: 700;
}

.access-field input {
  display: block;
  width: 100%;
  min-width: 0;
  height: 48px;
  padding: 10px 13px;

  border: 1px solid #b8cbd6;
  border-radius: 9px;

  background: #ffffff;
  color: #12344f;

  font: inherit;
  font-size: 16px;
}

.access-field input::placeholder {
  color: #6b8190;
}

.access-field input:hover:not(:disabled) {
  border-color: #739ba9;
}

.access-field input:focus-visible {
  border-color: #008c99;
  outline: 3px solid rgba(0, 185, 199, 0.28);
  outline-offset: 1px;
}

.access-password {
  position: relative;
}

.access-password input {
  padding-right: 94px;
}

.password-toggle {
  position: absolute;
  top: 2px;
  right: 3px;

  min-width: 78px;
  min-height: 44px;
  padding: 8px 10px;

  border: 0;
  border-radius: 7px;

  background: transparent;
  color: #006e80;

  font-size: 13px;
  font-weight: 700;
}

.password-toggle:hover:not(:disabled) {
  background: #e9f6f8;
}

.password-toggle:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 2px;
}

.access-error {
  margin: 0;
  padding: 11px 12px;

  background: #fff0ed;
  border: 1px solid #efc4bd;
  border-radius: 8px;

  color: #852d25;
  font-size: 13px;
  line-height: 1.5;
}

.access-submit {
  display: flex;
  align-items: center;
  justify-content: center;

  width: 100%;
  min-height: 48px;
  margin-top: 3px;
  padding: 10px 16px;

  background: #01305c;
  border: 1px solid #01305c;
  border-radius: 9px;

  color: #ffffff;
  font-size: 15px;
  font-weight: 700;

  cursor: pointer;
}

.access-submit:hover:not(:disabled) {
  background: #084a75;
}

.access-submit:focus-visible {
  outline: 3px solid #00b9c7;
  outline-offset: 3px;
}

.access-submit:disabled {
  opacity: 0.65;
  cursor: wait;
}

@media (max-width: 480px) {
  .access-card {
    padding: 24px 20px;
    border-radius: 12px;
  }

  .access-heading {
    margin-bottom: 20px;
  }
}

@media (max-height: 650px) {
  .access-card {
    padding-top: 20px;
    padding-bottom: 20px;
  }

  .access-heading {
    margin-bottom: 15px;
  }

  .access-form {
    gap: 12px;
  }
}

@media (prefers-reduced-motion: reduce) {
  .access-card *,
  .access-card {
    scroll-behavior: auto;
  }
}
</style>