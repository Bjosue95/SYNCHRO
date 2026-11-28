<script setup>
import { RouterLink } from "vue-router";
import SynButton from "../components/ui/SynButton.vue";
import { useCookies } from "../composables/useCookies.js";
const { abrirPreferencias } = useCookies();
</script>
<template>
  <article class="contenedor syn-politica" aria-labelledby="politica-titulo">
    <RouterLink to="/" class="syn-politica-volver">← Volver al inicio</RouterLink>
    <header>
      <h1 id="politica-titulo">Política de cookies</h1>
      <p>Conoce cómo SYNCHRO utiliza cookies, almacenamiento local y servicios externos en su portal web.</p>
      <SynButton @click="abrirPreferencias">Configurar preferencias</SynButton>
    </header>
    <section><h2>1. ¿Qué son las cookies?</h2><p>Son pequeños datos que el navegador guarda o recibe al visitar un sitio. El almacenamiento local también permite recordar ajustes del navegador, aunque no es una cookie.</p></section>
    <section><h2>2. Tecnologías esenciales y preferencias solicitadas</h2>
      <div class="syn-politica-tabla" tabindex="0" role="region" aria-label="Tecnologías esenciales">
        <table><thead><tr><th>Nombre</th><th>Finalidad</th><th>Duración</th></tr></thead><tbody>
          <tr><td>SYNCHRO.ClienteWeb</td><td>Cookie de sesión de clientes, segura y HttpOnly.</td><td>Sesión del navegador, con autorización de hasta 30 minutos o cierre de sesión.</td></tr>
          <tr><td>Cookie antifalsificación de ASP.NET Core</td><td>Protege formularios y solicitudes frente a CSRF.</td><td>Sesión del navegador.</td></tr>
          <tr><td>synchro-usuarios-tema</td><td>Almacenamiento local del tema claro u oscuro que eliges.</td><td>Hasta que cambies el tema o borres el almacenamiento.</td></tr>
          <tr><td>synchro-preferencias-cookies</td><td>Almacenamiento local de tu decisión sobre el mapa.</td><td>180 días de validez; se renueva al guardar una nueva decisión.</td></tr>
        </tbody></table>
      </div>
      <p>Las funciones de acceso y seguridad no dependen de aceptar contenido externo. El tema se recuerda cuando lo cambias voluntariamente.</p>
    </section>
    <section><h2>3. Contenido externo opcional</h2><p>El mapa interactivo utiliza imágenes de OpenStreetMap. Si lo activas, el proveedor recibe datos técnicos de conexión, como la dirección IP y las solicitudes de imágenes del mapa. Puedes consultar las ubicaciones en la lista sin cargarlo. Al retirar el permiso, se desmonta el mapa y se impiden nuevas cargas; esto no elimina datos ya recibidos por el proveedor.</p><p>No hemos incorporado cookies de publicidad ni herramientas de analítica al aviso actual. Los enlaces a Google Maps, WhatsApp y redes sociales abren servicios externos cuando decides utilizarlos; se aplican sus propias condiciones y políticas.</p></section>
    <section><h2>4. Solicitudes de cotización</h2><p>Cuando envías el formulario, los datos que introduces se transmiten a EmailJS y al servicio de correo para atender tu solicitud. Esta operación se inicia al pulsar Enviar solicitud y es independiente de la elección del mapa. No se guardan los campos en cookies ni en almacenamiento local por parte del formulario.</p></section>
    <section><h2>5. Cambiar tus preferencias</h2><p>Puedes elegir solo esenciales, aceptar todas o configurar el mapa. Usa el enlace Preferencias de cookies del pie de página o de tu cuenta en cualquier momento. También puedes borrar las cookies y los datos del sitio desde tu navegador; esto puede cerrar tu sesión y eliminar tus ajustes.</p><SynButton variante="secundario" @click="abrirPreferencias">Modificar preferencias</SynButton></section>
    <section><h2>6. Contacto</h2><p>Para consultas relacionadas con esta política, escríbenos a <a href="mailto:synchrohn@gmail.com">synchrohn@gmail.com</a>.</p></section>
    <p class="syn-politica-fecha">Versión 1 · Noviembre de 2026</p>
  </article>
</template>
<style scoped>
.syn-politica { max-width:900px; padding-block:clamp(28px,5vw,65px); display:grid; gap:24px; color:var(--text); }
.syn-politica-volver,.syn-politica a { color:var(--accent-text); }
.syn-politica header,.syn-politica section { display:grid; gap:12px; padding:clamp(16px,3vw,26px); background:var(--surface); border:1px solid var(--border); border-radius:16px; }
.syn-politica h1 { font-size:clamp(26px,4vw,38px); line-height:1.2; color:var(--text-strong); margin:0; }
.syn-politica h2 { font-size:19px; color:var(--text-strong); margin:0; }
.syn-politica p { font-size:14px; color:var(--text-muted); line-height:1.7; margin:0; }
.syn-politica :deep(.syn-button) { justify-self:start; }
.syn-politica-tabla { overflow:auto; }
.syn-politica table { width:100%; min-width:520px; border-collapse:collapse; font-size:13px; }
.syn-politica th,.syn-politica td { padding:12px; text-align:left; border-bottom:1px solid var(--border); vertical-align:top; overflow-wrap:anywhere; }
.syn-politica th { background:var(--surface-soft); color:var(--text-strong); }
.syn-politica-fecha { font-size:12px!important; }
</style>
