<script setup>
import AppIcon from "../ui/AppIcon.vue";
</script>

<template>
  <section id="nosotros" class="syn-about">
    <div class="contenedor">
      <div class="syn-about-top">
        <div class="syn-about-story">
          <span class="syn-about-kicker">CÓMO COMENZÓ SYNCHRO</span>
          <h2>Una idea nacida de lo cotidiano.</h2>
        </div>
        <div class="syn-about-story-copy">
          <p>
            SYNCHRO nació en 2026 como iniciativa de seis estudiantes de
            Administración y Generación de Empresas de la UNAH, Campus Comayagua.
          </p>
          <p>
            Partimos de situaciones que conocemos: cargar la mochila o la laptop
            todo el día, quedarse sin batería y coordinar entregas de encomiendas
            cuando los horarios no coinciden.
          </p>
          <p>
            Queremos conectar esas necesidades con estaciones de autoservicio
            adaptables a distintos espacios de Honduras.
          </p>
        </div>
      </div>

      <div class="syn-about-values">
        <div class="syn-about-block">
          <span class="syn-about-icon"><AppIcon nombre="seguridad" :tamano="29"/></span>
          <h3>Nuestra misión</h3>
          <p>
            Facilitar a personas y negocios el resguardo temporal de pertenencias,
            la entrega y retiro de encomiendas y la carga de dispositivos electrónicos,
            mediante soluciones tecnológicas de autoservicio que integren casilleros
            inteligentes, espacios con temperatura controlada y procesos seguros,
            accesibles y prácticos, brindando comodidad y confianza en sus
            actividades cotidianas.
          </p>
        </div>
        <div class="syn-about-block">
          <span class="syn-about-icon"><AppIcon nombre="ubicacion" :tamano="29"/></span>
          <h3>Hacia dónde vamos</h3>
          <p>
            Consolidarnos como una empresa hondureña referente en soluciones
            inteligentes de almacenamiento y autoservicio, desarrollando una red
            de estaciones tecnológicas en puntos estratégicos del país, reconocida
            por su innovación, seguridad y accesibilidad, que transforme la manera
            en que las personas y los negocios resguardan sus pertenencias,
            gestionan encomiendas y cargan sus dispositivos.
          </p>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.syn-about { padding:clamp(60px,8vw,110px) 0; background:var(--surface); border-top:1px solid var(--border); }
.syn-about-top { display:grid; grid-template-columns: minmax(0,.85fr) minmax(0,1.15fr); gap:clamp(20px,5vw,85px); align-items:start; padding-bottom:55px; }
.syn-about-kicker { font-size:12px; font-weight:750; color:var(--accent-text); letter-spacing:.06em; }
.syn-about-story h2 { margin:17px 0 0; color:var(--text-strong); font-size:clamp(34px,4.3vw,56px); line-height:1.13; letter-spacing:-.048em; font-weight:620; }
.syn-about-story-copy p { color:var(--text-muted); font-size:15px; line-height:1.85; margin:0 0 17px; }
.syn-about-values { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:20px; }
.syn-about-block { padding:clamp(21px,3vw,34px); border:1px solid var(--border); border-radius:16px; background:var(--surface-soft); }
.syn-about-icon { width:52px; height:52px; display:grid; place-items:center; border-radius:13px; background:var(--surface-accent); margin-bottom:20px; }
.syn-about-block h3 { color:var(--text-strong); font-size:21px; letter-spacing:-.02em; font-weight:650; margin:0 0 14px; }
.syn-about-block p { color:var(--text-muted); font-size:13px; line-height:1.82; margin:0; }
@media(max-width:820px) { .syn-about-top { grid-template-columns:1fr; gap:26px; padding-bottom:36px; } }
@media(max-width:620px) { .syn-about-values { grid-template-columns:1fr; } .syn-about-block { padding:22px; } }
</style>
