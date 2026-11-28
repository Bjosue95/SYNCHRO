<script setup>
import AppIcon from "../ui/AppIcon.vue";
import { empresa, linkWhatsapp } from "../../data/empresa.js";

const correo = "mailto:" + empresa.correo;
const whatsapp = linkWhatsapp("Hola, quisiera información sobre los servicios de SYNCHRO.");
const mapa = "https://www.google.com/maps/search/?api=1&query=" +
  encodeURIComponent(empresa.direccion);
</script>

<template>
  <section id="contacto" class="syn-contacto syn-grid-surface" aria-labelledby="syn-contacto-titulo">
    <div class="contenedor">
      <header class="syn-contacto-heading">
        <div>
          <span class="syn-contacto-eyebrow">ESTAMOS PARA ESCUCHARTE</span>
          <h2 id="syn-contacto-titulo">Conversemos.</h2>
          <p>¿Tienes preguntas sobre nuestros servicios, necesitas asistencia o quieres explorar una colaboración? Elige el canal de atención que prefieras.</p>
        </div>
        <a class="syn-contacto-cta" :href="whatsapp" target="_blank" rel="noopener noreferrer">
          <AppIcon nombre="whatsapp" :tamano="24"/>
          <span>Escribir por WhatsApp</span>
          <AppIcon nombre="enlace-externo" :tamano="18"/>
        </a>
      </header>

      <div class="syn-contacto-grid">
        <article class="syn-contacto-item">
          <span class="syn-contacto-icono"><AppIcon nombre="whatsapp" :tamano="32"/></span>
          <div class="syn-contacto-copy">
            <h3>WhatsApp</h3>
            <p class="syn-contacto-dato">{{ empresa.whatsapp }}</p>
            <p>Atención directa y consultas sobre nuestros servicios.</p>
          </div>
        </article>
        <article class="syn-contacto-item">
          <span class="syn-contacto-icono"><AppIcon nombre="correo" :tamano="32"/></span>
          <div class="syn-contacto-copy">
            <h3>Correo electrónico</h3>
            <a class="syn-contacto-dato syn-contacto-mail" :href="correo">{{ empresa.correo }} <AppIcon nombre="flecha" :tamano="17"/></a>
            <p>Consultas, cotizaciones y propuestas de colaboración.</p>
          </div>
        </article>
        <article class="syn-contacto-item">
          <span class="syn-contacto-icono"><AppIcon nombre="reloj" :tamano="32"/></span>
          <div class="syn-contacto-copy">
            <h3>Horario de atención</h3>
            <p class="syn-contacto-dato">{{ empresa.horario }}</p>
            <p>Horario comercial de nuestros canales de atención.</p>
          </div>
        </article>
      </div>

      <div class="syn-contacto-inferior">
        <article class="syn-contacto-address">
          <span class="syn-contacto-icono"><AppIcon nombre="ubicacion" :tamano="32"/></span>
          <div class="syn-contacto-copy">
            <h3>Dirección de contacto</h3>
            <p class="syn-contacto-direccion">{{ empresa.direccion }}</p>
            <a class="syn-contacto-map" :href="mapa" target="_blank" rel="noopener noreferrer">
              Ver en Google Maps <AppIcon nombre="enlace-externo" :tamano="18"/>
            </a>
          </div>
        </article>
        <aside class="syn-contacto-seguridad">
          <span class="syn-contacto-icono"><AppIcon nombre="seguridad" :tamano="32"/></span>
          <div class="syn-contacto-copy">
            <h3>Seguridad en cada operación</h3>
            <p>La solicitud web, el consentimiento y la verificación forman parte del proceso de protección de SYNCHRO.</p>
            <a class="syn-contacto-seguridad-link" href="/#seguridad">Conoce nuestras soluciones de seguridad <AppIcon nombre="flecha" :tamano="17"/></a>
          </div>
        </aside>
      </div>
    </div>
  </section>
</template>

<style scoped>
.syn-contacto { padding:clamp(52px,6vw,90px) 0; background:var(--bg); }
.syn-contacto-heading { display:flex; justify-content:space-between; align-items:center; gap:28px; margin-bottom:clamp(23px,3vw,36px); }
.syn-contacto-heading > div { max-width:660px; min-width:0; }
.syn-contacto-eyebrow { font-size:12px; color:var(--accent-text); font-weight:650; letter-spacing:.065em; }
.syn-contacto-heading h2 { color:var(--text-strong); font-size:clamp(32px,3.8vw,48px); line-height:1.2; font-weight:700; margin:9px 0 12px; letter-spacing:-.04em; }
.syn-contacto-heading p { margin:0; color:var(--text-muted); font-size:15px; line-height:1.7; max-width:590px; }
.syn-contacto-cta { display:inline-flex; align-items:center; justify-content:center; gap:11px; flex-shrink:0; min-height:50px; padding:13px 19px; border-radius:12px; font-size:14px; text-decoration:none; font-weight:650; color:var(--on-accent); background:var(--brand-accent); transition:background .18s; }
.syn-contacto-cta:hover { background:var(--accent-hover); }
.syn-contacto-cta :deep(.icono-ui) { filter:brightness(0); }
.syn-contacto-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:16px; }
.syn-contacto-item,.syn-contacto-address,.syn-contacto-seguridad { display:flex; min-width:0; align-items:flex-start; gap:16px; padding:clamp(19px,2.2vw,27px); border:1px solid var(--border); background:var(--surface); border-radius:18px; box-shadow:var(--shadow-sm); }
.syn-contacto-item { flex-direction:column; gap:15px; }
.syn-contacto-icono { display:grid; place-items:center; width:50px; height:50px; border:1px solid var(--border); border-radius:13px; background:var(--surface-accent); flex-shrink:0; }
.syn-contacto-copy { display:grid; gap:8px; min-width:0; }
.syn-contacto-copy h3 { margin:0; color:var(--text-strong); font-size:16px; line-height:1.35; font-weight:650; }
.syn-contacto-copy p { margin:0; color:var(--text-muted); font-size:13px; line-height:1.6; overflow-wrap:anywhere; font-weight:400; }
.syn-contacto-copy .syn-contacto-dato { color:var(--text-strong); font-size:15px; font-weight:500; line-height:1.5; overflow-wrap:anywhere; }
.syn-contacto-mail { display:inline-flex; align-items:center; flex-wrap:wrap; gap:5px; text-decoration:none; width:fit-content; }
.syn-contacto-mail:hover { color:var(--accent-text); text-decoration:underline; }
.syn-contacto-inferior { display:grid; grid-template-columns:minmax(0,1.35fr) minmax(0,1fr); gap:16px; margin-top:16px; }
.syn-contacto-address,.syn-contacto-seguridad { height:100%; }
.syn-contacto-seguridad { background:var(--surface-soft); border-left:3px solid var(--brand-accent); }
.syn-contacto-direccion { max-width:480px; }
.syn-contacto-map { display:inline-flex; align-items:center; justify-content:center; gap:9px; width:fit-content; min-height:44px; padding:9px 13px; margin-top:4px; border:1px solid var(--border-strong); border-radius:10px; text-decoration:none; color:var(--text-strong); background:var(--surface); font-size:13px; font-weight:650; }
.syn-contacto-map:hover { background:var(--surface-hover); border-color:var(--brand-accent); }
.syn-contacto-seguridad-link { display:inline-flex; align-items:center; gap:6px; width:fit-content; min-height:44px; color:var(--accent-text); font-size:13px; font-weight:600; text-decoration:none; }
.syn-contacto-seguridad-link:hover { text-decoration:underline; }
.syn-contacto a:focus-visible { outline:3px solid var(--ring); outline-offset:3px; }
@media(max-width:940px) { .syn-contacto-heading { align-items:flex-start; flex-direction:column; gap:19px; } .syn-contacto-inferior { grid-template-columns:1fr; } }
@media(max-width:700px) { .syn-contacto-grid { grid-template-columns:1fr; gap:12px; } .syn-contacto-item { flex-direction:row; } .syn-contacto-inferior { gap:12px; margin-top:12px; } }
@media(max-width:400px) { .syn-contacto-cta { width:100%; } .syn-contacto-item,.syn-contacto-address,.syn-contacto-seguridad { gap:12px; padding:16px; } .syn-contacto-icono { width:42px; height:42px; } .syn-contacto-icono :deep(svg) { width:27px; height:27px; } }
@media(prefers-reduced-motion:reduce) { .syn-contacto-cta { transition:none; } }
</style>
