<script setup>
import { RouterLink } from "vue-router";
import AppIcon from "../components/ui/AppIcon.vue";
</script>

<template>
  <section class="syn-dashboard">
    <header class="syn-dashboard-heading">
      <h1>Gestiona tus encomiendas</h1>
      <p>Desde aquí puedes gestionar tus solicitudes, consultar su estado o iniciar una nueva.</p>
    </header>

    <div class="syn-dashboard-actions">
      <article class="syn-dashboard-new">
        <div class="syn-dashboard-icon">
          <AppIcon nombre="encomienda" :tamano="37"/>
        </div>
        <div class="syn-dashboard-new-content">
          <h2>Crear pre-reserva</h2>
          <p>Indica el tamaño de la encomienda y el WhatsApp del destinatario.</p>
          <div class="syn-dashboard-sequence" aria-label="Solicitud, consentimiento y verificación">
            <div><strong>01</strong><span>Solicitud</span></div>
            <i aria-hidden="true"></i>
            <div><strong>02</strong><span>Consentimiento</span></div>
            <i aria-hidden="true"></i>
            <div><strong>03</strong><span>Verificación</span></div>
          </div>
          <RouterLink class="boton boton-primario syn-dashboard-cta" to="/reservar/encomienda">
            Nueva pre-reserva <AppIcon nombre="flecha" :tamano="19"/>
          </RouterLink>
        </div>
      </article>

      <article class="syn-dashboard-lookup">
        <div class="syn-dashboard-lookup-mark">
          <AppIcon nombre="buscar" :tamano="35"/>
        </div>
        <h2>Consultar solicitud</h2>
        <p>Revisa una pre-reserva con su número de referencia y clave privada.</p>
        <RouterLink class="syn-dashboard-lookup-link" to="/consultar">
          Consultar estado <AppIcon nombre="flecha" :tamano="19"/>
        </RouterLink>
        <div class="syn-dashboard-reference" aria-hidden="true">
          <span><i></i><i></i><i></i></span>
          <span><i></i><i></i><i></i><i></i><i></i></span>
        </div>
      </article>
    </div>

    <div class="syn-dashboard-notice">
      <AppIcon nombre="seguridad" :tamano="24"/>
      <p>
        La pre-reserva de encomienda no asigna automáticamente un casillero.
        El depósito depende del consentimiento y las verificaciones correspondientes.
      </p>
    </div>

    <div class="syn-dashboard-services">
      <h2>También en SYNCHRO</h2>
      <div class="syn-dashboard-service-row">
        <span><AppIcon nombre="casillero" :tamano="27"/></span>
        <div>
          <h3>Almacenamiento personal</h3>
          <p>Resguardo temporal de pertenencias. La reserva web todavía no está habilitada.</p>
        </div>
      </div>
      <div class="syn-dashboard-service-row">
        <span><AppIcon nombre="carga" :tamano="27"/></span>
        <div>
          <h3>Carga de dispositivos</h3>
          <p>Servicio de uso presencial en las estaciones.</p>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.syn-dashboard { max-width: 1070px; padding: 4px 0 52px; }
.syn-dashboard-heading { max-width: 630px; margin-bottom: 30px; }
.syn-dashboard-heading h1 { margin: 0 0 12px; font-size: clamp(30px,3.1vw,40px); line-height: 1.17; letter-spacing: -.035em; font-weight: 600; color: var(--text-strong); }
.syn-dashboard-heading p { font-size: 14px; line-height: 1.7; color: var(--text-muted); margin: 0; }
.syn-dashboard-actions { display: grid; grid-template-columns: minmax(0,1.38fr) minmax(0,.85fr); gap: 17px; }
.syn-dashboard-new, .syn-dashboard-lookup { min-width: 0; border-radius: 17px; padding: 31px; }
.syn-dashboard-new { background: var(--surface); border: 1px solid var(--border); display: grid; grid-template-columns: 64px minmax(0,1fr); gap: 20px; }
.syn-dashboard-icon, .syn-dashboard-lookup-mark { width: 59px; height: 59px; display: grid; place-items: center; border-radius: 13px; background: var(--surface-accent); }
.syn-dashboard-new h2, .syn-dashboard-lookup h2 { font-size: 23px; line-height: 1.3; font-weight: 600; letter-spacing: -.02em; margin: 0 0 8px; }
.syn-dashboard-new h2 { color: var(--text-strong); }
.syn-dashboard-new p, .syn-dashboard-lookup p { font-size: 14px; line-height: 1.68; margin: 0; }
.syn-dashboard-new p { color: var(--text-muted); }
.syn-dashboard-sequence { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; margin: 27px 0 26px; }
.syn-dashboard-sequence div { display: grid; gap: 5px; }
.syn-dashboard-sequence strong { font: 600 12px/1 ui-monospace,monospace; color: var(--accent-text); }
.syn-dashboard-sequence span { color: var(--text-muted); font-size: 12px; }
.syn-dashboard-sequence i { height: 1px; width: 18px; background: var(--border-strong); }
.syn-dashboard-cta { width: fit-content; min-height: 46px; }
.syn-dashboard-lookup { position: relative; background: #01305c; color: #fff; display: flex; flex-direction: column; overflow: hidden; isolation: isolate; }
.syn-dashboard-lookup-mark { background: #174b68; margin-bottom: 23px; }
.syn-dashboard-lookup-mark :deep(img) { filter: brightness(0) invert(1); }
.syn-dashboard-lookup h2 { color: #fff; }
.syn-dashboard-lookup p { color: #d1e2e9; }
.syn-dashboard-lookup-link { display: flex; align-items: center; gap: 10px; color: #9bedf0; text-decoration: none; font-size: 14px; font-weight: 600; margin-top: 24px; width: fit-content; }
.syn-dashboard-lookup-link:hover { text-decoration: underline; }
.syn-dashboard-lookup-link :deep(img) { filter: brightness(0) invert(1); }
.syn-dashboard-reference { position: absolute; bottom: 26px; right: 20px; transform: rotate(-9deg); opacity: .16; display: grid; gap: 8px; z-index: -1; pointer-events: none; }
.syn-dashboard-reference > span { display: flex; gap: 6px; }
.syn-dashboard-reference i { width: 29px; height: 17px; background: #fff; border-radius: 4px; }
.syn-dashboard-notice { display: flex; gap: 16px; align-items: flex-start; padding: 22px 0; border-bottom: 1px solid var(--border); }
.syn-dashboard-notice p { margin: 0; color: var(--text-muted); font-size: 13px; line-height: 1.68; max-width: 670px; }
.syn-dashboard-services { margin-top: 35px; }
.syn-dashboard-services > h2 { color: var(--text-strong); font-size: 19px; font-weight: 600; margin: 0 0 15px; }
.syn-dashboard-service-row { display: flex; gap: 16px; align-items: center; padding: 17px 0; border-bottom: 1px solid var(--border); }
.syn-dashboard-service-row > span { width: 40px; flex-shrink: 0; }
.syn-dashboard-service-row h3 { font-size: 14px; color: var(--text-strong); font-weight: 600; margin: 0 0 4px; }
.syn-dashboard-service-row p { font-size: 12px; line-height: 1.5; color: var(--text-muted); margin: 0; }
@media (max-width: 880px) { .syn-dashboard-actions { grid-template-columns: 1fr; } .syn-dashboard-new, .syn-dashboard-lookup { padding: 26px; } .syn-dashboard-lookup { min-height: 210px; } }
@media (max-width: 540px) { .syn-dashboard-new { grid-template-columns: 1fr; gap: 16px; } .syn-dashboard-sequence { gap: 7px; } .syn-dashboard-sequence i { width: 10px; } .syn-dashboard-heading { margin-bottom: 23px; } }
@media (prefers-reduced-motion: no-preference) {
  .syn-dashboard-cta:hover :deep(img), .syn-dashboard-lookup-link:hover :deep(img) { transform: translateX(3px); transition: transform 160ms ease; }
}
</style>