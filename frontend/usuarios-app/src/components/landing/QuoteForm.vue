<script setup>
import { computed, reactive, ref } from "vue";
import AppIcon from "../ui/AppIcon.vue";
import SynButton from "../ui/SynButton.vue";
import { empresa, soluciones } from "../../data/empresa.js";
import { enviarCotizacionEmail } from "../../services/cotizacionesEmail.js";

const datos = reactive({
  nombre: "", correo: "", telefono: "", organizacion: "",
  solucion: "modulos", ciudad: "", descripcion: ""
});
const mensajeEstado = ref("");
const error = ref(false);
const enviando = ref(false);
const solucionSeleccionada = computed(() =>
  soluciones.find(s => s.id === datos.solucion)?.nombre ?? "Consulta"
);

function validar() {
  const nombre = datos.nombre.trim();
  const email = datos.correo.trim();
  const texto = datos.descripcion.trim();
  if (nombre.length < 3 || nombre.length > 100) return "Escribe tu nombre completo.";
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 180)
    return "Escribe un correo electrónico válido.";
  if (datos.telefono && !/^[+\d\s()-]{7,22}$/.test(datos.telefono.trim()))
    return "Revisa el número de teléfono.";
  if (texto.length < 15 || texto.length > 1800)
    return "Describe tu proyecto en al menos 15 caracteres.";
  if (!soluciones.some(s => s.id === datos.solucion))
    return "Selecciona una solución.";
  return "";
}

async function enviarSolicitud() {
  if (enviando.value) return;
  mensajeEstado.value = validar();
  error.value = Boolean(mensajeEstado.value);
  if (error.value) return;
  enviando.value = true;
  try {
    await enviarCotizacionEmail(datos, solucionSeleccionada.value, import.meta.env);
    Object.assign(datos, { nombre: "", correo: "", telefono: "", organizacion: "",
      solucion: "modulos", ciudad: "", descripcion: "" });
    mensajeEstado.value = "Tu solicitud se ha enviado correctamente. Nuestro equipo se pondrá en contacto contigo.";
  } catch (e) {
    error.value = true;
    mensajeEstado.value = e?.codigo === "CONFIGURACION"
      ? "El envío de cotizaciones aún no está disponible. Escríbenos a " + empresa.correo + "."
      : "No pudimos confirmar el envío. Tus datos siguen aquí. Inténtalo nuevamente o escríbenos a " + empresa.correo + ".";
  } finally {
    enviando.value = false;
  }
}
</script>

<template>
  <section id="cotizacion" class="syn-quote">
    <div class="contenedor syn-quote-grid">
      <div class="syn-quote-intro">
        <span class="syn-quote-overline">SYNCHRO PARA EMPRESAS E INSTITUCIONES</span>
        <h2>Hablemos de tu próximo espacio.</h2>
        <p>
          ¿Te interesa incorporar casilleros inteligentes en una universidad,
          aeropuerto, empresa o centro comercial? Cuéntanos qué necesitas
          y podremos preparar una propuesta.
        </p>
        <div class="syn-quote-benefits">
          <div><AppIcon nombre="casillero" :tamano="28" /> Módulos según el lugar y el servicio</div>
          <div><AppIcon nombre="temperatura" :tamano="28" /> Espacios normales y de temperatura controlada</div>
          <div><AppIcon nombre="carga" :tamano="28" /> Estaciones de carga de dispositivos</div>
        </div>
        <p class="syn-quote-small">
          Las características y precios se definen mediante cotización.
        </p>
      </div>

      <div class="syn-quote-card">
        <h3>Solicitar cotización</h3>
        <p class="syn-quote-subtitle">Cuéntanos sobre tu proyecto.</p>
        <form class="syn-quote-form" @submit.prevent="enviarSolicitud">
          <div class="syn-quote-row">
            <label>
              <span>Nombre completo *</span>
              <input :disabled="enviando" v-model="datos.nombre" autocomplete="name" maxlength="100"
                required placeholder="Tu nombre" />
            </label>
            <label>
              <span>Correo electrónico *</span>
              <input :disabled="enviando" v-model="datos.correo" type="email" autocomplete="email"
                maxlength="180" required placeholder="nombre@empresa.com" />
            </label>
          </div>

          <div class="syn-quote-row">
            <label>
              <span>Organización</span>
              <input :disabled="enviando" v-model="datos.organizacion" autocomplete="organization"
                maxlength="100" placeholder="Opcional" />
            </label>
            <label>
              <span>Teléfono</span>
              <input :disabled="enviando" v-model="datos.telefono" type="tel" autocomplete="tel"
                maxlength="22" placeholder="+504..." />
            </label>
          </div>

          <div class="syn-quote-row">
            <label>
              <span>Solución de interés *</span>
              <select :disabled="enviando" v-model="datos.solucion" required>
                <option v-for="opcion in soluciones" :key="opcion.id" :value="opcion.id">
                  {{ opcion.nombre }}
                </option>
              </select>
            </label>
            <label>
              <span>Ciudad o ubicación</span>
              <input :disabled="enviando" v-model="datos.ciudad" maxlength="120" placeholder="Ej. Comayagua" />
            </label>
          </div>

          <label>
            <span>Cuéntanos tu proyecto *</span>
            <textarea :disabled="enviando" v-model="datos.descripcion" required minlength="15"
              maxlength="1800" rows="4"
              placeholder="Qué servicio necesitas y en qué lugar lo instalarías"></textarea>
          </label>

          <p v-if="mensajeEstado" role="status"
            class="syn-quote-notice" :class="{ error }">{{ mensajeEstado }}</p>
          <div class="syn-quote-actions">
            <SynButton tipo="submit" :cargando="enviando">
              {{ enviando ? "Enviando solicitud..." : "Enviar solicitud" }}
              <AppIcon v-if="!enviando" nombre="enviar" :tamano="19" />
            </SynButton>
          </div>
          <p class="syn-quote-disclaimer">
            Usaremos tus datos de contacto y la información del proyecto para atender tu solicitud de cotización.
          </p>
        </form>
      </div>
    </div>
  </section>
</template>

<style scoped>
.syn-quote { padding: clamp(55px, 7vw, 100px) 0; background: var(--surface-soft); border-top: 1px solid var(--border); }
.syn-quote-grid { display:grid; grid-template-columns: minmax(0,.88fr) minmax(0,1.12fr); gap: clamp(24px,5vw,85px); align-items: start; }
.syn-quote-intro { padding-top: 19px; }
.syn-quote-overline { font-size: 12px; letter-spacing:.065em; color:var(--accent-text); font-weight:750; }
.syn-quote-intro h2 { color:var(--text-strong); font-size:clamp(32px,3.7vw,49px); letter-spacing:-.043em; line-height:1.14; font-weight:650; margin:15px 0 23px; }
.syn-quote-intro > p { max-width:420px; font-size:15px; line-height:1.8; color:var(--text-muted); }
.syn-quote-benefits { display:grid; gap:19px; margin:35px 0; }
.syn-quote-benefits div { display:flex; align-items:center; gap:15px; color:var(--text); font-size:13px; line-height:1.55; }
.syn-quote-small { font-size:12px !important; }
.syn-quote-card { background:var(--surface); border:1px solid var(--border); border-radius:18px; padding:clamp(21px,3vw,36px); box-shadow:var(--shadow-sm); min-width:0; }
.syn-quote-card h3 { color:var(--text-strong); font-size:25px; font-weight:650; margin:0 0 6px; letter-spacing:-.025em; }
.syn-quote-subtitle { color:var(--text-muted); font-size:13px; margin:0; }
.syn-quote-form { display:grid; gap:17px; margin-top:25px; }
.syn-quote-row { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:15px; }
.syn-quote-form label { display:grid; gap:7px; min-width:0; }
.syn-quote-form label > span { color:var(--text-strong); font-size:12px; font-weight:700; }
.syn-quote-form input, .syn-quote-form select, .syn-quote-form textarea {
  background:var(--surface-soft); color:var(--text); border:1px solid var(--border-strong);
  border-radius:10px; padding:11px 12px; min-width:0; width:100%; font-size:14px;
}
.syn-quote-form textarea { resize:vertical; min-height:106px; }
.syn-quote-form :disabled:not(button) { opacity:.75; cursor:wait; }
.syn-quote-form input:focus-visible,.syn-quote-form textarea:focus-visible,.syn-quote-form select:focus-visible { outline:3px solid var(--ring); outline-offset:2px; }
.syn-quote-actions { display:flex; flex-wrap:wrap; align-items:center; gap:12px; }
.syn-quote-notice { margin:0; padding:12px 14px; background:var(--success-bg); color:var(--success-text); border-radius:9px; font-size:13px; line-height:1.55; }
.syn-quote-notice.error { background:var(--danger-bg); color:var(--danger-text); }
.syn-quote-disclaimer { color:var(--text-subtle); font-size:11px; line-height:1.6; margin:0; }
@media(max-width:900px) { .syn-quote-grid { grid-template-columns:1fr; gap:22px; } .syn-quote-intro { padding-top:0; } }
@media(max-width:560px) { .syn-quote-row { grid-template-columns:1fr; gap:17px; } .syn-quote-card { padding:22px 18px; } }
</style>
