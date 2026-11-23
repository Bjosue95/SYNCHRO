<script setup>
import AppIcon from "../ui/AppIcon.vue";
import { empresa, linkWhatsapp } from "../../data/empresa.js";

const correo = "mailto:" + empresa.correo;
const whatsapp = linkWhatsapp("Hola, quisiera información sobre los servicios de SYNCHRO.");
const mapa = "https://www.google.com/maps/search/?api=1&query=" +
  encodeURIComponent(empresa.direccion);
</script>

<template>
  <section id="contacto" class="syn-contacto">
    <div class="contenedor">
      <div class="syn-contacto-heading">
        <div>
          <span class="syn-contacto-eyebrow">ESTAMOS PARA ESCUCHARTE</span>
          <h2>Conversemos.</h2>
          <p>¿Tienes preguntas sobre SYNCHRO o quieres explorar una colaboración? Estos son nuestros canales oficiales.</p>
        </div>
        <a class="syn-contacto-cta" :href="whatsapp" target="_blank" rel="noopener noreferrer">
          <AppIcon nombre="whatsapp" :tamano="26"/>
          Escribir por WhatsApp
          <span aria-hidden="true">↗</span>
        </a>
      </div>

      <div class="syn-contacto-grid">
        <a class="syn-contacto-item" :href="whatsapp" target="_blank" rel="noopener noreferrer">
          <span class="syn-contacto-icono"><AppIcon nombre="whatsapp" :tamano="32"/></span>
          <span class="syn-contacto-copy">
            <strong>WhatsApp</strong>
            <span>{{ empresa.whatsapp }}</span>
            <small>Escríbenos directamente</small>
          </span>
        </a>
        <a class="syn-contacto-item" :href="correo">
          <span class="syn-contacto-icono"><AppIcon nombre="correo" :tamano="32"/></span>
          <span class="syn-contacto-copy">
            <strong>Correo electrónico</strong>
            <span>{{ empresa.correo }}</span>
            <small>Consultas y cotizaciones</small>
          </span>
        </a>
        <div class="syn-contacto-item no-link">
          <span class="syn-contacto-icono"><AppIcon nombre="reloj" :tamano="32"/></span>
          <span class="syn-contacto-copy">
            <strong>Atención</strong>
            <span>{{ empresa.horario }}</span>
            <small>Horario comercial</small>
          </span>
        </div>
      </div>

      <div class="syn-contacto-address">
        <span class="syn-contacto-address-icon"><AppIcon nombre="ubicacion" :tamano="31"/></span>
        <div>
          <strong>Dirección de contacto</strong>
          <p>{{ empresa.direccion }}</p>
          <a :href="mapa" target="_blank" rel="noopener noreferrer">Consultar dirección en el mapa ↗</a>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.syn-contacto { padding: clamp(55px,6vw,88px) 0; background:var(--bg); }
.syn-contacto-heading { display:flex; justify-content:space-between; align-items:center; gap:35px; margin-bottom:29px; }
.syn-contacto-heading > div { max-width:660px; }
.syn-contacto-eyebrow { font-size:12px; color:var(--accent-text); font-weight:720; letter-spacing:.065em; }
.syn-contacto-heading h2 { color:var(--text-strong); font-size:clamp(31px,3.8vw,47px); font-weight:650; margin:7px 0 12px; letter-spacing:-.04em; }
.syn-contacto-heading p { margin:0; color:var(--text-muted); font-size:14px; line-height:1.7; }
.syn-contacto-cta { display:inline-flex; align-items:center; justify-content:center; gap:12px; flex-shrink:0; padding:14px 19px; border-radius:12px; font-size:13px; text-decoration:none; font-weight:770; color:#062b3a; background:#67dfde; }
.syn-contacto-cta:hover { background:#8fefec; }
.syn-contacto-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:16px; }
.syn-contacto-item { display:flex; min-width:0; align-items:flex-start; gap:15px; padding:24px 21px; text-decoration:none; border:1px solid var(--border); background:var(--surface); border-radius:15px; transition:border-color .2s; }
a.syn-contacto-item:hover { border-color:var(--brand-accent); }
.syn-contacto-icono { display:grid; place-items:center; width:47px; height:47px; border-radius:12px; background:var(--surface-accent); flex-shrink:0; }
.syn-contacto-copy { display:grid; gap:5px; min-width:0; }
.syn-contacto-copy strong { color:var(--text-strong); font-size:13px; }
.syn-contacto-copy > span { color:var(--text); font-weight:720; font-size:13px; overflow-wrap:anywhere; }
.syn-contacto-copy small { color:var(--text-muted); font-size:11px; }
.syn-contacto-address { display:flex; align-items:flex-start; gap:16px; margin-top:20px; padding:22px 24px; border-radius:14px; background:var(--surface-soft); border:1px solid var(--border); }
.syn-contacto-address-icon { display:grid; place-items:center; width:45px; height:45px; flex-shrink:0; background:var(--surface-accent); border-radius:12px; }
.syn-contacto-address strong { color:var(--text-strong); font-size:13px; }
.syn-contacto-address p { color:var(--text-muted); font-size:13px; line-height:1.65; margin:6px 0 8px; }
.syn-contacto-address a { text-decoration:none; color:var(--accent-text); font-weight:720; font-size:12px; }
.syn-contacto-address a:hover { text-decoration:underline; }
@media(max-width:940px) { .syn-contacto-grid { grid-template-columns:repeat(2,minmax(0,1fr)); } .syn-contacto-heading { align-items:flex-start; flex-direction:column; gap:19px; } }
@media(max-width:580px) { .syn-contacto-grid { grid-template-columns:1fr; gap:11px; } .syn-contacto-item { padding:19px; } .syn-contacto-address { padding:18px; } }
</style>
