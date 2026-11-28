<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { espaciosPara, consultarEspaciosCarga } from "../services/catalogoEspaciosCarga.js";
const { oscuro } = useTema();
const propsVistaPrevia = defineProps({
  modoVistaPrevia: { type: Boolean, default: false },
  espaciosEjemplo: { type: Array, default: () => [] }
});
const esVistaPrevia = import.meta.env.DEV && propsVistaPrevia.modoVistaPrevia;
const route = useRoute();
const router = useRouter();
const dispositivo = computed(() => ["Celular", "Laptop"].includes(route.query.dispositivo) ? route.query.dispositivo : "");
const modalidad = computed(() => dispositivo.value === "Laptop" ? "Laptop" :
  ["Inalambrica", "Cable"].includes(route.query.modalidad) ? route.query.modalidad : "");
const regreso = computed(() => esVistaPrevia
  ? (dispositivo.value === "Laptop"
    ? "/vista-previa/carga-dispositivo"
    : "/vista-previa/carga-modalidad")
  : (dispositivo.value === "Laptop" ? "/cargar" : "/cargar/modalidad"));
const espacios = ref([]);
const seleccionado = ref("");
const cargando = ref(true);
const error = ref("");
const pagina = ref(0);
const porPagina = 4;
const disponibles = computed(() => espaciosPara(espacios.value, dispositivo.value, modalidad.value));
const totalPaginas = computed(() => Math.max(1, Math.ceil(disponibles.value.length / porPagina)));
const visibles = computed(() => disponibles.value.slice(pagina.value * porPagina, (pagina.value + 1) * porPagina));
const puedeContinuar = computed(() => !cargando.value && !error.value && disponibles.value.some(x => x.codigo === seleccionado.value));
async function consultar() {
  if (esVistaPrevia) return;
  cargando.value = true;
  error.value = "";
  try {
    espacios.value = await consultarEspaciosCarga();
    if (!disponibles.value.some(x => x.codigo === seleccionado.value)) seleccionado.value = "";
    if (pagina.value >= totalPaginas.value) pagina.value = 0;
  } catch(e) {
    espacios.value = [];
    seleccionado.value = "";
    error.value = e?.message || "No se pudo consultar los espacios.";
  } finally { cargando.value = false; }
}
function cambiarPagina(p) {
  if (p < 0 || p >= totalPaginas.value) return;
  pagina.value = p;
  seleccionado.value = "";
}
function continuar() {
  if (!dispositivo.value || !modalidad.value || !puedeContinuar.value) return;
  if (esVistaPrevia) {
    router.push({
      path: "/vista-previa/carga-rostro",
      query: {
        dispositivo: dispositivo.value,
        modalidad: modalidad.value,
        espacio: seleccionado.value
      }
    });
    return;
  }
  router.push({ path: "/cargar/rostro", query: {
    dispositivo: dispositivo.value, modalidad: modalidad.value, espacio: seleccionado.value,
  }});
}
onMounted(() => {
  if (esVistaPrevia) {
    espacios.value = propsVistaPrevia.espaciosEjemplo.map(item => ({
      codigo: String(item.codigo),
      estado: String(item.estado)
    }));
    cargando.value = false;
    return;
  }
  if (!dispositivo.value) { router.replace("/cargar"); return; }
  if (dispositivo.value === "Celular" && !modalidad.value) { router.replace("/cargar/modalidad"); return; }
  void consultar();
});
</script>

<template>
  <main class="pantalla-guardar" :class="{ 'tema-oscuro': oscuro }">
    <RouterLink :to="regreso" class="regresar">
      <span class="icono-regresar" aria-hidden="true">
        <img src="/iconos/complementarios/28_regresar.svg" alt="" class="regresar-claro" />
        <img src="/iconos/complementarios/28_regresar-oscuro.svg" alt="" class="regresar-oscuro" />
      </span>
      <span>Volver a dispositivos</span>
    </RouterLink>

    <div class="contenido-guardar">
      <h1 class="titulo">Elige un espacio de carga</h1>

      <section class="cuadricula" aria-label="Espacios de carga disponibles">
        <div v-if="cargando" class="mensaje-carga" role="status">Consultando espacios disponibles...</div>
        <div v-else-if="error" class="mensaje-carga" role="alert">
          <p>{{ error }}</p>
          <button type="button" class="boton-continuar" @click="consultar">Reintentar</button>
        </div>
        <div v-else-if="!disponibles.length" class="mensaje-carga" role="status">
          <p>No hay espacios de carga disponibles.</p>
          <button type="button" class="boton-continuar" @click="consultar">Actualizar</button>
        </div>
        <button
          v-for="item in (!cargando && !error ? visibles : [])"
          :key="item.codigo"
          type="button"
          class="tarjeta"
          :class="{ seleccionada: seleccionado === item.codigo }"
          :aria-pressed="seleccionado === item.codigo"
          data-preview-permitido
          @click="seleccionado = item.codigo"
        >
          <span class="contenedor-icono" aria-hidden="true">
            <img src="/iconos/complementarios/15_ubicacion_casillero.svg" alt="" class="imagen-icono icono-claro" />
            <img src="/iconos/complementarios/15_ubicacion_casillero-oscuro.svg" alt="" class="imagen-icono icono-oscuro" />
          </span>
          <span class="nombre-tarjeta">{{ item.codigo }}</span>
          <span v-if="modalidad === 'Cable'" class="tipo-cable">
  {{ item.codigo === 'C1' || item.codigo === 'C3' ? 'USB-C' : 'Lightning' }}
</span>
          <span class="estado-carga">{{ seleccionado === item.codigo ? "Seleccionado" : "Disponible" }}</span>
        </button>
      </section>

      <div class="accion-guardar" :class="{ 'con-paginacion': totalPaginas > 1 }">
        <div v-if="totalPaginas > 1" class="paginacion">
          <button type="button" class="pagina-boton" :disabled="pagina === 0" data-preview-permitido @click="cambiarPagina(pagina - 1)" aria-label="Página anterior">‹</button>
          <span>{{ pagina + 1 }} / {{ totalPaginas }}</span>
          <button type="button" class="pagina-boton" :disabled="pagina === totalPaginas - 1" data-preview-permitido @click="cambiarPagina(pagina + 1)" aria-label="Página siguiente">›</button>
        </div>
        <button type="button" class="boton-continuar" :disabled="!puedeContinuar" data-preview-permitido @click="continuar">
          <span class="texto-continuar">Continuar</span>
          <span class="icono-continuar" aria-hidden="true">
            <img src="/iconos/complementarios/29_continuar.svg" class="continuar-claro" alt="" />
            <img src="/iconos/complementarios/29_continuar-oscuro.svg" class="continuar-oscuro" alt="" />
          </span>
        </button>
      </div>
    </div>
  </main>
</template>

<style scoped>
/* SYNCHRO | GUARDAR. Cuatro filas reales: volver, título, tarjetas, continuar. */
.pantalla-guardar {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr);
  gap: clamp(8px, 1.4vh, 16px);
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(10px, 1.7vh, 20px) clamp(12px, 2.4vw, 28px);
  background: #f2f7fa;
  color: #01305c;
  transition: background-color 150ms ease, color 150ms ease;
}
.regresar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  justify-self: start;
  width: max-content;
  max-width: 100%;
  min-height: 52px;
  padding: 8px 18px;
  border: 1.5px solid #c7e2e9;
  border-radius: 13px;
  background: #e4f2f6;
  color: inherit;
  font-size: 16px;
  font-weight: 700;
  text-decoration: none;
  transition: background-color 150ms ease, transform 150ms ease;
}
.regresar:active { transform: scale(.985); }
.icono-regresar { position: relative; display: block; width: 26px; height: 26px; flex: 0 0 26px; }
.icono-regresar img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; transition: opacity 150ms ease; }
.regresar-claro, .icono-claro { opacity: 1; }
.regresar-oscuro, .icono-oscuro { opacity: 0; }
.contenido-guardar {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(0, 1fr) auto;
  gap: clamp(9px, 1.4vh, 16px);
  width: min(100%, 760px);
  height: min(100%, 630px);
  min-width: 0;
  min-height: 0;
  place-self: center;
}
.titulo {
  margin: 0;
  font-size: clamp(21px, 2.8vw, 32px);
  line-height: 1.18;
  text-align: center;
}
.cuadricula {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  grid-template-rows: repeat(2, minmax(0, 1fr));
  gap: clamp(9px, 1.5vh, 18px);
  width: 100%;
  height: min(100%, 510px);
  min-height: 0;
  min-width: 0;
  place-self: center;
  margin: 0 auto;
}
.tarjeta {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: clamp(6px, 1.2vh, 13px);
  width: 100%;
  height: 100%;
  min-width: 0;
  min-height: 0;
  padding: clamp(8px, 1.8vh, 18px);
  border: 2px solid #dfeaf0;
  border-radius: 18px;
  background: #ffffff;
  color: #01305c;
  box-shadow: 0 3px 12px rgba(1,48,92,.05);
  text-align: center;
  cursor: pointer;
  transition: background-color 150ms ease, border-color 150ms ease, color 150ms ease, box-shadow 150ms ease, transform 150ms ease;
}
.tarjeta:active { transform: scale(.985); }
.tarjeta.seleccionada {
  border-color: #00b9c7;
  background: #e8f9fa;
  box-shadow: 0 0 0 2px rgba(0,185,199,.12);
}
/* La marca no afecta a las dimensiones de la tarjeta. */
.tarjeta.seleccionada::after {
  content: "\2713";
  position: absolute;
  top: 10px;
  right: 10px;
  display: grid;
  place-items: center;
  width: clamp(26px, 3vw, 38px);
  aspect-ratio: 1;
  border-radius: 50%;
  background: #00b9c7;
  color: #01305c;
  font-size: clamp(17px, 1.8vw, 23px);
  font-weight: 800;
  box-shadow: 0 2px 8px rgba(1, 48, 92, .12);
  pointer-events: none;
}
.contenedor-icono {
  position: relative;
  display: block;
  width: clamp(42px, 8vh, 80px);
  aspect-ratio: 1;
  flex: 0 1 auto;
}
.imagen-icono { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: contain; transition: opacity 150ms ease; }
.nombre-tarjeta { font-size: clamp(14px, 1.7vw, 20px); font-weight: 600; line-height: 1.2; overflow-wrap: anywhere; }
.accion-guardar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  justify-self: center;
  width: 100%;
  min-height: 0;
  margin: 0;
}
.boton-continuar {
  min-width: 194px;
  min-height: 54px;
  padding: 9px 24px;
  border: 2px solid #00a8b8;
  border-radius: 14px;
  background: #00b9c7;
  color: #01305c;
  box-shadow: 0 4px 12px rgba(0, 185, 199, .18);
  font: inherit;
  font-size: 17px;
  font-weight: 800;
  cursor: pointer;
  touch-action: manipulation;
  transition: transform 150ms ease, opacity 150ms ease,
              box-shadow 150ms ease, background-color 150ms ease;
}
.boton-continuar:active:not(:disabled) { transform: scale(.985); }
.boton-continuar:disabled {
  opacity: 1;
  background: #d7e7ed;
  border-color: #bdcfd6;
  color: #526e7b;
  box-shadow: none;
  cursor: not-allowed;
}
.solo-lectura {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  padding: 0;
  margin: -1px;
  clip: rect(0,0,0,0);
  white-space: nowrap;
  border: 0;
}
.pantalla-guardar.tema-oscuro { background: #061f36; color: #ffffff; }
.tema-oscuro .regresar { background: #204766; border-color: #42647e; }
.tema-oscuro .regresar-claro, .tema-oscuro .icono-claro { opacity: 0; }
.tema-oscuro .regresar-oscuro, .tema-oscuro .icono-oscuro { opacity: 1; }
.tema-oscuro .tarjeta { background: #103956; border-color: #315573; color: #ffffff; }
.tema-oscuro .tarjeta.seleccionada { background: #174b65; border-color: #00b9c7; }
.tema-oscuro .boton-continuar:disabled { background: #294961; border-color: #42647e; color: #d5e6ef; }
.tarjeta:focus-visible, .regresar:focus-visible, .boton-continuar:focus-visible { outline: 3px solid #00b9c7; outline-offset: 3px; }
@media (orientation: portrait) {
  .contenido-guardar { width: min(100%, 620px); height: min(100%, 560px); }
  .cuadricula { height: min(100%, 470px); }
  .accion-guardar { justify-content: center; }
}
@media (max-height: 620px) {
  .pantalla-guardar { gap: 6px; padding: 7px 12px; }
  .contenido-guardar { gap: 6px; }
  .regresar { min-height: 42px; padding: 5px 12px; }
  .titulo { font-size: 21px; }
  .cuadricula { gap: 8px; }
  .tarjeta { padding: 6px; gap: 4px; }
  .contenedor-icono { width: clamp(32px, 8vh, 52px); }
  .nombre-tarjeta { font-size: 14px; }
  .boton-continuar { min-height: 42px; }
}
@media (max-height: 450px) {
  .pantalla-guardar { min-height: 350px; }
  .contenido-guardar { min-height: 255px; }
  .cuadricula { min-height: 200px; }
}
@media (prefers-reduced-motion: reduce) {
  .pantalla-guardar, .regresar, .tarjeta, .imagen-icono, .boton-continuar { transition: none; }
  .regresar:active, .tarjeta:active { transform: none; }
}

/* Tablet: cuadrícula grande y proporcionada; teléfono intacto */
@media (min-width: 600px) and (min-height: 700px),
       (min-width: 768px) and (min-height: 550px) {
  .contenido-guardar {
    width: min(100%, 1060px);
    height: min(100%, 760px);
    gap: clamp(12px, 1.8vh, 22px);
  }
  .titulo { font-size: clamp(30px, 3vw, 40px); }
  .cuadricula { height: 100%; gap: clamp(14px, 2vh, 24px); }
  .tarjeta {
    padding: clamp(12px, 2.4vh, 28px);
    gap: clamp(9px, 1.6vh, 17px);
    border-radius: 24px;
  }
  .contenedor-icono { width: clamp(78px, 12vh, 124px); }
  .nombre-tarjeta { font-size: clamp(21px, 2.3vw, 27px); }
  .regresar {
    min-height: 56px;
    padding: 9px 20px;
    font-size: clamp(17px, 1.5vw, 19px);
  }
  .boton-continuar {
    min-width: 240px;
    min-height: 62px;
    font-size: 21px;
  }
}
@media (min-width: 600px) and (min-height: 700px) and (orientation: portrait) {
  .contenido-guardar {
    width: min(100%, 820px);
    height: min(100%, 1040px);
  }
}
@media (min-width: 768px) and (min-height: 550px) and (max-height: 650px) {
  .contenido-guardar { gap: 8px; }
  .titulo { font-size: 27px; }
  .cuadricula { gap: 10px; }
  .tarjeta { padding: 9px; gap: 5px; }
  .contenedor-icono { width: clamp(58px, 10vh, 77px); }
  .nombre-tarjeta { font-size: 19px; }
  .regresar { min-height: 48px; font-size: 16px; padding: 6px 16px; }
  .boton-continuar { min-height: 52px; font-size: 18px; }
}


/* Ajustes exclusivos de Carga, sin alterar Guardar */

.mensaje-carga { grid-column: 1 / -1; grid-row: 1 / -1; display:flex; flex-direction:column; align-items:center; justify-content:center; gap:12px; text-align:center; }
.mensaje-carga p { margin:0; font-size:clamp(15px,1.7vw,20px); }
.estado-carga { color:#5b6770; font-size:clamp(12px,1.4vw,17px); }
.tema-oscuro .estado-carga { color:#c6dfea; }
.accion-guardar.con-paginacion { justify-content:space-between; gap:12px; }
.paginacion { display:flex; align-items:center; justify-content:center; gap:10px; font-weight:700; }
.pagina-boton { min-width:44px; min-height:44px; border:1.5px solid #c7e2e9; border-radius:13px; background:#e4f2f6; color:#01305c; cursor:pointer; font:inherit; font-size:28px; }
.pagina-boton:disabled { opacity:.45; cursor:not-allowed; }
.tema-oscuro .pagina-boton { background:#204766; border-color:#42647e; color:#fff; }
.pagina-boton:focus-visible { outline:3px solid #00b9c7; outline-offset:3px; }
@media (orientation:portrait) { .accion-guardar.con-paginacion { justify-content:space-between; } }
@media (max-width:480px) { .accion-guardar.con-paginacion { flex-wrap:wrap; justify-content:center; gap:6px; } .accion-guardar.con-paginacion .boton-continuar { min-width:194px; } }


/* SYNCHRO_TIPOCABLE_V7 */

/* Descripcion del conector debajo del codigo. */
.tipo-cable {
  display: block;
  color: #01305c;
  font-size: clamp(14px, 1.6vw, 18px);
  font-weight: 700;
  line-height: 1.25;
}

.tema-oscuro .tipo-cable {
  color: #5ec6e8;
}

/* Separar Regresar del titulo sin modificar las tarjetas. */
.pantalla-guardar {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: clamp(10px, 1.6vh, 18px);
}

.pantalla-guardar > .regresar {
  flex: 0 0 auto;
  align-self: flex-start;
  position: relative;
  z-index: 1;
}

.pantalla-guardar > .contenido-guardar {
  flex: 1 1 0;
  align-self: center;
  height: auto;
  max-height: none;
  min-height: 0;
  grid-template-rows: auto minmax(0, 1fr) auto;
}

.pantalla-guardar .cuadricula {
  height: 100%;
  min-height: 0;
}

</style>