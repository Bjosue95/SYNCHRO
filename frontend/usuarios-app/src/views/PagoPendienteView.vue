<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { ErrorApi } from '../services/clientesApi.js';
import { estadoSesion } from '../services/sesion.js';

const route=useRoute();
const router=useRouter();
const reservaId=computed(()=>Number(route.params.id));
const estadoAccesible=ref('Comprobando el estado de la operación');
const error=ref('');
const mostrarAyuda=ref(false);
const fallosConsecutivos=ref(0);
const etapa=ref<'consultando'|'pago'|'pin'|'entrega'>('consultando');
let comienzoEspera=Date.now();
let reloj:ReturnType<typeof setInterval>|undefined;
let consultaActiva=false;
let montada=true;
let version=0;
let ultimoIntentoSolicitud=0;
let redirigiendoAcceso=false;
async function gestionarSesion(error:unknown){
  if(!(error instanceof ErrorApi&&error.estado===401))return false;
  if(redirigiendoAcceso)return true;
  redirigiendoAcceso=true;
  clearInterval(reloj);
  estadoSesion.cliente=null;
  estadoAccesible.value='La sesión terminó. Vuelve a ingresar para continuar con la reserva.';
  await router.replace({name:'acceso',query:{volver:route.fullPath}});
  return true;
}
const finalizada=(d:any)=>Boolean(d?.historica||d?.cancelacion||
  ['Cancelada','Vencida','Rechazada'].includes(d?.estado));
async function consultar(){
  const id=reservaId.value;
  if(!Number.isSafeInteger(id)||id<=0||consultaActiva)return;
  consultaActiva=true;
  const actual=++version;
  try{
    const d=await encomiendasApi.detalle(id);
    if(!montada||actual!==version||id!==reservaId.value)return;
    error.value='';
    fallosConsecutivos.value=0;
    if(finalizada(d)){
      clearInterval(reloj);
      estadoAccesible.value='La reserva ya no está activa.';
      await router.replace('/reservas/'+id);
      return;
    }
    if(d.pago?.estado==='ConfirmadoManual'||d.condiciones?.pagoConfirmado){
      clearInterval(reloj);
      estadoAccesible.value='Pago confirmado. Continúa con la solicitud del PIN.';
      await router.replace('/reservas/'+id+'/solicitar-pin');
      return;
    }else{
      etapa.value='pago';
      estadoAccesible.value='Esperando la confirmación del pago.';
    }
  }catch(e){
    if(await gestionarSesion(e))return;
    if(montada&&actual===version){
      error.value=(e as Error).message||'No se pudo consultar la operación.';
      fallosConsecutivos.value++;
      mostrarAyuda.value=true;
    }
  }finally{
    consultaActiva=false;
    if(montada&&actual===version&&Date.now()-comienzoEspera>=20000)mostrarAyuda.value=true;
  }
}
function iniciar(){
  clearInterval(reloj);
  error.value='';
  mostrarAyuda.value=false;
  fallosConsecutivos.value=0;
  etapa.value='consultando';
  comienzoEspera=Date.now();
  ultimoIntentoSolicitud=0;
  redirigiendoAcceso=false;
  void consultar();
  reloj=setInterval(()=>{if(!document.hidden)void consultar();},5000);
}
watch(()=>route.params.id,()=>{version++;iniciar();});
onMounted(iniciar);
onBeforeUnmount(()=>{montada=false;version++;clearInterval(reloj);});
</script>

<template>
  <section class="env-page espera-pago" aria-label="Procesando la operación">
    <div class="espera-pago__animacion" aria-hidden="true">
      <div class="espera-pago__circulo espera-pago__circulo--exterior"></div>
      <div class="espera-pago__circulo espera-pago__circulo--interior"></div>
      <div class="espera-pago__tarjeta">
        <div class="espera-pago__chip"></div>
        <div class="espera-pago__lineas"><span></span><span></span></div>
        <div class="espera-pago__banda"></div>
      </div>
      <div class="espera-pago__destello espera-pago__destello--uno"></div>
      <div class="espera-pago__destello espera-pago__destello--dos"></div>
    </div>
    <div v-if="mostrarAyuda" class="espera-pago__informacion" role="status" aria-live="polite">
      <p v-if="error">{{ error }}</p>
      <p v-else-if="etapa==='entrega'">Estamos esperando que SYNCHRO confirme la entrega de tu PIN de depósito.</p>
      <p v-else-if="etapa==='pin'">Pago confirmado. Tu PIN de depósito está pendiente de gestión.</p>
      <p v-else-if="etapa==='pago'">El pago continúa pendiente de confirmación.</p>
      <p v-else>Estamos comprobando el estado de tu reserva.</p>
      <button type="button" class="espera-pago__consulta" @click="router.push('/reservas/'+reservaId)">Ver estado de mi reserva</button>
      <small>Esta solicitud no genera un segundo pago ni otra reserva.</small>
    </div>
    <span v-else class="espera-pago__lectura" role="status" aria-live="polite">{{ estadoAccesible }}</span>
  </section>
</template>

<style scoped>
:global(.app-shell:has(.espera-pago) .topbar){display:none;}
:global(.panel-contenido:has(.espera-pago)){
  height:100dvh;
  min-height:0;
  max-height:100dvh;
  overflow:hidden;
  box-sizing:border-box;
  padding:0;
}
:global(.contenido-limite:has(.espera-pago)){
  width:100%;
  max-width:none;
  height:100%;
  min-height:0;
  overflow:hidden;
  padding:0;
}
.espera-pago {
  width:100%;
  height:100%;
  min-height:0;
  max-height:100dvh;
  overflow:hidden;
  display:grid;
  place-items:center;
  padding:clamp(12px,3vw,32px);
  box-sizing:border-box;
}
.espera-pago__informacion{
  position:absolute;
  top:calc(50% + min(130px,30vw,26dvh));
  left:50%;transform:translateX(-50%);
  width:min(calc(100% - 32px),400px);
  display:grid;justify-items:center;gap:9px;
  text-align:center;font-size:12px;line-height:1.4;
  color:var(--text-strong);
}
.espera-pago__informacion p{margin:0;}
.espera-pago__informacion small{font-size:10px;color:var(--text-muted);}
.espera-pago__consulta{
  cursor:pointer;border:1px solid #00b9c7;background:#01305c;color:#fff;
  border-radius:12px;padding:11px 16px;min-height:42px;font-weight:750;
}
.espera-pago__consulta:focus-visible{outline:3px solid #00b9c7;outline-offset:3px;}
.espera-pago__lectura{
  position:absolute;
  width:1px;height:1px;
  overflow:hidden;clip:rect(0,0,0,0);
  white-space:nowrap;
}
.espera-pago__animacion {
  position:relative;display:grid;place-items:center;
  width:min(260px,60vw,52dvh);aspect-ratio:1;
  isolation:isolate;
}
.espera-pago__circulo {
  position:absolute;inset:7%;border-radius:50%;
  border:2px solid rgb(0 185 199 / 18%);
}
.espera-pago__circulo--exterior {
  border-top-color:#00b9c7;border-right-color:#5ec6e8;
  animation:espera-giro 3.4s linear infinite;
}
.espera-pago__circulo--interior {
  inset:17%;border:1px dashed rgb(94 198 232 / 55%);
  animation:espera-giro 9s linear infinite reverse;
}
.espera-pago__tarjeta {
  position:relative;display:flex;flex-direction:column;justify-content:space-between;
  transform:rotate(-10deg);width:61%;height:38%;
  border:1px solid #5ec6e8;border-radius:15px;
  background:linear-gradient(130deg,#01305c,#075578 67%,#00a2b2);
  box-shadow:0 17px 40px rgb(1 48 92 / 22%);
  padding:clamp(12px,2vw,18px);overflow:hidden;
  animation:espera-flotar 2.8s ease-in-out infinite;
}
.espera-pago__chip {
  width:25px;height:19px;border-radius:5px;
  background:linear-gradient(140deg,#a6d4db,#eaf9fa 54%,#73bbc9);
}
.espera-pago__lineas{display:grid;gap:6px;width:73%;}
.espera-pago__lineas span{height:5px;border-radius:10px;background:rgb(255 255 255 / 70%);}
.espera-pago__lineas span:last-child{width:60%;opacity:.65;}
.espera-pago__banda {
  position:absolute;inset:0 auto 0 -45%;width:32%;
  background:linear-gradient(90deg,transparent,rgb(255 255 255 / 16%),transparent);
  transform:skewX(-24deg);animation:espera-brillo 3s ease-in-out infinite;
}
.espera-pago__destello{position:absolute;width:10px;height:10px;border-radius:50%;background:#00b9c7;box-shadow:0 0 0 5px rgb(0 185 199 / 12%);}
.espera-pago__destello--uno{top:17%;right:14%;animation:espera-latido 2s ease-in-out infinite;}
.espera-pago__destello--dos{bottom:20%;left:15%;background:#5ec6e8;animation:espera-latido 2s ease-in-out 1s infinite;}
@keyframes espera-giro{to{transform:rotate(360deg);}}
@keyframes espera-flotar{0%,100%{transform:rotate(-10deg) translateY(0);}50%{transform:rotate(-7deg) translateY(-9px);}}
@keyframes espera-brillo{0%,25%{left:-45%;}75%,100%{left:125%;}}
@keyframes espera-latido{0%,100%{opacity:.45;transform:scale(.75);}50%{opacity:1;transform:scale(1.12);}}
@media(prefers-reduced-motion:reduce){
  .espera-pago__circulo,.espera-pago__tarjeta,.espera-pago__banda,.espera-pago__destello{animation:none!important;}
}
</style>
