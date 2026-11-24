<script setup>
import { computed, ref, useId } from "vue";
import AppIcon from "./AppIcon.vue";
import ObjetoEstacion from "./ObjetoEstacion.vue";

// Ilustración interactiva local: no ejecuta depósitos ni consulta estaciones.
const etapas = Object.freeze([
  { nombre: "Solicitud", titulo: "Prepara tu envío", detalle: "Selecciona un servicio y consulta sus pasos.", icono: "reservas" },
  { nombre: "Consentimiento", titulo: "El destinatario autoriza", detalle: "El consentimiento por WhatsApp forma parte de las encomiendas.", icono: "whatsapp" },
  { nombre: "Entrega y retiro", titulo: "La entrega se verifica", detalle: "La apertura ilustra la entrega después de verificar el acceso.", icono: "seguridad" }
]);
const activa = ref(0);
const seleccionado = ref("");
const aperturaManual = ref(false);
const etapa = computed(() => etapas[activa.value]);
const fase = computed(() => ["01", "02", "03"][activa.value]);
const puertaAbierta = computed(() => activa.value === 2);
const idEstacion = useId();
const recorte = codigo => `${idEstacion}-puerta-${codigo}`;
const ladoObjeto = celda => Math.min(celda.h - 10, celda.w * .56, 64);

// Tres columnas juntas, seguidas por la tablet y el módulo de carga.
const casilleros = Object.freeze([
  { codigo: "M01", x: 43, y: 113, w: 121, h: 72, tipo: "normal", objeto: "mochila" },
  { codigo: "M02", x: 43, y: 191, w: 121, h: 72, tipo: "frio", objeto: "frio" },
  { codigo: "L01", x: 43, y: 269, w: 121, h: 101, tipo: "maleta", objeto: "maleta" },
  { codigo: "S01", x: 170, y: 113, w: 98, h: 44, tipo: "normal", objeto: "caja" },
  { codigo: "S02", x: 170, y: 163, w: 98, h: 44, tipo: "normal", objeto: "caja" },
  { codigo: "M04", x: 170, y: 213, w: 98, h: 75, tipo: "normal", objeto: "caja" },
  { codigo: "M05", x: 170, y: 294, w: 98, h: 76, tipo: "normal", objeto: "mochila" },
  { codigo: "M03", x: 278, y: 113, w: 121, h: 72, tipo: "normal", objeto: "caja" },
  { codigo: "M06", x: 278, y: 191, w: 121, h: 72, tipo: "calor", objeto: "caliente" },
  { codigo: "L02", x: 278, y: 269, w: 121, h: 101, tipo: "maleta", objeto: "maleta" }
]);
// Zona derecha: pantalla superior y 2 columnas x 4 filas de carga.
const cargas = Object.freeze(Array.from({ length: 8 }, (_, i) => ({
  codigo: `C${String(i + 1).padStart(2, "0")}`,
  x: 533 + (i % 2) * 80,
  y: 204 + Math.floor(i / 2) * 43,
  w: 73, h: 37,
  tipo: i < 2 ? "laptop" : "celular",
  objeto: i < 2 ? "laptop" : "celular"
})));
function estaAbierto(codigo) {
  return seleccionado.value === codigo || (puertaAbierta.value && !aperturaManual.value && codigo === "M03");
}
function cambiarPuerta(codigo) {
  const abierta = estaAbierto(codigo);
  aperturaManual.value = true;
  seleccionado.value = abierta ? "" : codigo;
}
function elegir(indice) {
  if (indice >= 0 && indice < etapas.length) { activa.value = indice; seleccionado.value = ""; aperturaManual.value = false; }
}
function controlarTeclado(evento) {
  if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(evento.key)) return;
  evento.preventDefault();
  if (evento.key === "Home") elegir(0);
  else if (evento.key === "End") elegir(etapas.length - 1);
  else elegir((activa.value + (evento.key === "ArrowRight" ? 1 : etapas.length - 1)) % etapas.length);
}
</script>

<template>
  <div class="syn-journey syn-v4" aria-label="Ilustración interactiva de la estación multifuncional SYNCHRO">
    <div class="syn-v4-stage" :data-step="activa">
      <div class="syn-v4-scene" role="region" tabindex="0" aria-label="Estación multifuncional. En pantallas pequeñas puedes desplazarla horizontalmente.">
        <svg class="syn-v4-station" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 732 410"
          role="group" aria-label="Estación con compartimentos normales, refrigerados, calientes, maletas, tablet y carga de dispositivos">
          <defs>
            <clipPath v-for="c in [...casilleros, ...cargas]" :id="recorte(c.codigo)" :key="c.codigo" clipPathUnits="userSpaceOnUse">
              <rect :x="c.x" :y="c.y" :width="c.w" :height="c.h" rx="4" />
            </clipPath>
          </defs>
          <rect x="19" y="29" width="695" height="370" rx="15" class="syn-v4-housing" />
          <image href="/branding/marca-completa.png" x="46" y="44" width="245" height="53" preserveAspectRatio="xMinYMid meet" />
          <circle cx="688" cy="71" r="7" fill="#00b9c7" stroke="#cdeff0" stroke-width="5" />

          <g v-for="c in casilleros" :key="c.codigo" class="syn-v4-cell" role="button" tabindex="0"
            :aria-label="`${c.codigo}, ${c.tipo}, ${estaAbierto(c.codigo) ? 'cerrar' : 'abrir'} demostración`"
            :aria-pressed="estaAbierto(c.codigo)" @click="cambiarPuerta(c.codigo)"
            @keydown.enter.prevent="cambiarPuerta(c.codigo)" @keydown.space.prevent="cambiarPuerta(c.codigo)">
            <rect :x="c.x" :y="c.y" :width="c.w" :height="c.h" rx="4" class="syn-v4-cavity" />
            <g :clip-path="`url(#${recorte(c.codigo)})`">
            <ObjetoEstacion :tipo="c.objeto" :x="c.x + (c.w - ladoObjeto(c)) / 2" :y="c.y + (c.h - ladoObjeto(c)) / 2"
              :width="ladoObjeto(c)" :height="ladoObjeto(c)" class="syn-v4-object" :class="{ visible: estaAbierto(c.codigo) }" />
            <g class="syn-v4-door" :class="[c.tipo, { 'is-open': estaAbierto(c.codigo) }]" :style="{ '--door-shift': `${-c.w * .82}px` }">
              <rect :x="c.x" :y="c.y" :width="c.w" :height="c.h" rx="4" class="syn-v4-front" />
              <text :x="c.x + 9" :y="c.y + 17" class="syn-v4-code">{{ c.codigo }}</text>
              <g v-for="p in 4" :key="p">
                <circle :cx="c.x + 10 + ((p - 1) % 2) * 5" :cy="c.y + 24 + Math.floor((p - 1) / 2) * 5" r="1.15" fill="#698997" />
              </g>
              <rect :x="c.x + c.w - 11" :y="c.y + c.h * .36" width="3.5" :height="c.h * .28" rx="1.8" fill="#7ba2b0" />
            </g>
            </g>
          </g>

          <g class="syn-v4-tablet" transform="translate(131 0)">
          <rect x="278" y="110" width="107" height="262" rx="7" fill="#d8e7ed" stroke="#83a7b5" stroke-width="1.5" />
          <rect x="289" y="126" width="85" height="127" rx="6" fill="#01305c" stroke="#345a75" stroke-width="2" />
          <image href="/branding/isotipo-oscuro.png" x="311" y="151" width="42" height="48" preserveAspectRatio="xMidYMid meet" />
          <path d="M308 218h45M317 225h28" stroke="#4c8194" stroke-width="3" stroke-linecap="round" />
          <circle cx="311" cy="285" r="9" class="syn-v4-ring" /><circle cx="353" cy="285" r="9" class="syn-v4-ring" />
          <rect x="307" y="316" width="11" height="11" rx="2" class="syn-v4-mini" />
          <rect x="344" y="316" width="11" height="11" rx="2" class="syn-v4-mini" />
          <text x="331" y="358" text-anchor="middle" class="syn-v4-mark">SYNCHRO</text>
          </g>

          <rect class="syn-v4-charge-housing" x="525" y="110" width="169" height="262" rx="7" fill="#dfeaf0" stroke="#83a7b5" stroke-width="1.5" />
          <rect class="syn-v4-charge-screen" x="532" y="117" width="155" height="80" rx="5" fill="#e2edf1" stroke="#91b0bd" />
          <rect x="540" y="124" width="139" height="66" rx="5" fill="#01305c" />
          <g class="syn-v4-screen-brand" aria-label="SYNCHRO">
            <image href="/branding/isotipo-oscuro.png" x="550" y="141" width="27" height="32" preserveAspectRatio="xMidYMid meet" />
            <image href="/branding/nombre-blanco.png" x="584" y="148" width="85" height="18" preserveAspectRatio="xMidYMid meet" />
          </g>
          <path d="M584 178h77" stroke="#517e95" stroke-width="3" stroke-linecap="round" />

          <g v-for="c in cargas" :key="c.codigo" class="syn-v4-cell" role="button" tabindex="0"
            :aria-label="`${c.codigo}, carga de ${c.tipo}, ${estaAbierto(c.codigo) ? 'cerrar' : 'abrir'} demostración`"
            :aria-pressed="estaAbierto(c.codigo)" @click="cambiarPuerta(c.codigo)"
            @keydown.enter.prevent="cambiarPuerta(c.codigo)" @keydown.space.prevent="cambiarPuerta(c.codigo)">
            <rect :x="c.x" :y="c.y" :width="c.w" :height="c.h" rx="4" class="syn-v4-cavity" />
            <g :clip-path="`url(#${recorte(c.codigo)})`">
            <ObjetoEstacion :tipo="c.objeto" :x="c.x + (c.w - ladoObjeto(c)) / 2" :y="c.y + (c.h - ladoObjeto(c)) / 2"
              :width="ladoObjeto(c)" :height="ladoObjeto(c)" class="syn-v4-object" :class="{ visible: estaAbierto(c.codigo) }" />
            <g class="syn-v4-door" :class="{ 'is-open': estaAbierto(c.codigo) }" :style="{ '--door-shift': `${-c.w * .82}px` }">
              <rect :x="c.x" :y="c.y" :width="c.w" :height="c.h" rx="4" class="syn-v4-front" />
              <text :x="c.x + 10" :y="c.y + 23" class="syn-v4-code">{{ c.codigo }}</text>
              <rect :x="c.x + c.w - 12" :y="c.y + 12" width="3" height="13" rx="1.5" fill="#7ba2b0" />
            </g>
            </g>
          </g>
          <rect x="38" y="381" width="656" height="5" rx="2.5" fill="#698d9c" />
        </svg>
        <span class="syn-v4-hint">Pulsa un casillero para abrirlo o cerrarlo.<span class="syn-v4-hint-movil"> Desliza para ver toda la estación.</span></span>
      </div>
      <aside class="syn-v4-note" aria-live="polite">
        <span class="syn-v4-note-icon"><AppIcon :nombre="etapa.icono" :tamano="27" tema-forzado="claro" /></span>
        <span class="syn-v4-label">Paso {{ fase }} / 03</span>
        <strong>{{ etapa.nombre }}</strong>
        <p>{{ seleccionado ? `Casillero ${seleccionado} seleccionado en la demostración.` : etapa.titulo }}</p>
      </aside>
    </div>

    <div class="syn-journey-controls">
      <div class="syn-journey-description">
        <span class="syn-journey-overline">De la solicitud a la entrega</span>
        <Transition name="syn-story" mode="out-in">
          <div :key="activa" class="syn-journey-copy" aria-live="polite">
            <h3>{{ etapa.titulo }}</h3>
            <p>{{ etapa.detalle }}</p>
          </div>
        </Transition>
      </div>
      <div class="syn-journey-steps" role="group" aria-label="Etapas del servicio" @keydown="controlarTeclado">
        <button v-for="(item, i) in etapas" :key="item.nombre" type="button"
          class="syn-journey-step" :class="{ selected: activa === i }"
          :aria-label="`Ver paso ${i + 1}: ${item.nombre}`" :aria-pressed="activa === i"
          @click="elegir(i)">
          <span class="syn-journey-number">0{{ i + 1 }}</span>
          <span class="syn-journey-step-label">{{ item.nombre }}</span>
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.syn-v4 { width:100%; min-width:0; border:1px solid var(--border); border-radius:20px; background:var(--surface); overflow:hidden; box-shadow:0 15px 40px rgba(1,48,92,.08); }
.syn-v4-stage { display:grid; grid-template-columns:minmax(0,1fr) 195px; align-items:center; gap:12px; min-width:0; padding:22px; background-color:#e6eff3; background-image:linear-gradient(rgba(25,75,103,.065) 1px,transparent 1px),linear-gradient(90deg,rgba(25,75,103,.065) 1px,transparent 1px); background-size:42px 42px; }
.syn-v4-scene { min-width:0; width:100%; display:flex; flex-direction:column; align-items:center; }
/* Estrechar el lienzo conserva la escala y altura visibles de las puertas existentes. */
.syn-v4-station { width:calc(100% * 732 / 900); max-width:calc(940px * 732 / 900); height:auto; display:block; overflow:visible; }
.syn-v4-housing { fill:#eef5f7; stroke:#557d90; stroke-width:2; filter:drop-shadow(7px 7px 3px rgba(28,65,80,.13)); }
.syn-v4-cavity { fill:#D8E5EC; stroke:#93B1C0; stroke-width:1.2; }
.syn-v4-door { transition:transform .28s ease-in-out; }
.syn-v4-door.is-open { transform:translateX(var(--door-shift)); }
.syn-v4-object { opacity:0; pointer-events:none; transition:opacity .2s ease; }
.syn-v4-object.visible { opacity:1; }
.syn-v4-front { fill:#f9fcfd; stroke:#96b6c4; stroke-width:1.2; }
.syn-v4-door.frio .syn-v4-front { fill:#ddf1fa; stroke:#78b9d2; }
.syn-v4-door.calor .syn-v4-front { fill:#fae9da; stroke:#d7a77b; }
.syn-v4-code { fill:#2e536c; font-size:12px; font-weight:700; font-family:ui-monospace,Consolas,monospace; }
.syn-v4-mark { fill:#466b80; font-weight:700; font-size:11px; }
.syn-v4-ring { fill:#eef5f7; stroke:#00b9c7; stroke-width:3; }
.syn-v4-mini { fill:#eef5f7; stroke:#577d92; stroke-width:2; }
.syn-v4-cell { outline:none; cursor:pointer; }
.syn-v4-cell:hover .syn-v4-front { stroke:#00b9c7; stroke-width:2.2; }
.syn-v4-cell:focus-visible { outline:none; }
.syn-v4-cell:focus-visible > .syn-v4-cavity,
.syn-v4-cell:focus-visible .syn-v4-front { stroke:#01305c; stroke-width:2.2; }
.syn-v4-hint { color:#46677a; font-size:11px; text-align:center; margin-top:-3px; }
.syn-v4-hint-movil { display:none; }
.syn-v4-note { box-sizing:border-box; width:100%; min-width:0; padding:20px 16px; background:rgba(255,255,255,.98); border:1px solid #cfdee5; border-radius:15px; box-shadow:0 9px 24px rgba(1,48,92,.09); display:flex; flex-direction:column; gap:8px; color:#01305c; }
.syn-v4-note-icon { color:#00a8bd; margin-bottom:7px; }
.syn-v4-label { color:#587485; font-size:12px; }
.syn-v4-note strong { font-size:20px; line-height:1.25; }
.syn-v4-note p { color:#496579; font-size:12px; line-height:1.5; margin:0; overflow-wrap:anywhere; }
.syn-journey-controls { padding:21px 24px; display:grid; grid-template-columns:minmax(0,1fr) minmax(0,1.1fr); align-items:center; gap:24px; }
.syn-journey-overline { color:var(--text-muted); font-size:11px; }
.syn-journey-copy h3 { margin:6px 0; color:var(--text-strong); font-size:18px; }
.syn-journey-copy p { margin:0; color:var(--text-muted); font-size:12px; line-height:1.5; }
.syn-journey-steps { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:8px; }
.syn-journey-step { display:flex; align-items:center; justify-content:center; gap:7px; min-width:0; min-height:46px; border:1px solid var(--border); background:var(--surface-soft); border-radius:9px; color:var(--text-muted); font-size:12px; cursor:pointer; }
.syn-journey-step.selected { border-color:var(--brand-accent); background:var(--surface-accent); color:var(--text-strong); }
.syn-journey-number { color:var(--accent-text); font-size:11px; font-weight:700; }
.syn-story-enter-active,.syn-story-leave-active { transition:opacity .18s, transform .18s; }
.syn-story-enter-from { opacity:0; transform:translateY(5px); }
.syn-story-leave-to { opacity:0; transform:translateY(-5px); }
@media(max-width:790px) { .syn-v4-stage { grid-template-columns:1fr; padding:14px; } .syn-v4-note { max-width:100%; display:grid; grid-template-columns:30px auto 1fr; align-items:center; gap:6px 12px; } .syn-v4-note-icon { grid-row:span 2; margin:0; } .syn-v4-note p { grid-column:2/-1; } }
@media(max-width:580px) { .syn-journey-controls { grid-template-columns:1fr; padding:16px; gap:14px; } .syn-journey-step { flex-direction:column; gap:3px; font-size:10px; padding:5px 2px; } .syn-v4-hint { font-size:10px; } .syn-v4-stage { padding:8px; } }
@media(max-width:650px) {
  .syn-v4-scene { display:block; overflow-x:auto; overscroll-behavior-x:contain; scrollbar-width:thin; }
  .syn-v4-station { min-width:calc(640px * 732 / 900); }
  .syn-v4-hint { display:block; margin:7px 0; }
  .syn-v4-hint-movil { display:inline; }
}
@media (prefers-reduced-motion: reduce) { .syn-v4-door,.syn-v4-object,.syn-story-enter-active,.syn-story-leave-active { transition:none; } }
</style>
