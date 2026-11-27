<script setup>
import { computed,ref,onMounted } from "vue";
import { RouterLink,useRoute,useRouter } from "vue-router";
import { useTema } from "../composables/useTema";
import { elegirServicioReservaWeb,obtenerEleccionReservaWeb } from "../services/reservaWebSesion.js";

const route=useRoute();
const router=useRouter();
const {oscuro}=useTema();
const elegirTipo=computed(()=>route.path==="/guardar/reserva/tipo");
const seleccionado=ref("");
const opciones=computed(()=>elegirTipo.value?[
  {id:"Normal",nombre:"Normal",icono:"/iconos/complementarios/15_ubicacion_casillero"},
  {id:"Refrigerado",nombre:"Frío",icono:"/iconos/07_frio"},
  {id:"Caliente",nombre:"Calor",icono:"/iconos/08_caliente"}
]:[
  {id:"Personal",nombre:"Pertenencias personales",icono:"/iconos/32_guardar_pertenencias_personales"},
  {id:"Encomienda",nombre:"Encomienda",icono:"/iconos/09_encomienda_paquete"}
]);
onMounted(()=>{
  if(elegirTipo.value&&obtenerEleccionReservaWeb()?.servicio!=="Encomienda"){
    router.replace("/guardar/reserva");
  }
});
function continuar(){
  if(!opciones.value.some(x=>x.id===seleccionado.value))return;
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
  <main class="reserva-eleccion" :class="{'tema-oscuro':oscuro}">
    <RouterLink :to="elegirTipo?'/guardar/reserva':'/menu'" class="reserva-volver">Regresar</RouterLink>
    <div class="reserva-contenido">
      <h1>{{ elegirTipo?'Elige el tipo de casillero':'¿Qué deseas depositar?' }}</h1>
      <div class="reserva-opciones" :class="{'tres':elegirTipo}" role="group" :aria-label="elegirTipo?'Tipo de casillero':'Tipo de reserva'">
        <button v-for="item in opciones" :key="item.id" type="button" class="reserva-opcion"
          :class="{seleccionada:seleccionado===item.id}"
          :aria-pressed="seleccionado===item.id" @click="seleccionado=item.id">
          <span class="reserva-marca" v-if="seleccionado===item.id" aria-hidden="true">✓</span>
          <span class="reserva-imagen" aria-hidden="true">
            <img :src="item.icono+'.svg'" class="icono-claro" alt="" />
            <img :src="item.icono+'-oscuro.svg'" class="icono-oscuro" alt="" />
          </span>
          <strong>{{ item.nombre }}</strong>
        </button>
      </div>
      <div class="reserva-acciones">
        <button type="button" class="reserva-continuar" :disabled="!seleccionado" @click="continuar">Continuar
          <img src="/iconos/complementarios/29_continuar.svg" alt="" />
        </button>
      </div>
    </div>
  </main>
</template>

<style scoped>
.reserva-eleccion,.reserva-eleccion *{box-sizing:border-box}
.reserva-eleccion{display:grid;grid-template-rows:auto minmax(0,1fr);gap:clamp(8px,1.5vh,18px);width:100%;height:100%;min-height:0;padding:clamp(10px,1.7vh,20px) clamp(12px,2.4vw,28px);background:#f2f7fa;color:#01305c;}
.reserva-volver{justify-self:start;align-self:start;padding:12px 19px;border:1.5px solid #c7e2e9;background:#e4f2f6;border-radius:14px;text-decoration:none;color:inherit;font-weight:800;}
.reserva-contenido{display:grid;grid-template-rows:auto minmax(0,1fr) auto;gap:clamp(12px,2vh,21px);width:min(100%,980px);height:min(100%,660px);min-height:0;place-self:center;}
.reserva-contenido h1{font-size:clamp(23px,3vw,36px);text-align:center;line-height:1.2;margin:0;}
.reserva-opciones{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));grid-template-rows:minmax(0,1fr);gap:clamp(10px,1.6vw,20px);width:min(100%,760px);height:min(100%,360px);min-height:0;place-self:center;}
.reserva-opciones.tres{grid-template-columns:repeat(2,minmax(0,1fr));grid-template-rows:repeat(2,minmax(0,1fr));height:min(100%,510px);width:min(100%,720px);}
.reserva-opcion{position:relative;display:flex;align-items:center;justify-content:center;flex-direction:column;gap:clamp(6px,1.4vh,14px);min-height:0;width:100%;border:2px solid #dfeaf0;border-radius:19px;background:white;color:#01305c;font:inherit;cursor:pointer;padding:12px;text-align:center;}
.reserva-opcion.seleccionada{border-color:#00b9c7;background:#e8f9fa;box-shadow:0 0 0 2px rgba(0,185,199,.13);}
.reserva-marca{position:absolute;top:9px;right:9px;display:grid;place-items:center;width:30px;height:30px;border-radius:50%;background:#00b9c7;color:#01305c;font-weight:900;}
.reserva-imagen{position:relative;display:block;width:clamp(42px,8vh,78px);aspect-ratio:1;}
.reserva-imagen img{position:absolute;inset:0;width:100%;height:100%;object-fit:contain;}
.icono-oscuro{opacity:0;}
.reserva-opcion strong{font-size:clamp(14px,2vw,21px);line-height:1.25;}
.reserva-acciones{display:flex;justify-content:flex-end;}
.reserva-continuar{display:flex;align-items:center;justify-content:center;gap:13px;min-width:190px;min-height:54px;padding:10px 22px;border:2px solid #00a8b8;border-radius:14px;background:#00b9c7;color:#01305c;font:inherit;font-weight:800;cursor:pointer;}
.reserva-continuar:disabled{opacity:.5;cursor:not-allowed;}
.reserva-continuar img{width:23px;height:23px;}
.tema-oscuro{background:#061f36;color:#fff;}
.tema-oscuro .reserva-volver{background:#10334e;border-color:#356b7c;}
.tema-oscuro .reserva-opcion{background:#11324b;color:white;border-color:#446679;}
.tema-oscuro .reserva-opcion.seleccionada{background:#113c52;border-color:#00b9c7;}
.tema-oscuro .icono-claro{opacity:0;}
.tema-oscuro .icono-oscuro{opacity:1;}
@media(max-width:430px){.reserva-contenido{gap:10px;}.reserva-opciones{height:min(100%,270px);}.reserva-opciones.tres{height:min(100%,400px);}.reserva-continuar{min-height:48px;}}
@media(prefers-reduced-motion:reduce){.reserva-opcion,.reserva-continuar{transition:none;}}
</style>
