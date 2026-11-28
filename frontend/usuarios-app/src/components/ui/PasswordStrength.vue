<script setup>
import { computed } from "vue";
import AppIcon from "./AppIcon.vue";
import { evaluarFortalezaContrasena } from "../../services/fortalezaContrasena.js";

const props = defineProps({
  contrasena: { type: String, required: true },
  usuario: { type: String, default: "" },
  telefono: { type: String, default: "" }
});
const resultado = computed(() => evaluarFortalezaContrasena(props.contrasena, props.usuario, props.telefono));
</script>

<template>
  <div id="fortaleza-contrasena" class="fortaleza" :data-nivel="resultado.nivel">
    <div class="fortaleza-cabecera">
      <span>Fortaleza estimada</span>
      <strong role="status" aria-live="polite">{{ resultado.etiqueta }}</strong>
    </div>
    <div class="fortaleza-barra" role="meter" aria-label="Fortaleza estimada de la contraseña"
      :aria-valuenow="resultado.nivel" aria-valuemin="0" aria-valuemax="5" :aria-valuetext="resultado.etiqueta">
      <span v-for="segmento in 5" :key="segmento" :class="{ lleno: segmento <= resultado.nivel }"></span>
    </div>
    <p class="fortaleza-mensaje">{{ resultado.mensaje }}</p>
    <div class="fortaleza-requisito">
      <span class="fortaleza-marca" :class="{ cumple: resultado.reglas[0].cumple }">
        <AppIcon v-if="resultado.reglas[0].cumple" nombre="check" :tamano="15"/>
        <span v-else class="fortaleza-punto" aria-hidden="true"></span>
      </span>
      <span>{{ resultado.reglas[0].texto }}</span>
      <span class="fortaleza-estado">{{ resultado.reglas[0].cumple ? "Cumplido" : "Obligatorio" }}</span>
    </div>
    <p class="fortaleza-subtitulo">Recomendaciones opcionales</p>
    <ul class="fortaleza-reglas">
      <li v-for="regla in resultado.reglas.slice(1)" :key="regla.clave">
        <span class="fortaleza-marca" :class="{ cumple: regla.cumple }">
          <AppIcon v-if="regla.cumple" nombre="check" :tamano="15"/>
          <span v-else class="fortaleza-punto" aria-hidden="true"></span>
        </span>
        <span>{{ regla.texto }}<span class="fortaleza-sr">: {{ regla.cumple ? "Presente" : "No presente" }}</span></span>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.fortaleza { --nivel:#b42318; --nivel-barra:#d92d20; --marca:#13754b; display:grid; gap:9px; min-width:0; padding:13px 14px; border:1px solid var(--border); border-radius:12px; background:var(--surface-soft); }
.fortaleza[data-nivel="2"] { --nivel:#a34a0a; --nivel-barra:#ea8a20; }
.fortaleza[data-nivel="3"] { --nivel:#806000; --nivel-barra:#d6a413; }
.fortaleza[data-nivel="4"] { --nivel:#13754b; --nivel-barra:#2b9a69; }
.fortaleza[data-nivel="5"] { --nivel:#087348; --nivel-barra:#087f5b; }
:global(:root[data-theme="oscuro"]) .fortaleza { --nivel:#ffaaa2; --nivel-barra:#f87171; --marca:#83ddb1; }
:global(:root[data-theme="oscuro"]) .fortaleza[data-nivel="2"] { --nivel:#ffbc79; --nivel-barra:#f59e49; }
:global(:root[data-theme="oscuro"]) .fortaleza[data-nivel="3"] { --nivel:#f5d56b; --nivel-barra:#e3b746; }
:global(:root[data-theme="oscuro"]) .fortaleza[data-nivel="4"] { --nivel:#90e2b5; --nivel-barra:#51bd89; }
:global(:root[data-theme="oscuro"]) .fortaleza[data-nivel="5"] { --nivel:#83e5b7; --nivel-barra:#35ae7d; }
.fortaleza-cabecera { display:flex; justify-content:space-between; align-items:center; gap:8px; flex-wrap:wrap; font-size:12px; color:var(--text-muted); }
.fortaleza-cabecera strong { color:var(--nivel); font-size:13px; font-weight:650; }
.fortaleza-barra { display:grid; grid-template-columns:repeat(5,minmax(0,1fr)); gap:5px; }
.fortaleza-barra span { height:5px; border-radius:8px; background:var(--border-strong); transition:background-color .2s ease; }
.fortaleza-barra span.lleno { background:var(--nivel-barra); }
.fortaleza-mensaje,.fortaleza-subtitulo { margin:0; color:var(--text-muted); font-size:12px; line-height:1.45; }
.fortaleza-requisito,.fortaleza-reglas li { display:flex; align-items:center; gap:7px; font-size:12px; font-weight:400; color:var(--text-muted); }
.fortaleza-requisito { border-top:1px solid var(--border); padding-top:9px; flex-wrap:wrap; }
.fortaleza-estado { margin-left:auto; font-size:11px; color:var(--text-muted); }
.fortaleza-marca { width:17px; height:17px; flex:0 0 17px; display:grid; place-items:center; }
.fortaleza-punto { width:9px; height:9px; border:1.5px solid var(--text-muted); border-radius:50%; }
.fortaleza-marca.cumple { color:var(--marca); }
.fortaleza-reglas { margin:0; padding:0; list-style:none; display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:6px 8px; }
.fortaleza-subtitulo { font-size:11px; }
.fortaleza-sr { position:absolute; width:1px; height:1px; padding:0; overflow:hidden; clip-path:inset(50%); white-space:nowrap; }
@media(max-width:350px) { .fortaleza-reglas { grid-template-columns:1fr; } }
@media(prefers-reduced-motion:reduce) { .fortaleza-barra span { transition:none; } }
</style>
