<script setup>
import AppIcon from "../ui/AppIcon.vue";
</script>

<template>
  <section id="nosotros" class="syn-about syn-grid-surface" aria-labelledby="syn-about-titulo">
    <div class="contenedor">
      <div class="syn-about-top">
        <div class="syn-about-story">
          <span class="syn-about-kicker">CÓMO COMENZÓ SYNCHRO</span>
          <h2 id="syn-about-titulo">Una idea nacida <span>de lo cotidiano.</span></h2>
          <div class="syn-about-origin" aria-label="Origen de SYNCHRO">
            <div class="syn-about-stat"><strong>2026</strong><span>Año de origen</span></div>
            <div class="syn-about-stat"><strong>6</strong><span>Estudiantes fundadores</span></div>
          </div>
          <div class="syn-about-campus">
            <AppIcon nombre="instituciones" :tamano="25" tema-forzado="oscuro"/>
            <span>UNAH · Campus Comayagua</span>
          </div>
        </div>
        <div class="syn-about-story-copy">
          <h3>Nuestra historia</h3>
          <p class="syn-about-lead">
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

      <div class="syn-about-purpose">
        <header class="syn-about-purpose-heading"><h3>Nuestro propósito</h3><p>Lo que nos impulsa y lo que queremos alcanzar.</p></header>
        <div class="syn-about-values">
        <article class="syn-about-block syn-about-mision">
          <div class="syn-about-card-header">
            <span class="syn-about-icon"><AppIcon nombre="comunidad" :tamano="32"/></span>
            <div><span class="syn-about-card-label">Nuestro compromiso</span><h4>Nuestra misión</h4></div>
          </div>
          <p>
            Facilitar a personas y negocios el resguardo temporal de pertenencias,
            la entrega y retiro de encomiendas y la carga de dispositivos electrónicos,
            mediante soluciones tecnológicas de autoservicio que integren casilleros
            inteligentes, espacios con temperatura controlada y procesos seguros,
            accesibles y prácticos, brindando comodidad y confianza en sus
            actividades cotidianas.
          </p>
        </article>
        <article class="syn-about-block syn-about-vision">
          <div class="syn-about-card-header">
            <span class="syn-about-icon"><AppIcon nombre="mapa" :tamano="32"/></span>
            <div><span class="syn-about-card-label">Hacia dónde vamos</span><h4>Nuestra visión</h4></div>
          </div>
          <p>
            Consolidarnos como una empresa hondureña referente en soluciones
            inteligentes de almacenamiento y autoservicio, desarrollando una red
            de estaciones tecnológicas en puntos estratégicos del país, reconocida
            por su innovación, seguridad y accesibilidad, que transforme la manera
            en que las personas y los negocios resguardan sus pertenencias,
            gestionan encomiendas y cargan sus dispositivos.
          </p>
        </article>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.syn-about { padding:clamp(48px,6vw,85px) 0; background:var(--surface); border-top:1px solid var(--border); }
.syn-about-top { display:grid; grid-template-columns:minmax(0,.95fr) minmax(0,1.05fr); gap:clamp(18px,3vw,36px); align-items:stretch; }
.syn-about-story { display:flex; flex-direction:column; justify-content:center; gap:18px; min-width:0; padding:clamp(24px,3vw,38px); border:1px solid #286681; border-radius:22px; background-color:#01305c; background-image:linear-gradient(rgba(94,198,232,.08) 1px,transparent 1px),linear-gradient(90deg,rgba(94,198,232,.08) 1px,transparent 1px),linear-gradient(135deg,#041c34,#084765 68%,#086878); background-size:42px 42px,42px 42px,100% 100%; color:#fff; }
.syn-about-kicker { font-size:11px; font-weight:650; color:#a3f5f3; letter-spacing:.07em; }
.syn-about-story h2 { margin:0; color:#fff; font-size:clamp(31px,3.4vw,46px); line-height:1.12; letter-spacing:-.04em; font-weight:700; }
.syn-about-story h2 span { color:#83e3e9; }
.syn-about-origin { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:12px; }
.syn-about-stat { display:grid; gap:5px; min-width:0; padding:14px 16px; border:1px solid rgba(180,237,241,.32); border-radius:13px; background:rgba(255,255,255,.075); }
.syn-about-stat strong { color:#fff; font-size:clamp(25px,2.7vw,35px); font-weight:650; line-height:1.1; font-variant-numeric:tabular-nums; }
.syn-about-stat span { color:#cde8f1; font-size:12px; line-height:1.4; }
.syn-about-campus { display:flex; align-items:center; gap:10px; padding-top:14px; border-top:1px solid rgba(180,237,241,.28); color:#e0f2f8; font-size:13px; }
.syn-about-story-copy { display:flex; flex-direction:column; justify-content:center; gap:16px; min-width:0; padding:clamp(23px,3vw,38px); border:1px solid var(--border); border-radius:22px; background:var(--surface); box-shadow:var(--shadow-sm); }
.syn-about-story-copy h3,.syn-about-purpose-heading h3 { color:var(--text-strong); font-size:clamp(22px,2.4vw,28px); font-weight:650; letter-spacing:-.025em; line-height:1.3; margin:0; }
.syn-about-story-copy p { color:var(--text-muted); font-size:15px; line-height:1.75; margin:0; }
.syn-about-story-copy .syn-about-lead { color:var(--text); font-size:16px; font-weight:500; }
.syn-about-purpose { margin-top:clamp(28px,4vw,48px); }
.syn-about-purpose-heading { margin-bottom:18px; }
.syn-about-purpose-heading p { color:var(--text-muted); font-size:14px; line-height:1.6; margin:5px 0 0; }
.syn-about-values { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:18px; }
.syn-about-block { position:relative; min-width:0; padding:clamp(23px,3vw,34px); border:1px solid var(--border); border-radius:20px; background:var(--surface-soft); overflow:hidden; box-shadow:var(--shadow-sm); }
.syn-about-block::before { position:absolute; inset:0 0 auto; height:4px; content:""; background:var(--brand-accent); }
.syn-about-vision::before { background:var(--brand-sky); }
.syn-about-card-header { display:flex; align-items:center; gap:14px; margin-bottom:19px; }
.syn-about-icon { width:58px; height:58px; flex:none; display:grid; place-items:center; border:1px solid var(--border); border-radius:15px; background:var(--surface-accent); }
.syn-about-card-label { color:var(--accent-text); font-size:12px; font-weight:500; line-height:1.4; }
.syn-about-block h4 { color:var(--text-strong); font-size:clamp(21px,2.1vw,25px); letter-spacing:-.02em; font-weight:650; margin:3px 0 0; line-height:1.3; }
.syn-about-block p { color:var(--text-muted); font-size:15px; line-height:1.75; margin:0; }
@media(max-width:820px) { .syn-about-top { grid-template-columns:1fr; } }
@media(max-width:690px) { .syn-about-values { grid-template-columns:1fr; gap:14px; } .syn-about-story,.syn-about-story-copy,.syn-about-block { padding:22px; border-radius:17px; } }
@media(max-width:350px) { .syn-about-stat { padding:12px; } .syn-about-story,.syn-about-story-copy,.syn-about-block { padding:17px; } .syn-about-card-header { gap:10px; } .syn-about-icon { width:48px; height:48px; } }
</style>
