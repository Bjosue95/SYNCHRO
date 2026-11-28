<script setup>
import { ref, watch } from "vue";
import { RouterLink } from "vue-router";
import BrandLogo from "./BrandLogo.vue";
import AppIcon from "./AppIcon.vue";
import SynButton from "./SynButton.vue";
import { useCookies } from "../../composables/useCookies.js";

const { preferencias, configurando, avisoVisible, mapaPermitido, guardarPreferencias,
  abrirPreferencias, cerrarPreferencias } = useCookies();
const opcionMapa = ref(false);
watch(configurando, abierta => { if (abierta) opcionMapa.value = mapaPermitido.value; });
</script>

<template>
  <Teleport to="body">
    <section v-if="avisoVisible" class="syn-cookie" aria-labelledby="syn-cookie-titulo">
      <div class="syn-cookie-cabecera">
        <BrandLogo compacto />
        <button v-if="configurando && preferencias" type="button" class="syn-cookie-cerrar"
          aria-label="Cerrar preferencias sin cambios" @click="cerrarPreferencias">×</button>
      </div>
      <h2 id="syn-cookie-titulo">{{ configurando ? "Preferencias de cookies" : "Tu privacidad importa" }}</h2>
      <p>Usamos cookies esenciales para mantener tu sesión segura y almacenamiento local para recordar tus preferencias. Puedes elegir si permites el mapa externo.</p>
      <RouterLink to="/politica-de-cookies" class="syn-cookie-enlace">Consultar política de cookies</RouterLink>
      <div v-if="configurando" class="syn-cookie-opciones">
        <div class="syn-cookie-fila">
          <div><strong>Esenciales</strong><small>Sesión, seguridad y elección de privacidad.</small></div>
          <span class="syn-cookie-fijo">Siempre activas</span>
        </div>
        <div class="syn-cookie-fila">
          <label for="syn-cookie-mapa"><strong>Mapa externo</strong><small>Permite cargar imágenes de OpenStreetMap.</small></label>
          <input id="syn-cookie-mapa" v-model="opcionMapa" type="checkbox" />
        </div>
      </div>
      <div class="syn-cookie-acciones">
        <SynButton variante="secundario" @click="guardarPreferencias(false)">Solo esenciales</SynButton>
        <SynButton variante="secundario" @click="guardarPreferencias(true)">Aceptar todas</SynButton>
        <SynButton v-if="configurando" @click="guardarPreferencias(opcionMapa)">Guardar preferencias</SynButton>
        <button v-else class="syn-cookie-ajustar" type="button" @click="abrirPreferencias">
          <AppIcon nombre="seguridad" :tamano="17"/> Configurar
        </button>
      </div>
    </section>
  </Teleport>
</template>

<style scoped>
.syn-cookie { position:fixed; z-index:5000; inset:auto 24px 24px auto; width:min(460px,calc(100% - 48px)); box-sizing:border-box; max-height:calc(100dvh - 48px); overflow:auto; padding:22px; border:1px solid var(--border-strong); border-top:3px solid var(--brand-accent); border-radius:17px; background:var(--surface); color:var(--text-strong); box-shadow:var(--shadow-lg); display:grid; gap:12px; }
.syn-cookie-cabecera { display:flex; align-items:center; justify-content:space-between; gap:12px; }
.syn-cookie-cerrar { border:1px solid var(--border); background:var(--surface-soft); color:var(--text); border-radius:8px; width:36px; height:36px; font-size:24px; cursor:pointer; }
.syn-cookie h2 { font-size:21px; line-height:1.2; margin:0; letter-spacing:-.02em; }
.syn-cookie p { color:var(--text-muted); font-size:13px; line-height:1.65; margin:0; }
.syn-cookie-enlace,.syn-cookie-ajustar { color:var(--accent-text); font-size:12px; font-weight:650; text-decoration:underline; text-underline-offset:3px; }
.syn-cookie-acciones { display:grid; grid-template-columns:1fr 1fr; gap:9px; }
.syn-cookie-acciones > :last-child { grid-column:1/-1; }
.syn-cookie-ajustar { display:flex; align-items:center; justify-content:center; gap:8px; background:none; border:0; min-height:36px; cursor:pointer; }
.syn-cookie-opciones { display:grid; gap:8px; }
.syn-cookie-fila { display:flex; justify-content:space-between; align-items:center; gap:12px; padding:12px; background:var(--surface-soft); border:1px solid var(--border); border-radius:10px; font-size:12px; }
.syn-cookie-fila strong,.syn-cookie-fila small { display:block; }
.syn-cookie-fila small { color:var(--text-muted); margin-top:4px; line-height:1.5; }
.syn-cookie-fijo { color:var(--accent-text); flex-shrink:0; font-size:11px; font-weight:650; }
.syn-cookie-fila input { width:20px; height:20px; flex-shrink:0; accent-color:var(--accent-text); }
.syn-cookie :focus-visible { outline:2px solid var(--ring); outline-offset:3px; }
@media(max-width:560px) { .syn-cookie { inset:auto 12px max(12px,env(safe-area-inset-bottom)) 12px; width:auto; padding:16px; max-height:calc(100dvh - 32px); } .syn-cookie-acciones :deep(.syn-button) { padding:9px; font-size:12px; } }
</style>
