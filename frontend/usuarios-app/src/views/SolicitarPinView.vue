<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';
import { ErrorApi } from '../services/clientesApi.js';
import { estadoSesion } from '../services/sesion.js';
import AppIcon from '../components/ui/AppIcon.vue';

const route=useRoute();
const router=useRouter();
const reservaId=computed(()=>Number(route.params.id));
const detalle=ref<any>(null);
const consultando=ref(true);
const enviando=ref(false);
const error=ref('');
let activo=true;
let reloj:ReturnType<typeof setInterval>|undefined;
let consultandoRed=false;
let version=0;
let redirigiendo=false;

const pinEntregado=computed(()=>Boolean(detalle.value?.pines?.some((p:any)=>
  p.finalidad==='Deposito'&&['Entregado','Consumido'].includes(p.estado))));
const pinEmitido=computed(()=>Boolean(detalle.value?.pines?.some((p:any)=>
  p.finalidad==='Deposito'&&['Emitido','Entregado','Consumido'].includes(p.estado))));
const pinSolicitado=computed(()=>Boolean(detalle.value?.historial?.some((h:any)=>
  h.accion==='PinDepositoSolicitado')));
const pagoConfirmado=computed(()=>Boolean(detalle.value?.pago?.estado==='ConfirmadoManual'||detalle.value?.condiciones?.pagoConfirmado));
const whatsappAutorizado=computed(()=>Boolean(detalle.value?.condiciones?.remitenteAutorizado));
const terminada=computed(()=>Boolean(detalle.value?.historica||detalle.value?.cancelacion||
  ['Cancelada','Vencida','Rechazada'].includes(detalle.value?.estado)));
const esperandoEntrega=computed(()=>pinSolicitado.value||pinEmitido.value);
const puedeSolicitar=computed(()=>pagoConfirmado.value&&whatsappAutorizado.value
  &&!terminada.value&&!pinEntregado.value&&!esperandoEntrega.value&&!enviando.value);
async function manejarSesion(e:unknown){
  if(!(e instanceof ErrorApi&&e.estado===401))return false;
  if(redirigiendo)return true;
  redirigiendo=true;
  clearInterval(reloj);
  estadoSesion.cliente=null;
  await router.replace({name:'acceso',query:{volver:route.fullPath}});
  return true;
}
async function cargar(){
  const id=reservaId.value;
  if(!Number.isSafeInteger(id)||id<=0||consultandoRed)return;
  consultandoRed=true;
  const actual=++version;
  try{
    const respuesta=await encomiendasApi.detalle(id);
    if(!activo||actual!==version||id!==reservaId.value)return;
    detalle.value=respuesta;
    error.value='';
    const entregado=Boolean(respuesta?.pines?.some((p:any)=>
      p.finalidad==='Deposito'&&['Entregado','Consumido'].includes(p.estado)));
    if(entregado){
      clearInterval(reloj);
      await router.replace('/bienvenida');
      return;
    }
    if(respuesta?.historica||respuesta?.cancelacion||
      ['Cancelada','Vencida','Rechazada'].includes(respuesta?.estado)){
      clearInterval(reloj);
      await router.replace('/reservas/'+id);
    }
  }catch(e){
    if(await manejarSesion(e))return;
    if(activo&&actual===version)error.value=(e as Error).message||'No se pudo consultar la reserva.';
  }finally{
    consultandoRed=false;
    if(activo&&actual===version)consultando.value=false;
  }
}
async function solicitar(){
  if(!puedeSolicitar.value)return;
  enviando.value=true;
  error.value='';
  try{
    await encomiendasApi.solicitarPinDeposito(reservaId.value);
    await cargar();
  }catch(e){
    if(await manejarSesion(e))return;
    error.value=(e as Error).message||'No se pudo registrar la solicitud del PIN.';
    await cargar();
  }finally{enviando.value=false;}
}
function iniciar(){
  clearInterval(reloj);
  detalle.value=null;consultando.value=true;error.value='';
  void cargar();
  reloj=setInterval(()=>{if(!document.hidden)void cargar();},5000);
}
watch(()=>route.params.id,()=>{version++;iniciar();});
onMounted(iniciar);
onBeforeUnmount(()=>{activo=false;version++;clearInterval(reloj);});
</script>

<template>
  <section class="pin-page" aria-labelledby="pin-page-titulo">
    <div class="pin-page__contenido">
      <div v-if="consultando && !detalle" class="pin-page__cargando" role="status">
        <div class="pin-page__espera" aria-hidden="true"><span></span><span></span><span></span></div>
        Consultando tu reserva…
      </div>
      <template v-else>
        <div class="pin-page__icono" aria-hidden="true">
          <AppIcon :nombre="esperandoEntrega?'seguridad':'casillero'" :tamano="38" />
        </div>
        <p class="pin-page__etapa">{{ esperandoEntrega ? 'SOLICITUD REGISTRADA' : 'PAGO CONFIRMADO' }}</p>
        <h1 id="pin-page-titulo">{{ esperandoEntrega ? 'Estamos preparando tu PIN' : 'Solicita tu PIN de depósito' }}</h1>
        <p class="pin-page__descripcion">
          {{ esperandoEntrega
            ? 'Tu solicitud ya está registrada. Cuando SYNCHRO confirme que te entregó el PIN por WhatsApp, volverás al inicio automáticamente.'
            : 'El pago de tu reserva está confirmado. Solicita el PIN que utilizarás para depositar en la estación SYNCHRO.' }}
        </p>
        <div v-if="detalle" class="pin-page__reserva">
          <span>Reserva #{{ reservaId }}</span>
          <span v-if="detalle.pago?.importeHnl!=null">L {{ Number(detalle.pago.importeHnl).toFixed(2) }}</span>
        </div>
        <template v-if="esperandoEntrega">
          <div class="pin-page__espera" role="status" aria-label="Esperando confirmación de entrega del PIN">
            <span></span><span></span><span></span>
          </div>
          <p class="pin-page__secundario">La entrega del PIN se gestiona exclusivamente con el teléfono autorizado.</p>
        </template>
        <template v-else-if="!whatsappAutorizado">
          <p class="pin-page__mensaje">Primero debes completar la autorización de WhatsApp asociada a tu cuenta.</p>
          <RouterLink :to="'/reservas/'+reservaId" class="pin-page__boton">Revisar autorización</RouterLink>
        </template>
        <template v-else-if="!pagoConfirmado">
          <p class="pin-page__mensaje">El pago todavía no aparece confirmado en tu reserva.</p>
          <RouterLink :to="'/reservas/'+reservaId" class="pin-page__boton">Ver estado de la reserva</RouterLink>
        </template>
        <button v-else type="button" class="pin-page__boton" :disabled="!puedeSolicitar" @click="solicitar">
          {{ enviando?'Registrando solicitud…':'Solicitar PIN de depósito' }}
          <AppIcon nombre="continuar" :tamano="21" />
        </button>
        <p v-if="error" class="pin-page__error" role="alert">{{ error }}</p>
        <p v-if="esperandoEntrega" class="pin-page__nota">Puedes consultar esta reserva más adelante desde «Mis reservas».</p>
      </template>
    </div>
  </section>
</template>

<style scoped>
.pin-page{display:grid;place-items:center;min-height:calc(100dvh - 145px);padding:clamp(12px,3vw,32px);box-sizing:border-box;}
.pin-page__contenido{width:min(100%,490px);display:flex;flex-direction:column;align-items:center;text-align:center;gap:clamp(13px,2.1vh,19px);padding:clamp(12px,2vw,22px);}
.pin-page__icono{display:grid;place-items:center;width:76px;height:76px;border-radius:22px;background:var(--surface-accent);color:var(--accent-text);border:1px solid var(--border);}
.pin-page__etapa{font-size:11px;letter-spacing:.12em;font-weight:850;color:var(--accent-text);}
.pin-page__contenido h1{font-size:clamp(24px,4vw,34px);line-height:1.2;color:var(--text-strong);}
.pin-page__descripcion{font-size:clamp(13px,1.5vw,15px);line-height:1.6;color:var(--text-muted);max-width:42ch;}
.pin-page__reserva{display:flex;justify-content:space-between;gap:16px;width:min(100%,360px);padding:14px 17px;background:var(--surface-soft);border:1px solid var(--border);border-radius:13px;font-size:14px;font-weight:800;color:var(--text-strong);}
.pin-page__boton{display:inline-flex;align-items:center;justify-content:center;gap:12px;width:min(100%,360px);min-height:54px;padding:13px 18px;border:2px solid #00b9c7;border-radius:14px;background:#01305c;color:white;text-decoration:none;cursor:pointer;font-weight:800;font-size:14px;}
.pin-page__boton:disabled{opacity:.55;cursor:wait;}
.pin-page__boton:focus-visible{outline:3px solid #00b9c7;outline-offset:3px;}
.pin-page__mensaje,.pin-page__error{font-size:13px;line-height:1.5;color:var(--text-strong);}
.pin-page__error{padding:12px 14px;border-left:3px solid #c15d37;background:var(--surface-soft);}
.pin-page__nota,.pin-page__secundario{font-size:11px;line-height:1.5;color:var(--text-muted);}
.pin-page__espera{display:flex;justify-content:center;align-items:center;gap:7px;padding:10px;}
.pin-page__espera span{width:9px;height:9px;border-radius:50%;background:#00b9c7;animation:pin-espera 1.3s ease-in-out infinite;}
.pin-page__espera span:nth-child(2){animation-delay:.18s;}
.pin-page__espera span:nth-child(3){animation-delay:.36s;}
.pin-page__cargando{display:grid;justify-items:center;gap:10px;color:var(--text-muted);font-size:13px;}
@keyframes pin-espera{0%,100%{transform:scale(.6);opacity:.3;}50%{transform:scale(1);opacity:1;}}
@media(max-width:470px){.pin-page{min-height:calc(100dvh - 78px);padding:10px;}.pin-page__contenido{gap:13px;}}
@media(prefers-reduced-motion:reduce){.pin-page__espera span{animation:none;}}
</style>
