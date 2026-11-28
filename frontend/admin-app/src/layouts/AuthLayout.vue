<script setup>
import { ref } from "vue";
import AdminIcon from "../components/AdminIcon.vue";
const claveTema="synchro-admin-acceso-tema";
function inicial() {
  try { const t=localStorage.getItem(claveTema); if(t==="claro"||t==="oscuro")return t; } catch {}
  return typeof window!=="undefined" && window.matchMedia("(prefers-color-scheme: dark)").matches ? "oscuro":"claro";
}
const tema=ref(inicial());
function cambiarTema(){
  tema.value=tema.value==="oscuro"?"claro":"oscuro";
  try { localStorage.setItem(claveTema,tema.value); } catch {}
}
const modulos=[
  {icono:"lockers",titulo:"Control de estaciones",detalle:"Casilleros, disponibilidad y operaciones."},
  {icono:"activity",titulo:"Gestión operativa",detalle:"Reservas, encomiendas, retiros y cobros."},
  {icono:"report",titulo:"Seguimiento y reportes",detalle:"Historial de movimientos e información de gestión."}
];
</script>
<template>
  <div class="auth-shell" :data-acceso-tema="tema">
    <header class="auth-header">
      <div class="auth-header-inner">
        <div class="auth-brand" aria-label="SYNCHRO">
          <img src="/assets/isotipo-fondo-oscuro.png" alt="" class="auth-symbol"/>
          <img src="/assets/nombre-blanco.png" alt="SYNCHRO" class="auth-wordmark"/>
        </div>
        <div class="auth-header-actions">
          <span class="auth-product">Administración</span>
          <button type="button" class="auth-theme" @click="cambiarTema"
            :aria-label="tema==='oscuro'?'Activar modo claro':'Activar modo oscuro'"
            :title="tema==='oscuro'?'Activar modo claro':'Activar modo oscuro'">
            <AdminIcon :name="tema==='oscuro'?'sun':'moon'" :size="21"/>
          </button>
        </div>
      </div>
    </header>
    <main class="auth-main">
      <div class="auth-grid">
        <section class="auth-intro" aria-labelledby="auth-intro-title">
          <div class="auth-intro-top">
            <div class="auth-intro-symbol"><img src="/assets/isotipo-fondo-oscuro.png" alt=""/></div>
            <span class="auth-intro-tag"><AdminIcon name="shield" :size="17"/> Panel administrativo</span>
          </div>
          <h2 id="auth-intro-title">Gestiona SYNCHRO desde un solo lugar.</h2>
          <p class="auth-intro-description">Supervisa las operaciones de la estación y administra sus servicios desde una plataforma centralizada.</p>
          <div class="auth-modulos">
            <article v-for="modulo in modulos" :key="modulo.icono" class="auth-modulo">
              <span class="auth-modulo-icono"><AdminIcon :name="modulo.icono" :size="23"/></span>
              <div><h3>{{ modulo.titulo }}</h3><p>{{ modulo.detalle }}</p></div>
            </article>
          </div>
          <p class="auth-intro-footer">El punto donde todo se conecta</p>
        </section>
        <slot/>
      </div>
    </main>
    <footer class="auth-footer"><span>SYNCHRO</span><span aria-hidden="true">·</span><span>Administración de estaciones inteligentes</span></footer>
  </div>
</template>
<style scoped>
.auth-shell {
  --auth-page:#f4f7fa; --auth-card:#fff; --auth-soft:#eaf5f7; --auth-text:#01305c;
  --auth-muted:#536c7d; --auth-border:#d8e5ed; --auth-input:#f8fafc; --auth-input-border:#bdcfdb;
  --auth-accent-text:#006f7e; --auth-ring:#008a9a; --auth-action:#01305c; --auth-action-hover:#064878; --auth-on-action:#fff;
  --auth-error-bg:#fff1ee; --auth-error-border:#efc4bd; --auth-error-text:#852d25;
  --auth-shadow:0 18px 54px #01305c0e; --auth-grid-line:#01305c0a;
  display:flex; flex-direction:column; min-height:100dvh; color-scheme:light; color:var(--auth-text);
  background-color:var(--auth-page);
  background-image:linear-gradient(var(--auth-grid-line) 1px,transparent 1px),linear-gradient(90deg,var(--auth-grid-line) 1px,transparent 1px);
  background-size:56px 56px;
}
.auth-shell[data-acceso-tema="oscuro"] {
  color-scheme:dark; --auth-page:#0a1725; --auth-card:#132638; --auth-soft:#18384b; --auth-text:#effaff;
  --auth-muted:#aec2d0; --auth-border:#2c4255; --auth-input:#172d40; --auth-input-border:#405c70;
  --auth-accent-text:#5ec6e8; --auth-ring:#5ec6e8; --auth-action:#5ec6e8; --auth-action-hover:#89ddf1; --auth-on-action:#061d31;
  --auth-error-bg:#48272b; --auth-error-border:#86504f; --auth-error-text:#ffb9b1;
  --auth-shadow:0 18px 54px #0004; --auth-grid-line:#5ec6e80b;
  --syn-scroll-thumb:#698fa5;
}
.auth-header { flex:none; background:#01305c; color:#fff; border-bottom:1px solid #28516c; }
.auth-header-inner { display:flex; align-items:center; justify-content:space-between; gap:16px; max-width:1220px; min-height:70px; margin:auto; padding:10px 24px; }
.auth-brand,.auth-header-actions { display:flex; align-items:center; gap:12px; min-width:0; }
.auth-symbol { width:37px; height:37px; object-fit:contain; flex:none; }
.auth-wordmark { width:158px; max-width:100%; height:38px; object-fit:contain; object-position:left center; }
.auth-product { color:#d5ebf5; font-size:13px; }
.auth-theme { display:grid; place-items:center; width:44px; height:44px; flex:none; border:1px solid #41627e; border-radius:11px; background:#164365; color:#e8f7ff; }
.auth-theme:hover { background:#215779; }
.auth-theme:focus-visible { outline:3px solid #5ec6e8; outline-offset:3px; }
.auth-main { flex:1; display:grid; place-items:center; width:100%; padding:clamp(22px,5vh,58px) 24px; }
.auth-grid { width:min(100%,1080px); display:grid; grid-template-columns:minmax(0,1.15fr) minmax(0,1fr); align-items:center; gap:clamp(22px,4vw,54px); }
.auth-intro { min-width:0; align-self:stretch; display:flex; flex-direction:column; justify-content:center; gap:18px; position:relative; overflow:hidden; padding:clamp(25px,3.7vw,43px); border:1px solid #296b85; border-radius:24px; color:#f5fcff; background-color:#01305c; background-image:linear-gradient(#5ec6e812 1px,transparent 1px),linear-gradient(90deg,#5ec6e812 1px,transparent 1px),linear-gradient(145deg,#01305c,#084d6c 70%,#086876); background-size:40px 40px,40px 40px,100% 100%; box-shadow:0 20px 45px #01305c22; }
.auth-intro-top { display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:10px; }
.auth-intro-symbol { display:grid; place-items:center; width:65px; height:65px; background:#ffffff12; border:1px solid #ffffff35; border-radius:18px; }
.auth-intro-symbol img { width:44px; height:44px; object-fit:contain; }
.auth-intro-tag { display:flex; align-items:center; gap:7px; color:#c9edf4; font-size:12px; }
.auth-intro h2 { margin:0; color:#fff; font-size:clamp(28px,3.1vw,41px); letter-spacing:-.035em; line-height:1.15; font-weight:740; }
.auth-intro-description { margin:0; color:#d1e8f0; font-size:14px; line-height:1.7; }
.auth-modulos { display:grid; gap:10px; }
.auth-modulo { display:flex; align-items:center; gap:12px; padding:11px 12px; border:1px solid #ffffff26; border-radius:13px; background:#ffffff0b; }
.auth-modulo-icono { display:grid; place-items:center; width:42px; height:42px; flex:none; border:1px solid #ffffff26; border-radius:11px; color:#85e2ec; background:#ffffff0d; }
.auth-modulo h3 { margin:0 0 3px; font-size:14px; color:#fff; font-weight:650; }
.auth-modulo p { margin:0; color:#c8e2ec; font-size:12px; line-height:1.5; }
.auth-intro-footer { margin:0; color:#c5edf4; font-size:12px; padding-top:12px; border-top:1px solid #ffffff30; }
.auth-footer { flex:none; display:flex; justify-content:center; flex-wrap:wrap; gap:6px; padding:10px 18px 17px; color:var(--auth-muted); font-size:12px; text-align:center; }
@media(max-width:820px) { .auth-grid { max-width:460px; grid-template-columns:minmax(0,1fr); } .auth-intro { display:none; } }
@media(max-width:480px) { .auth-header-inner { min-height:60px; padding:8px 15px; } .auth-symbol { width:31px; height:31px; } .auth-wordmark { width:126px; height:32px; } .auth-product { display:none; } .auth-main { padding:18px 13px; } }
@media(max-height:650px) and (min-width:821px) { .auth-main { padding-block:17px; } .auth-intro { gap:11px; padding:24px; } .auth-intro-symbol { width:48px; height:48px; } .auth-intro-symbol img { width:34px; height:34px; } .auth-modulo { padding:8px 11px; } .auth-intro h2 { font-size:30px; } }
</style>
