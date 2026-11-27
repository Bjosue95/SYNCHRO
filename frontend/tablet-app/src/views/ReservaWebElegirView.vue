<script setup>
import { computed,ref,onMounted,watch } from "vue";
import { RouterLink,useRoute,useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { elegirServicioReservaWeb,obtenerEleccionReservaWeb } from "../services/reservaWebSesion.js";
import { comprobarSesionTerminalReservaWeb } from "../services/reservasWebTerminalApi.js";

const route=useRoute();
const router=useRouter();
const {oscuro}=useTema();
const elegirTipo=computed(()=>route.path==="/guardar/reserva/tipo");
const seleccionado=ref("");
const consultandoTerminal=ref(true);
const errorTerminal=ref("");
async function comprobarTerminal(){
  consultandoTerminal.value=true;
  errorTerminal.value="";
  try{
    await comprobarSesionTerminalReservaWeb();
    return true;
  }catch(e){
    errorTerminal.value=e instanceof Error?e.message:"No fue posible comprobar la autorización de la terminal.";
    return false;
  }finally{consultandoTerminal.value=false;}
}
watch(elegirTipo,()=>{seleccionado.value="";});
const opciones=computed(()=>elegirTipo.value?[
  {id:"Normal",nombre:"Normal",icono:"/iconos/01_guardar_depositar"},
  {id:"Refrigerado",nombre:"Frío",icono:"/iconos/07_frio"},
  {id:"Caliente",nombre:"Calor",icono:"/iconos/08_caliente"}
]:[
  {id:"Personal",nombre:"Pertenencias personales",icono:"/iconos/32_guardar_pertenencias_personales"},
  {id:"Encomienda",nombre:"Encomienda",icono:"/iconos/09_encomienda_paquete"}
]);
onMounted(()=>{
  void comprobarTerminal();
  if(elegirTipo.value&&obtenerEleccionReservaWeb()?.servicio!=="Encomienda"){
    router.replace("/guardar/reserva");
  }
});
async function continuar(){
  if(consultandoTerminal.value||errorTerminal.value||!opciones.value.some(x=>x.id===seleccionado.value))return;
  if(!await comprobarTerminal())return;
  if(!elegirTipo.value&&seleccionado.value==="Encomienda"){
    elegirServicioReservaWeb("Encomienda");
    router.push("/guardar/reserva/tipo");
    return;
  }
  if(elegirTipo.value)elegirServicioReservaWeb("Encomienda",seleccionado.value);
  else elegirServicioReservaWeb("Personal");
  router.push("/guardar/reserva/captura");
}
</script>

<template>
  <main class="pantalla-guardar" :class="{ 'tema-oscuro': oscuro }">
    <RouterLink :to="elegirTipo ? '/guardar/reserva' : '/menu'" class="regresar">
      <span class="icono-regresar" aria-hidden="true">
        <img src="/iconos/complementarios/28_regresar.svg" class="regresar-claro" alt="" />
        <img src="/iconos/complementarios/28_regresar-oscuro.svg" class="regresar-oscuro" alt="" />
      </span>
      <span>{{ elegirTipo ? 'Volver' : 'Volver al menú' }}</span>
    </RouterLink>

    <div class="contenido-guardar" :class="{ 'sin-terminal': !!errorTerminal }">
      <h1 class="titulo">{{ elegirTipo ? 'Selecciona el tipo de casillero' : '¿Qué deseas depositar?' }}</h1>
      <div v-if="errorTerminal" class="aviso-terminal" role="alert">
        <p>{{ errorTerminal }}</p>
        <button type="button" class="revisar-terminal" @click="comprobarTerminal">Comprobar nuevamente</button>
      </div>
      <section v-else class="cuadricula" :class="{ 'dos-opciones': !elegirTipo }"
        :aria-label="elegirTipo ? 'Tipos de casillero para encomiendas' : 'Tipo de reserva'">
        <button v-for="item in opciones" :key="item.id"
          type="button" class="tarjeta" :class="{ seleccionada: seleccionado===item.id }"
          :aria-pressed="seleccionado===item.id" @click="seleccionado=item.id">
          <span class="contenedor-icono" aria-hidden="true">
            <img :src="`${item.icono}.svg`" class="imagen-icono icono-claro" alt="" />
            <img :src="`${item.icono}-oscuro.svg`" class="imagen-icono icono-oscuro" alt="" />
          </span>
          <span class="nombre-tarjeta">{{ item.nombre }}</span>
        </button>
        <div v-if="elegirTipo" class="espacio-vacio" aria-hidden="true"></div>
      </section>

      <div class="accion-guardar">
        <button type="button" class="boton-continuar" :disabled="!seleccionado || consultandoTerminal || !!errorTerminal" @click="continuar">
          <span>Continuar</span>
          <span class="icono-continuar" aria-hidden="true">
            <img src="/iconos/complementarios/29_continuar.svg" class="continuar-claro" alt="" />
            <img src="/iconos/complementarios/29_continuar-oscuro.svg" class="continuar-oscuro" alt="" />
          </span>
        </button>
      </div>
    </div>
    <span class="solo-lectura" aria-live="polite">
      {{ seleccionado ? 'Seleccionaste: '+opciones.find(x=>x.id===seleccionado)?.nombre : '' }}
    </span>
  </main>
</template>

<style scoped>
/* Patrón de GuardarView: regreso, título, cuadrícula y continuar, sin desplazamiento. */
.aviso-terminal{position:relative;z-index:1;display:flex;align-items:center;justify-content:center;flex-wrap:wrap;gap:10px;border:1px solid #d8a58a;border-radius:12px;padding:10px 14px;background:#fff0e9;color:#762d21;font-size:13px;line-height:1.4;}
.aviso-terminal p{margin:0;flex:1 1 210px;}
.revisar-terminal{border:1px solid #01305c;border-radius:10px;background:#01305c;color:#fff;padding:9px 12px;cursor:pointer;font:inherit;font-weight:750;}
.tema-oscuro .aviso-terminal{background:#543333;color:#fff0df;border-color:#ae8177;}

.pantalla-guardar {
  display: grid;
  grid-template-columns: minmax(0,1fr);
  grid-template-rows: auto minmax(0,1fr);
  gap: clamp(8px,1.4vh,16px);
  width:100%; height:100%; min-width:0; min-height:0;
  padding:clamp(10px,1.7vh,20px) clamp(12px,2.4vw,28px);
  background:#f2f7fa; color:#01305c;
  box-sizing:border-box;
  transition:background-color 150ms ease,color 150ms ease;
}
.pantalla-guardar * { box-sizing:border-box; }
.regresar {
  display:inline-flex; align-items:center; justify-content:center;
  gap:10px; justify-self:start; width:max-content; max-width:100%; min-height:52px;
  padding:8px 18px; border:1.5px solid #c7e2e9; border-radius:13px;
  background:#e4f2f6; color:inherit; font-size:16px; font-weight:700;
  text-decoration:none; transition:background-color 150ms ease,transform 150ms ease;
}
.regresar:active { transform:scale(.985); }
.icono-regresar { position:relative; display:block; width:26px; height:26px; flex:0 0 26px; }
.icono-regresar img { position:absolute; inset:0; width:100%; height:100%; object-fit:contain; transition:opacity 150ms ease; }
.regresar-claro,.icono-claro,.continuar-claro { opacity:1; }
.regresar-oscuro,.icono-oscuro,.continuar-oscuro { opacity:0; }
.contenido-guardar {
  display:grid; grid-template-columns:minmax(0,1fr);
  grid-template-rows:auto minmax(0,1fr) auto; gap:clamp(9px,1.4vh,16px);
  width:min(100%,760px); height:min(100%,630px); min-width:0; min-height:0; place-self:center;
}
.contenido-guardar.sin-terminal { grid-template-rows:auto minmax(0,1fr) auto; }
.aviso-terminal { grid-row:2; align-self:center; }
.titulo { margin:0; font-size:clamp(21px,2.8vw,32px); font-weight:700; line-height:1.18; text-align:center; }
.cuadricula {
  display:grid; grid-template-columns:repeat(2,minmax(0,1fr));
  grid-template-rows:repeat(2,minmax(0,1fr)); gap:clamp(9px,1.5vh,18px);
  width:100%; height:min(100%,510px); min-height:0; min-width:0; place-self:center; margin:0 auto;
}
.cuadricula.dos-opciones { grid-template-rows:minmax(0,1fr); height:min(100%,270px); }
.tarjeta {
  position:relative; display:flex; flex-direction:column; align-items:center; justify-content:center;
  gap:clamp(6px,1.2vh,13px); width:100%; height:100%; min-width:0; min-height:0;
  padding:clamp(8px,1.8vh,18px); border:2px solid #dfeaf0; border-radius:18px;
  background:#fff; color:#01305c; box-shadow:0 3px 12px rgba(1,48,92,.05);
  text-align:center; font:inherit; cursor:pointer;
  transition:background-color 150ms ease,border-color 150ms ease,color 150ms ease,box-shadow 150ms ease,transform 150ms ease;
}
.tarjeta:active { transform:scale(.985); }
.tarjeta.seleccionada {
  border-color:#00b9c7; background:#e8f9fa;
  box-shadow:0 0 0 2px rgba(0,185,199,.12);
}
.tarjeta.seleccionada::after {
  content:"\2713"; position:absolute; top:10px; right:10px;
  display:grid; place-items:center; width:clamp(26px,3vw,38px);
  aspect-ratio:1; border-radius:50%; background:#00b9c7; color:#01305c;
  font-size:clamp(17px,1.8vw,23px); font-weight:800;
  box-shadow:0 2px 8px rgba(1,48,92,.12); pointer-events:none;
}
.contenedor-icono { position:relative; display:block; width:clamp(42px,8vh,80px); aspect-ratio:1; flex:0 1 auto; }
.imagen-icono { position:absolute; inset:0; width:100%; height:100%; object-fit:contain; transition:opacity 150ms ease; }
.nombre-tarjeta { font-size:clamp(14px,1.7vw,20px); font-weight:600; line-height:1.2; overflow-wrap:anywhere; }
.espacio-vacio { visibility:hidden; pointer-events:none; }
.accion-guardar {
  display:flex; align-items:center; justify-content:flex-end;
  justify-self:center; width:100%; min-height:0; margin:0;
}
.boton-continuar {
  display:inline-flex; align-items:center; justify-content:center; gap:12px;
  min-width:194px; min-height:54px; padding:9px 24px;
  border:2px solid #00a8b8; border-radius:14px; background:#00b9c7;
  color:#01305c; box-shadow:0 4px 12px rgba(0,185,199,.18);
  font:inherit; font-size:17px; font-weight:800; cursor:pointer;
  touch-action:manipulation;
  transition:transform 150ms ease,opacity 150ms ease,box-shadow 150ms ease,background-color 150ms ease;
}
.boton-continuar:active:not(:disabled) { transform:scale(.985); }
.boton-continuar:disabled { opacity:1; background:#d7e7ed; border-color:#bdcfd6; color:#526e7b; box-shadow:none; cursor:not-allowed; }
.icono-continuar { position:relative; display:block; width:40px; height:40px; flex:0 0 40px; }
.icono-continuar img { position:absolute; inset:0; width:100%; height:100%; object-fit:contain; transition:opacity 150ms ease; }
.solo-lectura { position:absolute; width:1px; height:1px; overflow:hidden; padding:0; margin:-1px; clip:rect(0,0,0,0); white-space:nowrap; border:0; }
.pantalla-guardar.tema-oscuro { background:#061f36; color:#fff; }
.tema-oscuro .regresar { background:#204766; border-color:#42647e; }
.tema-oscuro .tarjeta { background:#103956; border-color:#315573; color:#fff; }
.tema-oscuro .tarjeta.seleccionada { background:#174b65; border-color:#00b9c7; }
.tema-oscuro .boton-continuar:disabled { background:#294961; border-color:#42647e; color:#d5e6ef; }
.tema-oscuro .regresar-claro,.tema-oscuro .icono-claro,.tema-oscuro .continuar-claro { opacity:0; }
.tema-oscuro .regresar-oscuro,.tema-oscuro .icono-oscuro,.tema-oscuro .continuar-oscuro { opacity:1; }
.tarjeta:focus-visible,.regresar:focus-visible,.boton-continuar:focus-visible { outline:3px solid #00b9c7; outline-offset:3px; }
@media (orientation:portrait) {
  .contenido-guardar { width:min(100%,620px); height:min(100%,560px); }
  .cuadricula { height:min(100%,470px); }
  .cuadricula.dos-opciones { height:min(100%,260px); }
  .accion-guardar { justify-content:center; }
}
@media (max-height:620px) {
  .pantalla-guardar { gap:6px; padding:7px 12px; }
  .contenido-guardar { gap:6px; }
  .regresar { min-height:42px; padding:5px 12px; }
  .titulo { font-size:21px; }
  .cuadricula { gap:8px; }
  .tarjeta { padding:6px; gap:4px; }
  .contenedor-icono { width:clamp(32px,8vh,52px); }
  .nombre-tarjeta { font-size:14px; }
  .boton-continuar { min-height:42px; }
}
@media (max-height:450px) {
  .pantalla-guardar { min-height:350px; }
  .contenido-guardar { min-height:255px; }
  .cuadricula { min-height:200px; }
}
@media (min-width:600px) and (min-height:700px),
       (min-width:768px) and (min-height:550px) {
  .contenido-guardar { width:min(100%,1060px); height:min(100%,760px); gap:clamp(12px,1.8vh,22px); }
  .titulo { font-size:clamp(30px,3vw,40px); }
  .cuadricula { height:100%; gap:clamp(14px,2vh,24px); }
  .cuadricula.dos-opciones { height:min(100%,360px); }
  .tarjeta { padding:clamp(12px,2.4vh,28px); gap:clamp(9px,1.6vh,17px); border-radius:24px; }
  .contenedor-icono { width:clamp(78px,12vh,124px); }
  .nombre-tarjeta { font-size:clamp(21px,2.3vw,27px); }
  .regresar { min-height:56px; padding:9px 20px; font-size:clamp(17px,1.5vw,19px); }
  .boton-continuar { min-width:240px; min-height:62px; font-size:21px; }
}
@media (min-width:600px) and (min-height:700px) and (orientation:portrait) {
  .contenido-guardar { width:min(100%,820px); height:min(100%,1040px); }
}
@media (min-width:768px) and (min-height:550px) and (max-height:650px) {
  .contenido-guardar { gap:8px; }
  .titulo { font-size:27px; }
  .cuadricula { gap:10px; }
  .tarjeta { padding:9px; gap:5px; }
  .contenedor-icono { width:clamp(58px,10vh,77px); }
  .nombre-tarjeta { font-size:19px; }
  .regresar { min-height:48px; font-size:16px; padding:6px 16px; }
  .boton-continuar { min-height:52px; font-size:18px; }
}
@media (prefers-reduced-motion:reduce) {
  .pantalla-guardar,.regresar,.tarjeta,.imagen-icono,.boton-continuar { transition:none; }
  .regresar:active,.tarjeta:active { transform:none; }
}
</style>
