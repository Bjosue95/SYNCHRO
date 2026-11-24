<script setup>
import { computed, shallowRef } from "vue";
import ObjetoEstacion from "./ObjetoEstacion.vue";

const props = defineProps({
  tipo: { type: String, required: true, validator: v => ["personales", "temperatura", "maletas", "encomiendas", "carga"].includes(v) }
});
// Ilustración autónoma para las tarjetas de servicios; no conecta con hardware real.
const modelos = Object.freeze({
  personales: { titulo: "Almacenamiento personal", columnas: 3, filas: 4, prefijo: "P" },
  temperatura: { titulo: "Frío y calor", columnas: 4, filas: 3, prefijo: "T" },
  maletas: { titulo: "Casilleros para maletas", columnas: 5, filas: 2, prefijo: "L" },
  encomiendas: { titulo: "Encomiendas", columnas: 3, filas: 3, prefijo: "E" },
  carga: { titulo: "Carga de dispositivos", columnas: 2, filas: 4, prefijo: "C" }
});
const modelo = computed(() => modelos[props.tipo]);
const celdas = computed(() => Array.from({ length: modelo.value.columnas * modelo.value.filas }, (_, i) => {
  const col = i % modelo.value.columnas;
  const fila = Math.floor(i / modelo.value.columnas);
  const frio = props.tipo === "temperatura" && col < 2;
  const calor = props.tipo === "temperatura" && col >= 2;
  const laptop = props.tipo === "carga" && fila === 0;
  return {
    codigo: `${modelo.value.prefijo}${String(i + 1).padStart(2, "0")}`,
    clase: frio ? "frio" : calor ? "calor" : laptop ? "laptop" : "normal",
    contenido: props.tipo === "temperatura" ? (frio ? "frio" : "caliente")
      : props.tipo === "maletas" ? ["maleta", "mochila", "bolso"][i % 3]
      : props.tipo === "encomiendas" ? "caja"
      : props.tipo === "carga" ? (laptop ? "laptop" : "celular")
      : ["mochila", "bolso", "caja"][i % 3]
  };
}));
const abiertos = shallowRef(new Set());
function alternar(codigo) {
  const siguientes = new Set(abiertos.value);
  if (siguientes.has(codigo)) siguientes.delete(codigo);
  else siguientes.add(codigo);
  abiertos.value = siguientes;
}
</script>

<template>
  <div class="syn-mini-demo" role="group" :aria-label="`Demostración de ${modelo.titulo}`">
    <div class="syn-mini-carcasa">
      <div class="syn-mini-marca">
        <img src="/branding/nombre-azul.png" alt="SYNCHRO" loading="lazy" />
        <span aria-hidden="true"></span>
      </div>
      <div class="syn-mini-rejilla" :style="{ '--cols': modelo.columnas, '--rows': modelo.filas }">
        <button v-for="c in celdas" :key="c.codigo" type="button" class="syn-mini-celda"
          :class="[c.clase, { abierta: abiertos.has(c.codigo) }]"
          :aria-label="`${c.codigo}: ${abiertos.has(c.codigo) ? 'cerrar' : 'abrir'} casillero de demostración`"
          :aria-pressed="abiertos.has(c.codigo)" @click="alternar(c.codigo)">
          <span class="syn-mini-ventana" aria-hidden="true">
            <span class="syn-mini-interior"><ObjetoEstacion :tipo="c.contenido" /></span>
            <span class="syn-mini-puerta">
              <span class="syn-mini-codigo">{{ c.codigo }}</span>
              <span class="syn-mini-braille"></span>
              <span class="syn-mini-tirador"></span>
            </span>
          </span>
        </button>
      </div>
    </div>
    <span class="syn-mini-instruccion">Toca un compartimento para abrirlo.</span>
  </div>
</template>

<style scoped>
.syn-mini-demo { width:100%; max-width:360px; min-width:0; margin:18px 0 12px; }
.syn-mini-carcasa { width:100%; box-sizing:border-box; background:#e9f2f6; border:1.5px solid #7095a7; border-radius:10px; padding:8px; box-shadow:4px 5px 0 rgba(1,48,92,.18); }
.syn-mini-marca { height:25px; display:flex; justify-content:space-between; align-items:center; padding:0 5px 5px; }
.syn-mini-marca img { width:95px; max-height:18px; object-fit:contain; object-position:left; }
.syn-mini-marca span { width:7px; height:7px; border-radius:50%; background:#00b9c7; }
.syn-mini-rejilla { width:100%; display:grid; grid-template-columns:repeat(var(--cols), minmax(0,1fr)); grid-template-rows:repeat(var(--rows),minmax(0,1fr)); gap:4px; height:clamp(115px,14vw,168px); }
.syn-mini-celda { min-width:0; min-height:0; position:relative; display:block; padding:0; cursor:pointer; border:1px solid #97b9c8; border-radius:3px; background:#608394; }
.syn-mini-ventana { position:absolute; inset:0; overflow:hidden; border-radius:inherit; pointer-events:none; }
.syn-mini-interior { position:absolute; inset:0; display:grid; place-items:center; opacity:0; transition:opacity .24s ease; }
.syn-mini-interior :deep(svg) { position:absolute; width:calc(100% - 8px); height:calc(100% - 6px); max-width:64px; max-height:64px; }
.syn-mini-puerta { position:absolute; inset:0; box-sizing:border-box; display:block; text-align:left; padding:4px 5px; border:1px solid #a3c3d0; border-radius:2px; background:#f9fcfd; transition:transform .28s ease; }
.syn-mini-celda.abierta .syn-mini-puerta { transform:translateX(-88%); }
.syn-mini-celda.abierta .syn-mini-interior { opacity:1; }
.syn-mini-celda.frio .syn-mini-puerta { background:#def1fa; border-color:#79b7d0; }
.syn-mini-celda.calor .syn-mini-puerta { background:#faeadd; border-color:#d8ad89; }
.syn-mini-celda:hover .syn-mini-puerta { border-color:#00b9c7; }
.syn-mini-celda:focus-visible { outline:3px solid #00b9c7; outline-offset:2px; z-index:2; }
.syn-mini-codigo { display:block; color:#37576a; font:700 8px/1.1 ui-monospace,monospace; }
.syn-mini-braille { width:9px; height:8px; display:block; margin-top:2px; background-image:radial-gradient(circle,#698c9c .8px,transparent 1px); background-size:4px 4px; }
.syn-mini-tirador { position:absolute; width:3px; height:12px; right:4px; top:calc(50% - 6px); border-radius:3px; background:#7ca4b3; }
.syn-mini-instruccion { display:block; margin-top:8px; color:var(--text-muted); font-size:11px; line-height:1.4; }
@media(max-width:690px) { .syn-mini-rejilla { height:150px; } }
@media (prefers-reduced-motion: reduce) { .syn-mini-puerta, .syn-mini-interior { transition:none; } }
</style>
