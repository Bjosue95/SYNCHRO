<script setup>
import { onMounted, onBeforeUnmount, ref } from "vue";
import { RouterLink } from "vue-router";
import AppIcon from "../components/ui/AppIcon.vue";
import LockerJourney from "../components/ui/LockerJourney.vue";
import LocationsMap from "../components/landing/LocationsMap.vue";
import QuoteForm from "../components/landing/QuoteForm.vue";
import ContactSection from "../components/landing/ContactSection.vue";
import AboutSynchro from "../components/landing/AboutSynchro.vue";

const pagina = ref(null);
let observador = null;

// Revelado de contenido con API nativa. Si falta soporte o se solicita
// movimiento reducido, todo el contenido se muestra inmediatamente.
onMounted(() => {
  const elementos = [...(pagina.value?.querySelectorAll("[data-reveal]") || [])];
  const reducido = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches;
  if (!("IntersectionObserver" in window) || reducido) return;
  observador = new IntersectionObserver(entradas => {
    for (const entrada of entradas) {
      if (!entrada.isIntersecting) continue;
      entrada.target.classList.remove("syn-pendiente");
      observador?.unobserve(entrada.target);
    }
  }, { threshold: 0.09, rootMargin: "0px 0px 32px 0px" });
  elementos.forEach(el => {
    el.classList.add("syn-pendiente");
    observador.observe(el);
  });
});
onBeforeUnmount(() => observador?.disconnect());
</script>

<template>
  <div ref="pagina" class="syn-home">
    <section class="syn-home-hero">
      <div class="contenedor syn-hero-grid">
        <div class="syn-hero-content">
          <span class="syn-overline">Guardar · Entregar · Conectar</span>
          <h1>Tu espacio.<br><span>Tu tiempo.</span></h1>
          <p>
            SYNCHRO reúne almacenamiento temporal, encomiendas y carga de dispositivos.
            Desde tu cuenta puedes preparar una encomienda y consultar su estado.
          </p>
          <div class="syn-hero-actions">
            <RouterLink to="/reservar/encomienda" class="boton boton-primario">
              Crear pre-reserva
              <AppIcon nombre="flecha" :tamano="19" />
            </RouterLink>
            <a href="#servicios" class="syn-hero-secondary">
              Explorar servicios <span aria-hidden="true">↗</span>
            </a>
          </div>
          <p class="syn-hero-foot">
            La pre-reserva no asigna un casillero. Cada depósito requiere autorización.
          </p>
        </div>
        <LockerJourney />
      </div>
    </section>

    <section id="servicios" class="contenedor syn-service-section">
      <header class="syn-service-intro" data-reveal>
        <span class="syn-overline">Servicios SYNCHRO</span>
        <h2>Soluciones para cada momento.</h2>
        <p>El formato de las estaciones se adapta al lugar y al servicio que necesita cada persona.</p>
      </header>

      <div class="syn-service-grid">
        <article class="syn-feature syn-feature-main" data-reveal>
          <div class="syn-feature-top">
            <span class="syn-feature-symbol"><AppIcon nombre="encomienda" :tamano="37"/></span>
            <span class="syn-feature-type">Solicitud web</span>
          </div>
          <h3>Encomiendas</h3>
          <p>Prepara el envío desde tu cuenta. El destinatario autoriza las comunicaciones y la entrega se verifica en la estación.</p>
          <div class="syn-parcel-path" aria-label="Solicitud, consentimiento y verificación">
            <span><AppIcon nombre="reservas" :tamano="23"/> Solicitud</span>
            <i aria-hidden="true"></i>
            <span><AppIcon nombre="whatsapp" :tamano="23"/> Consentimiento</span>
            <i aria-hidden="true"></i>
            <span><AppIcon nombre="seguridad" :tamano="23"/> Verificación</span>
          </div>
          <RouterLink to="/reservar/encomienda" class="syn-feature-action">
            Preparar encomienda <AppIcon nombre="flecha" :tamano="20"/>
          </RouterLink>
        </article>

        <article class="syn-feature syn-feature-storage" data-reveal>
          <div class="syn-feature-top">
            <span class="syn-feature-symbol"><AppIcon nombre="casillero" :tamano="37"/></span>
            <span class="syn-feature-type">En las estaciones</span>
          </div>
          <h3>Almacenamiento personal</h3>
          <p>Guarda tus pertenencias de manera temporal y retíralas mediante el proceso de verificación.</p>
          <div class="syn-locker-pattern" aria-hidden="true">
            <span v-for="numero in 8" :key="numero" :class="{ 'syn-locker-highlight': numero === 3 }"></span>
          </div>
          <span class="syn-feature-foot">Reserva web aún no habilitada</span>
        </article>

        <article class="syn-feature syn-feature-thermal" data-reveal>
          <div class="syn-small-icon"><AppIcon nombre="temperatura" :tamano="32"/></div>
          <div>
            <h3>Temperatura controlada</h3>
            <p>Compartimentos especiales de frío y calor para artículos compatibles.</p>
            <div class="syn-thermal-options" aria-label="Frío y calor">
              <span><i class="syn-cold-dot"></i>Frío</span>
              <span><i class="syn-warm-dot"></i>Calor</span>
            </div>
          </div>
        </article>

        <article class="syn-feature syn-feature-charge" data-reveal>
          <div class="syn-small-icon"><AppIcon nombre="carga" :tamano="32"/></div>
          <div>
            <h3>Carga de dispositivos</h3>
            <p>Estaciones presenciales para celulares y laptops, con pago al finalizar.</p>
            <div class="syn-charge-visual" aria-label="Servicio de carga presencial">
              <div class="syn-charge-battery" aria-hidden="true">
                <span></span><span></span><span></span><span></span>
              </div>
              <span>Uso presencial</span>
            </div>
          </div>
        </article>
      </div>
    </section>

    <AboutSynchro />
    <LocationsMap />
    <QuoteForm />
    <ContactSection />

    <section class="contenedor syn-bottom-message" data-reveal>
      <AppIcon nombre="seguridad" :tamano="26"/>
      <p>
        En SYNCHRO, la seguridad forma parte de cada operación.
        La solicitud web, el consentimiento y la verificación
        son pasos distintos de un mismo servicio.
      </p>
    </section>
  </div>
</template>

<style scoped>
.syn-home { overflow: clip; }
.syn-home-hero { padding: 64px 0 88px; background: var(--bg); }
.syn-hero-grid { display: grid; grid-template-columns: minmax(0,.87fr) minmax(0,1.13fr); gap: clamp(32px,5vw,80px); align-items: center; }
.syn-hero-content { min-width: 0; max-width: 500px; }
.syn-overline { color: var(--accent-text); font-size: 13px; font-weight: 600; }
.syn-hero-content h1 { font-size: clamp(49px,5.2vw,79px); line-height: 1.06; letter-spacing: -.05em; font-weight: 600; margin: 22px 0 24px; color: var(--text-strong); }
.syn-hero-content h1 span { color: var(--accent-text); }
.syn-hero-content > p:not(.syn-hero-foot) { font-size: 16px; line-height: 1.75; color: var(--text-muted); max-width: 425px; margin: 0; }
.syn-hero-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 18px; margin-top: 32px; }
.syn-hero-actions .boton { min-height: 50px; }
.syn-hero-secondary { display: inline-flex; align-items: center; gap: 8px; text-decoration: none; font-weight: 600; font-size: 14px; color: var(--text-strong); }
.syn-hero-secondary:hover { color: var(--accent-text); }
.syn-hero-foot { font-size: 12px; line-height: 1.65; color: var(--text-subtle); max-width: 360px; margin: 21px 0 0; }
.syn-service-section { padding-top: 68px; padding-bottom: 72px; }
.syn-service-intro { max-width: 660px; margin-bottom: 34px; }
.syn-service-intro h2 { color: var(--text-strong); font-size: clamp(32px,3.1vw,45px); font-weight: 600; letter-spacing: -.035em; margin: 9px 0; }
.syn-service-intro p { margin: 0; color: var(--text-muted); font-size: 15px; line-height: 1.7; }
.syn-service-grid { display: grid; grid-template-columns: 1.13fr .87fr; grid-template-rows: auto auto; gap: 20px; }
.syn-feature { min-width: 0; border-radius: 16px; background: var(--surface); border: 1px solid var(--border); padding: 28px; }
.syn-feature-main, .syn-feature-storage { min-height: 350px; position: relative; }
.syn-feature-main { border-color: var(--border-strong); display: flex; flex-direction: column; }
.syn-feature-top { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; margin-bottom: 23px; }
.syn-feature-symbol { width: 56px; height: 56px; border-radius: 13px; display: grid; place-items: center; background: var(--surface-accent); }
.syn-feature-type { color: var(--text-muted); font-size: 12px; }
.syn-feature h3 { font-size: 23px; font-weight: 600; letter-spacing: -.02em; color: var(--text-strong); line-height: 1.25; margin: 0 0 11px; }
.syn-feature p { margin: 0; max-width: 460px; font-size: 14px; line-height: 1.7; color: var(--text-muted); }
.syn-parcel-path { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin: 28px 0 24px; }
.syn-parcel-path span { display: inline-flex; align-items: center; gap: 5px; font-size: 11px; font-weight: 600; color: var(--text); }
.syn-parcel-path i { width: 16px; height: 1px; flex: 0 0 16px; background: var(--border-strong); }
.syn-feature-action { color: var(--accent-text); display: inline-flex; align-items: center; gap: 9px; font-size: 14px; font-weight: 600; text-decoration: none; margin-top: auto; width: fit-content; }
.syn-feature-action:hover { text-decoration: underline; }
.syn-feature-storage { display: flex; flex-direction: column; }
.syn-locker-pattern { display: grid; grid-template-columns: repeat(4,25px); grid-template-rows: repeat(2,36px); gap: 6px; margin: 22px 0; }
.syn-locker-pattern span { border: 1px solid var(--border-strong); border-radius: 3px; background: var(--surface-soft); position: relative; }
.syn-locker-pattern span::after { content:""; position:absolute; right:4px; top:45%; width:3px; height:7px; background:var(--border-strong); border-radius:2px; }
.syn-locker-pattern .syn-locker-highlight { border-color:var(--brand-accent); background:var(--surface-accent); }
.syn-feature-foot { font-size: 12px; color: var(--text-subtle); margin-top: auto; }
.syn-feature-thermal, .syn-feature-charge { display: grid; grid-template-columns: 53px minmax(0,1fr); gap: 19px; align-items: start; min-height: 185px; }
.syn-small-icon { display: grid; place-items: center; width: 48px; height: 48px; border-radius: 12px; background: var(--surface-accent); }
.syn-feature-thermal h3, .syn-feature-charge h3 { font-size: 20px; }
.syn-thermal-options { display: flex; gap: 23px; align-items: center; margin-top: 16px; font-size: 12px; color: var(--text-muted); }
.syn-thermal-options span { display: inline-flex; align-items: center; gap: 8px; }
.syn-thermal-options i { display: inline-block; width: 10px; height: 10px; border-radius: 50%; }
.syn-cold-dot { background: #4fabc4; } .syn-warm-dot { background: #d7a477; }
.syn-charge-visual { display: flex; align-items: center; gap: 13px; margin-top: 18px; font-size: 12px; color: var(--text-muted); }
.syn-charge-battery { display: grid; grid-template-columns: repeat(4,8px); gap: 4px; padding: 5px; border: 1px solid var(--border-strong); border-radius: 5px; position: relative; }
.syn-charge-battery::after { content:""; position:absolute; width:3px; height:10px; background:var(--border-strong); top:8px; right:-4px; border-radius:0 2px 2px 0; }
.syn-charge-battery span { height:16px; background: var(--brand-accent); border-radius:1px; opacity:.25; animation: syn-charge-show 2.2s ease-out both; }
.syn-charge-battery span:nth-child(1) { animation-delay:.15s; }.syn-charge-battery span:nth-child(2) { animation-delay:.3s; }.syn-charge-battery span:nth-child(3) { animation-delay:.45s; }.syn-charge-battery span:nth-child(4) { animation-delay:.6s; }
@keyframes syn-charge-show { to { opacity:.9; } }
.syn-bottom-message { display: flex; align-items: flex-start; gap: 17px; max-width: 1170px; padding: 26px 0 70px; border-top: 1px solid var(--border); }
.syn-bottom-message p { margin: 0; font-size: 14px; line-height: 1.7; max-width: 690px; color: var(--text-muted); }
.syn-pendiente { opacity: 0; transform: translateY(14px); transition: opacity 400ms ease, transform 400ms ease; }
.syn-service-grid > :nth-child(2) { transition-delay: 70ms; } .syn-service-grid > :nth-child(3) { transition-delay: 100ms; } .syn-service-grid > :nth-child(4) { transition-delay: 150ms; }
@media (max-width: 1000px) {
  .syn-hero-grid { grid-template-columns: 1fr; gap: 36px; }
  .syn-hero-content { max-width: 660px; }
  .syn-home-hero { padding: 54px 0 65px; }
  .syn-service-grid { grid-template-columns: 1fr 1fr; }
}
@media (max-width: 690px) {
  .syn-home-hero { padding: 36px 0 48px; }
  .syn-hero-content h1 { font-size: clamp(45px, 10vw, 62px); }
  .syn-service-section { padding-top: 52px; padding-bottom: 50px; }
  .syn-service-grid { grid-template-columns: 1fr; gap: 14px; }
  .syn-feature { padding: 22px; }
  .syn-feature-main, .syn-feature-storage { min-height: 0; }
  .syn-feature-thermal, .syn-feature-charge { min-height: 0; }
  .syn-bottom-message { padding: 24px 0 46px; }
}
@media (prefers-reduced-motion: reduce) {
  .syn-charge-battery span { animation: none; opacity: .9; }
  .syn-pendiente { opacity:1; transform:none; }
}
</style>
