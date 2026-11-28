<script setup>
import { RouterLink } from "vue-router";
import BrandLogo from "../ui/BrandLogo.vue";
import AppIcon from "../ui/AppIcon.vue";
import { useCookies } from "../../composables/useCookies.js";
const { abrirPreferencias } = useCookies();
import { empresa } from "../../data/empresa.js";
</script>

<template>
  <footer class="syn-footer">
    <div class="contenedor">
      <div class="syn-footer-intro">
        <div>
          <span class="syn-footer-kicker">SYNCHRO</span>
          <h2>El punto donde todo se conecta.</h2>
          <p>Almacenamiento, encomiendas y conectividad en una sola experiencia.</p>
        </div>
        <span class="syn-footer-emblema" aria-hidden="true"><AppIcon nombre="tecnologia" :tamano="46"/></span>
      </div>

      <div class="syn-footer-grid">
        <div class="syn-footer-brand">
          <RouterLink to="/" class="syn-footer-logo" aria-label="SYNCHRO - Inicio">
            <BrandLogo />
          </RouterLink>
          <p>
            Soluciones inteligentes de almacenamiento, encomiendas,
            temperatura controlada y carga de dispositivos.
          </p>
          <div class="syn-footer-social">
            <a :href="empresa.instagram" target="_blank" rel="noopener noreferrer"
              aria-label="Instagram oficial de SYNCHRO" title="Instagram">
              <AppIcon nombre="instagram" :tamano="23"/>
            </a>
            <a :href="empresa.tiktok" target="_blank" rel="noopener noreferrer"
              aria-label="TikTok oficial de SYNCHRO" title="TikTok">
              <AppIcon nombre="tiktok" :tamano="23"/>
            </a>
          </div>
        </div>

        <div class="syn-footer-column">
          <h3>Soluciones</h3>
          <a href="/#servicios">Almacenamiento personal</a>
          <a href="/#servicios">Encomiendas</a>
          <a href="/#servicios">Temperatura controlada</a>
          <a href="/#servicios">Carga de dispositivos</a>
          <a href="/#seguridad">Seguridad y accesibilidad</a>
          <a href="/#energia">Energía solar y respaldo</a>
        </div>

        <div class="syn-footer-column">
          <h3>Explorar</h3>
          <RouterLink to="/acceso">Mi cuenta</RouterLink>
          <RouterLink to="/reservar/encomienda">Pre-reservas</RouterLink>
          <a href="/#nosotros">Quiénes somos</a>
          <a href="/#ubicaciones">Ubicaciones</a>
          <a href="/#cotizacion">Solicitar cotización</a>
          <a href="/#contacto">Contacto</a>
        </div>

        <div class="syn-footer-column syn-footer-contact">
          <h3>Contacto</h3>
          <a :href="'mailto:' + empresa.correo"><AppIcon nombre="correo" :tamano="17"/>{{ empresa.correo }}</a>
          <a :href="'tel:' + empresa.whatsapp"><AppIcon nombre="telefono" :tamano="17"/>{{ empresa.whatsapp }}</a>
          <p class="syn-footer-line"><AppIcon nombre="reloj" :tamano="17"/>{{ empresa.horario }}</p>
          <h3 class="syn-footer-address-title">Dirección de referencia</h3>
          <p class="syn-footer-line"><AppIcon nombre="ubicacion" :tamano="19"/>{{ empresa.direccion }}</p>
        </div>
      </div>

      <div class="syn-footer-bottom">
        <span>© {{ new Date().getFullYear() }} SYNCHRO. Todos los derechos reservados.</span>
        <div class="syn-footer-privacidad">
          <RouterLink to="/politica-de-cookies">Política de cookies</RouterLink>
          <button type="button" @click="abrirPreferencias">Preferencias de cookies</button>
        </div>
      </div>
    </div>
  </footer>
</template>

<style scoped>
.syn-footer { padding:0; background:var(--surface); border-top:1px solid var(--border); }
.syn-footer-intro { display:flex; justify-content:space-between; align-items:center; gap:20px; padding:42px 0; border-bottom:1px solid var(--border); }
.syn-footer-kicker { color:var(--accent-text); font-size:12px; letter-spacing:.07em; font-weight:720; }
.syn-footer-intro h2 { margin:8px 0 0; color:var(--text-strong); font-size:clamp(25px,3vw,37px); letter-spacing:-.035em; font-weight:650; }
.syn-footer-intro p { color:var(--text-muted); font-size:14px; line-height:1.6; margin:10px 0 0; }
.syn-footer-emblema { display:grid; place-items:center; width:78px; height:78px; flex:none; border-radius:20px; border:1px solid var(--border); background:var(--surface-accent); }
.syn-footer-grid { display:grid; grid-template-columns:minmax(0,1.3fr) repeat(2,minmax(0,.8fr)) minmax(0,1.1fr); gap:clamp(17px,3vw,44px); padding:56px 0 63px; }
.syn-footer-brand p { margin:24px 0 20px; font-size:13px; line-height:1.75; max-width:270px; color:var(--text-muted); }
.syn-footer-logo { display:inline-flex; text-decoration:none; }
.syn-footer-social { display:flex; align-items:center; gap:10px; }
.syn-footer-social a { display:grid; place-items:center; width:42px; height:42px; background:var(--surface-soft); border:1px solid var(--border); border-radius:12px; color:var(--text-strong); }
.syn-footer-social a:hover { background:var(--surface-accent); border-color:var(--brand-accent); }
.syn-footer-social .icono-ui { width:23px; height:23px; }
.syn-footer-column { min-width:0; display:flex; flex-direction:column; align-items:flex-start; gap:14px; }
.syn-footer-column h3 { font-size:14px; font-weight:750; color:var(--text-strong); margin:0 0 9px; }
.syn-footer-column > a { color:var(--text-muted); text-decoration:none; font-size:13px; line-height:1.5; overflow-wrap:anywhere; }
.syn-footer-column > a:hover { color:var(--accent-text); text-decoration:underline; }
.syn-footer-contact p { font-size:12px; color:var(--text-muted); line-height:1.65; margin:0; }
.syn-footer-contact a, .syn-footer-line { display:flex; align-items:flex-start; gap:9px; }
.syn-footer-contact a :deep(svg), .syn-footer-line :deep(svg) { flex:0 0 auto; margin-top:1px; }
.syn-footer-address-title { margin:9px 0 0 !important; }
.syn-footer-bottom { display:flex; justify-content:space-between; flex-wrap:wrap; gap:15px; padding:23px 0; border-top:1px solid var(--border); color:var(--text-subtle); font-size:11px; }
.syn-footer-privacidad { display:flex; flex-wrap:wrap; gap:12px 20px; }
.syn-footer-privacidad a,.syn-footer-privacidad button { color:var(--text-muted); font:inherit; background:none; border:0; padding:0; cursor:pointer; text-decoration:underline; text-underline-offset:3px; }
.syn-footer-privacidad a:focus-visible,.syn-footer-privacidad button:focus-visible { outline:2px solid var(--ring); outline-offset:4px; }
@media(max-width:960px) { .syn-footer-grid { grid-template-columns:repeat(2,minmax(0,1fr)); row-gap:37px; } }
@media(max-width:560px) { .syn-footer-grid { grid-template-columns:1fr 1fr; column-gap:25px; } .syn-footer-brand,.syn-footer-contact { grid-column:span 2; } .syn-footer-intro { align-items:flex-start; } .syn-footer-emblema { display:none; } .syn-footer-bottom { flex-direction:column; } }
@media(max-width:360px) { .syn-footer-grid { grid-template-columns:1fr; } .syn-footer-brand,.syn-footer-contact { grid-column:auto; } }
</style>
