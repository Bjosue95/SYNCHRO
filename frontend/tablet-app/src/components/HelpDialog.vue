<script setup>
import { computed, nextTick, ref } from "vue";
import { temasAyuda } from "../services/temasAyuda.js";

const props = defineProps({ oscuro: { type: Boolean, default: false } });
const dialogo = ref(null);
const temaActivo = ref("");
const tituloDetalle = ref(null);
const tituloInicio = ref(null);
const tema = computed(() => temasAyuda.find(t => t.id === temaActivo.value));
let origen = null;

function icono(ruta) {
  return ruta + (props.oscuro ? "-oscuro" : "") + ".svg";
}
async function abrir() {
  if (!dialogo.value || dialogo.value.open) return;
  origen = document.activeElement;
  temaActivo.value = "";
  dialogo.value.showModal();
  await nextTick();
  tituloInicio.value?.focus();
}
function cerrar() { dialogo.value?.close(); }
function alCerrar() {
  temaActivo.value = "";
  if (origen instanceof HTMLElement && origen.isConnected) origen.focus({preventScroll:true});
  origen = null;
}
async function elegir(id) {
  if (!temasAyuda.some(t => t.id === id)) return;
  temaActivo.value = id;
  await nextTick();
  tituloDetalle.value?.focus();
}
async function volver() {
  const anterior = temaActivo.value;
  temaActivo.value = "";
  await nextTick();
  dialogo.value?.querySelector('[data-tema="' + anterior + '"]')?.focus();
}
defineExpose({ abrir, cerrar });
</script>

<template>
  <dialog ref="dialogo" class="centro-ayuda" :class="{ oscuro }"
    aria-labelledby="titulo-ayuda" @close="alCerrar" @keydown.stop>
    <header class="ayuda-cabecera">
      <span class="ayuda-emblema"><img :src="icono('/iconos/complementarios/23_ayuda')" alt="" /></span>
      <div class="ayuda-titulos">
        <h2 id="titulo-ayuda" ref="tituloInicio" tabindex="-1">Centro de ayuda</h2>
        <p>Encuentra orientación para utilizar SYNCHRO.</p>
      </div>
      <button type="button" class="ayuda-x" aria-label="Cerrar ayuda" @click="cerrar">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18"/></svg>
      </button>
    </header>

    <div class="ayuda-cuerpo">
      <template v-if="!tema">
        <p class="ayuda-indicacion">¿En qué podemos ayudarte?</p>
        <div class="ayuda-temas" aria-label="Temas de ayuda">
          <button v-for="item in temasAyuda" :key="item.id" type="button" class="ayuda-tema"
            :data-tema="item.id" @click="elegir(item.id)">
            <span class="ayuda-icono"><img :src="icono(item.icono)" alt=""/></span>
            <span class="ayuda-tema-texto"><strong>{{ item.titulo }}</strong><small>{{ item.resumen }}</small></span>
            <img class="ayuda-flecha" :src="icono('/iconos/complementarios/29_continuar')" alt=""/>
          </button>
        </div>
      </template>
      <section v-else class="ayuda-detalle" aria-labelledby="ayuda-detalle-titulo">
        <div class="ayuda-detalle-cabecera">
          <span class="ayuda-icono"><img :src="icono(tema.icono)" alt=""/></span>
          <div><h3 ref="tituloDetalle" id="ayuda-detalle-titulo" tabindex="-1">{{ tema.titulo }}</h3><p>{{ tema.resumen }}</p></div>
        </div>
        <ol class="ayuda-pasos">
          <li v-for="(paso,i) in tema.pasos" :key="i"><span class="ayuda-numero" aria-hidden="true">{{ i+1 }}</span><span>{{ paso }}</span></li>
        </ol>
        <aside class="ayuda-nota">
          <img :src="icono('/iconos/complementarios/26_alerta')" alt=""/>
          <div><strong>Ten en cuenta</strong><p>{{ tema.nota }}</p></div>
        </aside>
      </section>
    </div>
    <footer class="ayuda-pie">
      <button v-if="tema" class="ayuda-volver" type="button" @click="volver">
        <img :src="icono('/iconos/complementarios/28_regresar')" alt=""/> Volver a temas
      </button>
      <span v-else class="ayuda-pie-texto">Puedes consultar esta ayuda sin cancelar tu operación.</span>
      <button class="ayuda-cerrar" type="button" @click="cerrar">Cerrar ayuda</button>
    </footer>
  </dialog>
</template>

<style scoped>
.centro-ayuda {
  --ay-fondo:#fff; --ay-superficie:#f2f7fa; --ay-texto:#01305c; --ay-sec:#4c6578;
  --ay-borde:#d6e4ec; --ay-acento:#008b9a; --ay-boton:#01305c; --ay-sobre:#fff;
  --ay-numero:#e2f5f7; --ay-nota:#edf7fa;
  width:min(900px,calc(100vw - 32px)); max-width:none; max-height:calc(100dvh - 36px);
  margin:auto; padding:0; border:1px solid var(--ay-borde); border-radius:24px;
  background:var(--ay-fondo); color:var(--ay-texto); overflow:hidden;
  box-shadow:0 24px 75px #001b3660; font-family:inherit;
}
.centro-ayuda[open] { display:flex; flex-direction:column; animation:ayuda-entrada .2s ease-out; }
.centro-ayuda.oscuro {
  --ay-fondo:#10304b; --ay-superficie:#173d59; --ay-texto:#f2f9ff; --ay-sec:#c4dce9;
  --ay-borde:#3b5d76; --ay-acento:#5ec6e8; --ay-boton:#5ec6e8; --ay-sobre:#06243c;
  --ay-numero:#194b64; --ay-nota:#173d59;
}
.centro-ayuda::backdrop { background:#001326b8; backdrop-filter:blur(3px); }
.ayuda-cabecera { flex:none; display:flex; gap:15px; align-items:center; padding:clamp(16px,2.6vw,25px); border-bottom:1px solid var(--ay-borde); }
.ayuda-emblema,.ayuda-icono { display:grid; place-items:center; flex:none; width:56px; height:56px; border:1px solid var(--ay-borde); background:var(--ay-superficie); border-radius:15px; }
.ayuda-emblema img { width:35px; height:35px; object-fit:contain; }
.ayuda-titulos { flex:1; min-width:0; }
.ayuda-titulos h2 { font-size:clamp(22px,3vw,30px); line-height:1.2; font-weight:700; }
.ayuda-titulos p,.ayuda-detalle-cabecera p { color:var(--ay-sec); font-size:clamp(13px,1.7vw,15px); margin-top:4px; }
.ayuda-x { flex:none; display:grid; place-items:center; width:46px; height:46px; background:var(--ay-superficie); border:1px solid var(--ay-borde); border-radius:12px; color:var(--ay-texto); }
.ayuda-x svg { width:24px; height:24px; }
.ayuda-cuerpo { min-height:0; overflow-y:auto; overscroll-behavior:contain; padding:clamp(16px,2.6vw,25px); }
.ayuda-indicacion { font-size:17px; font-weight:600; margin:0 0 14px; }
.ayuda-temas { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:12px; }
.ayuda-tema { display:flex; align-items:center; gap:12px; min-width:0; min-height:100px; text-align:left; padding:14px; color:var(--ay-texto); border:1px solid var(--ay-borde); border-radius:16px; background:var(--ay-fondo); transition:background .15s,border-color .15s; }
.ayuda-tema:hover,.ayuda-tema:active { background:var(--ay-superficie); border-color:var(--ay-acento); }
.ayuda-icono { width:52px; height:52px; }
.ayuda-icono img { width:35px; height:35px; object-fit:contain; }
.ayuda-tema-texto { display:grid; gap:4px; flex:1; min-width:0; }
.ayuda-tema-texto strong { font-size:clamp(14px,1.9vw,17px); line-height:1.3; font-weight:650; }
.ayuda-tema-texto small { color:var(--ay-sec); font-size:13px; line-height:1.4; }
.ayuda-flecha { width:19px; height:19px; flex:none; object-fit:contain; }
.ayuda-detalle { display:grid; gap:15px; }
.ayuda-detalle-cabecera { display:flex; align-items:center; gap:12px; }
.ayuda-detalle-cabecera h3 { font-size:clamp(20px,2.6vw,25px); line-height:1.25; font-weight:650; }
.ayuda-pasos { list-style:none; padding:0; margin:0; display:grid; gap:9px; }
.ayuda-pasos li { display:flex; align-items:flex-start; gap:12px; border:1px solid var(--ay-borde); border-radius:12px; padding:12px 14px; font-size:clamp(14px,1.8vw,16px); line-height:1.48; }
.ayuda-numero { display:grid; place-items:center; flex:0 0 32px; height:32px; border-radius:10px; background:var(--ay-numero); color:var(--ay-texto); font-size:15px; font-weight:650; }
.ayuda-nota { display:flex; gap:12px; align-items:flex-start; padding:14px; border-radius:12px; background:var(--ay-nota); border:1px solid var(--ay-borde); border-left:3px solid var(--ay-acento); }
.ayuda-nota img { width:26px; height:26px; flex:none; }
.ayuda-nota strong { font-size:14px; font-weight:650; }
.ayuda-nota p { margin-top:3px; color:var(--ay-sec); font-size:14px; line-height:1.5; }
.ayuda-pie { flex:none; display:flex; align-items:center; justify-content:space-between; gap:12px; padding:14px clamp(16px,2.6vw,25px); border-top:1px solid var(--ay-borde); }
.ayuda-pie-texto { color:var(--ay-sec); font-size:13px; }
.ayuda-cerrar,.ayuda-volver { min-height:50px; padding:10px 20px; display:flex; align-items:center; justify-content:center; gap:8px; border:1px solid var(--ay-borde); border-radius:12px; font-weight:650; font-size:15px; white-space:nowrap; }
.ayuda-cerrar { background:var(--ay-boton); color:var(--ay-sobre); margin-left:auto; min-width:150px; }
.ayuda-volver { background:var(--ay-superficie); color:var(--ay-texto); }
.ayuda-volver img { width:22px; height:22px; object-fit:contain; }
button:focus-visible { outline:3px solid var(--ay-acento); outline-offset:3px; }
h2:focus,h3:focus { outline:none; }
@keyframes ayuda-entrada { from { opacity:0; transform:translateY(8px); } to { opacity:1; transform:translateY(0); } }
@media(max-width:600px) {
  .centro-ayuda { width:calc(100vw - 20px); max-height:calc(100dvh - 20px); border-radius:17px; }
  .ayuda-temas { grid-template-columns:1fr; gap:8px; }
  .ayuda-tema { min-height:72px; padding:9px 11px; }
  .ayuda-icono,.ayuda-emblema { width:43px; height:43px; border-radius:11px; }
  .ayuda-icono img,.ayuda-emblema img { width:29px; height:29px; }
  .ayuda-pie-texto { display:none; }
  .ayuda-pie { flex-wrap:wrap; }
  .ayuda-cerrar,.ayuda-volver { flex:1; min-width:0; padding:10px; }
}
@media(max-height:650px) and (min-width:601px) {
  .ayuda-cabecera,.ayuda-cuerpo { padding:13px 20px; }
  .ayuda-pie { padding:10px 20px; }
  .ayuda-tema { min-height:76px; padding:9px 12px; }
  .ayuda-detalle { gap:9px; }
  .ayuda-pasos { gap:6px; }
  .ayuda-pasos li { padding:8px 12px; font-size:14px; }
  .ayuda-nota { padding:9px 12px; }
}
@media(prefers-reduced-motion:reduce) { .centro-ayuda[open] { animation:none; } .ayuda-tema { transition:none; } }
</style>
