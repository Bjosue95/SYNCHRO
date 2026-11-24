<script setup>
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { ubicaciones, linkMaps } from "../../data/empresa.js";
import AppIcon from "../ui/AppIcon.vue";
import "leaflet/dist/leaflet.css";

const contenedorMapa = ref(null);
const filtro = ref("todas");
const seleccionado = ref("curc");
const estadoMapa = ref("cargando");
const zonas = [
  { id: "todas", etiqueta: "Todas" },
  { id: "centro", etiqueta: "Comayagua" },
  { id: "tegucigalpa", etiqueta: "Tegucigalpa" },
  { id: "sps", etiqueta: "San Pedro Sula" }
];
const visibles = computed(() => filtro.value === "todas"
  ? ubicaciones
  : ubicaciones.filter(x => x.zona === filtro.value)
);
let L = null;
let mapa = null;
let grupo = null;
let observador = null;
let desmontado = false;
let preparando = false;
const marcadores = new Map();

function popupSeguro(lugar) {
  const elemento = document.createElement("div");
  const titulo = document.createElement("strong");
  titulo.textContent = lugar.nombre;
  const detalle = document.createElement("p");
  detalle.textContent = lugar.ciudad + " · Lugar de referencia";
  detalle.style.cssText = "margin:7px 0 2px;line-height:1.45";
  elemento.append(titulo, detalle);
  return elemento;
}

async function iniciarMapa() {
  if (preparando || mapa || desmontado) return;
  preparando = true;
  try {
    const modulo = await import("leaflet");
    L = modulo.default || modulo;
    if (desmontado || !contenedorMapa.value) return;
    mapa = L.map(contenedorMapa.value, {
      zoomControl: true, scrollWheelZoom: false,
      keyboard: true, attributionControl: true
    });
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
      maxZoom: 19,
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(mapa);
    grupo = L.layerGroup().addTo(mapa);
    for (const lugar of ubicaciones.filter(x => x.coordenadas)) {
      const icono = L.divIcon({
        className: "syn-location-pin",
        html: '<span aria-hidden="true"></span>',
        iconSize: [22, 30],
        iconAnchor: [11, 28]
      });
      const marcador = L.marker(lugar.coordenadas, { icon: icono, title: lugar.nombre });
      marcador.bindPopup(popupSeguro(lugar), { maxWidth: 220 });
      marcador.on("click", () => { seleccionado.value = lugar.id; });
      marcadores.set(lugar.id, marcador);
    }
    actualizarMarcadores();
    estadoMapa.value = "listo";
  } catch {
    estadoMapa.value = "error";
  } finally {
    preparando = false;
  }
}

function actualizarMarcadores() {
  if (!mapa || !grupo || !L) return;
  grupo.clearLayers();
  const puntos = [];
  for (const lugar of visibles.value) {
    const marcador = marcadores.get(lugar.id);
    if (!marcador) continue;
    marcador.addTo(grupo);
    puntos.push(lugar.coordenadas);
  }
  if (puntos.length) {
    mapa.fitBounds(L.latLngBounds(puntos), {
      padding: [42, 42], maxZoom: 13, animate: false
    });
  }
}

function cambiarFiltro(zona) {
  filtro.value = zona;
  const primero = visibles.value.find(x => x.coordenadas) || visibles.value[0];
  if (primero) seleccionado.value = primero.id;
}
function elegirLugar(lugar) {
  seleccionado.value = lugar.id;
  if (!lugar.coordenadas || !mapa) return;
  mapa.flyTo(lugar.coordenadas, Math.max(mapa.getZoom(), 13), {
    animate: !window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches,
    duration: .7
  });
  marcadores.get(lugar.id)?.openPopup();
}
watch(filtro, actualizarMarcadores);

onMounted(() => {
  if (!("IntersectionObserver" in window)) { void iniciarMapa(); return; }
  observador = new IntersectionObserver(entradas => {
    if (!entradas.some(x => x.isIntersecting)) return;
    observador?.disconnect();
    void iniciarMapa();
  }, { rootMargin: "200px" });
  if (contenedorMapa.value) observador.observe(contenedorMapa.value);
});
onBeforeUnmount(() => {
  desmontado = true;
  observador?.disconnect();
  mapa?.remove();
  mapa = null;
});
</script>

<template>
  <section id="ubicaciones" class="syn-map-section">
    <div class="contenedor">
      <header class="syn-map-heading">
        <div>
          <span class="syn-map-kicker">ESPACIOS QUE NOS CONECTAN</span>
          <h2>SYNCHRO, más cerca de ti.</h2>
          <p>Explora universidades, aeropuertos y espacios públicos de nuestra red de interés.</p>
        </div>
        <span class="syn-map-note">Universidades, aeropuertos y espacios de ciudad</span>
      </header>

      <div class="syn-map-filters" role="group" aria-label="Filtrar por zona">
        <button v-for="zona in zonas" :key="zona.id" type="button"
          :class="{ activo: filtro === zona.id }"
          :aria-pressed="filtro === zona.id" @click="cambiarFiltro(zona.id)">
          {{ zona.etiqueta }}
        </button>
      </div>

      <div class="syn-map-grid">
        <div class="syn-map-surface">
          <div ref="contenedorMapa" class="syn-map-canvas" role="region"
            aria-label="Mapa interactivo de lugares de referencia"></div>
          <div v-if="estadoMapa === 'cargando'" class="syn-map-loading" aria-live="polite">
            Cargando mapa…
          </div>
          <div v-else-if="estadoMapa === 'error'" class="syn-map-loading" role="status">
            No se pudo cargar el mapa. Puedes consultar las ubicaciones en la lista.
          </div>
        </div>

        <div class="syn-map-list" aria-label="Lugares de referencia">
          <div class="syn-map-list-header">
            <strong>{{ visibles.length }} lugares</strong>
            <span>Selecciona uno para acercar el mapa</span>
          </div>
          <div v-for="lugar in visibles" :key="lugar.id" class="syn-map-location"
            :class="{ activo: seleccionado === lugar.id }">
            <button class="syn-map-select" type="button"
              :aria-pressed="seleccionado === lugar.id"
              @click="elegirLugar(lugar)">
              <AppIcon nombre="ubicacion" :tamano="20"/>
              <span class="syn-map-location-label">
                <strong>{{ lugar.nombre }}</strong>
                <small>{{ lugar.ciudad }}</small>
              </span>
            </button>
            <a :href="linkMaps(lugar)" target="_blank" rel="noopener noreferrer"
              class="syn-map-link" :aria-label="'Buscar ' + lugar.nombre + ' en Google Maps'">
              Ver ubicación <AppIcon nombre="enlace-externo" :tamano="14"/>
            </a>
            <p v-if="!lugar.coordenadas" class="syn-map-unpinned">
              Plaza Ciudad Nueva, Comayagua.
            </p>
          </div>
        </div>
      </div>
      <p class="syn-map-footnote">
        Los puntos indican lugares geográficos de referencia. © OpenStreetMap contributors.
      </p>
    </div>
  </section>
</template>

<style scoped>
.syn-map-section { padding:clamp(55px,6vw,90px) 0; background:var(--bg); }
.syn-map-heading { display:flex; justify-content:space-between; align-items:end; gap:28px; margin-bottom:25px; }
.syn-map-kicker { color:var(--accent-text); font-size:12px; font-weight:720; letter-spacing:.065em; }
.syn-map-heading h2 { color:var(--text-strong); font-size:clamp(31px,3.2vw,44px); font-weight:650; letter-spacing:-.04em; margin:10px 0; }
.syn-map-heading p { color:var(--text-muted); font-size:14px; line-height:1.7; margin:0; }
.syn-map-note { font-size:12px; line-height:1.5; max-width:200px; color:var(--text-muted); }
.syn-map-filters { display:flex; flex-wrap:wrap; gap:8px; margin-bottom:20px; }
.syn-map-filters button { border:1px solid var(--border-strong); border-radius:99px; background:var(--surface); color:var(--text-muted); padding:9px 15px; min-height:42px; font-size:12px; font-weight:720; }
.syn-map-filters button.activo { color:var(--on-action); background:var(--action-bg); border-color:var(--action-bg); }
.syn-map-grid { display:grid; grid-template-columns:minmax(0,1.45fr) minmax(275px,.75fr); border:1px solid var(--border); border-radius:18px; overflow:hidden; background:var(--surface); min-height:500px; }
.syn-map-surface { min-height:500px; position:relative; }
.syn-map-canvas { height:100%; width:100%; min-height:500px; background:#dfeaed; z-index:0; }
.syn-map-loading { position:absolute; inset:0; display:grid; place-items:center; background:var(--surface-soft); color:var(--text-muted); text-align:center; padding:22px; font-size:14px; }
.syn-map-list { max-height:500px; overflow-y:auto; border-left:1px solid var(--border); }
.syn-map-list-header { display:grid; gap:4px; padding:21px; border-bottom:1px solid var(--border); background:var(--surface-soft); }
.syn-map-list-header strong { font-size:16px; color:var(--text-strong); }
.syn-map-list-header span { color:var(--text-subtle); font-size:11px; }
.syn-map-location { padding:14px 18px; border-bottom:1px solid var(--border); }
.syn-map-location.activo { background:var(--surface-accent); }
.syn-map-select { display:flex; width:100%; align-items:flex-start; gap:10px; border:0; background:transparent; padding:0; text-align:left; color:var(--text); }
.syn-map-location-dot { display:block; flex:0 0 9px; height:9px; margin-top:6px; border:2px solid var(--accent-text); border-radius:50%; }
.syn-map-location.activo .syn-map-location-dot { background:var(--brand-accent); }
.syn-map-location-label { display:grid; gap:3px; }
.syn-map-location-label strong { color:var(--text-strong); font-size:12px; font-weight:720; line-height:1.45; }
.syn-map-location-label small { color:var(--text-muted); font-size:11px; }
.syn-map-link { display:inline-flex; align-items:center; gap:6px; margin:9px 0 0 19px; display:inline-block; font-size:11px; font-weight:700; color:var(--accent-text); text-decoration:none; }
.syn-map-link:hover { text-decoration:underline; }
.syn-map-unpinned { margin:7px 0 0 19px; line-height:1.5; font-size:11px; color:var(--text-muted); }
.syn-map-footnote { margin:12px 0 0; font-size:11px; line-height:1.55; color:var(--text-subtle); }
:global(.syn-location-pin) { background:transparent; border:0; }
:global(.syn-location-pin span) { width:19px; height:19px; display:block; border:3px solid #fff; border-radius:50% 50% 50% 0; background:#00a8b5; transform:rotate(-45deg); box-shadow:0 3px 10px rgba(1,48,92,.35); }
@media(max-width:830px) { .syn-map-grid { grid-template-columns:1fr; } .syn-map-surface,.syn-map-canvas { min-height:380px; height:380px; } .syn-map-list { border-left:0; border-top:1px solid var(--border); max-height:350px; } .syn-map-heading { display:block; } .syn-map-note { display:block; margin-top:13px; max-width:unset; } }
@media(max-width:540px) { .syn-map-surface,.syn-map-canvas { min-height:325px; height:325px; } .syn-map-grid { min-height:0; } }
</style>
