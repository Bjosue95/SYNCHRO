<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref, watch } from 'vue';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import { encomiendasApi } from '../services/encomiendasApi.js';
import AppIcon from '../components/ui/AppIcon.vue';

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
    <div class="env-card espera-pago__panel" aria-labelledby="espera-pago-titulo">
      <div class="espera-pago__simbolo" aria-hidden="true">
        <AppIcon nombre="reloj" :tamano="34" />
      </div>
      <div class="espera-pago__encabezado">
        <span class="espera-pago__etiqueta">SOLICITUD REGISTRADA</span>
        <h1 id="espera-pago-titulo">Esperando confirmación del pago</h1>
        <p>Tu solicitud de pago se envió a SYNCHRO. El administrador debe comprobar y confirmar la operación.</p>
      </div>
      <div class="espera-pago__datos">
        <div><span>Reserva</span><strong>#{{ reservaId }}</strong></div>
        <div><span>Total solicitado</span><strong>{{ importe }}</strong></div>
      </div>
      <div v-if="terminada" class="espera-pago__estado" role="status">
        Esta reserva ya no está activa. Revisa los detalles para conocer su estado.
      </div>
      <div v-else class="espera-pago__estado" role="status">
        <span class="espera-pago__punto" aria-hidden="true"></span>
        {{ cargando&&!detalle?'Consultando la reserva…':'Pendiente de confirmación administrativa' }}
      </div>
      <p v-if="error" class="alerta" role="alert">{{ error }}</p>
      <p class="espera-pago__nota">La información se actualiza automáticamente cada pocos segundos. Cuando el administrador confirme el pago, se abrirá tu reserva para continuar con la autorización y el PIN de depósito.</p>
      <p class="espera-pago__nota">El pago es demostrativo: no se efectuó ningún cargo bancario. No necesitas crear otra reserva.</p>
      <div class="espera-pago__acciones">
        <button type="button" class="boton boton-primario" :disabled="cargando" @click="consultar">
          <AppIcon nombre="actualizar" :tamano="18" />
          Actualizar estado
        </button>
        <RouterLink class="boton boton-secundario" :to="'/reservas/'+reservaId">Ver mi reserva</RouterLink>
      </div>
    </div>
  </section>
</template>

<style scoped>
.espera-pago {display:grid;align-content:center;justify-items:center;min-height:calc(100dvh - 150px);padding:clamp(12px,2vw,28px);}
.espera-pago__panel {display:grid;justify-items:start;gap:clamp(13px,2.5vh,22px);width:min(100%,620px);padding:clamp(20px,4vw,38px);}
.espera-pago__simbolo {width:64px;height:64px;display:grid;place-items:center;background:var(--surface-accent);border:1px solid var(--border);border-radius:18px;color:var(--accent-text);}
.espera-pago__encabezado {display:grid;gap:9px;}
.espera-pago__encabezado h1 {color:var(--text-strong);font-size:clamp(23px,4vw,32px);line-height:1.2;}
.espera-pago__encabezado p,.espera-pago__nota {color:var(--text-muted);font-size:clamp(12px,2vw,14px);line-height:1.6;}
.espera-pago__etiqueta {color:var(--accent-text);font-size:11px;font-weight:800;letter-spacing:.08em;}
.espera-pago__datos {display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;width:100%;padding:15px;border-radius:12px;background:var(--surface-soft);border:1px solid var(--border);}
.espera-pago__datos>div {min-width:0;display:grid;gap:5px;}
.espera-pago__datos span {color:var(--text-muted);font-size:12px;}
.espera-pago__datos strong {color:var(--text-strong);font-size:clamp(16px,3vw,22px);overflow-wrap:anywhere;}
.espera-pago__estado {display:flex;align-items:center;gap:10px;color:var(--text-strong);font-weight:750;font-size:13px;padding:13px;border-radius:11px;background:var(--surface-accent);width:100%;}
.espera-pago__punto {width:11px;height:11px;background:#00b9c7;border-radius:50%;box-shadow:0 0 0 4px rgb(0 185 199 / 15%);flex:0 0 11px;}
.espera-pago__acciones {display:flex;gap:10px;flex-wrap:wrap;justify-content:flex-end;width:100%;}
.espera-pago__acciones .boton {display:inline-flex;justify-content:center;align-items:center;gap:8px;padding:12px 18px;min-height:48px;text-decoration:none;}
@media(max-width:420px){.espera-pago__datos{gap:8px;padding:12px}.espera-pago__acciones .boton{flex:1 1 auto;}}
</style>
