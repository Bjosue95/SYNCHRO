<script setup>
import { nextTick, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";

import AuthLayout from "../layouts/AuthLayout.vue";
import AdminIcon from "../components/AdminIcon.vue";
import { apiAdmin, consultarSesion } from "../services/adminApi";

const ruta = useRoute();
const router = useRouter();

const usuario = ref("");
const clave = ref("");
const visible = ref(false);
const cargando = ref(false);
const error = ref("");
const alerta = ref(null);

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
    visible.value = false;
    await router.replace(destinoSeguro());
  } catch (e) {
    error.value = e?.status === 401 || e?.status === 400
      ? "No pudimos iniciar sesión. Verifica tu usuario y contraseña."
      : e?.status === 429 ? "Demasiados intentos. Espera un momento antes de volver a intentarlo."
      : e?.status === 403 ? "Tu cuenta no dispone de acceso administrativo."
      : "No pudimos conectar con el servicio de acceso. Inténtalo nuevamente.";
    visible.value = false;
    await nextTick();
    alerta.value?.focus();
  } finally {
    cargando.value = false;
  }
}
</script>

<template>
  <AuthLayout>
    <section class="access-card" aria-labelledby="access-title">
      <div class="access-emblem"><AdminIcon name="shield" :size="30"/></div>
      <div class="access-heading">
        <h1 id="access-title">Iniciar sesión</h1>
        <p>Ingresa tus credenciales para acceder al panel administrativo.</p>
      </div>
      <form class="access-form" :aria-busy="cargando" @submit.prevent="entrar">
        <div class="access-field">
          <label for="admin-usuario">Nombre de usuario</label>
          <div class="access-input">
            <AdminIcon name="user" :size="21"/>
            <input id="admin-usuario" v-model.trim="usuario" name="username" type="text"
              autocomplete="username" autocapitalize="none" spellcheck="false"
              placeholder="Tu nombre de usuario" :disabled="cargando" required/>
          </div>
        </div>
        <div class="access-field">
          <label for="admin-clave">Contraseña</label>
          <div class="access-input access-password">
            <AdminIcon name="lock" :size="21"/>
            <input id="admin-clave" v-model="clave" name="password"
              :type="visible ? 'text' : 'password'" autocomplete="current-password"
              placeholder="Tu contraseña" :disabled="cargando" required/>
            <button type="button" class="password-toggle" aria-controls="admin-clave"
              :aria-label="visible ? 'Ocultar contraseña' : 'Mostrar contraseña'"
              :aria-pressed="visible" :disabled="cargando" @mousedown.prevent @click="visible = !visible">
              <AdminIcon :name="visible ? 'eye-off' : 'eye'" :size="22"/>
            </button>
          </div>
        </div>
        <p v-if="error" ref="alerta" tabindex="-1" class="access-error" role="alert">
          <AdminIcon name="info" :size="19"/><span>{{ error }}</span>
        </p>
        <button class="access-submit" type="submit" :disabled="cargando" :aria-busy="cargando">
          <span v-if="cargando" class="access-spinner" aria-hidden="true"></span>
          {{ cargando ? "Verificando acceso..." : "Iniciar sesión" }}
          <AdminIcon v-if="!cargando" name="arrow" :size="21"/>
        </button>
      </form>
      <div class="access-note"><AdminIcon name="lock" :size="18"/><p>Acceso reservado al personal autorizado de SYNCHRO.</p></div>
    </section>
  </AuthLayout>
</template>

<style scoped>
.access-card { width:100%; min-width:0; padding:clamp(24px,3.2vw,40px); align-self:center; background:var(--auth-card); border:1px solid var(--auth-border); border-radius:24px; box-shadow:var(--auth-shadow); }
.access-emblem { display:grid; place-items:center; width:60px; height:60px; border:1px solid var(--auth-border); border-radius:17px; background:var(--auth-soft); color:var(--auth-accent-text); margin-bottom:19px; }
.access-heading { margin-bottom:26px; }
.access-heading h1 { margin:0 0 8px; font-size:clamp(28px,3vw,36px); line-height:1.18; letter-spacing:-.04em; font-weight:750; color:var(--auth-text); }
.access-heading p { margin:0; color:var(--auth-muted); font-size:14px; line-height:1.6; }
.access-form { display:grid; gap:19px; }
.access-field { display:grid; gap:8px; min-width:0; }
.access-field label { color:var(--auth-text); font-size:13px; font-weight:650; }
.access-input { position:relative; display:flex; align-items:center; min-width:0; border:1px solid var(--auth-input-border); border-radius:12px; background:var(--auth-input); color:var(--auth-muted); }
.access-input > svg { position:absolute; left:14px; pointer-events:none; }
.access-field input { display:block; width:100%; min-width:0; min-height:54px; padding:12px 14px 12px 46px; border:0; border-radius:inherit; background:transparent; color:var(--auth-text); font:inherit; font-size:16px; }
.access-field input::placeholder { color:var(--auth-muted); opacity:.9; }
.access-input:focus-within { border-color:var(--auth-accent-text); outline:2px solid var(--auth-ring); outline-offset:2px; }
.access-field input:focus-visible { outline:none; }
.access-password input { padding-right:58px; }
.password-toggle { position:absolute; right:4px; top:50%; transform:translateY(-50%); display:grid; place-items:center; width:46px; height:46px; border:0; border-radius:9px; color:var(--auth-accent-text); background:transparent; }
.password-toggle:hover:not(:disabled) { background:var(--auth-soft); }
.access-error { display:flex; align-items:flex-start; gap:9px; padding:12px; border:1px solid var(--auth-error-border); border-radius:10px; background:var(--auth-error-bg); color:var(--auth-error-text); font-size:13px; line-height:1.5; margin:0; }
.access-error svg { flex:none; margin-top:1px; }
.access-submit { display:flex; align-items:center; justify-content:center; gap:10px; width:100%; min-height:54px; margin-top:3px; border:1px solid transparent; border-radius:12px; background:var(--auth-action); color:var(--auth-on-action); font:inherit; font-size:15px; font-weight:700; transition:background .18s; }
.access-submit:hover:not(:disabled) { background:var(--auth-action-hover); }
.access-submit:disabled { opacity:.7; cursor:wait; }
.access-spinner { width:18px; height:18px; border:2px solid currentColor; border-right-color:transparent; border-radius:50%; animation:access-spin .8s linear infinite; }
.access-note { display:flex; align-items:center; gap:9px; margin-top:22px; padding-top:18px; border-top:1px solid var(--auth-border); color:var(--auth-muted); }
.access-note svg { flex:none; color:var(--auth-accent-text); }
.access-note p { margin:0; font-size:12px; line-height:1.5; }
button:focus-visible,.access-error:focus-visible { outline:3px solid var(--auth-ring); outline-offset:3px; }
@keyframes access-spin { to { transform:rotate(360deg); } }
@media(max-width:480px) { .access-card { padding:23px; border-radius:18px; } .access-emblem { width:48px; height:48px; margin-bottom:14px; } .access-form { gap:15px; } }
@media(max-height:650px) { .access-card { padding-block:22px; } .access-heading { margin-bottom:18px; } .access-emblem { margin-bottom:12px; width:48px; height:48px; } .access-form { gap:13px; } .access-note { margin-top:16px; padding-top:12px; } }
@media(prefers-reduced-motion:reduce) { .access-submit { transition:none; } .access-spinner { animation:none; } }
</style>
