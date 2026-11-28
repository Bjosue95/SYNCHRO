<script setup>
import { RouterLink } from "vue-router";
import SynButton from "../components/ui/SynButton.vue";
import AppIcon from "../components/ui/AppIcon.vue";
import { useCookies } from "../composables/useCookies.js";
const { abrirPreferencias } = useCookies();
</script>
<template>
  <article class="contenedor syn-politica" aria-labelledby="politica-titulo">
    <RouterLink to="/" class="syn-politica-volver">
      <AppIcon nombre="volver" :tamano="20"/>
      <span>Volver al inicio</span>
    </RouterLink>
    <header class="syn-politica-hero">
      <div class="syn-politica-presentacion">
        <h1 id="politica-titulo">Política de cookies</h1>
        <p>Conoce cómo SYNCHRO utiliza cookies, almacenamiento local y servicios externos en su portal web.</p>
        <SynButton @click="abrirPreferencias"><AppIcon nombre="privacidad" :tamano="20"/> Configurar preferencias</SynButton>
      </div>
      <div class="syn-politica-emblema" aria-hidden="true">
        <span class="syn-politica-emblema-interior"><AppIcon nombre="privacidad" :tamano="74" tema-forzado="oscuro"/></span>
      </div>
    </header>
    <section><h2><span class="syn-politica-icono"><AppIcon nombre="ayuda" :tamano="25"/></span><span>1. ¿Qué son las cookies?</span></h2><p>Son pequeños datos que el navegador guarda o recibe al visitar un sitio. El almacenamiento local también permite recordar ajustes del navegador, aunque no es una cookie.</p></section>
    <section><h2><span class="syn-politica-icono"><AppIcon nombre="seguridad" :tamano="25"/></span><span>2. Tecnologías esenciales y preferencias solicitadas</span></h2>
      <div class="syn-politica-tabla" tabindex="0" role="region" aria-label="Tecnologías esenciales">
        <table><thead><tr><th>Nombre</th><th>Finalidad</th><th>Duración</th></tr></thead><tbody>
          <tr><td data-etiqueta="Nombre">SYNCHRO.ClienteWeb</td><td data-etiqueta="Finalidad">Cookie de sesión de clientes, segura y HttpOnly.</td><td data-etiqueta="Duración">Sesión del navegador, con autorización de hasta 30 minutos o cierre de sesión.</td></tr>
          <tr><td data-etiqueta="Nombre">Cookie antifalsificación de ASP.NET Core</td><td data-etiqueta="Finalidad">Protege formularios y solicitudes frente a CSRF.</td><td data-etiqueta="Duración">Sesión del navegador.</td></tr>
          <tr><td data-etiqueta="Nombre">synchro-usuarios-tema</td><td data-etiqueta="Finalidad">Almacenamiento local del tema claro u oscuro que eliges.</td><td data-etiqueta="Duración">Hasta que cambies el tema o borres el almacenamiento.</td></tr>
          <tr><td data-etiqueta="Nombre">synchro-preferencias-cookies</td><td data-etiqueta="Finalidad">Almacenamiento local de tu decisión sobre el mapa.</td><td data-etiqueta="Duración">180 días de validez; se renueva al guardar una nueva decisión.</td></tr>
        </tbody></table>
      </div>
      <p>Las funciones de acceso y seguridad no dependen de aceptar contenido externo. El tema se recuerda cuando lo cambias voluntariamente.</p>
    </section>
    <section><h2><span class="syn-politica-icono"><AppIcon nombre="mapa" :tamano="25"/></span><span>3. Contenido externo opcional</span></h2><p>El mapa interactivo utiliza imágenes de OpenStreetMap. Si lo activas, el proveedor recibe datos técnicos de conexión, como la dirección IP y las solicitudes de imágenes del mapa. Puedes consultar las ubicaciones en la lista sin cargarlo. Al retirar el permiso, se desmonta el mapa y se impiden nuevas cargas; esto no elimina datos ya recibidos por el proveedor.</p><p>No hemos incorporado cookies de publicidad ni herramientas de analítica al aviso actual. Los enlaces a Google Maps, WhatsApp y redes sociales abren servicios externos cuando decides utilizarlos; se aplican sus propias condiciones y políticas.</p></section>
    <section><h2><span class="syn-politica-icono"><AppIcon nombre="cotizacion" :tamano="25"/></span><span>4. Solicitudes de cotización</span></h2><p>Cuando envías el formulario, los datos que introduces se transmiten a EmailJS y al servicio de correo para atender tu solicitud. Esta operación se inicia al pulsar Enviar solicitud y es independiente de la elección del mapa. No se guardan los campos en cookies ni en almacenamiento local por parte del formulario.</p></section>
    <section><h2><span class="syn-politica-icono"><AppIcon nombre="control-acceso" :tamano="25"/></span><span>5. Cambiar tus preferencias</span></h2><p>Puedes elegir solo esenciales, aceptar todas o configurar el mapa. Usa el enlace Preferencias de cookies del pie de página o de tu cuenta en cualquier momento. También puedes borrar las cookies y los datos del sitio desde tu navegador; esto puede cerrar tu sesión y eliminar tus ajustes.</p><SynButton variante="secundario" @click="abrirPreferencias"><AppIcon nombre="control-acceso" :tamano="20"/> Modificar preferencias</SynButton></section>
    <section><h2><span class="syn-politica-icono"><AppIcon nombre="correo" :tamano="25"/></span><span>6. Contacto</span></h2><p>Para consultas relacionadas con esta política, escríbenos a nuestro correo oficial.</p><a class="syn-politica-contacto" href="mailto:synchrohn@gmail.com"><AppIcon nombre="correo" :tamano="20"/> <span>synchrohn@gmail.com</span><AppIcon nombre="flecha" :tamano="17"/></a></section>
    <p class="syn-politica-fecha">Versión 1 · Noviembre de 2026</p>
  </article>
</template>
<style scoped>
.syn-politica { max-width:980px; padding-block:clamp(24px,4vw,60px); display:grid; gap:20px; color:var(--text); }
.syn-politica-volver,.syn-politica-contacto { display:inline-flex; align-items:center; justify-content:center; gap:10px; justify-self:start; min-height:44px; max-width:100%; box-sizing:border-box; padding:10px 15px; border:1px solid var(--border-strong); border-radius:12px; background:var(--surface); color:var(--text-strong); text-decoration:none; font-size:13px; font-weight:650; transition:background-color .18s,border-color .18s,transform .18s; }
.syn-politica-volver:hover,.syn-politica-contacto:hover { background:var(--surface-hover); border-color:var(--brand-accent); transform:translateY(-1px); }
.syn-politica-volver:focus-visible,.syn-politica-contacto:focus-visible,.syn-politica-tabla:focus-visible { outline:3px solid var(--ring); outline-offset:3px; }
.syn-politica section { display:grid; gap:16px; padding:clamp(18px,3vw,30px); min-width:0; background:var(--surface); border:1px solid var(--border); border-radius:18px; box-shadow:var(--shadow-sm); }
.syn-politica-hero { display:grid; grid-template-columns:minmax(0,1fr) minmax(160px,.38fr); align-items:stretch; overflow:hidden; border-radius:20px; border:1px solid var(--border); background:var(--surface); box-shadow:var(--shadow-sm); }
.syn-politica-presentacion { padding:clamp(23px,4vw,42px); display:grid; gap:16px; align-content:center; min-width:0; }
.syn-politica-emblema { display:grid; place-items:center; min-height:200px; background-color:#075478; background-image:linear-gradient(rgba(94,198,232,.12) 1px,transparent 1px),linear-gradient(90deg,rgba(94,198,232,.12) 1px,transparent 1px),linear-gradient(145deg,#01305c,#008d9d); background-size:32px 32px,32px 32px,100% 100%; }
.syn-politica-emblema-interior { display:grid; place-items:center; width:116px; height:116px; border:1px solid #5ec6e8; border-radius:27px; background:#103e5c; box-shadow:0 15px 32px #01274455; }
.syn-politica h1 { font-size:clamp(28px,4vw,40px); line-height:1.15; color:var(--text-strong); margin:0; letter-spacing:-.035em; }
.syn-politica h2 { display:flex; align-items:center; gap:12px; font-size:clamp(17px,2vw,21px); color:var(--text-strong); line-height:1.3; margin:0; letter-spacing:-.015em; }
.syn-politica-icono { display:grid; place-items:center; width:46px; height:46px; flex:0 0 46px; border-radius:12px; border:1px solid var(--border); background:var(--surface-accent); }
.syn-politica p { font-size:14px; color:var(--text-muted); line-height:1.75; margin:0; }
.syn-politica :deep(.syn-button) { justify-self:start; }
.syn-politica-tabla { overflow:auto; border:1px solid var(--border); border-radius:12px; }
.syn-politica table { width:100%; table-layout:fixed; border-collapse:collapse; font-size:13px; line-height:1.6; }
.syn-politica th,.syn-politica td { padding:14px 13px; text-align:left; border-bottom:1px solid var(--border); vertical-align:top; overflow-wrap:anywhere; }
.syn-politica th { background:var(--surface-accent); color:var(--text-strong); font-weight:650; }
.syn-politica th:first-child { width:30%; }
.syn-politica td { color:var(--text-muted); font-weight:500; }
.syn-politica td:first-child { color:var(--text-strong); }
.syn-politica tbody tr:nth-child(even) { background:var(--surface-soft); }
.syn-politica tbody tr:last-child td { border-bottom:0; }
.syn-politica-contacto { justify-content:flex-start; }
.syn-politica-contacto span { min-width:0; overflow-wrap:anywhere; }
.syn-politica-fecha { font-size:12px!important; color:var(--text-subtle); text-align:center; }
@media(max-width:640px) {
  .syn-politica { gap:15px; }
  .syn-politica-hero { grid-template-columns:1fr; }
  .syn-politica-emblema { display:none; }
  .syn-politica-tabla { border:0; overflow:visible; border-radius:0; }
  .syn-politica table,.syn-politica tbody { display:block; }
  .syn-politica thead { position:absolute; width:1px; height:1px; padding:0; margin:-1px; overflow:hidden; clip-path:inset(50%); }
  .syn-politica tbody { display:grid; gap:10px; }
  .syn-politica tbody tr,.syn-politica tbody tr:nth-child(even) { display:grid; background:var(--surface-soft); border:1px solid var(--border); border-radius:12px; padding:12px; }
  .syn-politica td { display:grid; gap:3px; padding:7px 0; border:0; }
  .syn-politica td::before { content:attr(data-etiqueta); color:var(--text-subtle); font-size:11px; font-weight:500; }
  .syn-politica-icono { width:40px; height:40px; flex-basis:40px; }
  .syn-politica-icono :deep(svg) { width:22px; height:22px; }
}
@media(prefers-reduced-motion:reduce) { .syn-politica-volver,.syn-politica-contacto { transition:none; } .syn-politica-volver:hover,.syn-politica-contacto:hover { transform:none; } }
</style>
