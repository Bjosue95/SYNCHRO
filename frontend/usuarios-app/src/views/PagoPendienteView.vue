<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';

const route=useRoute();
const router=useRouter();
const reservaId=computed(()=>Number(route.params.id));
const detalle=ref<any>(null);
const cargando=ref(false);
const error=ref('');
let reloj: ReturnType<typeof setInterval>|undefined;
let consultaActiva=false;
let montada=true;
let version=0;
const importe=computed(()=>{
  const valor=detalle.value?.pago?.importeHnl ?? detalle.value?.pago?.tarifaConfigurada ?? detalle.value?.reserva?.tarifaHnl;
  return typeof valor==='number'&&Number.isFinite(valor)
    ?new Intl.NumberFormat('es-HN',{style:'currency',currency:'HNL',minimumFractionDigits:2,maximumFractionDigits:2}).format(valor)
    :'Pendiente de verificar';
});
const terminada=computed(()=>Boolean(detalle.value?.historica||detalle.value?.cancelacion||
  ['Cancelada','Vencida','Rechazada'].includes(detalle.value?.estado)));
async function consultar(){
  const id=reservaId.value;
  if(!Number.isSafeInteger(id)||id<=0||consultaActiva)return;
  consultaActiva=true;
  cargando.value=true;
  const actual=++version;
  try{
    const d=await encomiendasApi.detalle(id);
    if(!montada||actual!==version||id!==reservaId.value)return;
    detalle.value=d;
    error.value='';
    if(d.pago?.estado==='ConfirmadoManual'||d.condiciones?.pagoConfirmado){
      clearInterval(reloj);
      await router.replace('/reservas/'+id);
    }else if(d.reserva?.modalidadPago!=='WebTarjeta'||terminada.value){
      clearInterval(reloj);
    }
  }catch(e){
    if(montada&&actual===version)error.value=(e as Error).message||'No se pudo consultar el estado de la reserva.';
  }finally{
    consultaActiva=false;
    if(actual===version)cargando.value=false;
  }
}
function iniciar(){
  clearInterval(reloj);
  detalle.value=null;
  error.value='';
  void consultar();
  reloj=setInterval(()=>{if(!document.hidden)void consultar();},5000);
}
watch(()=>route.params.id,()=>{version++;iniciar();});
onMounted(iniciar);
onBeforeUnmount(()=>{montada=false;version++;clearInterval(reloj);});
</script>

<template>
  <section class="env-page espera-pago">
    <div class="espera-pago__panel" aria-labelledby="espera-pago-titulo">
      <div v-if="!terminada" class="espera-pago__animacion" aria-hidden="true">
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
      <div v-else class="espera-pago__terminada" aria-hidden="true">!</div>

      <h1 id="espera-pago-titulo">{{ terminada ? 'La reserva ya no está activa' : 'Verificando pago' }}</h1>
      <p v-if="!terminada" class="espera-pago__subtitulo" role="status" aria-live="polite">
        <span class="espera-pago__puntos" aria-hidden="true"><span></span><span></span><span></span></span>
        Continuaremos automáticamente cuando se confirme
      </p>
      <p v-else class="espera-pago__subtitulo" role="status">Consulta el estado de esta solicitud en tus reservas.</p>

      <div class="espera-pago__datos">
        <div><span>Reserva</span><strong>#{{ reservaId }}</strong></div>
        <div><span>Total solicitado</span><strong>{{ importe }}</strong></div>
      </div>
      <p v-if="error" class="alerta" role="alert">{{ error }}</p>
      <p class="espera-pago__aviso">Pago de demostración · Sin cargo bancario real</p>
    </div>
  </section>
</template>

<style scoped>
.espera-pago {
  display:grid;place-items:center;min-height:calc(100dvh - 135px);
  padding:clamp(16px,3vw,32px);overflow:hidden;
}
.espera-pago__panel {
  width:min(100%,500px);display:flex;flex-direction:column;align-items:center;
  gap:clamp(15px,2.6vh,24px);text-align:center;
  padding:clamp(18px,4vw,32px);
}
.espera-pago__panel h1 {
  font-size:clamp(23px,4.6vw,32px);line-height:1.2;
  color:var(--text-strong);font-weight:850;letter-spacing:-.02em;
}
.espera-pago__subtitulo {
  display:flex;flex-wrap:wrap;align-items:center;justify-content:center;gap:10px;
  font-size:clamp(12px,1.8vw,14px);line-height:1.5;color:var(--text-muted);
}
.espera-pago__animacion {
  position:relative;display:grid;place-items:center;
  width:clamp(190px,45vw,260px);aspect-ratio:1;
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
  border:1px solid rgb(255 255 255 / 55%);
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
.espera-pago__puntos{display:inline-flex;gap:4px;align-items:center;}
.espera-pago__puntos span{width:6px;height:6px;border-radius:50%;background:#00b9c7;animation:espera-latido 1.1s ease-in-out infinite;}
.espera-pago__puntos span:nth-child(2){animation-delay:.18s;}
.espera-pago__puntos span:nth-child(3){animation-delay:.36s;}
.espera-pago__datos {
  display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;
  width:100%;padding:14px;border-radius:14px;
  border:1px solid var(--border);background:var(--surface);
  text-align:left;
}
.espera-pago__datos>div{min-width:0;display:grid;gap:5px;}
.espera-pago__datos span{font-size:12px;color:var(--text-muted);}
.espera-pago__datos strong{font-size:clamp(16px,3vw,21px);color:var(--text-strong);overflow-wrap:anywhere;}
.espera-pago__aviso{font-size:11px;line-height:1.4;color:var(--text-muted);}
.espera-pago__terminada{
  display:grid;place-items:center;width:76px;height:76px;border:2px solid var(--border-strong);
  border-radius:50%;color:var(--text-muted);font-size:36px;font-weight:700;
}
@keyframes espera-giro{to{transform:rotate(360deg);}}
@keyframes espera-flotar{0%,100%{transform:rotate(-10deg) translateY(0);}50%{transform:rotate(-7deg) translateY(-9px);}}
@keyframes espera-brillo{0%,25%{left:-45%;}75%,100%{left:125%;}}
@keyframes espera-latido{0%,100%{opacity:.45;transform:scale(.75);}50%{opacity:1;transform:scale(1.12);}}
@media(max-width:420px){
  .espera-pago{min-height:calc(100dvh - 90px);padding:14px;}
  .espera-pago__panel{padding:10px 4px;gap:18px;}
  .espera-pago__animacion{width:clamp(180px,54vw,235px);}
}
@media(prefers-reduced-motion:reduce){
  .espera-pago__circulo,.espera-pago__tarjeta,.espera-pago__banda,
  .espera-pago__destello,.espera-pago__puntos span{animation:none!important;}
}
</style>
